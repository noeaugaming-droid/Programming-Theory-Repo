
#if UNITY_EDITOR

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class TheyBeBugginProjectExporter : EditorWindow
{
    private bool includeAssets = true;
    private bool includePackages = true;
    private bool includeProjectSettings = true;
    private bool includeEditorLog = true;
    private bool includeDiagnostics = true;

    private Vector2 scroll;
    private string status = "Ready.";

    [MenuItem("Tools/They Be Buggin/Export Full Project For Rebuild")]
    public static void Open()
    {
        GetWindow<TheyBeBugginProjectExporter>(
            "TBB Project Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label(
            "THEY BE BUGGIN",
            EditorStyles.boldLabel);

        GUILayout.Label(
            "FULL PROJECT EXPORTER",
            EditorStyles.largeLabel);

        EditorGUILayout.HelpBox(
            "Creates a ZIP containing the project files " +
            "and a diagnostic report. It does not modify " +
            "your existing scenes, scripts, or prefabs.",
            MessageType.Info);

        includeAssets = EditorGUILayout.Toggle(
            "Include Assets", includeAssets);

        includePackages = EditorGUILayout.Toggle(
            "Include Packages", includePackages);

        includeProjectSettings = EditorGUILayout.Toggle(
            "Include Project Settings",
            includeProjectSettings);

        includeEditorLog = EditorGUILayout.Toggle(
            "Include Unity Editor Log",
            includeEditorLog);

        includeDiagnostics = EditorGUILayout.Toggle(
            "Include Diagnostics",
            includeDiagnostics);

        EditorGUILayout.Space();

        if (GUILayout.Button(
            "EXPORT PROJECT ZIP",
            GUILayout.Height(50)))
        {
            ExportProject();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.TextArea(
            status,
            GUILayout.ExpandHeight(true));

        EditorGUILayout.EndScrollView();
    }

    private void ExportProject()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Stop Play Mode",
                "Exit Play Mode before exporting.",
                "OK");
            return;
        }

        if (!EditorSceneManager
            .SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string projectRoot =
            Directory.GetParent(Application.dataPath).FullName;

        string defaultName =
            "TheyBeBuggin_FullProject_" +
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            ".zip";

        string destination =
            EditorUtility.SaveFilePanel(
                "Save Project Export",
                projectRoot,
                defaultName,
                "zip");

        if (string.IsNullOrEmpty(destination))
            return;

        string temporaryFolder =
            Path.Combine(
                Path.GetTempPath(),
                "TBB_Export_" +
                Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(temporaryFolder);

            var report = new StringBuilder();

            report.AppendLine(
                "THEY BE BUGGIN - FULL PROJECT DIAGNOSTICS");

            report.AppendLine(
                "Unity Version: " +
                Application.unityVersion);

            report.AppendLine(
                "Project: " +
                Application.productName);

            report.AppendLine(
                "Generated: " +
                DateTime.Now.ToString("O"));

            report.AppendLine();

            if (includeAssets)
            {
                CopyDirectory(
                    Path.Combine(projectRoot, "Assets"),
                    Path.Combine(temporaryFolder, "Assets"));
            }

            if (includePackages)
            {
                CopyDirectory(
                    Path.Combine(projectRoot, "Packages"),
                    Path.Combine(temporaryFolder, "Packages"));
            }

            if (includeProjectSettings)
            {
                CopyDirectory(
                    Path.Combine(
                        projectRoot,
                        "ProjectSettings"),
                    Path.Combine(
                        temporaryFolder,
                        "ProjectSettings"));
            }

            if (includeDiagnostics)
            {
                BuildDiagnostics(report);

                string diagnosticsDirectory =
                    Path.Combine(
                        temporaryFolder,
                        "RebuildDiagnostics");

                Directory.CreateDirectory(
                    diagnosticsDirectory);

                File.WriteAllText(
                    Path.Combine(
                        diagnosticsDirectory,
                        "ProjectDiagnostics.txt"),
                    report.ToString(),
                    new UTF8Encoding(false));
            }

            if (includeEditorLog)
            {
                CopyEditorLog(temporaryFolder);
            }

            if (File.Exists(destination))
                File.Delete(destination);

            ZipFile.CreateFromDirectory(
                temporaryFolder,
                destination,
                System.IO.Compression.CompressionLevel.Optimal,
                false);

            FileInfo zip = new FileInfo(destination);

            status =
                "EXPORT COMPLETE\n\n" +
                "File: " + destination + "\n" +
                "Size: " +
                (zip.Length / 1024f / 1024f)
                    .ToString("F2") +
                " MB\n\n" +
                "Upload this ZIP to ChatGPT.";

            Debug.Log(status);

            EditorUtility.DisplayDialog(
                "Export Complete",
                "Project ZIP created successfully.\n\n" +
                destination,
                "OK");

            EditorUtility.RevealInFinder(destination);
        }
        catch (Exception ex)
        {
            status =
                "EXPORT FAILED\n\n" + ex;

            Debug.LogException(ex);

            EditorUtility.DisplayDialog(
                "Export Failed",
                ex.Message,
                "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            if (Directory.Exists(temporaryFolder))
            {
                try
                {
                    Directory.Delete(
                        temporaryFolder,
                        true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "Temporary export cleanup failed: " +
                        ex.Message);
                }
            }

            Repaint();
        }
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        if (!Directory.Exists(source))
            return;

        Directory.CreateDirectory(destination);

        string[] files =
            Directory.GetFiles(
                source,
                "*",
                SearchOption.AllDirectories);

        for (int i = 0; i < files.Length; i++)
        {
            string file = files[i];

            string relative =
                file.Substring(source.Length)
                    .TrimStart(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);

            string target =
                Path.Combine(destination, relative);

            string folder =
                Path.GetDirectoryName(target);

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            EditorUtility.DisplayProgressBar(
                "Exporting THEY BE BUGGIN",
                relative,
                (float)(i + 1) /
                Mathf.Max(1, files.Length));

            File.Copy(file, target, true);
        }
    }

    private static void BuildDiagnostics(
        StringBuilder report)
    {
        report.AppendLine(
            "========================================");
        report.AppendLine("SCENES");
        report.AppendLine(
            "========================================");

        string[] sceneGuids =
            AssetDatabase.FindAssets("t:Scene");

        foreach (string guid in sceneGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            report.AppendLine("Scene: " + path);
        }

        report.AppendLine();

        report.AppendLine(
            "========================================");
        report.AppendLine("PREFABS");
        report.AppendLine(
            "========================================");

        string[] prefabGuids =
            AssetDatabase.FindAssets("t:Prefab");

        foreach (string guid in prefabGuids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);

            if (prefab == null)
                continue;

            report.AppendLine();
            report.AppendLine("PREFAB: " + path);

            DescribeHierarchy(
                prefab.transform,
                report,
                0);
        }

        report.AppendLine();

        report.AppendLine(
            "========================================");
        report.AppendLine("CURRENT SCENE HIERARCHY");
        report.AppendLine(
            "========================================");

        Scene scene = SceneManager.GetActiveScene();

        report.AppendLine(
            "Active Scene: " + scene.path);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            DescribeHierarchy(
                root.transform,
                report,
                0);
        }

        report.AppendLine();

        report.AppendLine(
            "========================================");
        report.AppendLine("PHYSICS LAYERS");
        report.AppendLine(
            "========================================");

        for (int i = 0; i < 32; i++)
        {
            report.AppendLine(
                i + ": " +
                LayerMask.LayerToName(i));
        }

        report.AppendLine();

        report.AppendLine(
            "========================================");
        report.AppendLine("PHYSICS COLLISION MATRIX");
        report.AppendLine(
            "========================================");

        for (int a = 0; a < 32; a++)
        {
            for (int b = a; b < 32; b++)
            {
                if (!Physics.GetIgnoreLayerCollision(a, b))
                {
                    report.AppendLine(
                        a + " <-> " + b +
                        ": COLLIDE");
                }
            }
        }

        report.AppendLine();

        report.AppendLine(
            "========================================");
        report.AppendLine("ASSET INVENTORY");
        report.AppendLine(
            "========================================");

        string[] allAssets =
            AssetDatabase.GetAllAssetPaths();

        foreach (string path in allAssets)
        {
            if (!path.StartsWith("Assets/"))
                continue;

            report.AppendLine(path);
        }

        report.AppendLine();

        report.AppendLine(
            "========================================");
        report.AppendLine("BUILD SCENES");
        report.AppendLine(
            "========================================");

        foreach (EditorBuildSettingsScene buildScene in
                 EditorBuildSettings.scenes)
        {
            report.AppendLine(
                buildScene.path +
                " | Enabled: " +
                buildScene.enabled);
        }
    }

    private static void DescribeHierarchy(
        Transform transform,
        StringBuilder report,
        int depth)
    {
        string indent =
            new string(' ', depth * 2);

        GameObject go = transform.gameObject;

        report.AppendLine(
            indent +
            "GAMEOBJECT: " + go.name);

        report.AppendLine(
            indent +
            "  Active: " + go.activeSelf);

        report.AppendLine(
            indent +
            "  Layer: " +
            LayerMask.LayerToName(go.layer) +
            " (" + go.layer + ")");

        report.AppendLine(
            indent +
            "  Position: " +
            transform.localPosition);

        report.AppendLine(
            indent +
            "  Rotation: " +
            transform.localEulerAngles);

        report.AppendLine(
            indent +
            "  Scale: " +
            transform.localScale);

        Component[] components =
            go.GetComponents<Component>();

        foreach (Component component in components)
        {
            if (component == null)
            {
                report.AppendLine(
                    indent +
                    "  MISSING SCRIPT");
                continue;
            }

            report.AppendLine(
                indent +
                "  COMPONENT: " +
                component.GetType().FullName);

            DescribeComponent(
                component,
                report,
                indent + "    ");
        }

        foreach (Transform child in transform)
        {
            DescribeHierarchy(
                child,
                report,
                depth + 1);
        }
    }

    private static void DescribeComponent(
        Component component,
        StringBuilder report,
        string indent)
    {
        try
        {
            SerializedObject so =
                new SerializedObject(component);

            SerializedProperty property =
                so.GetIterator();

            bool enterChildren = true;

            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;

                string value =
                    DescribeProperty(property);

                report.AppendLine(
                    indent +
                    property.propertyPath +
                    " = " + value);
            }
        }
        catch (Exception ex)
        {
            report.AppendLine(
                indent +
                "INSPECTION ERROR: " +
                ex.Message);
        }
    }

    private static string DescribeProperty(
        SerializedProperty property)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.longValue.ToString();

            case SerializedPropertyType.Boolean:
                return property.boolValue.ToString();

            case SerializedPropertyType.Float:
                return property.doubleValue.ToString("R");

            case SerializedPropertyType.String:
                return property.stringValue;

            case SerializedPropertyType.Color:
                return property.colorValue.ToString();

            case SerializedPropertyType.Vector2:
                return property.vector2Value.ToString();

            case SerializedPropertyType.Vector3:
                return property.vector3Value.ToString();

            case SerializedPropertyType.Vector4:
                return property.vector4Value.ToString();

            case SerializedPropertyType.Quaternion:
                return property.quaternionValue.ToString();

            case SerializedPropertyType.Enum:
                return property.enumValueIndex.ToString();

            case SerializedPropertyType.ObjectReference:
                {
                    UnityEngine.Object obj =
                        property.objectReferenceValue;

                    if (obj == null)
                        return "NULL";

                    string path =
                        AssetDatabase.GetAssetPath(obj);

                    return obj.name +
                        " | Type: " +
                        obj.GetType().Name +
                        " | Asset: " +
                        path;
                }

            default:
                return property.propertyType.ToString();
        }
    }

    private static void CopyEditorLog(
        string temporaryFolder)
    {
        string logPath = "";

#if UNITY_EDITOR_WIN
        logPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Unity",
            "Editor",
            "Editor.log");
#elif UNITY_EDITOR_OSX
        logPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.Personal),
            "Library",
            "Logs",
            "Unity",
            "Editor.log");
#elif UNITY_EDITOR_LINUX
        logPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.Personal),
            ".config",
            "unity3d",
            "Editor.log");
#endif

        if (!string.IsNullOrEmpty(logPath) &&
            File.Exists(logPath))
        {
            string directory =
                Path.Combine(
                    temporaryFolder,
                    "RebuildDiagnostics");

            Directory.CreateDirectory(directory);

            File.Copy(
                logPath,
                Path.Combine(
                    directory,
                    "UnityEditor.log"),
                true);
        }
    }
}

#endif
