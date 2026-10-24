#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Conservative configuration repair tool for They Be Buggin (Unity 6).
/// Does not rewrite game code or claim to solve all navigation bugs.
/// Use Tools > They Be Buggin > Project Fixer.
/// </summary>
public sealed class TheyBeBugginProjectFixer : EditorWindow
{
    #region Settings
    private bool fixRigidbody = true;
    private bool fixLayerMasks = true;
    private bool addMissingSensor = true;
    private bool fixExcessiveGap = true;
    private bool includeScenes = true;
    private bool includePrefabs = true;
    private float maximumAllowedGap = 1.5f;
    private Vector2 scroll;
    private string preview = "Click Preview to inspect proposed changes.";
    private readonly List<string> findings = new List<string>();
    #endregion

    #region Menu_And_Window
    [MenuItem("Tools/They Be Buggin/Project Fixer")]
    private static void ShowWindow()
    {
        GetWindow<TheyBeBugginProjectFixer>("TBB Project Fixer").minSize = new Vector2(650, 480);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("They Be Buggin — Project Fixer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Targets confirmed or strongly suspicious enemy Inspector configurations. " +
            "It cannot guarantee a bug-free game. Preview first; the tool backs up assets before editing.",
            MessageType.Info);

        EditorGUILayout.Space();
        includePrefabs = EditorGUILayout.ToggleLeft("Inspect all prefabs", includePrefabs);
        includeScenes = EditorGUILayout.ToggleLeft("Inspect all enabled/disabled project scenes", includeScenes);
        EditorGUILayout.Space();
        fixRigidbody = EditorGUILayout.ToggleLeft("Align crawler Rigidbody: kinematic, no gravity", fixRigidbody);
        fixLayerMasks = EditorGUILayout.ToggleLeft("Align navigation sensor environment mask with crawler", fixLayerMasks);
        addMissingSensor = EditorGUILayout.ToggleLeft("Add missing EnemyNavigationSensor to crawler objects", addMissingSensor);
        fixExcessiveGap = EditorGUILayout.ToggleLeft("Clamp unusually large Maximum Surface Gap", fixExcessiveGap);
        using (new EditorGUI.DisabledScope(!fixExcessiveGap))
            maximumAllowedGap = EditorGUILayout.Slider("Maximum Surface Gap", maximumAllowedGap, 0.5f, 3f);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1. Preview Fixes", GUILayout.Height(32))) Run(false);
            if (GUILayout.Button("2. Back Up and Apply Fixes", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog("Apply Project Fixes?",
                    "This will save changed prefab/scene assets after making timestamped backups. " +
                    "Close Play Mode, commit or back up your project first. Continue?", "Apply", "Cancel"))
                    Run(true);
            }
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Findings / Changes", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(preview, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    #endregion

    #region Scan_And_Apply
    private void Run(bool apply)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Exit Play Mode", "Stop Play Mode before running the fixer.", "OK");
            return;
        }
        if (!includeScenes && !includePrefabs)
        {
            preview = "Select scenes and/or prefabs.";
            return;
        }
        if (apply && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        findings.Clear();
        int modifiedAssets = 0;
        string backupRoot = null;
        if (apply)
        {
            backupRoot = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "TheyBeBugginFixBackups", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(backupRoot);
            findings.Add("Backup directory: " + backupRoot);
        }

        SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            if (includePrefabs)
            {
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject root = null;
                    try
                    {
                        root = PrefabUtility.LoadPrefabContents(path);
                        bool changed = ProcessHierarchy(root, path, apply);
                        if (apply && changed)
                        {
                            BackupAsset(path, backupRoot);
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            modifiedAssets++;
                        }
                    }
                    catch (Exception ex) { findings.Add("ERROR " + path + ": " + ex.Message); }
                    finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
                }
            }

