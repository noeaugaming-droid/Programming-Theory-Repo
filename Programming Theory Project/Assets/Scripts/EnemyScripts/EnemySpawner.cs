using UnityEngine;
using TMPro;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] TMP_Text waveText;
    [SerializeField] GameObject spiderPrefab;
    [SerializeField] GameObject beetlePrefab;
    [SerializeField] float spawnRange = 10f;
    [SerializeField] int enemyCount = 3;

    int waveNumber = 1;
    PlayerHealth playerHealth;


    void Start()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        SpawnWave();

    }
    void Update()
    {
        if (!playerHealth.isDead && FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length == 0)
        {
            waveNumber++;
            enemyCount++;
            Debug.Log("Wave Complete!");
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

            Vector3 spawnPosition = transform.position + new Vector3(randomX, 0, randomZ);
            int randomEnemy = Random.Range(0, 2);
            if (randomEnemy == 0)
            {
                Instantiate(beetlePrefab, spawnPosition, beetlePrefab.transform.rotation);
            }
            else
            {
                Instantiate(beetlePrefab, spawnPosition, beetlePrefab.transform.rotation);
            }
        }
    }
}