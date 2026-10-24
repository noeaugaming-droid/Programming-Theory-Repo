
#if UNITY_EDITOR

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class TheyBeBugginMasterRebuilder : EditorWindow
{
    private const string Menu =
        "Tools/They Be Buggin/Master Project Rebuilder";

    private GameObject player;
    private GameObject spiderPrefab;
    private GameObject beetlePrefab;
    private GameObject bulletPrefab;

    private bool repairPrefabs = true;
    private bool repairReferences = true;
    private bool repairPhysics = true;
    private bool validateSpawning = true;

    private Vector2 scroll;
    private string lastReport = "";

    private readonly StringBuilder log =
        new StringBuilder();

    private int assigned;
    private int added;
    private int warnings;

    [MenuItem(Menu)]
    public static void Open()
    {
        GetWindow<TheyBeBugginMasterRebuilder>(
            "TBB Master Rebuilder");
    }

    private void OnEnable()
    {
        DetectAssets();
    }

    private void OnGUI()
    {
        GUILayout.Label(
            "THEY BE BUGGIN",
            EditorStyles.boldLabel);

        GUILayout.Label(
            "MASTER PROJECT REBUILDER",
            EditorStyles.largeLabel);

        EditorGUILayout.HelpBox(
            "Creates a separate rebuilt scene, repairs " +
            "identifiable gameplay references, validates " +
            "spawners and physics, and optionally repairs " +
            "enemy prefab components. Unknown references " +
            "are reported rather than guessed.",
            MessageType.Info);

        player = (GameObject)EditorGUILayout.ObjectField(
            "Player", player, typeof(GameObject), true);

        spiderPrefab =
            (GameObject)EditorGUILayout.ObjectField(
                "Spider Prefab",
                spiderPrefab,
                typeof(GameObject),
                false);

        beetlePrefab =
            (GameObject)EditorGUILayout.ObjectField(
                "Beetle Prefab",
                beetlePrefab,
                typeof(GameObject),
                false);

        bulletPrefab =
            (GameObject)EditorGUILayout.ObjectField(
                "Bullet Prefab",
                bulletPrefab,
                typeof(GameObject),
                false);

        EditorGUILayout.Space();

        repairPrefabs = EditorGUILayout.Toggle(
            "Repair Enemy Prefabs", repairPrefabs);

        repairReferences = EditorGUILayout.Toggle(
            "Repair Inspector References",
            repairReferences);

        repairPhysics = EditorGUILayout.Toggle(
            "Validate Enemy Physics",
            repairPhysics);

        validateSpawning = EditorGUILayout.Toggle(
            "Validate Spawning",
            validateSpawning);

        EditorGUILayout.Space();

        if (GUILayout.Button(
            "AUTO-DETECT ASSETS",
            GUILayout.Height(35)))
        {
            DetectAssets();
        }

        if (GUILayout.Button(
            "REBUILD PROJECT SETUP",
            GUILayout.Height(50)))
        {
            RunRebuild();
        }

        if (GUILayout.Button(
            "SAVE REPORT",
            GUILayout.Height(30)))
        {
            SaveReport();
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.TextArea(
            lastReport,
            GUILayout.ExpandHeight(true));

        EditorGUILayout.EndScrollView();
    }

    private void DetectAssets()
    {
        if (spiderPrefab == null)
            spiderPrefab = FindPrefabWith("Spider");

        if (beetlePrefab == null)
            beetlePrefab = FindPrefabWith("Beetle");

        if (bulletPrefab == null)
            bulletPrefab = FindPrefabNamed("bullet");

        if (player == null)
            player = FindSceneComponent("PlayerController");
    }

    private static Type FindScriptType(string name)
    {
        foreach (var assembly in
                 AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type match = assembly.GetTypes()
                    .FirstOrDefault(t =>
                        t.Name == name &&
                        typeof(Component).IsAssignableFrom(t));

                if (match != null)
                    return match;
            }
            catch (System.Reflection.ReflectionTypeLoadException)
            {
                // Skip assemblies that cannot be fully inspected.
            }
        }

        return null;
    }

    private static Component FindComponent(
        GameObject obj,
        string typeName)
    {
        if (obj == null)
            return null;

        Type type = FindScriptType(typeName);

        return type == null
            ? null
            : obj.GetComponent(type);
    }

    private static GameObject FindSceneComponent(
        string typeName)
    {
        Type type = FindScriptType(typeName);

        if (type == null)
            return null;

        Component[] found =
            UnityEngine.Object.FindObjectsByType(
                type,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Cast<Component>()
            .ToArray();

        return found.Length == 1
            ? found[0].gameObject
            : null;
    }

    private static GameObject FindPrefabWith(
        string typeName)
    {
        string[] guids =
            AssetDatabase.FindAssets("t:Prefab");

        Type type = FindScriptType(typeName);

        if (type == null)
            return null;

        var matches = new List<GameObject>();

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);

            if (prefab != null &&
                prefab.GetComponent(type) != null)
            {
                matches.Add(prefab);
            }
        }

        return matches.Count == 1
            ? matches[0]
            : null;
    }

    private static GameObject FindPrefabNamed(
        string fragment)
    {
        string[] guids =
            AssetDatabase.FindAssets("t:Prefab");

        var matches = new List<GameObject>();

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    path);

            if (prefab != null &&
                prefab.name.IndexOf(
                    fragment,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                matches.Add(prefab);
            }
        }

        return matches.Count == 1
            ? matches[0]
            : null;
    }

    private void RunRebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Stop Play Mode",
                "Exit Play Mode before rebuilding.",
                "OK");
            return;
        }

        Scene current = SceneManager.GetActiveScene();

        if (!current.IsValid() ||
            string.IsNullOrEmpty(current.path))
        {
            EditorUtility.DisplayDialog(
                "Save Your Scene",
                "Save the current scene before rebuilding.",
                "OK");
            return;
        }

        if (!EditorSceneManager
            .SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        if (!EditorUtility.DisplayDialog(
            "Rebuild Project Setup",
            "This will create a new scene copy and " +
            "repair identifiable project setup.\n\n" +
            "If prefab repair is enabled, prefab assets " +
            "will be backed up before modification.\n\n" +
            "Back up the project folder first.",
            "Rebuild",
            "Cancel"))
            return;

        log.Clear();
        assigned = 0;
        added = 0;
        warnings = 0;

        Write("THEY BE BUGGIN - MASTER REBUILD");
        Write("Unity: " + Application.unityVersion);
        Write("Time: " + DateTime.Now);

        string sourcePath = current.path;

        string directory =
            Path.GetDirectoryName(sourcePath)
                .Replace('\\', '/');

        string sceneName =
            Path.GetFileNameWithoutExtension(sourcePath);

        string destination =
            AssetDatabase.GenerateUniqueAssetPath(
                directory + "/" +
                sceneName + "_MASTER_REBUILT.unity");

        if (!AssetDatabase.CopyAsset(
            sourcePath, destination))
        {
            Warn("Unable to copy scene.");
            Finish();
            return;
        }

        try
        {
            EditorSceneManager.OpenScene(
                destination,
                OpenSceneMode.Single);

            // Scene object references from the source
            // scene must not be reused in the copy.
            player = null;
            DetectAssets();

            if (player == null)
            {
                Warn(
                    "PlayerController not uniquely found. " +
                    "Scene-specific player repair skipped.");
            }
            else
            {
                RepairPlayer();
            }

            if (repairReferences)
                RepairSceneReferences();

            if (validateSpawning)
                RepairSpawners();

            if (repairPrefabs)
            {
                RepairEnemyPrefab(
                    spiderPrefab,
                    "Spider");

                RepairEnemyPrefab(
                    beetlePrefab,
                    "Beetle");
            }

            EditorSceneManager.MarkSceneDirty(
                SceneManager.GetActiveScene());

            EditorSceneManager.SaveOpenScenes();

            AssetDatabase.SaveAssets();

            Write("Rebuilt scene: " + destination);
        }
        catch (Exception ex)
        {
            Warn("Rebuild failed: " + ex);
            Debug.LogException(ex);
        }

        Finish();
    }

    private void RepairPlayer()
    {
        Write("");
        Write("PLAYER SETUP");

        EnsureComponent(
            player,
            "PlayerHealth");

        Rigidbody rb = player.GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = Undo.AddComponent<Rigidbody>(player);
            added++;
            Write("Added player Rigidbody.");
        }

        if (player.GetComponent<Collider>() == null)
        {
            Undo.AddComponent<CapsuleCollider>(player);
            added++;
            Write("Added player CapsuleCollider.");
        }

        GameObject cameraObject =
            FindSceneComponent("CameraLook");

        Transform cameraPivot =
            cameraObject != null
                ? cameraObject.transform
                : null;

        Component controller =
            FindComponent(player, "PlayerController");

        Assign(
            controller,
            "cameraPivot",
            cameraPivot);

        Transform visual =
            FindChild(player.transform, "visual");

        if (visual == null)
            visual = FindChild(
                player.transform,
                "model");

        Assign(
            controller,
            "visualModel",
            visual);
    }

    private void RepairSceneReferences()
    {
        Write("");
        Write("INSPECTOR RECONNECTION");

        GameObject managerObject =
            FindSceneComponent("GameManager");

        GameObject shooterObject =
            FindSceneComponent("BulletShooter");

        GameObject cameraLookObject =
            FindSceneComponent("CameraLook");

        GameObject cameraFollowObject =
            FindSceneComponent("CameraFollow");

        Component manager =
            FindComponent(managerObject, "GameManager");

        Component shooter =
            FindComponent(shooterObject, "BulletShooter");

        Component cameraLook =
            FindComponent(cameraLookObject, "CameraLook");

        Component cameraFollow =
            FindComponent(cameraFollowObject, "CameraFollow");

        Component controller =
            FindComponent(player, "PlayerController");

        Component health =
            FindComponent(player, "PlayerHealth");

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Camera[] cameras =
                FindObjectsByType<Camera>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            if (cameras.Length == 1)
                mainCamera = cameras[0];
        }

        Transform pivot =
            cameraLookObject != null
                ? cameraLookObject.transform
                : null;

        Assign(
            controller,
            "cameraPivot",
            pivot);

        Assign(
            cameraFollow,
            "cameraPivot",
            pivot);

        Assign(
            shooter,
            "aimCamera",
            mainCamera);

        Assign(
            shooter,
            "bulletPrefab",
            bulletPrefab);

        Transform muzzle = player != null
            ? FindChild(player.transform, "bulletspawn")
            : null;

        if (muzzle == null && player != null)
            muzzle = FindChild(
                player.transform,
                "muzzle");

        if (muzzle == null && player != null)
            muzzle = FindChild(
                player.transform,
                "firepoint");

        Assign(
            shooter,
            "bulletSpawnPoint",
            muzzle);

        Assign(
            manager,
            "playerController",
            controller);

        Assign(
            manager,
            "cameraLook",
            cameraLook);

        Assign(
            manager,
            "bulletShooter",
            shooter);

        Assign(
            manager,
            "titlePanel",
            FindUniqueUI("title"));

        Assign(
            manager,
            "hud",
            FindUniqueUI("hud"));

        Assign(
            health,
            "gameManager",
            manager);

        Assign(
            health,
            "healthText",
            FindUniqueText("health"));

        Assign(
            health,
            "gameOverPanel",
            FindUniqueUI("gameover"));

        Assign(
            health,
            "hud",
            FindUniqueUI("hud"));

        // Only reconnect known fields.
        // Do not guess unknown custom assignments.
    }

    private void RepairSpawners()
    {
        Write("");
        Write("ENEMY SPAWNING");

        GameObject spawnerObject =
            FindSceneComponent("EnemySpawner");

        GameObject supervisorObject =
            FindSceneComponent(
                "TheyBeBugginSpawnSupervisor");

        Component spawner =
            FindComponent(
                spawnerObject,
                "EnemySpawner");

        Component supervisor =
            FindComponent(
                supervisorObject,
                "TheyBeBugginSpawnSupervisor");

        if (spawner == null && supervisor == null)
        {
            Type type =
                FindScriptType("EnemySpawner");

            if (type != null)
            {
                GameObject go =
                    new GameObject("EnemySpawner");

                spawner = go.AddComponent(type);
                added++;

                if (player != null)
                    go.transform.position =
                        player.transform.position;

                Write("Created EnemySpawner.");
            }
            else
            {
                Warn("EnemySpawner script missing.");
            }
        }

        foreach (Component target in
                 new[] { spawner, supervisor })
        {
            Assign(
                target,
                "spiderPrefab",
                spiderPrefab);

            Assign(
                target,
                "beetlePrefab",
                beetlePrefab);

            Assign(
                target,
                "waveText",
                FindUniqueText("wave"));
        }

        if (spawner is Behaviour first &&
            supervisor is Behaviour second &&
            first.isActiveAndEnabled &&
            second.isActiveAndEnabled)
        {
            // Preserve the currently enabled
            // EnemySpawner and disable the competing
            // supervisor in the rebuilt scene.
            Undo.RecordObject(
                second,
                "Disable Duplicate Spawner");

            second.enabled = false;
            EditorUtility.SetDirty(second);

            Write(
                "Disabled duplicate SpawnSupervisor.");
        }

        if (spiderPrefab == null)
            Warn("Spider prefab unresolved.");

        if (beetlePrefab == null)
            Warn("Beetle prefab unresolved.");

        if (spiderPrefab != null)
            ValidateSpawnShape(spiderPrefab);

        if (beetlePrefab != null)
            ValidateSpawnShape(beetlePrefab);
    }

    private void ValidateSpawnShape(
        GameObject prefab)
    {
        Collider collider =
            prefab.GetComponent<Collider>();

        if (collider == null)
        {
            Warn(
                prefab.name +
                " has no root collider. " +
                "Spawn validation may reject it.");
            return;
        }

        if (collider.isTrigger)
        {
            Warn(
                prefab.name +
                " root collider is a trigger.");
        }

        if (!(collider is CapsuleCollider) &&
            !(collider is SphereCollider) &&
            !(collider is BoxCollider))
        {
            Warn(
                prefab.name +
                " uses an unsupported root collider " +
                "for the current safe-spawn algorithm.");
        }
    }

    private void RepairEnemyPrefab(
        GameObject prefab,
        string enemyType)
    {
        if (prefab == null)
        {
            Warn(enemyType + " prefab not found.");
            return;
        }

        string path =
            AssetDatabase.GetAssetPath(prefab);

        if (string.IsNullOrEmpty(path))
        {
            Warn(enemyType + " prefab has no asset path.");
            return;
        }

        Write("");
        Write("PREFAB: " + path);

        string withoutExtension =
            path.Substring(
                0,
                path.Length -
                Path.GetExtension(path).Length);

        string backup =
            AssetDatabase.GenerateUniqueAssetPath(
                withoutExtension +
                "_PRE_REBUILD_BACKUP.prefab");

        if (!AssetDatabase.CopyAsset(
            path,
            backup))
        {
            Warn(
                "Could not back up prefab: " +
                path);

            return;
        }

        GameObject root = null;

        try
        {
            root =
                PrefabUtility.LoadPrefabContents(
                    path);

            if (FindComponent(
                root,
                enemyType) == null)
            {
                Warn(
                    enemyType +
                    " component not on prefab root.");
                return;
            }

            EnsureComponent(
                root,
                "EnemySurfaceCrawler");

            Rigidbody rb =
                root.GetComponent<Rigidbody>();

            if (rb == null)
            {
                rb = root.AddComponent<Rigidbody>();
                added++;
            }

            Collider collider =
                root.GetComponent<Collider>();

            if (collider == null)
            {
                Warn(
                    "Root collider missing on " +
                    enemyType +
                    ". No collider was guessed.");
                return;
            }

            if (repairPhysics)
            {
                rb.isKinematic = true;
                rb.useGravity = false;

                rb.interpolation =
                    RigidbodyInterpolation.Interpolate;

                rb.collisionDetectionMode =
                    CollisionDetectionMode
                        .ContinuousSpeculative;

                collider.isTrigger = false;

                if (collider is CapsuleCollider capsule)
                {
                    Write(
                        enemyType +
                        " capsule axis preserved: " +
                        capsule.direction);
                }
            }

            ValidateChildMeshColliders(root);

            PrefabUtility.SaveAsPrefabAsset(
                root,
                path);

            Write(
                "Saved repaired " +
                enemyType + " prefab.");
        }
        catch (Exception ex)
        {
            Warn(
                enemyType +
                " prefab repair failed: " +
                ex.Message);
        }
        finally
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private void ValidateChildMeshColliders(
        GameObject root)
    {
        MeshCollider[] meshes =
            root.GetComponentsInChildren<MeshCollider>(
                true);

        foreach (MeshCollider mesh in meshes)
        {
            if (mesh.sharedMesh == null)
            {
                Warn(
                    "MeshCollider missing mesh: " +
                    mesh.name);
            }

            if (!mesh.enabled)
            {
                Warn(
                    "MeshCollider disabled: " +
                    mesh.name);
            }

            if (!mesh.convex &&
                mesh.attachedRigidbody != null)
            {
                Warn(
                    "Non-convex MeshCollider attached " +
                    "to Rigidbody: " + mesh.name +
                    ". Review collision setup.");
            }

            // Do not force Convex here:
            // that can alter hitboxes and physics.
        }
    }

    private Component EnsureComponent(
        GameObject obj,
        string typeName)
    {
        if (obj == null)
            return null;

        Type type =
            FindScriptType(typeName);

        if (type == null)
        {
            Warn(
                "Script type not found: " +
                typeName);
            return null;
        }

        Component existing =
            obj.GetComponent(type);

        if (existing != null)
            return existing;

        Component result =
            obj.AddComponent(type);

        added++;

        Write(
            "Attached " + typeName +
            " to " + obj.name);

        return result;
    }

    private void Assign(
        Component target,
        string fieldName,
        UnityEngine.Object value)
    {
        if (target == null)
            return;

        SerializedObject so =
            new SerializedObject(target);

        SerializedProperty property =
            so.FindProperty(fieldName);

        if (property == null ||
            property.propertyType !=
                SerializedPropertyType.ObjectReference)
        {
            // Not every version of a script has
            // the same serialized fields.
            return;
        }

        if (property.objectReferenceValue != null)
            return;

        if (value == null)
        {
            Warn(
                target.GetType().Name +
                "." + fieldName +
                " is unassigned.");
            return;
        }

        property.objectReferenceValue = value;

        if (so.ApplyModifiedProperties())
        {
            assigned++;

            Write(
                "Connected " +
                target.GetType().Name +
                "." + fieldName +
                " -> " + value.name);
        }
    }

    private static Transform FindChild(
        Transform root,
        string name)
    {
        if (root == null)
            return null;

        var matches =
            root.GetComponentsInChildren<Transform>(
                true)
            .Where(t =>
                t != root &&
                t.name.IndexOf(
                    name,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();

        return matches.Length == 1
            ? matches[0]
            : null;
    }

    private static GameObject FindUniqueUI(
        string name)
    {
        GameObject[] matches =
            FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(t =>
                t.name.IndexOf(
                    name,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            .Select(t => t.gameObject)
            .Distinct()
            .ToArray();

        return matches.Length == 1
            ? matches[0]
            : null;
    }

    private static TMP_Text FindUniqueText(
        string name)
    {
        TMP_Text[] matches =
            FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(t =>
                t.name.IndexOf(
                    name,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();

        return matches.Length == 1
            ? matches[0]
            : null;
    }

    private void Warn(string message)
    {
        warnings++;
        Write("WARNING: " + message);
    }

    private void Write(string message)
    {
        log.AppendLine(message);
    }

    private void Finish()
    {
        Write("");
        Write("REBUILD SUMMARY");
        Write("Inspector assignments: " + assigned);
        Write("Components added: " + added);
        Write("Warnings: " + warnings);

        lastReport = log.ToString();

        Debug.Log(lastReport);

        Repaint();

        EditorUtility.DisplayDialog(
            "Rebuild Finished",
            "Assignments: " + assigned +
            "\nComponents added: " + added +
            "\nWarnings: " + warnings +
            "\n\nReview the report and test the " +
            "rebuilt scene before replacing your " +
            "original.",
            "OK");
    }

    private void SaveReport()
    {
        string path =
            EditorUtility.SaveFilePanel(
                "Save Rebuild Report",
                Application.dataPath,
                "TheyBeBuggin_MasterRebuildReport",
                "txt");

        if (string.IsNullOrEmpty(path))
            return;

        File.WriteAllText(path, lastReport);

        EditorUtility.RevealInFinder(path);
    }
}

#endif
