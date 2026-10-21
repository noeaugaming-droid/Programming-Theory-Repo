using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] GameObject spiderPrefab;
    [SerializeField] float spawnRange = 10f;
    [SerializeField] int enemyCount = 3;
    int waveNumber = 1;


    void Start()
    {
        SpawnWave();

    }
    void Update()
    {
        if (FindObjectsByType<Enemy>(FindObjectsSortMode.None).Length == 0)
        {
            waveNumber++;
            enemyCount++;
            Debug.Log("Wave Complete!");
            SpawnWave();
        }
    }

    void SpawnWave()
    {
        for (int i = 0; i < enemyCount; i++)
        {


            float randomX = Random.Range(-spawnRange, spawnRange);
            float randomZ = Random.Range(-spawnRange, spawnRange);

            Vector3 spawnPosition = transform.position + new Vector3(randomX, 0, randomZ);
            Instantiate(spiderPrefab, spawnPosition, Quaternion.identity);
        }
    }
}