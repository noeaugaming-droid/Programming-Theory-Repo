
using UnityEngine;

// ABSTRACTION: All enemies share these stats and behaviors.
public abstract class Enemy : MonoBehaviour
{
    #region Combat_And_Health_Variables
    // Values and references used by this part of the script.
    #endregion
    #region Physics_And_Movement_Variables
    // Values and references used by this part of the script.
    #endregion
    #region Combat_And_Health_Variables
    // Values and references used by this part of the script.
    [Header("Enemy Stats")]
    [SerializeField] private float health = 100f;
    [SerializeField] protected float speed = 5f;
    [SerializeField] protected float damage = 10f;

    public static int AliveCount { get; private set; }

    #endregion
    #region Unity_Lifecycle_Methods
    // Methods Unity calls automatically during the object's lifetime.
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

    #endregion
    #region Combat_And_Health_Methods
    // Attacks, damage, and health-related behavior.
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
    #endregion
}
