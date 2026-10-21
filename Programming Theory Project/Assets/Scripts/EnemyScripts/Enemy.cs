using UnityEngine;

public abstract class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
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
