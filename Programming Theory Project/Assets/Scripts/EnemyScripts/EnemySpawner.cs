using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] GameObject spiderPrefab;
    [SerializeField] float spawnRange = 10f;

    void Start()
    {
        float randomX = Random.Range(-spawnRange, spawnRange);
        float randomZ = Random.Range(-spawnRange, spawnRange);

        Vector3 spawnPosition = transform.position + new Vector3(randomX, 0, randomZ);
        Instantiate(spiderPrefab, spawnPosition, Quaternion.identity);
    }
}