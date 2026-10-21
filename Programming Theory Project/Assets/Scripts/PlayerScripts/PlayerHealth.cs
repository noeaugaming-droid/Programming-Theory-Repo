using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float health = 100f;
    public void TakeDamage(float damageAmount)
    {
        health -= damageAmount;
        Debug.Log("Player Health: " + health);

        if (health <=0)
        {
            Debug.Log("Player Died");
        }
    }
}
