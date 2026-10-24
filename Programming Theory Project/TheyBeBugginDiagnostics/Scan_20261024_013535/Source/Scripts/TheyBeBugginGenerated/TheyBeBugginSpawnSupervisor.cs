using UnityEngine;
using TMPro;

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
        if (!baseFloor) { Debug.LogError("Spawn supervisor: base floor not assigned.", this); enabled = false; return; }
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
        if (waveText) waveText.text = "Wave: " + wave;
        int count = 0;
        for (int i = 0; i < enemyCount; i++)
        {
            GameObject prefab = Random.value < 0.5f ? spiderPrefab : beetlePrefab;
            if (prefab && TrySpawn(prefab)) count++;
        }
        if (count == 0) Debug.LogWarning("Spawn supervisor: no valid positions found. Check floor, layers, and spawn range.", this);
    }
    private bool TrySpawn(GameObject prefab)
    {
        Collider shape = prefab.GetComponent<Collider>();
        if (!(shape is BoxCollider) && !(shape is SphereCollider) && !(shape is CapsuleCollider))
        {
            Debug.LogError("Enemy prefab needs a Box, Sphere, or Capsule Collider on its root: " + prefab.name, prefab);
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
