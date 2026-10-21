using UnityEngine;
using TMPro;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private GameObject spiderPrefab;
    [SerializeField] private GameObject beetlePrefab;
    [SerializeField] private float spawnRange = 10f;
    [SerializeField] private int enemyCount = 3;

    private int waveNumber = 1;
    private PlayerHealth playerHealth;

    void Start()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        SpawnWave();
    }

    void Update()
    {
        if (playerHealth.isDead) return;

        if (FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length == 0)
        {
            waveNumber++;
            enemyCount++;
            SpawnWave();
        }
    }

    void SpawnWave()
    {
        waveText.text = "Wave: " + waveNumber;

        for (int i = 0; i < enemyCount; i++)
        {
            float randomX = Random.Range(-spawnRange, spawnRange);
            float randomZ = Random.Range(-spawnRange, spawnRange);
            Vector3 spawnPosition = transform.position + new Vector3(randomX, 0f, randomZ);

            int randomEnemy = Random.Range(0, 2);
            if (randomEnemy == 0)
            {
                Instantiate(spiderPrefab, spawnPosition, spiderPrefab.transform.rotation);
            }
            else
            {
                Instantiate(beetlePrefab, spawnPosition, beetlePrefab.transform.rotation);
            }
        }
    }
}
