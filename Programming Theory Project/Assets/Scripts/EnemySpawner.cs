
using UnityEngine;
using TMPro;

public class EnemySpawner : MonoBehaviour
{
    [Header("Wave UI")]
    [SerializeField] private TMP_Text waveText;

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject spiderPrefab;
    [SerializeField] private GameObject beetlePrefab;

    [Header("Wave Settings")]
    [SerializeField] private float spawnRange = 10f;
    [SerializeField] private int enemyCount = 3;
    [SerializeField] private float waveDelay = 2f;

    [Header("Safe Surface Spawning")]
    [SerializeField] private LayerMask spawnSurfaceLayers = ~0;
    [SerializeField] private LayerMask obstructionLayers = ~0;
    [SerializeField] private int attemptsPerEnemy = 40;
    [SerializeField] private float surfaceClearance = 0.08f;
    [SerializeField] private float overlapTolerance = 0.001f;

    [Header("Diagnostics")]
    [SerializeField] private bool logFailedSpawns = true;

    private int waveNumber = 1;
    private PlayerHealth playerHealth;
    private float nextWaveTime;
    private bool waitingForNextWave;

    private void Start()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();

        SpawnWave();
    }

    private void Update()
    {
        if (playerHealth != null && playerHealth.isDead)
            return;

        if (Enemy.AliveCount > 0)
        {
            waitingForNextWave = false;
            return;
        }

        if (!waitingForNextWave)
        {
            waitingForNextWave = true;
            nextWaveTime = Time.time + waveDelay;
            return;
        }

        if (Time.time < nextWaveTime)
            return;

        waitingForNextWave = false;

        waveNumber++;
        enemyCount++;

        Debug.Log("Wave Complete!");

        SpawnWave();
    }

    private void SpawnWave()
    {
        if (waveText != null)
            waveText.text = "Wave: " + waveNumber;

        int spawned = 0;

        for (int i = 0; i < enemyCount; i++)
        {
            GameObject prefab = ChooseEnemyPrefab();

            if (prefab == null)
                continue;

            if (TrySpawnEnemy(prefab))
                spawned++;
        }

        Debug.Log(
            "Wave " + waveNumber +
            ": spawned " + spawned +
            " / " + enemyCount + " enemies."
        );
    }

    private GameObject ChooseEnemyPrefab()
    {
        if (spiderPrefab == null)
            return beetlePrefab;

        if (beetlePrefab == null)
            return spiderPrefab;

        return Random.Range(0, 2) == 0
            ? spiderPrefab
            : beetlePrefab;
    }

    private bool TrySpawnEnemy(GameObject prefab)
    {
        int maxAttempts = Mathf.Max(1, attemptsPerEnemy);

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (!TryFindSurface(out RaycastHit surfaceHit))
                continue;

            Vector3 normal = surfaceHit.normal.normalized;

            Quaternion rotation =
                CalculateSurfaceRotation(prefab, normal);

            // Initially place the prefab on the surface.
            // Its collider is then used to calculate
            // the necessary distance away from it.
            GameObject candidate = Instantiate(
                prefab,
                surfaceHit.point,
                rotation
            );

            Collider[] colliders =
                candidate.GetComponentsInChildren<Collider>();

            if (colliders.Length == 0)
            {
                Debug.LogError(
                    prefab.name +
                    " has no enabled child or parent collider."
                );

                Destroy(candidate);
                return false;
            }

            Physics.SyncTransforms();

            float requiredOffset =
                CalculateRequiredOffset(
                    candidate,
                    colliders,
                    surfaceHit.point,
                    normal
                );

            candidate.transform.position =
                surfaceHit.point +
                normal * (requiredOffset + surfaceClearance);

            Physics.SyncTransforms();

            if (IsSpawnPositionClear(candidate, colliders))
            {
                return true;
            }

            Destroy(candidate);
        }

        if (logFailedSpawns)
        {
            Debug.LogWarning(
                "Could not find a safe spawn location for " +
                prefab.name +
                " after " + maxAttempts + " attempts."
            );
        }

        return false;
    }

    private bool TryFindSurface(out RaycastHit result)
    {
        result = default;

        float range = Mathf.Max(1f, spawnRange);

        // Cast through the spawn region from different
        // directions, allowing floors, walls and ceilings.
        Vector3 direction = Random.onUnitSphere;

        Vector3 origin =
            transform.position + direction * range;

        Vector3 castDirection = -direction;

        float castDistance = range * 2f;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            castDirection,
            castDistance,
            spawnSurfaceLayers,
            QueryTriggerInteraction.Ignore
        );

        if (hits.Length == 0)
            return false;

        // Select a random valid surface along the ray.
        int startIndex = Random.Range(0, hits.Length);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit =
                hits[(startIndex + i) % hits.Length];

            if (hit.collider == null)
                continue;

            if (hit.collider.isTrigger)
                continue;

            // Do not spawn directly on an enemy.
            if (hit.collider.GetComponentInParent<Enemy>()
                != null)
                continue;

            result = hit;
            return true;
        }

        return false;
    }

    private Quaternion CalculateSurfaceRotation(
        GameObject prefab,
        Vector3 normal)
    {
        Vector3 forward =
            Vector3.ProjectOnPlane(
                prefab.transform.forward,
                normal
            );

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.ProjectOnPlane(
                Vector3.forward,
                normal
            );
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.ProjectOnPlane(
                Vector3.right,
                normal
            );
        }

        return Quaternion.LookRotation(
            forward.normalized,
            normal
        );
    }

    private float CalculateRequiredOffset(
        GameObject candidate,
        Collider[] colliders,
        Vector3 surfacePoint,
        Vector3 normal)
    {
        float offset = 0f;

        foreach (Collider collider in colliders)
        {
            if (collider == null ||
                !collider.enabled ||
                collider.isTrigger)
                continue;

            Bounds bounds = collider.bounds;

            // Bounds are world-space AABBs.
            // This deliberately uses a conservative
            // clearance for rotated colliders.
            Vector3 extents = bounds.extents;

            float projectedRadius =
                Mathf.Abs(normal.x) * extents.x +
                Mathf.Abs(normal.y) * extents.y +
                Mathf.Abs(normal.z) * extents.z;

            float centerDistance = Vector3.Dot(
                bounds.center - surfacePoint,
                normal
            );

            float needed =
                projectedRadius - centerDistance;

            offset = Mathf.Max(offset, needed);
        }

        return offset;
    }

    private bool IsSpawnPositionClear(
        GameObject candidate,
        Collider[] colliders)
    {
        foreach (Collider enemyCollider in colliders)
        {
            if (enemyCollider == null ||
                !enemyCollider.enabled ||
                enemyCollider.isTrigger)
                continue;

            Bounds bounds = enemyCollider.bounds;

            Collider[] nearby = Physics.OverlapBox(
                bounds.center,
                bounds.extents +
                    Vector3.one * 0.01f,
                Quaternion.identity,
                obstructionLayers,
                QueryTriggerInteraction.Ignore
            );

            foreach (Collider obstacle in nearby)
            {
                if (obstacle == null ||
                    !obstacle.enabled ||
                    obstacle.isTrigger)
                    continue;

                // Ignore colliders belonging to
                // the candidate itself.
                if (obstacle.transform == candidate.transform ||
                    obstacle.transform.IsChildOf(
                        candidate.transform))
                    continue;

                if (Physics.ComputePenetration(
                    enemyCollider,
                    enemyCollider.transform.position,
                    enemyCollider.transform.rotation,
                    obstacle,
                    obstacle.transform.position,
                    obstacle.transform.rotation,
                    out Vector3 direction,
                    out float distance))
                {
                    if (distance > overlapTolerance)
                        return false;
                }
                else
                {
                    // ComputePenetration does not support
                    // every collider combination.
                    // For unsupported types, conservatively
                    // reject intersecting bounding boxes.
                    if (!SupportsPenetrationTest(enemyCollider) ||
                        !SupportsPenetrationTest(obstacle))
                    {
                        Bounds obstacleBounds = obstacle.bounds;

                        if (bounds.Intersects(obstacleBounds))
                            return false;
                    }
                }
            }
        }

        return true;
    }

    private bool SupportsPenetrationTest(Collider collider)
    {
        if (collider is BoxCollider)
            return true;

        if (collider is SphereCollider)
            return true;

        if (collider is CapsuleCollider)
            return true;

        if (collider is MeshCollider mesh)
            return mesh.convex;

        return false;
    }
}