            if (includeScenes)
            {
                string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    try
                    {
                        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        bool changed = false;
                        foreach (GameObject root in scene.GetRootGameObjects())
                            changed |= ProcessHierarchy(root, path, apply);
                        if (apply && changed)
                        {
                            BackupAsset(path, backupRoot);
                            EditorSceneManager.SaveScene(scene);
                            modifiedAssets++;
                        }
                    }
                    catch (Exception ex) { findings.Add("ERROR " + path + ": " + ex.Message); }
                }
            }
        }
        finally
        {
            if (includeScenes) EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            if (apply) AssetDatabase.Refresh();
        }

        StringBuilder report = new StringBuilder();
        report.AppendLine(apply ? "FIX RESULTS" : "PREVIEW — NO CHANGES MADE");
        report.AppendLine("Items flagged: " + findings.Count);
        if (apply) report.AppendLine("Assets modified: " + modifiedAssets);
        report.AppendLine();
        foreach (string item in findings) report.AppendLine(item);
        if (findings.Count == 0) report.AppendLine("No matching configuration problems found.");
        report.AppendLine();
        report.AppendLine("Not fixed automatically: crawler geometry algorithms, collision tunneling, " +
            "runtime race conditions, missing terrain connections, broken game logic, and unknown code defects.");
        preview = report.ToString();
        if (apply)
        {
            string reportPath = Path.Combine(backupRoot, "fix_report.txt");
            File.WriteAllText(reportPath, preview);
            Debug.Log("TBB fixer completed. Report: " + reportPath);
            EditorUtility.DisplayDialog("Fixer Finished",
                "Modified " + modifiedAssets + " assets. Backups and report saved in:\n" + backupRoot, "OK");
        }
    }
    #endregion

    #region Hierarchy_And_Component_Fixes
    private bool ProcessHierarchy(GameObject root, string assetPath, bool apply)
    {
        bool changed = false;
        EnemySurfaceCrawler[] crawlers = root.GetComponentsInChildren<EnemySurfaceCrawler>(true);
        foreach (EnemySurfaceCrawler crawler in crawlers)
        {
            if (crawler == null) continue;
            GameObject go = crawler.gameObject;
            string id = assetPath + " :: " + GetPath(go.transform);
            Rigidbody body = go.GetComponent<Rigidbody>();
            if (body == null)
            {
                findings.Add("MANUAL REVIEW " + id + " — crawler has no Rigidbody");
                continue;
            }

            if (fixRigidbody && (!body.isKinematic || body.useGravity))
            {
                findings.Add("RIGIDBODY " + id + " — set isKinematic=true, useGravity=false");
                if (apply)
                {
                    body.isKinematic = true;
                    body.useGravity = false;
                    EditorUtility.SetDirty(body);
                    changed = true;
                }
            }

            SerializedObject crawlerObject = new SerializedObject(crawler);
            SerializedProperty mask = crawlerObject.FindProperty("climbableLayers");
            SerializedProperty gap = crawlerObject.FindProperty("maximumSurfaceGap");
            int crawlerMask = mask != null ? mask.intValue : 0;

            if (crawlerMask == 0)
                findings.Add("MANUAL REVIEW " + id + " — Climbable Layers = Nothing; choose real terrain layers");

            if (fixExcessiveGap && gap != null && gap.floatValue > maximumAllowedGap)
            {
                findings.Add("SURFACE GAP " + id + " — " + gap.floatValue.ToString("F2") +
                    " -> " + maximumAllowedGap.ToString("F2") + " (test collider clearance after applying)");
                if (apply)
                {
                    gap.floatValue = maximumAllowedGap;
                    crawlerObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(crawler);
                    changed = true;
                }
            }

            EnemyNavigationSensor sensor = go.GetComponent<EnemyNavigationSensor>();
            if (sensor == null && addMissingSensor)
            {
                findings.Add("MISSING SENSOR " + id + " — add EnemyNavigationSensor");
                if (apply)
                {
                    sensor = go.AddComponent<EnemyNavigationSensor>();
                    changed = true;
                }
            }
            if (sensor != null && fixLayerMasks && crawlerMask != 0)
            {
                SerializedObject sensorObject = new SerializedObject(sensor);
                SerializedProperty environment = sensorObject.FindProperty("environmentLayers");
                if (environment != null && environment.intValue != crawlerMask)
                {
                    findings.Add("SENSOR MASK " + id + " — align " + environment.intValue +
                        " with crawler mask " + crawlerMask);
                    if (apply)
                    {
                        environment.intValue = crawlerMask;
                        sensorObject.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(sensor);
                        changed = true;
                    }
                }
            }

            if (go.GetComponents<EnemySurfaceCrawler>().Length > 1)
                findings.Add("MANUAL REVIEW " + id + " — duplicate EnemySurfaceCrawler components");
            if (go.GetComponent<Collider>() == null)
                findings.Add("MANUAL REVIEW " + id + " — crawler has no collider on same object");
        }
        return changed;
    }

    private static string GetPath(Transform transform)
    {
        string result = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            result = transform.name + "/" + result;
        }
        return result;
    }
    #endregion

    #region Backups
    private static void BackupAsset(string assetPath, string backupRoot)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string source = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        string destination = Path.Combine(backupRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(source, destination, true);
        if (File.Exists(source + ".meta")) File.Copy(source + ".meta", destination + ".meta", true);
    }
    #endregion
}
#endif
