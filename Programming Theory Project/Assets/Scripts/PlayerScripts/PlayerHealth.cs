using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float health = 100f;

    public bool isDead = false;

    public void TakeDamage(float damageAmount)
    {
        if (!isDead)
        {
            health -= damageAmount;

            Debug.Log("Player Health: " + health);

            if (health <= 0)
            {
                isDead = true;

                Debug.Log("Player Died!");

                GetComponent<PlayerController>().enabled = false;
                GetComponentInChildren<BulletShooter>().enabled = false;
            }
        }
    }
}