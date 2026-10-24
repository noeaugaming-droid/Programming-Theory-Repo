using UnityEngine;
using TMPro;

// Spawns a wave in distributed directions around this spawner.
public class EnemySpawner : MonoBehaviour
{
    #region Inspector_Variables
    [Header("Wave UI and Prefabs")]
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private GameObject spiderPrefab;
    [SerializeField] private GameObject beetlePrefab;

    [Header("Spawn Radius (Unity Units)")]
    [SerializeField, Min(1f)] private float spawnRange = 100f;
    [SerializeField, Min(0f)] private float minimumSpawnRange = 25f;
    [SerializeField, Min(1)] private int enemyCount = 15;
    [SerializeField, Min(0.5f)] private float minimumEnemySpacing = 7f;
    [SerializeField, Min(1)] private int attemptsPerEnemy = 24;

    [Header("Terrain Detection")]
    [SerializeField] private LayerMask spawnSurfaceLayers = ~0;
    [SerializeField, Min(1f)] private float rayStartHeight = 100f;
    [SerializeField, Min(1f)] private float rayLength = 250f;
    [SerializeField, Min(0f)] private float spawnSurfaceOffset = 0.7f;
    [SerializeField, Range(0f, 1f)] private float minimumUpNormal = 0.45f;
    #endregion

    #region Private_Variables
    private int waveNumber = 1;
    private PlayerHealth playerHealth;
    #endregion

    #region Unity_Lifecycle_Methods
    private void Start()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        SpawnWave();
    }

    private void Update()
    {
        if (playerHealth == null || playerHealth.isDead) return;
        if (Enemy.AliveCount != 0) return;

        int additionalEnemies = Mathf.Max(1, enemyCount / 2);
        waveNumber++;
        enemyCount += additionalEnemies;
        SpawnWave();
    }
    #endregion

    #region Spawning_Methods
    private void SpawnWave()
    {
        if (waveText != null) waveText.text = "Wave: " + waveNumber;
        if (spiderPrefab == null && beetlePrefab == null)
        {
            Debug.LogError("EnemySpawner has no enemy prefabs assigned.", this);
            return;
        }

        // Use sectors so the wave is spread around the spawner rather than clustered.
        float startingAngle = Random.Range(0f, 360f);
        Vector3[] selectedPositions = new Vector3[enemyCount];
        int spawned = 0;
        for (int i = 0; i < enemyCount; i++)
        {
            if (!TryFindSpawnPoint(i, enemyCount, startingAngle, selectedPositions, spawned,
                out Vector3 spawnPosition, out Vector3 surfaceNormal))
            {
                Debug.LogWarning("No safe spawn point found for enemy " + i, this);
                continue;
            }

            GameObject prefab = ChoosePrefab();
            if (prefab == null) continue;

            // Preserve prefab orientation; crawler aligns to the surface after spawning.
            GameObject enemy = Instantiate(prefab, spawnPosition, prefab.transform.rotation);
            EnemyApproachVariation variation = enemy.GetComponent<EnemyApproachVariation>();
            if (variation == null) variation = enemy.AddComponent<EnemyApproachVariation>();
            variation.Initialize(Random.Range(0, 2) == 0 ? -1 : 1,
                Random.Range(0f, 1000f));

            selectedPositions[spawned++] = spawnPosition;
        }
    }

    private GameObject ChoosePrefab()
    {
        if (spiderPrefab == null) return beetlePrefab;
        if (beetlePrefab == null) return spiderPrefab;
        return Random.value < 0.5f ? spiderPrefab : beetlePrefab;
    }

    private bool TryFindSpawnPoint(int index, int total, float startAngle,
        Vector3[] selected, int selectedCount, out Vector3 point, out Vector3 normal)
    {
        point = default;
        normal = Vector3.up;
        float outer = Mathf.Max(1f, spawnRange);
        float inner = Mathf.Clamp(minimumSpawnRange, 0f, outer);
        float sectorWidth = 360f / Mathf.Max(1, total);

        for (int attempt = 0; attempt < attemptsPerEnemy; attempt++)
        {
            float angle = (startAngle + index * sectorWidth +
                Random.Range(-sectorWidth * 0.4f, sectorWidth * 0.4f)) * Mathf.Deg2Rad;
            float radius = Mathf.Sqrt(Random.Range(inner * inner, outer * outer));
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector3 origin = transform.position + offset + Vector3.up * rayStartHeight;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                rayLength, spawnSurfaceLayers, QueryTriggerInteraction.Ignore)) continue;
            if (hit.normal.y < minimumUpNormal) continue;
            if (hit.collider.GetComponentInParent<Enemy>() != null) continue;

            Vector3 candidate = hit.point + hit.normal * spawnSurfaceOffset;
            bool tooClose = false;
            for (int j = 0; j < selectedCount; j++)
            {
                if (Vector3.Distance(candidate, selected[j]) < minimumEnemySpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;
            point = candidate;
            normal = hit.normal;
            return true;
        }
        return false;
    }
    #endregion
}
