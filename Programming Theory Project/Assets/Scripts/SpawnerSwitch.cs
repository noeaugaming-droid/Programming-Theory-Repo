
using UnityEngine;

public class SpawnerSwitch : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform enemySpawner;

    private float activationRange = 50f;

    void Update()
    {
        float distance = Vector3.Distance(
            player.position,
            transform.position
        );

        if (distance <= activationRange)
        {
            Debug.Log("Player is within spawner range!");
            Debug.Log(gameObject.name);
            enemySpawner.position = transform.position;
        }
    }
}
