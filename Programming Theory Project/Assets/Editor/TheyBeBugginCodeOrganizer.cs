#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Safely organizes existing C# source with beginner-friendly region headings.
/// This tool does NOT change method bodies, identifiers, or serialized fields.
/// </summary>
public class TheyBeBugginCodeOrganizer : EditorWindow
{
    #region Editor_Window_Variables

    private bool includeEditorScripts;
    private bool includeGeneratedScripts;
    private bool includeTutorialScripts;
    private bool addBeginnerComments = true;
    private Vector2 scrollPosition;
    private string previewText = "Click Preview to see the proposed changes.";
    private readonly List<Change> pendingChanges = new List<Change>();
    private string lastBackupDirectory = "";

    private sealed class Change
    {
        public string AssetPath;
        public string Original;
        public string Updated;
        public int RegionCount;
    }

    #endregion

    #region Unity_Editor_Methods

    [MenuItem("Tools/They Be Buggin/Beginner Code Organizer")]
    public static void OpenWindow()
    {
        GetWindow<TheyBeBugginCodeOrganizer>("Code Organizer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("THEY BE BUGGIN - BEGINNER CODE ORGANIZER", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Organizes your C# scripts with descriptive #region sections and optional explanatory comments. " +
            "It preserves existing statements, method bodies, names, and Inspector field serialization. " +
            "No automated logic rewrites are performed.", MessageType.Info);

        EditorGUILayout.Space();
        includeEditorScripts = EditorGUILayout.ToggleLeft("Include Assets/Editor scripts", includeEditorScripts);
        includeGeneratedScripts = EditorGUILayout.ToggleLeft("Include generated runtime scripts", includeGeneratedScripts);
        includeTutorialScripts = EditorGUILayout.ToggleLeft("Include Unity tutorial scripts", includeTutorialScripts);
        addBeginnerComments = EditorGUILayout.ToggleLeft("Add beginner-friendly region explanations", addBeginnerComments);

        EditorGUILayout.HelpBox(
            "By default, only your gameplay scripts in Assets/Scripts are included. " +
            "Scripts with existing #region directives are skipped to avoid conflicting region layouts. " +
            "Files with uncertain syntax are left unchanged.", MessageType.None);

        EditorGUILayout.Space();
        if (GUILayout.Button("1. Preview Changes (No Files Modified)", GUILayout.Height(36)))
            BuildPreview();

        EditorGUI.BeginDisabledGroup(pendingChanges.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode);
        if (GUILayout.Button("2. Back Up and Apply Previewed Changes", GUILayout.Height(36)))
            ApplyChanges();
        EditorGUI.EndDisabledGroup();

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("Exit Play Mode before applying changes.", MessageType.Warning);

        if (GUILayout.Button("Open Most Recent Backup Folder") && Directory.Exists(lastBackupDirectory))
            EditorUtility.RevealInFinder(lastBackupDirectory);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview / Report", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.TextArea(previewText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    #endregion

    #region Script_Discovery_Methods

    private static bool IsEligible(string assetPath, bool editor, bool generated, bool tutorial)
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return false;
        if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) return false;
        if (path.IndexOf("/TutorialInfo/", StringComparison.OrdinalIgnoreCase) >= 0) return tutorial;
        if (path.IndexOf("/TheyBeBugginGenerated/", StringComparison.OrdinalIgnoreCase) >= 0) return generated;
        if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) return editor;
        return path.StartsWith("Assets/Scripts/", StringComparison.OrdinalIgnoreCase);
    }

    private void BuildPreview()
    {
        pendingChanges.Clear();
        StringBuilder report = new StringBuilder();
        report.AppendLine("PREVIEW - no project files have been changed");
        report.AppendLine("================================================");

        string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int unchanged = 0;
        int skipped = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!paths.Add(path) || !IsEligible(path, includeEditorScripts, includeGeneratedScripts, includeTutorialScripts))
                continue;

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath)) continue;
            string original = File.ReadAllText(fullPath);
            string reason;
            int regionCount;
            string updated = OrganizeSource(original, path, addBeginnerComments, out regionCount, out reason);

            if (updated == null)
            {
                skipped++;
                report.AppendLine("SKIPPED: " + path + " - " + reason);
            }
            else if (updated == original)
            {
                unchanged++;
                report.AppendLine("UNCHANGED: " + path);
            }
            else
            {
                pendingChanges.Add(new Change
                {
                    AssetPath = path,
                    Original = original,
                    Updated = updated,
                    RegionCount = regionCount
                });
                report.AppendLine("READY: " + path + " (" + regionCount + " regions)");
            }
        }

        report.AppendLine();
        report.AppendLine("Files ready: " + pendingChanges.Count);
        report.AppendLine("Already organized / no eligible members: " + unchanged);
        report.AppendLine("Skipped for safety: " + skipped);
        report.AppendLine("Preview again after changing any options or editing source files.");
        previewText = report.ToString();
        Repaint();
    }

    #endregion

    #region Code_Organization_Methods

    // A conservative scanner: it recognizes only simple, one-line declarations.
    // Everything else is preserved byte-for-byte, apart from inserted region lines.
    private static string OrganizeSource(string source, string assetPath, bool comments,
        out int regionCount, out string reason)
    {
        regionCount = 0;
        reason = "";
        if (Regex.IsMatch(source, @"(?m)^\s*#\s*(region|endregion)\b"))
        {
            reason = "Already contains regions";
            return null;
        }

        string newline = source.Contains("\r\n") ? "\r\n" : "\n";
        string[] lines = Regex.Split(source, "\r\n|\n");
        int[] depths = new int[lines.Length];
        bool[] codeLine = new bool[lines.Length];
        int depth = 0;
        bool blockComment = false;
        bool verbatimString = false;
        bool regularString = false;
        bool charLiteral = false;
        bool escaped = false;
        bool invalid = false;

        for (int i = 0; i < lines.Length; i++)
        {
            depths[i] = depth;
            string line = lines[i];
            for (int j = 0; j < line.Length; j++)
            {
                char c = line[j];
                char next = j + 1 < line.Length ? line[j + 1] : '\0';
                if (blockComment)
                {
                    if (c == '*' && next == '/') { blockComment = false; j++; }
                    continue;
                }
                if (verbatimString)
                {
                    if (c == '"' && next == '"') { j++; continue; }
                    if (c == '"') verbatimString = false;
                    continue;
                }
                if (regularString || charLiteral)
                {
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\') { escaped = true; continue; }
                    if ((regularString && c == '"') || (charLiteral && c == '\''))
                    { regularString = false; charLiteral = false; }
                    continue;
                }
                if (c == '/' && next == '/') break;
                if (c == '/' && next == '*') { blockComment = true; j++; continue; }
                if (c == '@' && next == '"') { verbatimString = true; j++; continue; }
                if (c == '"') { regularString = true; continue; }
                if (c == '\'') { charLiteral = true; continue; }
                if (c == '{') { depth++; codeLine[i] = true; }
                if (c == '}') { depth--; codeLine[i] = true; if (depth < 0) invalid = true; }
            }
        }

        if (invalid || depth != 0 || blockComment || verbatimString || regularString || charLiteral)
        {
            reason = "Unbalanced braces or unterminated comment/string";
            return null;
        }

        // Restrict to ordinary single-class files. Complex declarations remain untouched.
        int classLine = -1;
        int classDepth = -1;
        int classCount = 0;
        Regex classPattern = new Regex(@"\b(class|struct)\s+[A-Za-z_]\w*");
        for (int i = 0; i < lines.Length; i++)
        {
            string trim = lines[i].Trim();
            if (trim.StartsWith("//") || trim.StartsWith("*") || trim.StartsWith("/*")) continue;
            if (!classPattern.IsMatch(lines[i])) continue;
            classCount++;
            if (classLine >= 0) continue;
            for (int j = i; j < Math.Min(i + 8, lines.Length); j++)
            {
                if (lines[j].Contains("{"))
                {
                    classLine = j;
                    classDepth = depths[j] + 1;
                    break;
                }
            }
        }
        if (classCount != 1 || classLine < 0)
        {
            reason = "Requires exactly one recognizable class/struct";
            return null;
        }

        // Ignore declarations containing preprocessor conditional boundaries inside the class.
        int closeLine = -1;
        for (int i = classLine + 1; i < lines.Length; i++)
        {
            if (depths[i] == classDepth && Regex.IsMatch(lines[i], @"^\s*}"))
            { closeLine = i; break; }
        }
        if (closeLine < 0) { reason = "Class closing brace not found"; return null; }

        for (int i = classLine + 1; i < closeLine; i++)
        {
            if (Regex.IsMatch(lines[i], @"^\s*#\s*(if|elif|else|endif)\b"))
            {
                reason = "Conditional compilation inside class; skipped for safety";
                return null;
            }
        }

        List<int> starts = new List<int>();
        List<string> labels = new List<string>();
        for (int i = classLine + 1; i < closeLine; i++)
        {
            if (depths[i] != classDepth) continue;
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("*")) continue;
            if (line.StartsWith("#")) continue;
            // Handle attributes written on the same line as a declaration.
            string declaration = line;
            while (declaration.StartsWith("["))
            {
                int bracketEnd = declaration.IndexOf(']');
                if (bracketEnd < 0) break;
                declaration = declaration.Substring(bracketEnd + 1).TrimStart();
            }
            if (declaration.Length == 0) continue;

            bool field = Regex.IsMatch(declaration,
                @"^(?:(?:public|private|protected|internal|static|readonly|const|volatile|new|unsafe|sealed|partial)\s+)+[\w<>,.?\[\] ]+\s+\w+\s*(?:=\s*[^;]+)?;\s*(?://.*)?$");
            bool method = Regex.IsMatch(declaration,
                @"^(?:(?:public|private|protected|internal|static|virtual|override|abstract|sealed|async|new|extern|unsafe|partial)\s+)+[\w<>,.?\[\] ]+\s+\w+\s*\([^;]*\)\s*(?:\{|=>|$)");
            if (!field && !method) continue;

            string name = ExtractMemberName(declaration, method);
            string label = ClassifyRegion(assetPath, name, method);
            int start = i;
            // Attach immediately preceding attributes to the same region.
            while (start > classLine + 1 && depths[start - 1] == classDepth &&
                   lines[start - 1].TrimStart().StartsWith("[")) start--;
            starts.Add(start);
            labels.Add(label);
        }
        if (starts.Count == 0) return source;

        // Only place boundaries at declaration starts; do not move existing code.
        Dictionary<int, List<string>> before = new Dictionary<int, List<string>>();
        string active = null;
        for (int i = 0; i < starts.Count; i++)
        {
            if (labels[i] == active) continue;
            if (active != null) AddLine(before, starts[i], "    #endregion");
            AddLine(before, starts[i], "    #region " + labels[i]);
            if (comments) AddLine(before, starts[i], "    // " + DescribeRegion(labels[i]));
            active = labels[i];
            regionCount++;
        }
        if (active != null) AddLine(before, closeLine, "    #endregion");

        StringBuilder output = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            List<string> insertions;
            if (before.TryGetValue(i, out insertions))
            {
                foreach (string insertion in insertions) output.Append(insertion).Append(newline);
            }
            output.Append(lines[i]);
            if (i < lines.Length - 1) output.Append(newline);
        }
        return output.ToString();
    }

    private static void AddLine(Dictionary<int, List<string>> before, int index, string line)
    {
        List<string> list;
        if (!before.TryGetValue(index, out list))
        {
            list = new List<string>();
            before.Add(index, list);
        }
        list.Add(line);
    }

    private static string ExtractMemberName(string declaration, bool method)
    {
        Match match = method
            ? Regex.Match(declaration, @"([A-Za-z_]\w*)\s*\(")
            : Regex.Match(declaration, @"([A-Za-z_]\w*)\s*(?:=|;)");
        return match.Success ? match.Groups[1].Value : "General";
    }

    private static string ClassifyRegion(string path, string memberName, bool method)
    {
        string name = memberName.ToLowerInvariant();
        string file = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        string suffix = method ? "Methods" : "Variables";
        if (Regex.IsMatch(name, @"^(awake|start|update|fixedupdate|lateupdate|onenable|ondisable|ondestroy|ongui|onvalidate|reset|oncollision|ontrigger|onapplication|onbecame)"))
            return "Unity_Lifecycle_" + suffix;
        if (Regex.IsMatch(name, "camera|fov|zoom|aim|look|crosshair|pitch|yaw|recoil"))
            return "Camera_And_Aiming_" + suffix;
        if (Regex.IsMatch(name, "flip|cartwheel|trick|landing|ground|jump|dash|move|speed|velocity|gravity|physics|force|rigidbody|collision|surface|crawl"))
            return "Physics_And_Movement_" + suffix;
        if (Regex.IsMatch(name, "spawn|wave|enemy|spider|beetle|patrol|chase|attack|target|ai"))
            return "Enemies_And_Spawning_" + suffix;
        if (Regex.IsMatch(name, "health|damage|death|die|bullet|shoot|fire|weapon|hit"))
            return "Combat_And_Health_" + suffix;
        if (Regex.IsMatch(name, "canvas|ui|text|hud|menu|panel|score|gameover"))
            return "UI_And_Game_State_" + suffix;
        if (file.Contains("camera") || file.Contains("aim")) return "Camera_And_Aiming_" + suffix;
        if (file.Contains("enemy") || file.Contains("spider") || file.Contains("beetle") || file.Contains("spawn"))
            return "Enemies_And_Spawning_" + suffix;
        if (file.Contains("player")) return "Player_" + suffix;
        if (file.Contains("editor") || path.Contains("/Editor/")) return "Editor_Tools_" + suffix;
        return "General_" + suffix;
    }

    private static string DescribeRegion(string label)
    {
        if (label.EndsWith("Variables")) return "Values and references used by this part of the script.";
        if (label.StartsWith("Unity_Lifecycle")) return "Methods Unity calls automatically during the object's lifetime.";
        if (label.StartsWith("Physics")) return "Movement, jumping, collisions, and other physics behavior.";
        if (label.StartsWith("Camera")) return "Camera positioning, looking, and aiming behavior.";
        if (label.StartsWith("Enemies")) return "Enemy behavior and spawning logic.";
        if (label.StartsWith("Combat")) return "Attacks, damage, and health-related behavior.";
        if (label.StartsWith("UI")) return "User interface and game-state behavior.";
        return "Methods that help this script do its job.";
    }

    #endregion

    #region Backup_And_Apply_Methods

    private void ApplyChanges()
    {
        if (pendingChanges.Count == 0 || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorUtility.DisplayDialog("Apply Script Organization?",
            "This will back up " + pendingChanges.Count + " original files and insert region headings/comments. " +
            "No method bodies or existing identifiers will be rewritten. Continue?", "Back Up and Apply", "Cancel")) return;

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string backupRoot = Path.Combine(projectRoot, "TheyBeBuggin_CodeBackups");
        string backup = Path.Combine(backupRoot, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(backup);
        lastBackupDirectory = backup;

        StringBuilder log = new StringBuilder();
        log.AppendLine("THEY BE BUGGIN CODE ORGANIZER");
        log.AppendLine("Backup: " + backup);
        int applied = 0;
        try
        {
            // Verify that no file changed since Preview. Fail before writing anything.
            foreach (Change change in pendingChanges)
            {
                if (File.ReadAllText(Path.GetFullPath(change.AssetPath)) != change.Original)
                    throw new IOException("Source changed since preview: " + change.AssetPath + ". Preview again.");
            }
            // Back up every source first, preserving the folder layout.
            foreach (Change change in pendingChanges)
            {
                string destination = Path.Combine(backup, change.AssetPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(Path.GetFullPath(change.AssetPath), destination, true);
                log.AppendLine("BACKED UP: " + change.AssetPath);
            }
            File.WriteAllText(Path.Combine(backup, "OrganizerReport.txt"), log.ToString());

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (Change change in pendingChanges)
                {
                    File.WriteAllText(Path.GetFullPath(change.AssetPath), change.Updated, new UTF8Encoding(false));
                    applied++;
                    log.AppendLine("ORGANIZED: " + change.AssetPath + " (" + change.RegionCount + " regions)");
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }

            File.WriteAllText(Path.Combine(backup, "OrganizerReport.txt"), log.ToString());
            pendingChanges.Clear();
            previewText = log + "\nCompleted: " + applied + " files. Check the Unity Console for compilation errors.\n" +
                "To restore, copy the original .cs files from the backup folder over the corresponding project paths.";
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Organization Complete",
                "Organized " + applied + " scripts.\nBackup folder:\n" + backup, "OK");
        }
        catch (Exception exception)
        {
            Debug.LogError("Code Organizer failed: " + exception);
            previewText = "ERROR: " + exception.Message + "\nApplied before failure: " + applied +
                "\nBackup folder: " + backup + "\nRestore originals from this folder if needed.";
            AssetDatabase.Refresh();
        }
        Repaint();
    }

    #endregion
}
#endif
