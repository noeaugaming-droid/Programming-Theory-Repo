#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Read-only project inventory and optional Play Mode movement recorder.</summary>
public class TheyBeBugginProjectScanner : EditorWindow
{
    #region Fields
    private const string Root = "TheyBeBugginDiagnostics";
    private static readonly List<string> runtimeLines = new List<string>();
    private static bool recording;
    private static double lastSample;
    private static int maxSamples = 12000;
    private Vector2 scroll;
    private string lastExport = "Not exported yet";
    private bool includeSource = true;
    private bool includeSettings = true;
    private bool includeRuntime = true;
    #endregion

    #region Menu
    [MenuItem("Tools/They Be Buggin/Project Scanner")]
    public static void Open() { GetWindow<TheyBeBugginProjectScanner>("TBB Project Scanner"); }

    [MenuItem("Tools/They Be Buggin/Export Diagnostic ZIP")]
    public static void QuickExport() { Export(true, true, true); }
    #endregion

    #region Window
    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("They Be Buggin — Project Scanner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Read-only scan. Save open scenes before exporting. Play Mode recording captures observable movement, not private navigation decisions.", MessageType.Info);
        includeSource = EditorGUILayout.Toggle("Include C# source", includeSource);
        includeSettings = EditorGUILayout.Toggle("Include project settings", includeSettings);
        includeRuntime = EditorGUILayout.Toggle("Include Play Mode samples", includeRuntime);
        maxSamples = EditorGUILayout.IntSlider("Maximum runtime samples", maxSamples, 1000, 50000);
        if (GUILayout.Button("Export Full Diagnostic ZIP", GUILayout.Height(35)))
            lastExport = Export(includeSource, includeSettings, includeRuntime);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Runtime recording", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Status", recording ? "Recording" : "Stopped");
        EditorGUILayout.LabelField("Samples", runtimeLines.Count.ToString());
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || recording))
            if (GUILayout.Button("Start Recording")) StartRecording();
        using (new EditorGUI.DisabledScope(!recording))
            if (GUILayout.Button("Stop Recording")) StopRecording();
        EditorGUILayout.HelpBox("Enter Play Mode, start recording, reproduce the issue, stop, then export. Stop recording before leaving Play Mode.", MessageType.None);
        EditorGUILayout.SelectableLabel(lastExport, GUILayout.Height(45));
        EditorGUILayout.EndScrollView();
    }
    #endregion

    #region Export
    private static string Export(bool source, bool settings, bool runtime)
    {
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string parent = Path.Combine(Path.GetDirectoryName(Application.dataPath), Root);
        string folder = Path.Combine(parent, "Scan_" + stamp);
        Directory.CreateDirectory(folder);
        try
        {
            WriteInventory(folder);
            WriteIssues(folder);
            if (source) CopyFiles(Application.dataPath, Path.Combine(folder, "Source"), ".cs", ".asmdef", ".asmref");
            if (settings)
            {
                string project = Path.GetDirectoryName(Application.dataPath);
                CopyDirectory(Path.Combine(project, "ProjectSettings"), Path.Combine(folder, "ProjectSettings"), false);
                CopyDirectory(Path.Combine(project, "Packages"), Path.Combine(folder, "Packages"), true);
            }
            if (runtime) File.WriteAllLines(Path.Combine(folder, "runtime_samples.csv"), runtimeLines.ToArray());
            File.WriteAllText(Path.Combine(folder, "README.txt"),
                "They Be Buggin diagnostic export\nUnity: " + Application.unityVersion +
                "\nUTC: " + DateTime.UtcNow.ToString("o") +
                "\nInventory includes scenes, prefabs, components and serialized Inspector properties.\n" +
                "Runtime data is only collected when Start Recording is pressed in Play Mode.\n" +
                "Review the ZIP for secrets or private information before sharing.\n");
            string zip = folder + ".zip";
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(folder, zip);
            Debug.Log("[TBB Scanner] Exported " + zip);
            EditorUtility.RevealInFinder(zip);
            return zip;
        }
        catch (Exception ex)
        {
            Debug.LogError("[TBB Scanner] Export failed: " + ex);
            return "Export failed: " + ex.Message;
        }
    }

    private static void WriteInventory(string folder)
    {
        using (StreamWriter w = new StreamWriter(Path.Combine(folder, "project_inventory.txt"), false, Encoding.UTF8))
        {
            w.WriteLine("Unity=" + Application.unityVersion);
            w.WriteLine("Product=" + Application.productName);
            w.WriteLine("Physics.gravity=" + Physics.gravity);
            w.WriteLine("Time.fixedDeltaTime=" + Time.fixedDeltaTime);
            w.WriteLine("Collision matrix:");
            for (int a = 0; a < 32; a++)
                for (int b = a; b < 32; b++)
                    w.WriteLine(a + "," + b + ",ignore=" + Physics.GetIgnoreLayerCollision(a, b));
            string[] paths = AssetDatabase.GetAllAssetPaths();
            foreach (string path in paths)
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)) continue;
                if (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    InspectScene(path, w);
                else if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    InspectPrefab(path, w);
                else if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    InspectAsset(path, w);
            }
        }
    }

    private static void InspectScene(string path, StreamWriter w)
    {
        w.WriteLine("\n=== SCENE " + path + " ===");
        Scene scene = default(Scene);
        bool opened = false;
        try
        {
            scene = SceneManager.GetSceneByPath(path);
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                opened = true;
            }
            foreach (GameObject root in scene.GetRootGameObjects()) InspectObject(root, w);
        }
        catch (Exception ex) { w.WriteLine("SCENE_ERROR " + ex.Message); }
        finally { if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void InspectPrefab(string path, StreamWriter w)
    {
        w.WriteLine("\n=== PREFAB " + path + " ===");
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(path);
            InspectObject(root, w);
        }
        catch (Exception ex) { w.WriteLine("PREFAB_ERROR " + ex.Message); }
        finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void InspectAsset(string path, StreamWriter w)
    {
        UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
        if (asset == null) return;
        w.WriteLine("ASSET " + path + " type=" + asset.GetType().FullName + " guid=" + AssetDatabase.AssetPathToGUID(path));
    }

    private static void InspectObject(GameObject root, StreamWriter w)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            GameObject go = t.gameObject;
            w.WriteLine("GAMEOBJECT " + GetPath(t) + " activeSelf=" + go.activeSelf + " layer=" + LayerMask.LayerToName(go.layer) + "(" + go.layer + ") tag=" + go.tag +
                " localPosition=" + t.localPosition + " localRotation=" + t.localRotation.eulerAngles + " localScale=" + t.localScale);
            foreach (Component component in go.GetComponents<Component>())
            {
                if (component == null) { w.WriteLine("  MISSING_SCRIPT"); continue; }
                w.WriteLine("  COMPONENT " + component.GetType().FullName);
                try
                {
                    SerializedObject serialized = new SerializedObject(component);
                    SerializedProperty prop = serialized.GetIterator();
                    if (prop.NextVisible(true))
                    {
                        do
                        {
                            if (prop.propertyPath == "m_Script") continue;
                            w.WriteLine("    " + prop.propertyPath + " [" + prop.propertyType + "] = " + PropertyValue(prop));
                        } while (prop.NextVisible(false));
                    }
                }
                catch (Exception ex) { w.WriteLine("    SERIALIZE_ERROR " + ex.Message); }
            }
        }
    }

    private static string PropertyValue(SerializedProperty p)
    {
        try
        {
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer: return p.longValue.ToString();
                case SerializedPropertyType.Boolean: return p.boolValue.ToString();
                case SerializedPropertyType.Float: return p.doubleValue.ToString("R");
                case SerializedPropertyType.String: return p.stringValue;
                case SerializedPropertyType.Enum: return p.enumValueIndex + " (" + p.enumDisplayNames[Mathf.Clamp(p.enumValueIndex, 0, p.enumDisplayNames.Length - 1)] + ")";
                case SerializedPropertyType.ObjectReference:
                    UnityEngine.Object obj = p.objectReferenceValue;
                    return obj == null ? "NULL" : obj.name + " | " + AssetDatabase.GetAssetPath(obj);
                case SerializedPropertyType.Vector2: return p.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return p.vector3Value.ToString();
                case SerializedPropertyType.Vector4: return p.vector4Value.ToString();
                case SerializedPropertyType.Color: return p.colorValue.ToString();
                case SerializedPropertyType.LayerMask: return "mask=" + p.intValue;
                case SerializedPropertyType.ArraySize: return p.intValue.ToString();
                case SerializedPropertyType.Quaternion: return p.quaternionValue.eulerAngles.ToString();
                default: return p.propertyType.ToString();
            }
        }
        catch { return "UNREADABLE"; }
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }
    #endregion

    #region Static_Analysis
    private static void WriteIssues(string folder)
    {
        using (StreamWriter w = new StreamWriter(Path.Combine(folder, "detected_issues.tsv")))
        {
            w.WriteLine("Severity\tAsset\tObject\tIssue\tRecommendation");
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    {
                        GameObject go = t.gameObject;
                        foreach (Component c in go.GetComponents<Component>())
                            if (c == null) Issue(w, "ERROR", path, GetPath(t), "Missing script component", "Identify or remove missing component after review");
                        Rigidbody rb = go.GetComponent<Rigidbody>();
                        MonoBehaviour[] behaviours = go.GetComponents<MonoBehaviour>();
                        bool crawler = false, sensor = false;
                        foreach (MonoBehaviour mb in behaviours)
                        {
                            if (mb == null) continue;
                            if (mb.GetType().Name == "EnemySurfaceCrawler")
                            {
                                crawler = true;
                                SerializedObject so = new SerializedObject(mb);
                                SerializedProperty layers = so.FindProperty("climbableLayers");
                                if (layers != null && layers.intValue == 0)
                                    Issue(w, "ERROR", path, GetPath(t), "Crawler climbableLayers = Nothing", "Select terrain and platform layers");
                                SerializedProperty offset = so.FindProperty("surfaceOffset");
                                if (offset != null && offset.floatValue <= 0f)
                                    Issue(w, "WARNING", path, GetPath(t), "Surface offset <= 0", "Compare offset with collider extents");
                            }
                            if (mb.GetType().Name == "EnemyNavigationSensor") sensor = true;
                        }
                        if (crawler && rb == null) Issue(w, "ERROR", path, GetPath(t), "Crawler missing Rigidbody", "Add Rigidbody");
                        if (crawler && rb != null && (!rb.isKinematic || rb.useGravity))
                            Issue(w, "WARNING", path, GetPath(t), "Crawler Rigidbody settings differ from kinematic controller", "Review isKinematic=true and useGravity=false");
                        if (crawler && !sensor)
                            Issue(w, "INFO", path, GetPath(t), "No EnemyNavigationSensor", "Attach sensor if using sensor-driven navigation");
                    }
                }
                catch (Exception ex) { Issue(w, "WARNING", path, "", "Could not inspect prefab: " + ex.Message, "Check prefab"); }
                finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
    private static void Issue(StreamWriter w, string severity, string asset, string obj, string issue, string recommendation)
    { w.WriteLine(string.Join("\t", new [] { severity, asset, obj, issue, recommendation })); }
    #endregion

    #region Runtime_Recorder
    private static void StartRecording()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play Mode first."); return; }
        runtimeLines.Clear();
        runtimeLines.Add("utc,frame,object,position,rotation,velocity,angularVelocity,kinematic,colliderBounds,hasSurface,isJumping,isStuck,surfaceNormal");
        lastSample = 0;
        recording = true;
        EditorApplication.update -= SampleRuntime;
        EditorApplication.update += SampleRuntime;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }
    private static void StopRecording()
    {
        recording = false;
        EditorApplication.update -= SampleRuntime;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }
    private static void OnPlayModeChanged(PlayModeStateChange state)
    { if (state == PlayModeStateChange.ExitingPlayMode) StopRecording(); }
    private static void SampleRuntime()
    {
        if (!recording || !EditorApplication.isPlaying) return;
        if (EditorApplication.timeSinceStartup - lastSample < 0.1) return;
        lastSample = EditorApplication.timeSinceStartup;
        MonoBehaviour[] all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (MonoBehaviour mb in all)
        {
            if (mb == null || mb.GetType().Name != "EnemySurfaceCrawler") continue;
            Rigidbody rb = mb.GetComponent<Rigidbody>();
            Collider col = mb.GetComponent<Collider>();
            SerializedObject so = new SerializedObject(mb);
            // Public properties may be unavailable in SerializedObject; read safely by reflection.
            string surface = ReadProperty(mb, "HasSurface");
            string jumping = ReadProperty(mb, "IsJumping");
            string stuck = ReadProperty(mb, "IsStuck");
            string normal = ReadProperty(mb, "SurfaceNormal");
            string line = string.Join(",", new [] {
                DateTime.UtcNow.ToString("o"), Time.frameCount.ToString(), Csv(mb.gameObject.name), Csv(mb.transform.position.ToString("F3")),
                Csv(mb.transform.rotation.eulerAngles.ToString("F2")), Csv(rb == null ? "" : rb.linearVelocity.ToString("F3")),
                Csv(rb == null ? "" : rb.angularVelocity.ToString("F3")), rb == null ? "" : rb.isKinematic.ToString(),
                Csv(col == null ? "" : col.bounds.ToString()), surface, jumping, stuck, Csv(normal)
            });
            runtimeLines.Add(line);
            if (runtimeLines.Count >= maxSamples) { StopRecording(); Debug.LogWarning("[TBB Scanner] Runtime sample limit reached."); break; }
        }
    }
    private static string ReadProperty(object obj, string name)
    {
        try { var p = obj.GetType().GetProperty(name); return p == null ? "" : Convert.ToString(p.GetValue(obj, null)); }
        catch { return ""; }
    }
    private static string Csv(string value) { return "\"" + (value ?? "").Replace("\"", "\"\"") + "\""; }
    #endregion

    #region File_Helpers
    private static void CopyFiles(string from, string to, params string[] extensions)
    {
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            bool match = false;
            foreach (string ext in extensions) if (file.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) match = true;
            if (!match) continue;
            string rel = file.Substring(from.Length).TrimStart(Path.DirectorySeparatorChar);
            string dest = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(file, dest, true);
        }
    }
    private static void CopyDirectory(string from, string to, bool filterSecrets)
    {
        if (!Directory.Exists(from)) return;
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file).ToLowerInvariant();
            if (filterSecrets && (name.Contains("secret") || name.Contains("token") || name.Contains("credential") || name.EndsWith(".key") || name.EndsWith(".pem"))) continue;
            string rel = file.Substring(from.Length).TrimStart(Path.DirectorySeparatorChar);
            string dest = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(file, dest, true);
        }
    }
    #endregion
}
#endif
