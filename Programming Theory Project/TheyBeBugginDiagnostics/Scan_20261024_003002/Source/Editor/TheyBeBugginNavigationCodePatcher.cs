#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in source-code patch for the EnemySurfaceCrawler version included in
/// Scan_20261023_233353.zip. It never changes files on import or in Play Mode.
/// </summary>
public sealed class TheyBeBugginNavigationCodePatcher : EditorWindow
{
    #region Configuration
    private const string Target = "Assets/Scripts/EnemySurfaceCrawler.cs";
    private const string PatchId = "TBB_NAV_PATCH_20261023_V1";
    private bool disableDoubleSteering = true;
    private bool improveWallContact = true;
    private bool stopUnsupportedMovement = true;
    private string status = "Click Preview to inspect the source-code changes.";
    private Vector2 scroll;
    #endregion

    #region Editor_Window
    [MenuItem("Tools/They Be Buggin/Navigation Code Patcher")]
    private static void Open() { GetWindow<TheyBeBugginNavigationCodePatcher>("Navigation Patcher").minSize = new Vector2(650, 400); }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("They Be Buggin — Navigation Code Patcher", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Targets the crawler structure in your October 23 diagnostic ZIP. Makes limited code corrections, not a guaranteed full navigation solution. Backups are saved outside Assets. Stop Play Mode first.", MessageType.Warning);
        disableDoubleSteering = EditorGUILayout.ToggleLeft("Use sensor as primary steering (avoid conflicting double steering)", disableDoubleSteering);
        improveWallContact = EditorGUILayout.ToggleLeft("Prevent distant walls from being treated as connected surfaces", improveWallContact);
        stopUnsupportedMovement = EditorGUILayout.ToggleLeft("Prevent movement when the chosen direction has no valid surface", stopUnsupportedMovement);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("1. Preview Patch", GUILayout.Height(32))) Run(false);
            if (GUILayout.Button("2. Back Up and Apply Patch", GUILayout.Height(32)))
            {
                if (EditorUtility.DisplayDialog("Patch crawler source?", "A full backup of EnemySurfaceCrawler.cs will be created. This changes C# logic and requires gameplay testing. Continue?", "Apply", "Cancel")) Run(true);
            }
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(status, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    #endregion

    #region Patch_Execution
    private void Run(bool apply)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { status = "Stop Play Mode first."; return; }
        string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, Target.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolute)) { status = "Crawler not found: " + Target; return; }
        string original = File.ReadAllText(absolute);
        if (original.Contains(PatchId)) { status = "This patch has already been applied. Restore the backup to reapply."; return; }
        string changed = original;
        StringBuilder log = new StringBuilder();
        log.AppendLine("TARGET: " + Target);
        try
        {
            // Exact anchors are deliberate: abort instead of modifying an unknown script version.
            if (disableDoubleSteering)
            {
                string oldText = @"        // NEW: Let the raycast sensor refine
        // the chosen movement direction.
        //
        // Each enemy's sensor has its own
        // steering preferences and route memory.
        if (navigationSensor != null && hasSurface)
        {
            best =
                navigationSensor.GetNavigationDirection(
                    best
                );
        }";
                string newText = @"        // TBB: Use one steering authority. The sensor chooses among
        // nearby directions; the crawler alone moves the Rigidbody.
        if (navigationSensor != null && hasSurface)
        {
            Vector3 sensed = navigationSensor.GetNavigationDirection(toward + avoidance * 0.65f);
            if (sensed.sqrMagnitude > 0.0001f) best = sensed.normalized;
        }";
                changed = ReplaceExactlyOnce(changed, oldText, newText, "Sensor steering", log);
            }
            if (improveWallContact)
            {
                string oldText = @"            wall.distance <= maximumSurfaceGap)";
                string newText = @"            wall.distance <= Mathf.Min(maximumSurfaceGap, wallDetectionDistance) &&
            // A nearby wall is not enough: its contact point must be
            // close to the crawler's actual current attachment point.
            Vector3.Distance(position, wall.point + wall.normal * surfaceOffset)
                <= surfaceOffset + maximumSurfaceGap)";
                changed = ReplaceExactlyOnce(changed, oldText, newText, "Wall attachment clearance", log);
            }
            if (stopUnsupportedMovement)
            {
                string oldText = @"        return best.normalized;
    }";
                string newText = @"        // Never request normal crawling movement toward empty space.
        // Spiders may still cross gaps using their separate jump method.
        if (!TryFindConnectedSurface(position + best.normalized *
            Mathf.Min(edgeTurnSearchDistance, 0.5f), best, out _))
        {
            return Vector3.zero;
        }
        return best.normalized;
    }";
                changed = ReplaceExactlyOnce(changed, oldText, newText, "Unsupported direction guard", log);
            }
        }
        catch (InvalidOperationException ex) { status = "NO CHANGES MADE. " + ex.Message; return; }
        if (changed == original) { status = "No patch options selected."; return; }
        changed = "// " + PatchId + "\n" + changed;
        log.AppendLine("All anchors matched. No files have been changed yet." + (apply ? "" : " Preview only."));
        if (!apply) { status = log.ToString(); return; }
        string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TheyBeBugginFixBackups", DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_CodePatch");
        Directory.CreateDirectory(root);
        string backup = Path.Combine(root, "EnemySurfaceCrawler.cs");
        File.Copy(absolute, backup, false);
        File.WriteAllText(absolute, changed, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(root, "patch_report.txt"), log + "\nBackup: " + backup);
        AssetDatabase.ImportAsset(Target, ImportAssetOptions.ForceUpdate);
        status = log + "\nAPPLIED. Backup: " + backup + "\nCheck Console, then test climbing and edge transitions in Play Mode.";
    }

    private static string ReplaceExactlyOnce(string input, string oldText, string newText, string name, StringBuilder log)
    {
        // Normalize line endings to support Windows Unity projects.
        string normalized = input.Replace("\r\n", "\n");
        int first = normalized.IndexOf(oldText, StringComparison.Ordinal);
        if (first < 0) throw new InvalidOperationException(name + ": expected code not found. Your crawler differs from the scanned version.");
        if (normalized.IndexOf(oldText, first + oldText.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException(name + ": ambiguous multiple matches.");
        log.AppendLine("PATCH: " + name);
        return normalized.Substring(0, first) + newText + normalized.Substring(first + oldText.Length);
    }
    #endregion
}
#endif
