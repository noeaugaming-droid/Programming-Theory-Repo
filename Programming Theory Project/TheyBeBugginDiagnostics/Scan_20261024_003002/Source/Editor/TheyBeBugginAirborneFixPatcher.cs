#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in, backed-up patch for the crawler structure exported on Oct 23.
/// Does not patch on import, does not run during Play Mode.
/// </summary>
public sealed class TheyBeBugginAirborneFixPatcher : EditorWindow
{
    #region Settings
    private const string PathInProject = "Assets/Scripts/EnemySurfaceCrawler.cs";
    private const string Marker = "TBB_AIRBORNE_CONTACT_PATCH_V2";
    private string report = "Preview the patch before applying it.";
    private Vector2 scroll;
    #endregion

    #region Window
    [MenuItem("Tools/They Be Buggin/Airborne and Surface Fix Patcher")]
    private static void ShowWindow()
    {
        GetWindow<TheyBeBugginAirborneFixPatcher>("Airborne Fix").minSize = new Vector2(700, 420);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("They Be Buggin — Airborne / Surface Contact Fix", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Repairs specific surface-contact and fall/jump issues in the scanned EnemySurfaceCrawler. Does not guarantee all navigation bugs are fixed. Back up your project and stop Play Mode.", MessageType.Warning);
        if (GUILayout.Button("1. Preview Changes", GUILayout.Height(32))) Execute(false);
        if (GUILayout.Button("2. Back Up and Apply", GUILayout.Height(32)) &&
            EditorUtility.DisplayDialog("Apply source patch?", "Back up EnemySurfaceCrawler.cs and apply contact/airborne changes?", "Apply", "Cancel")) Execute(true);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }
    #endregion

    #region Patching
    private void Execute(bool apply)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { report = "Stop Play Mode first."; return; }
        string root = Directory.GetParent(Application.dataPath).FullName;
        string file = System.IO.Path.Combine(root, PathInProject.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (!File.Exists(file)) { report = "Cannot find " + PathInProject; return; }
        string original = File.ReadAllText(file);
        string updated = original.Replace("\r\n", "\n");
        if (updated.Contains(Marker)) { report = "Already patched. No changes made."; return; }
        try
        {
            // Fail closed if the source structure is not the one from the diagnostic scan.
            Require(updated, "private bool TryFindConnectedSurface(");
            Require(updated, "private void UpdateJump(float dt)");
            Require(updated, "private void Fall(float dt)");
            Require(updated, "private void FixedUpdate()");
            Require(updated, "private bool TrySurfaceRay(");

            updated = ReplaceMethod(updated, "    private bool TryFindConnectedSurface(",
                "    private bool TrySurfaceRay(", SurfaceMethod);
            updated = ReplaceMethod(updated, "    private void UpdateJump(float dt)",
                "    private void RotateToward(", JumpMethod);

            // Do not require a particular local-variable name in FixedUpdate.
            // Surface validation is handled by the replaced surface method.

            // Fix the first-frame gap where the crawler attaches, but keeps using the old rb.position.
            // Optional: older crawlers use this exact first-attachment sequence.
            const string attachOld = "            AttachToSurface(initialHit);\n            position = rb.position;";
            if (updated.Contains(attachOld))
                updated = ReplaceOnce(updated, attachOld,
                    "            AttachToSurface(initialHit);\n            position = initialHit.point + initialHit.normal * surfaceOffset;");

            // Do not snap to a new wall when the intended direction has no connected support.

            // Make the script easier to diagnose while testing.
            updated = updated.Replace("using UnityEngine;", "// " + Marker + "\nusing UnityEngine;");
        }
        catch (InvalidOperationException e) { report = "NO FILES CHANGED: " + e.Message; return; }

        report = "Ready to patch: " + PathInProject + "\n\n" +
            "Changes:\n- Prefer actual support contact over speculative wall contact\n" +
            "- Reject wall transitions when contact geometry is too far away\n" +
            "- Recheck jump landing at completion; fall if landing is invalid\n" +
            "- Correct initial attachment position bookkeeping\n\n" +
            "This does not add global pathfinding or guarantee corner climbing.\n";
        if (!apply) return;

        string backupDir = System.IO.Path.Combine(root, "TheyBeBugginFixBackups",
            DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_AirbornePatch");
        Directory.CreateDirectory(backupDir);
        string backup = System.IO.Path.Combine(backupDir, "EnemySurfaceCrawler.cs");
        File.Copy(file, backup, false);
        File.WriteAllText(file, updated, new UTF8Encoding(false));
        File.WriteAllText(System.IO.Path.Combine(backupDir, "report.txt"), report + "\nBackup: " + backup);
        AssetDatabase.ImportAsset(PathInProject, ImportAssetOptions.ForceUpdate);
        report += "\nAPPLIED. Original backed up to:\n" + backup +
            "\nCheck the Unity Console and test Play Mode.";
    }

    private static void Require(string source, string text)
    {
        if (!source.Contains(text)) throw new InvalidOperationException("Missing expected code: " + text);
    }

    private static string ReplaceOnce(string source, string before, string after)
    {
        int i = source.IndexOf(before, StringComparison.Ordinal);
        if (i < 0 || source.IndexOf(before, i + before.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Expected exactly one anchor: " + before);
        return source.Substring(0, i) + after + source.Substring(i + before.Length);
    }

    private static string ReplaceMethod(string source, string start, string next, string replacement)
    {
        int a = source.IndexOf(start, StringComparison.Ordinal);
        int b = a < 0 ? -1 : source.IndexOf(next, a + start.Length, StringComparison.Ordinal);
        if (a < 0 || b < 0 || source.IndexOf(start, a + start.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Unknown method layout: " + start);
        return source.Substring(0, a) + replacement + "\n\n" + source.Substring(b);
    }
    #endregion

    #region Replacement_Source
    private const string SurfaceMethod = @"    private bool TryFindConnectedSurface(Vector3 position, Vector3 travel,
        out RaycastHit hit)
    {
        hit = default;
        if (climbableLayers.value == 0) return false;

        // PRIORITY 1: Keep a valid attachment to the current surface.
        // A speculative forward wall must not override real floor support.
        Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f
            ? surfaceNormal.normalized : Vector3.up;
        Vector3 origin = position + normal * surfaceProbeDistance;
        if (TrySurfaceRay(origin, -normal,
            surfaceProbeDistance + surfaceOffset + maximumSurfaceGap,
            out RaycastHit support))
        {
            float clearance = Vector3.Distance(position,
                support.point + support.normal * surfaceOffset);
            if (clearance <= Mathf.Min(maximumSurfaceGap + 0.15f, surfaceOffset + 0.5f))
            {
                hit = support;
                return true;
            }
        }

        // PRIORITY 2: A wall is a transition only when it is very close
        // to the body and the candidate attachment point is reachable.
        if (travel.sqrMagnitude > 0.001f &&
            TrySurfaceRay(position + normal * 0.1f, travel.normalized,
                Mathf.Min(wallDetectionDistance, surfaceOffset + 0.35f),
                out RaycastHit wall) &&
            Vector3.Dot(wall.normal, normal) < 0.65f &&
            Vector3.Distance(position,
                wall.point + wall.normal * surfaceOffset) <= surfaceOffset + 0.35f)
        {
            hit = wall;
            return true;
        }

        // Do not claim an arbitrary distant wall is connected terrain.
        return false;
    }";

    private const string JumpMethod = @"    private void UpdateJump(float dt)
    {
        if (climbableLayers.value == 0)
        {
            jumping = false;
            hasSurface = false;
            Fall(dt);
            return;
        }

        jumpElapsed += dt;
        float t = Mathf.Min(jumpElapsed, jumpDuration);
        float gravity = Mathf.Max(1f, lostSurfaceGravity);
        Vector3 position = jumpStart + jumpVelocity * t
            + 0.5f * Vector3.down * gravity * t * t;
        rb.MovePosition(position);
        if (jumpElapsed < jumpDuration) return;

        jumping = false;
        // Only attach if the original landing location still has a
        // valid collider AND the contact is close to the intended point.
        if (TrySurfaceRay(jumpTarget + jumpLandingNormal * surfaceProbeDistance,
            -jumpLandingNormal,
            surfaceProbeDistance + surfaceOffset + maximumSurfaceGap,
            out RaycastHit landing) &&
            Vector3.Distance(landing.point + landing.normal * surfaceOffset,
                jumpTarget) <= 0.5f)
        {
            AttachToSurface(landing);
            return;
        }

        // Failed jump: gravity-driven fall, never continued air crawling.
        hasSurface = false;
        fallSpeed = Mathf.Max(0f, gravity * jumpDuration - jumpVelocity.y);
    }";
    #endregion
}
#endif
