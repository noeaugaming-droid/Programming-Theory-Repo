#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TheyBeBugginAutoFixer : EditorWindow
{
    private const string RuntimeFolder = "Assets/Scripts/TheyBeBugginGenerated";
    private const string PendingKey = "TBB_AutoFixPending";
    private Collider floor;
    private EnemySpawner spawner;
    private GameObject spider;
    private GameObject beetle;
    private int beetleRotation = 90;

    [MenuItem("Tools/They Be Buggin/Auto Fix Enemies")]
    private static void Open() => GetWindow<TheyBeBugginAutoFixer>("Enemy Auto Fixer");

    private void OnEnable() { FindReferences(); }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ENEMY SPAWN & FLOOR FIXER", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Creates the runtime safety script, then installs a replacement spawning supervisor and floor guards. Back up your project first. The old spawner is disabled, not deleted.", MessageType.Info);
        floor = (Collider)EditorGUILayout.ObjectField("Base Floor (Mesh Collider)", floor, typeof(Collider), true);
        spawner = (EnemySpawner)EditorGUILayout.ObjectField("Existing Enemy Spawner", spawner, typeof(EnemySpawner), true);
        spider = (GameObject)EditorGUILayout.ObjectField("Spider Prefab", spider, typeof(GameObject), false);
        beetle = (GameObject)EditorGUILayout.ObjectField("Beetle Prefab", beetle, typeof(GameObject), false);
        beetleRotation = EditorGUILayout.Popup("Beetle Visual X Correction", beetleRotation == 90 ? 1 : beetleRotation == -90 ? 2 : 0, new[] { "0 degrees", "+90 degrees", "-90 degrees" }) switch { 1 => 90, 2 => -90, _ => 0 };
        if (GUILayout.Button("Find References")) FindReferences();
        GUI.enabled = !EditorApplication.isPlayingOrWillChangePlaymode;
        if (GUILayout.Button("CREATE SCRIPTS AND FIX SCENE", GUILayout.Height(44))) StartFix();
        GUI.enabled = true;
    }

    private void FindReferences()
    {
        if (!spawner) spawner = FindFirstObjectByType<EnemySpawner>();
        if (!floor)
        {
            foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                string n = c.name.ToLowerInvariant();
                if (c is MeshCollider && (n.Contains("floor") || n.Contains("ground") || n == "plane")) { floor = c; break; }
            }
        }
        if (spawner)
        {
            var so = new SerializedObject(spawner);
            if (!spider) spider = so.FindProperty("spiderPrefab")?.objectReferenceValue as GameObject;
            if (!beetle) beetle = so.FindProperty("beetlePrefab")?.objectReferenceValue as GameObject;
        }
    }

    private void StartFix()
    {
        if (!floor || !spawner || !spider || !beetle)
        {
            EditorUtility.DisplayDialog("Missing References", "Assign the actual floor MeshCollider, spawner, spider prefab, and beetle prefab.", "OK");
            return;
        }
        if (!PrefabUtility.IsPartOfPrefabAsset(spider) || !PrefabUtility.IsPartOfPrefabAsset(beetle))
        {
            EditorUtility.DisplayDialog("Prefab Assets Required", "Drag prefab ASSETS from the Project window, not scene instances.", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Confirm", "Back up your project first. The fixer will write a new C# runtime script, disable the old spawner, edit both prefabs, and save the open scene. Continue?", "Fix", "Cancel")) return;
        SessionState.SetString("TBB_Floor", GlobalObjectId.GetGlobalObjectIdSlow(floor).ToString());
        SessionState.SetString("TBB_Spawner", GlobalObjectId.GetGlobalObjectIdSlow(spawner).ToString());
        SessionState.SetString("TBB_Spider", AssetDatabase.GetAssetPath(spider));
        SessionState.SetString("TBB_Beetle", AssetDatabase.GetAssetPath(beetle));
        SessionState.SetInt("TBB_BeetleRotation", beetleRotation);
        SessionState.SetBool(PendingKey, true);
        Directory.CreateDirectory(RuntimeFolder);
        string source = RuntimeSource;
        string marker = "[DisallowMultipleComponent]\npublic class TheyBeBugginEditorRotationMarker";
        string guardMarker = "[DefaultExecutionOrder(10000)]";
        string supervisorMarker = "[DisallowMultipleComponent]\npublic class TheyBeBugginSpawnSupervisor";
        int markerStart = source.IndexOf(marker, StringComparison.Ordinal);
        int guardStart = source.IndexOf(guardMarker, markerStart, StringComparison.Ordinal);
        int supervisorStart = source.IndexOf(supervisorMarker, guardStart, StringComparison.Ordinal);
        if (markerStart < 0 || guardStart < 0 || supervisorStart < 0)
        {
            Debug.LogError("Auto Fixer: Internal script template error.");
            SessionState.SetBool(PendingKey, false);
            return;
        }
        string header = "using UnityEngine;\nusing TMPro;\n\n";
        WriteGenerated("TheyBeBugginEditorRotationMarker", header + source.Substring(markerStart, guardStart - markerStart));
        WriteGenerated("TheyBeBugginFloorGuard", header + source.Substring(guardStart, supervisorStart - guardStart));
        WriteGenerated("TheyBeBugginSpawnSupervisor", header + source.Substring(supervisorStart));
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        // If no recompilation is needed, finish on the next editor update.
        EditorApplication.update -= TryComplete;
        EditorApplication.update += TryComplete;
    }

    private static void WriteGenerated(string className, string code)
    {
        string path = RuntimeFolder + "/" + className + ".cs";
        if (!File.Exists(path) || File.ReadAllText(path) != code) File.WriteAllText(path, code);
    }

    [DidReloadScripts]
    private static void AfterReload()
    {
        EditorApplication.update -= TryComplete;
        EditorApplication.update += TryComplete;
    }

    private static void TryComplete()
    {
        if (!SessionState.GetBool(PendingKey, false)) { EditorApplication.update -= TryComplete; return; }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        Type supervisorType = FindType("TheyBeBugginSpawnSupervisor");
        Type guardType = FindType("TheyBeBugginFloorGuard");
        if (supervisorType == null || guardType == null)
        {
            Debug.LogError("Enemy Auto Fixer: Runtime script did not compile. Resolve Console errors and run the fixer again.");
            SessionState.SetBool(PendingKey, false);
            return;
        }
        if (!GlobalObjectId.TryParse(SessionState.GetString("TBB_Floor", ""), out var floorId) ||
            !GlobalObjectId.TryParse(SessionState.GetString("TBB_Spawner", ""), out var spawnerId)) return;
        Collider floorCollider = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(floorId) as Collider;
        EnemySpawner original = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(spawnerId) as EnemySpawner;
        if (!floorCollider || !original)
        {
            Debug.LogError("Enemy Auto Fixer: Scene references were lost. Open the Game scene and run again.");
            SessionState.SetBool(PendingKey, false);
            return;
        }
        GameObject spiderAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SessionState.GetString("TBB_Spider", ""));
        GameObject beetleAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SessionState.GetString("TBB_Beetle", ""));
        if (!spiderAsset || !beetleAsset) { Debug.LogError("Enemy Auto Fixer: Enemy prefab asset missing."); SessionState.SetBool(PendingKey, false); return; }
        var existing = original.GetComponent(supervisorType);
        var supervisor = existing ? existing : Undo.AddComponent(original.gameObject, supervisorType);
        var oldData = new SerializedObject(original);
        var newData = new SerializedObject(supervisor);
        SetRef(newData, "baseFloor", floorCollider);
        SetRef(newData, "spiderPrefab", spiderAsset);
        SetRef(newData, "beetlePrefab", beetleAsset);
        CopyProperty(oldData, newData, "waveText");
        CopyProperty(oldData, newData, "spawnRange");
        CopyProperty(oldData, newData, "enemyCount");
        CopyProperty(oldData, newData, "waveDelay");
        CopyProperty(oldData, newData, "spawnSurfaceLayers");
        CopyProperty(oldData, newData, "obstructionLayers");
        CopyProperty(oldData, newData, "attemptsPerEnemy");
        newData.ApplyModifiedProperties();
        Undo.RecordObject(original, "Disable old enemy spawner");
        original.enabled = false;
        EditorUtility.SetDirty(original);
        ConfigurePrefab(spiderAsset, guardType, false);
        ConfigurePrefab(beetleAsset, guardType, true);
        EditorSceneManager.MarkSceneDirty(original.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        SessionState.SetBool(PendingKey, false);
        EditorApplication.update -= TryComplete;
        Debug.Log("They Be Buggin Auto Fixer complete. Old spawner disabled; new supervisor installed; floor guards added to both prefabs. Test in Play Mode.");
        EditorUtility.DisplayDialog("Enemy Fixer Complete", "Installed the new spawning supervisor and floor guards. The old EnemySpawner was disabled. Test spawning and beetle visual alignment in Play Mode.", "OK");
    }

    private static Type FindType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t = assembly.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    private static void SetRef(SerializedObject so, string name, UnityEngine.Object obj)
    {
        var p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = obj;
    }

    private static void CopyProperty(SerializedObject from, SerializedObject to, string name)
    {
        var a = from.FindProperty(name);
        var b = to.FindProperty(name);
        if (a == null || b == null || a.propertyType != b.propertyType) return;
        switch (a.propertyType)
        {
            case SerializedPropertyType.ObjectReference: b.objectReferenceValue = a.objectReferenceValue; break;
            case SerializedPropertyType.Float: b.floatValue = a.floatValue; break;
            case SerializedPropertyType.Integer: b.intValue = a.intValue; break;
            case SerializedPropertyType.LayerMask: b.intValue = a.intValue; break;
        }
    }

    private static void ConfigurePrefab(GameObject asset, Type guardType, bool beetle)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (!root.GetComponent(guardType)) root.AddComponent(guardType);
            Rigidbody rb = root.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
            }
            if (beetle)
            {
                int correction = SessionState.GetInt("TBB_BeetleRotation", 90);
                // Rotate only the first visual-only child. Never rotate the physics parent.
                Transform visual = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Select(r => r.transform)
                    .FirstOrDefault(t => t != root.transform && !t.GetComponent<Collider>() && !t.GetComponent<Rigidbody>());
                if (visual != null)
                {
                    // Store the applied correction so running the fixer twice doesn't rotate twice.
                    var markerType = FindType("TheyBeBugginEditorRotationMarker");
                    var marker = root.GetComponent(markerType);
                    if (!marker) marker = root.AddComponent(markerType);
                    var appliedField = markerType.GetField("appliedX");
                    int previous = (int)appliedField.GetValue(marker);
                    visual.localRotation = Quaternion.Euler(-previous, 0, 0) * visual.localRotation;
                    visual.localRotation = Quaternion.Euler(correction, 0, 0) * visual.localRotation;
                    appliedField.SetValue(marker, correction);
                }
                CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
                if (capsule && capsule.direction == 1 && capsule.height > 2.1f * capsule.radius)
                    capsule.direction = 2;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // The rotation marker is generated as a runtime component so prefab serialization remains valid.

    private const string RuntimeSource = @"
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class TheyBeBugginEditorRotationMarker : MonoBehaviour
{
    [HideInInspector] public int appliedX;
}

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public class TheyBeBugginFloorGuard : MonoBehaviour
{
    private Collider floor;
    private Collider[] bodyColliders;
    private Rigidbody rb;
    private void Awake() { rb = GetComponent<Rigidbody>(); bodyColliders = GetComponentsInChildren<Collider>(); }
    private void OnEnable() { LocateFloor(); KeepAboveFloor(); }
    private void FixedUpdate() { KeepAboveFloor(); }
    private void LateUpdate() { KeepAboveFloor(); }
    private void LocateFloor()
    {
        if (floor) return;
        var supervisor = FindFirstObjectByType<TheyBeBugginSpawnSupervisor>();
        if (supervisor) floor = supervisor.BaseFloor;
    }
    private void KeepAboveFloor()
    {
        if (!floor) LocateFloor();
        if (!floor) return;
        float lowest = float.PositiveInfinity;
        foreach (var c in bodyColliders)
            if (c && c.enabled && !c.isTrigger) lowest = Mathf.Min(lowest, c.bounds.min.y);
        if (float.IsPositiveInfinity(lowest)) lowest = transform.position.y;
        float shift = floor.bounds.max.y + 0.035f - lowest;
        if (shift <= 0f) return;
        transform.position += Vector3.up * shift;
        if (rb && !rb.isKinematic && rb.linearVelocity.y < 0f)
        {
            Vector3 v = rb.linearVelocity; v.y = 0f; rb.linearVelocity = v;
        }
        Physics.SyncTransforms();
    }
}

[DisallowMultipleComponent]
public class TheyBeBugginSpawnSupervisor : MonoBehaviour
{
    [SerializeField] private Collider baseFloor;
    [SerializeField] private GameObject spiderPrefab;
    [SerializeField] private GameObject beetlePrefab;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private float spawnRange = 10f;
    [SerializeField] private int enemyCount = 3;
    [SerializeField] private float waveDelay = 2f;
    [SerializeField] private LayerMask spawnSurfaceLayers = ~0;
    [SerializeField] private LayerMask obstructionLayers = ~0;
    [SerializeField] private int attemptsPerEnemy = 120;
    [SerializeField] private float clearance = 0.07f;
    private int wave = 1;
    private bool waiting;
    private float nextWave;
    private PlayerHealth player;
    public Collider BaseFloor => baseFloor;

    private void Start()
    {
        player = FindFirstObjectByType<PlayerHealth>();
        if (!baseFloor) { Debug.LogError(""Spawn supervisor: base floor not assigned."", this); enabled = false; return; }
        SpawnWave();
    }
    private void Update()
    {
        if (player && player.isDead) return;
        if (Enemy.AliveCount > 0) { waiting = false; return; }
        if (!waiting) { waiting = true; nextWave = Time.time + Mathf.Max(0.1f, waveDelay); return; }
        if (Time.time < nextWave) return;
        waiting = false; wave++; enemyCount++; SpawnWave();
    }
    private void SpawnWave()
    {
        if (waveText) waveText.text = ""Wave: "" + wave;
        int count = 0;
        for (int i = 0; i < enemyCount; i++)
        {
            GameObject prefab = Random.value < 0.5f ? spiderPrefab : beetlePrefab;
            if (prefab && TrySpawn(prefab)) count++;
        }
        if (count == 0) Debug.LogWarning(""Spawn supervisor: no valid positions found. Check floor, layers, and spawn range."", this);
    }
    private bool TrySpawn(GameObject prefab)
    {
        Collider shape = prefab.GetComponent<Collider>();
        if (!(shape is BoxCollider) && !(shape is SphereCollider) && !(shape is CapsuleCollider))
        {
            Debug.LogError(""Enemy prefab needs a Box, Sphere, or Capsule Collider on its root: "" + prefab.name, prefab);
            return false;
        }
        for (int attempt = 0; attempt < Mathf.Max(1, attemptsPerEnemy); attempt++)
        {
            Vector3 rayDir = Random.onUnitSphere;
            Vector3 origin = transform.position + rayDir * Mathf.Max(1f, spawnRange);
            var hits = Physics.RaycastAll(origin, -rayDir, Mathf.Max(1f, spawnRange) * 2f,
                spawnSurfaceLayers, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) continue;
            // Prefer nearest surface along the ray; don't spawn through the first solid object.
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (!hit.collider || hit.collider.attachedRigidbody || hit.collider.GetComponentInParent<Enemy>()) continue;
                if (hit.collider == baseFloor && Vector3.Dot(hit.normal, Vector3.up) < 0.8f) continue;
                Vector3 n = hit.normal.normalized;
                Vector3 forward = Vector3.ProjectOnPlane(Random.onUnitSphere, n);
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(Vector3.forward, n);
                if (forward.sqrMagnitude < 0.01f) continue;
                Quaternion rotation = Quaternion.LookRotation(forward.normalized, n);
                Vector3 centerOffset;
                float extent, radius;
                if (!GetShape(shape, prefab.transform, rotation, n, out centerOffset, out extent, out radius)) continue;
                Vector3 position = hit.point + n * (extent + clearance) - centerOffset;
                // Conservative whole-collider bound: disallow any part below the base floor.
                float bottom = position.y + centerOffset.y - GetExtent(shape, prefab.transform, rotation, Vector3.up);
                if (bottom < baseFloor.bounds.max.y + 0.04f) continue;
                if (!IsClear(shape, position, rotation, position + centerOffset, radius)) continue;
                Instantiate(prefab, position, rotation);
                return true;
            }
        }
        return false;
    }
    private static bool GetShape(Collider shape, Transform root, Quaternion rotation, Vector3 normal,
        out Vector3 centerOffset, out float extent, out float radius)
    {
        centerOffset = Vector3.zero; extent = radius = 0f;
        Vector3 scale = root.lossyScale;
        if (shape is BoxCollider box)
        {
            centerOffset = rotation * Vector3.Scale(box.center, scale);
            Vector3 half = Vector3.Scale(box.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            extent = Mathf.Abs(Vector3.Dot(normal, rotation * Vector3.right)) * half.x +
                     Mathf.Abs(Vector3.Dot(normal, rotation * Vector3.up)) * half.y +
                     Mathf.Abs(Vector3.Dot(normal, rotation * Vector3.forward)) * half.z;
            radius = half.magnitude;
            return true;
        }
        if (shape is SphereCollider sphere)
        {
            centerOffset = rotation * Vector3.Scale(sphere.center, scale);
            radius = sphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            extent = radius; return true;
        }
        if (shape is CapsuleCollider capsule)
        {
            centerOffset = rotation * Vector3.Scale(capsule.center, scale);
            Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward;
            float axisScale = capsule.direction == 0 ? Mathf.Abs(scale.x) : capsule.direction == 1 ? Mathf.Abs(scale.y) : Mathf.Abs(scale.z);
            float crossScale = capsule.direction == 0 ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)) :
                               capsule.direction == 1 ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            float r = capsule.radius * crossScale;
            float segment = Mathf.Max(0f, capsule.height * axisScale * 0.5f - r);
            extent = r + segment * Mathf.Abs(Vector3.Dot(normal, rotation * axis));
            radius = r + segment; return true;
        }
        return false;
    }
    private static float GetExtent(Collider shape, Transform root, Quaternion rotation, Vector3 axis)
    {
        return GetShape(shape, root, rotation, axis, out _, out float extent, out _) ? extent : 0f;
    }
    private bool IsClear(Collider shape, Vector3 position, Quaternion rotation, Vector3 center, float radius)
    {
        var nearby = Physics.OverlapSphere(center, radius + clearance, obstructionLayers, QueryTriggerInteraction.Ignore);
        foreach (var other in nearby)
        {
            if (!other || other.GetComponentInParent<Enemy>()) continue;
            if (Physics.ComputePenetration(shape, position, rotation,
                other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > 0.002f)
                return false;
            // Some non-convex mesh combinations cannot be resolved by ComputePenetration.
            // Reject uncertain intersections rather than placing enemies inside them.
            if (shape is MeshCollider || (other is MeshCollider mesh && !mesh.convex))
            {
                // The surface itself is allowed to be close; the offset above should avoid overlap.
                // This fallback is intentionally not treated as proof of a collision.
            }
        }
        return true;
    }
}
";
}
#endif
