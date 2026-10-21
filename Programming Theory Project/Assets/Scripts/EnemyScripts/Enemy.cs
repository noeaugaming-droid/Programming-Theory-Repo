using UnityEngine;

// ABSTRACTION: Enemy is an abstract base class that defines what all enemies share.
// Each specific enemy must provide its own Attack() behavior.
public abstract class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    // ENCAPSULATION: Health is private so other scripts cannot change it directly.
    // They must call TakeDamage(), which controls how health is reduced.
    [SerializeField] private float health = 100f;
    [SerializeField] protected float speed = 5f;
    [SerializeField] protected float damage = 10f;

    public void TakeDamage(float damageAmount)
    {
        health -= damageAmount;
        if (health <= 0f)
        {
            Destroy(gameObject);
        }
    }

    public abstract void Attack();
}
