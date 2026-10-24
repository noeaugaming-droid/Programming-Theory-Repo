
using UnityEngine;

// ABSTRACTION: All enemies share these stats and behaviors.
public abstract class Enemy : MonoBehaviour
{
    [Header("Enemy Stats")]
    [SerializeField] private float health = 100f;
    [SerializeField] protected float speed = 5f;
    [SerializeField] protected float damage = 10f;

    public static int AliveCount { get; private set; }

    protected virtual void OnEnable()
    {
        AliveCount++;
    }

    protected virtual void OnDisable()
    {
        AliveCount--;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount()
    {
        AliveCount = 0;
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;

        health -= amount;
        if (health <= 0f)
        {
            Destroy(gameObject);
        }
    }

    public abstract void Attack();
}
