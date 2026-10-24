#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public sealed class TheyBeBugginEnemyRepairV3 : EditorWindow
{
    private const string Target = "Assets/Scripts/EnemySurfaceCrawler.cs";
    private const string Marker = "TBB_COORDINATED_COLLIDER_REPAIR_20261024_V3";
    private const string Payload =
        "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" +
        "2+8f8Ubqcs/3wAAA==";
    private string report = "Preview before applying. Stop Play Mode.";
    private Vector2 scroll;

    [MenuItem("Tools/They Be Buggin/Enemy Collision Repair V3")]
    public static void Open() { GetWindow<TheyBeBugginEnemyRepairV3>("Enemy Repair V3"); }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Enemy Collider & Surface Repair V3", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Replaces only EnemySurfaceCrawler.cs. Preserves its .meta GUID and serialized fields. Makes an external backup before writing. Does not modify prefabs or scenes.", MessageType.Info);
        if (GUILayout.Button("1. Preview", GUILayout.Height(36))) Run(false);
        if (GUILayout.Button("2. Back Up and Apply", GUILayout.Height(36)) &&
            EditorUtility.DisplayDialog("Apply enemy repair?", "Back up and replace EnemySurfaceCrawler.cs?", "Apply", "Cancel")) Run(true);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void Run(bool apply)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        { report = "Stop Play Mode and wait for compilation."; return; }
        string root = Directory.GetParent(Application.dataPath).FullName;
        string path = Path.Combine(root, Target.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path)) { report = "Missing: " + Target; return; }
        try
        {
            string oldSource = File.ReadAllText(path);
            if (oldSource.Contains(Marker)) { report = "V3 already installed; no changes."; return; }
            string[] required = { "class EnemySurfaceCrawler", "TryBeginSpiderJump(", "SetMovement(", "ApplyKnockback(", "TryFindConnectedSurface(", "UpdateJump(float dt)", "MoveSafely(" };
            foreach (string token in required)
                if (!oldSource.Contains(token)) { report = "Unknown crawler version: missing " + token + ". No changes."; return; }
            string updated;
            using (MemoryStream memory = new MemoryStream(Convert.FromBase64String(Payload)))
            using (GZipStream gz = new GZipStream(memory, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(gz, Encoding.UTF8)) updated = reader.ReadToEnd();
            if (!updated.Contains(Marker)) throw new InvalidDataException("Embedded source marker missing.");
            report = "Ready: " + Target + "\nOriginal SHA256: " + Hash(oldSource) +
                "\nReplacement SHA256: " + Hash(updated) +
                "\n\nChanges: collision pose checks, penetration recovery, shape-aware jump sampling, safe rotation, surface reacquisition.\n" +
                "Preserves Spider/Beetle API and Inspector field names.\n";
            if (!apply) return;
            string backup = Path.Combine(root, "TheyBeBugginFixBackups", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_RepairV3");
            Directory.CreateDirectory(backup);
            File.Copy(path, Path.Combine(backup, "EnemySurfaceCrawler.cs"), false);
            string meta = path + ".meta";
            if (File.Exists(meta)) File.Copy(meta, Path.Combine(backup, "EnemySurfaceCrawler.cs.meta"), false);
            File.WriteAllText(Path.Combine(backup, "repair_report.txt"), report);
            string temp = path + ".tbb_tmp";
            try { File.WriteAllText(temp, updated, new UTF8Encoding(false)); File.Copy(temp, path, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            report += "\nAPPLIED. Backup: " + backup + "\nInspect Unity Console and test in Play Mode.";
            AssetDatabase.ImportAsset(Target, ImportAssetOptions.ForceUpdate);
        }
        catch (Exception ex) { report = "NO FURTHER CHANGES: " + ex; Debug.LogError(report); }
    }

    private static string Hash(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(bytes).Replace("-", "").Substring(0, 16);
        }
    }
}
#endif
