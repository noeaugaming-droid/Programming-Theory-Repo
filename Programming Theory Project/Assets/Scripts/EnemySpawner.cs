using UnityEngine;
using TMPro;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private GameObject spiderPrefab;
    [SerializeField] private GameObject beetlePrefab;
    [SerializeField] private float spawnRange = 127;
    [SerializeField] private int enemyCount = 15;

    private int waveNumber = 1;
    private PlayerHealth playerHealth;

    void Start()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        SpawnWave();
    }

    void Update()
    {
        int enemyAmount = enemyCount / 2;
        if (playerHealth == null || playerHealth.isDead) return;

        // AliveCount avoids searching every GameObject each frame.
        if (Enemy.AliveCount == 0)
        {
            waveNumber++;
            enemyCount += enemyAmount;
            SpawnWave();
        }
    }

    private void SpawnWave()
    {
        waveText.text = "Wave: " + waveNumber;
        for (int i = 0; i < enemyCount; i++)
        {
            float randomX = Random.Range(-spawnRange, spawnRange);
            float randomZ = Random.Range(-spawnRange, spawnRange);
            Vector3 spawnPosition = transform.position + new Vector3(randomX, 0f, randomZ);

            int randomEnemy = Random.Range(0, 2);
            if (randomEnemy == 0)
                Instantiate(spiderPrefab, spawnPosition, spiderPrefab.transform.rotation);
            else
                Instantiate(beetlePrefab, spawnPosition, beetlePrefab.transform.rotation);
        }
    }
}
