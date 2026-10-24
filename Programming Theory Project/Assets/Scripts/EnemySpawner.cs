
using System;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class EnemySpawner : MonoBehaviour
{
    [Header("Wave UI and Prefabs")]
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private GameObject spiderPrefab;
    [SerializeField] private GameObject beetlePrefab;

    [Header("Spawn Area")]
    [SerializeField, Min(1f)] private float spawnRange = 100f;
    [SerializeField, Min(0f)] private float minimumSpawnRange = 25f;
    [SerializeField, Min(1)] private int enemyCount = 15;
    [SerializeField, Min(0.5f)] private float minimumEnemySpacing = 7f;
    [SerializeField, Min(1)] private int attemptsPerEnemy = 60;

    [Header("Surface Detection")]
    [SerializeField] private LayerMask spawnSurfaceLayers = ~0;
    [SerializeField, Min(1f)] private float rayStartHeight = 100f;
    [SerializeField, Min(1f)] private float rayLength = 250f;
    [SerializeField, Range(0f, 1f)] private float minimumUpNormal = 0.7f;

    [Header("Collision Safety")]
    [SerializeField] private LayerMask obstructionLayers = ~0;
    [SerializeField, Min(0.01f)] private float spawnClearance = 0.12f;
    [SerializeField, Min(0f)] private float minimumPlayerDistance = 12f;
    [SerializeField] private bool logRejectedSpawns = false;

    private int waveNumber = 1;
    private PlayerHealth playerHealth;

    private void Start()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();

        // Prevent two different wave spawners from
        // independently creating enemies.
        TheyBeBugginSpawnSupervisor supervisor =
            FindAnyObjectByType<TheyBeBugginSpawnSupervisor>();

        if (supervisor != null &&
            supervisor.isActiveAndEnabled)
        {
            Debug.LogWarning(
                "EnemySpawner disabled because " +
                "TheyBeBugginSpawnSupervisor is active. " +
                "Use only one wave spawner.",
                this);

            enabled = false;
            return;
        }

        SpawnWave();
    }

    private void Update()
    {
        if (playerHealth == null)
        {
            playerHealth =
                FindAnyObjectByType<PlayerHealth>();

            if (playerHealth == null)
                return;
        }

        if (playerHealth.isDead)
            return;

        if (Enemy.AliveCount > 0)
            return;

        int increase = Mathf.Max(1, enemyCount / 2);

        waveNumber++;
        enemyCount += increase;

        SpawnWave();
    }

    private void SpawnWave()
    {
        if (waveText != null)
            waveText.text = "Wave: " + waveNumber;

        if (spiderPrefab == null &&
            beetlePrefab == null)
        {
            Debug.LogError(
                "EnemySpawner has no enemy prefabs.",
                this);
            return;
        }

        int spawned = 0;

        Vector3[] usedPositions =
            new Vector3[enemyCount];

        float startingAngle =
            UnityEngine.Random.Range(0f, 360f);

        for (int i = 0; i < enemyCount; i++)
        {
            GameObject prefab = ChoosePrefab();

            if (prefab == null)
                continue;

            if (!TryFindSafeSpawn(
                prefab,
                i,
                enemyCount,
                startingAngle,
                usedPositions,
                spawned,
                out Vector3 position,
                out Quaternion rotation))
            {
                Debug.LogWarning(
                    "Skipped " + prefab.name +
                    ": no safe spawn location found.",
                    this);

                continue;
            }

            GameObject enemy =
                Instantiate(
                    prefab,
                    position,
                    rotation);

            EnemyApproachVariation variation =
                enemy.GetComponent<EnemyApproachVariation>();

            if (variation == null)
            {
                variation =
                    enemy.AddComponent<EnemyApproachVariation>();
            }

            variation.Initialize(
                UnityEngine.Random.value < 0.5f
                    ? -1
                    : 1,
                UnityEngine.Random.Range(0f, 1000f));

            usedPositions[spawned] = position;
            spawned++;
        }

        Debug.Log(
            "Wave " + waveNumber +
            ": spawned " + spawned +
            " of " + enemyCount + " enemies.",
            this);
    }

    private GameObject ChoosePrefab()
    {
        if (spiderPrefab == null)
            return beetlePrefab;

        if (beetlePrefab == null)
            return spiderPrefab;

        return UnityEngine.Random.value < 0.5f
            ? spiderPrefab
            : beetlePrefab;
    }

    private bool TryFindSafeSpawn(
        GameObject prefab,
        int index,
        int total,
        float startingAngle,
        Vector3[] usedPositions,
        int usedCount,
        out Vector3 position,
        out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        Collider prefabCollider =
            prefab.GetComponent<Collider>();

        if (prefabCollider == null)
        {
            Debug.LogError(
                prefab.name +
                " needs a collider on its root.",
                prefab);
            return false;
        }

        if (!(prefabCollider is CapsuleCollider) &&
            !(prefabCollider is BoxCollider) &&
            !(prefabCollider is SphereCollider))
        {
            Debug.LogError(
                prefab.name +
                " requires a Capsule, Box, " +
                "or Sphere Collider for safe spawning.",
                prefab);
            return false;
        }

        float outer = Mathf.Max(1f, spawnRange);
        float inner = Mathf.Clamp(
            minimumSpawnRange,
            0f,
            outer);

        float sectorWidth =
            360f / Mathf.Max(1, total);

        for (int attempt = 0;
             attempt < attemptsPerEnemy;
             attempt++)
        {
            float angle =
                (startingAngle +
                 index * sectorWidth +
                 UnityEngine.Random.Range(
                     -sectorWidth * 0.45f,
                     sectorWidth * 0.45f))
                * Mathf.Deg2Rad;

            float radius = Mathf.Sqrt(
                UnityEngine.Random.Range(
                    inner * inner,
                    outer * outer));

            Vector3 horizontalOffset =
                new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle)) * radius;

            Vector3 origin =
                transform.position +
                horizontalOffset +
                Vector3.up * rayStartHeight;

            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    Vector3.down,
                    rayLength,
                    spawnSurfaceLayers,
                    QueryTriggerInteraction.Ignore);

            if (hits.Length == 0)
                continue;

            Array.Sort(
                hits,
                (a, b) =>
                    a.distance.CompareTo(b.distance));

            // Only consider the first physical surface.
            // Don't spawn through a roof or other object.
            RaycastHit hit = hits[0];

            if (hit.collider == null)
                continue;

            if (hit.collider.GetComponentInParent<Enemy>()
                != null)
                continue;

            if (hit.normal.y < minimumUpNormal)
                continue;

            Vector3 forward =
                Vector3.ProjectOnPlane(
                    prefab.transform.forward,
                    hit.normal);

            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;

            Quaternion candidateRotation =
                Quaternion.LookRotation(
                    forward.normalized,
                    hit.normal);

            // Beetle's floor motor expects an upright root.
            if (prefab.GetComponent<Beetle>() != null)
            {
                candidateRotation =
                    Quaternion.LookRotation(
                        Vector3.ProjectOnPlane(
                            forward,
                            Vector3.up).normalized,
                        Vector3.up);
            }

            if (!GetColliderGeometry(
                prefabCollider,
                prefab.transform,
                candidateRotation,
                out Vector3 centerOffset,
                out Vector3 axis,
                out float halfSegment,
                out float shapeRadius,
                out Vector3 boxHalfSize))
            {
                continue;
            }

            float supportExtent =
                GetExtent(
                    prefabCollider,
                    candidateRotation,
                    hit.normal,
                    axis,
                    halfSegment,
                    shapeRadius,
                    boxHalfSize);

            Vector3 candidatePosition =
                hit.point +
                hit.normal *
                (supportExtent + spawnClearance) -
                centerOffset;

            if (playerHealth != null &&
                Vector3.Distance(
                    candidatePosition,
                    playerHealth.transform.position)
                < minimumPlayerDistance)
            {
                continue;
            }

            bool tooClose = false;

            for (int j = 0; j < usedCount; j++)
            {
                if (Vector3.Distance(
                    candidatePosition,
                    usedPositions[j])
                    < minimumEnemySpacing)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            if (!IsSpawnClear(
                prefabCollider,
                candidatePosition,
                candidateRotation,
                centerOffset,
                axis,
                halfSegment,
                shapeRadius,
                boxHalfSize))
            {
                if (logRejectedSpawns)
                {
                    Debug.Log(
                        "Rejected blocked spawn for " +
                        prefab.name,
                        this);
                }

                continue;
            }

            position = candidatePosition;
            rotation = candidateRotation;
            return true;
        }

        return false;
    }

    private bool GetColliderGeometry(
        Collider shape,
        Transform prefabTransform,
        Quaternion rotation,
        out Vector3 centerOffset,
        out Vector3 axis,
        out float halfSegment,
        out float radius,
        out Vector3 boxHalfSize)
    {
        centerOffset = Vector3.zero;
        axis = Vector3.up;
        halfSegment = 0f;
        radius = 0f;
        boxHalfSize = Vector3.zero;

        Vector3 scale = prefabTransform.lossyScale;

        Vector3 absScale = new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z));

        if (shape is CapsuleCollider capsule)
        {
            Vector3 localAxis =
                capsule.direction == 0
                    ? Vector3.right
                    : capsule.direction == 2
                        ? Vector3.forward
                        : Vector3.up;

            float axisScale =
                capsule.direction == 0
                    ? absScale.x
                    : capsule.direction == 2
                        ? absScale.z
                        : absScale.y;

            float radialScale =
                capsule.direction == 0
                    ? Mathf.Max(absScale.y, absScale.z)
                    : capsule.direction == 2
                        ? Mathf.Max(absScale.x, absScale.y)
                        : Mathf.Max(absScale.x, absScale.z);

            radius = capsule.radius * radialScale;

            halfSegment = Mathf.Max(
                0f,
                capsule.height * axisScale * 0.5f -
                radius);

            axis = rotation * localAxis;

            centerOffset =
                rotation *
                Vector3.Scale(capsule.center, scale);

            return true;
        }

        if (shape is SphereCollider sphere)
        {
            radius =
                sphere.radius *
                Mathf.Max(
                    absScale.x,
                    Mathf.Max(absScale.y, absScale.z));

            centerOffset =
                rotation *
                Vector3.Scale(sphere.center, scale);

            return true;
        }

        if (shape is BoxCollider box)
        {
            boxHalfSize =
                Vector3.Scale(
                    box.size * 0.5f,
                    absScale);

            centerOffset =
                rotation *
                Vector3.Scale(box.center, scale);

            return true;
        }

        return false;
    }

    private float GetExtent(
        Collider shape,
        Quaternion rotation,
        Vector3 direction,
        Vector3 capsuleAxis,
        float halfSegment,
        float radius,
        Vector3 boxHalfSize)
    {
        direction.Normalize();

        if (shape is CapsuleCollider)
        {
            return radius +
                halfSegment *
                Mathf.Abs(
                    Vector3.Dot(
                        direction,
                        capsuleAxis.normalized));
        }

        if (shape is SphereCollider)
            return radius;

        if (shape is BoxCollider)
        {
            return
                Mathf.Abs(Vector3.Dot(
                    direction,
                    rotation * Vector3.right))
                    * boxHalfSize.x +
                Mathf.Abs(Vector3.Dot(
                    direction,
                    rotation * Vector3.up))
                    * boxHalfSize.y +
                Mathf.Abs(Vector3.Dot(
                    direction,
                    rotation * Vector3.forward))
                    * boxHalfSize.z;
        }

        return 0f;
    }

    private bool IsSpawnClear(
        Collider prefabCollider,
        Vector3 position,
        Quaternion rotation,
        Vector3 centerOffset,
        Vector3 axis,
        float halfSegment,
        float radius,
        Vector3 boxHalfSize)
    {
        Vector3 center = position + centerOffset;

        Collider[] nearby;

        if (prefabCollider is CapsuleCollider)
        {
            Vector3 a = center + axis * halfSegment;
            Vector3 b = center - axis * halfSegment;

            nearby = Physics.OverlapCapsule(
                a,
                b,
                radius,
                obstructionLayers,
                QueryTriggerInteraction.Ignore);
        }
        else if (prefabCollider is SphereCollider)
        {
            nearby = Physics.OverlapSphere(
                center,
                radius,
                obstructionLayers,
                QueryTriggerInteraction.Ignore);
        }
        else
        {
            nearby = Physics.OverlapBox(
                center,
                boxHalfSize,
                rotation,
                obstructionLayers,
                QueryTriggerInteraction.Ignore);
        }

        foreach (Collider other in nearby)
        {
            if (other == null ||
                other.isTrigger)
                continue;

            // Reject other enemies too.
            // No skipping their colliders.
            if (Physics.ComputePenetration(
                prefabCollider,
                position,
                rotation,
                other,
                other.transform.position,
                other.transform.rotation,
                out _,
                out float depth))
            {
                if (depth > 0.005f)
                    return false;
            }
            else if (other is MeshCollider mesh &&
                     !mesh.convex)
            {
                // Non-convex meshes can be problematic
                // for penetration queries. Reject uncertain
                // candidates rather than force a spawn.
                return false;
            }
        }

        return true;
    }
}
