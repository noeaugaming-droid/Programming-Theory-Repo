
using UnityEngine;

public class SpawnerSwitch : MonoBehaviour
{
    #region Enemies_And_Spawning_Variables
    // Values and references used by this part of the script.
    [SerializeField] private Transform player;
    [SerializeField] private Transform enemySpawner;

    private float activationRange = 127;

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
    #endregion
}
