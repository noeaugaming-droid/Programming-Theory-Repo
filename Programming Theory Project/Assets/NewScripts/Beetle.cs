using UnityEngine;

public class Beetle : Enemy
{
    [Header("Attack")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float chargeForce = 15f;
    [SerializeField] private float chargeDuration = 0.3f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float knockbackDuration = 0.3f;

    private Transform player;
    private PlayerHealth playerHealth;
    private Rigidbody beetleRb;
    private float attackTimer;
    private float knockbackTimer;
    private float chargeTimer;

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            Debug.LogError("Beetle needs a GameObject tagged Player.");
            enabled = false;
            return;
        }

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();
        beetleRb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (playerHealth == null || playerHealth.isDead) return;

        attackTimer -= Time.fixedDeltaTime;

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        // Do not overwrite the force from a charge immediately.
        if (chargeTimer > 0f)
        {
            chargeTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector3 direction = player.position - beetleRb.position;
        direction.y = 0f;

        if (direction.magnitude > attackRange)
        {
            // Keep the original beetle model's 90-degree rotation.
            beetleRb.MoveRotation(
                Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f));

            Vector3 movement = direction.normalized * speed;
            beetleRb.linearVelocity = new Vector3(
                movement.x, beetleRb.linearVelocity.y, movement.z);
        }
        else
        {
            StopMoving();
            if (attackTimer <= 0f)
            {
                // An Enemy reference can call the Beetle's own Attack method.
                Enemy enemy = this;
                enemy.Attack();
                attackTimer = attackCooldown;
            }
        }
    }

    private void StopMoving()
    {
        beetleRb.linearVelocity = new Vector3(0f, beetleRb.linearVelocity.y, 0f);
    }

    public override void Attack()
    {
        if (playerHealth == null || playerHealth.isDead) return;

        Vector3 direction = player.position - beetleRb.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            beetleRb.AddForce(direction.normalized * chargeForce, ForceMode.Impulse);
            chargeTimer = chargeDuration;
        }

        playerHealth.TakeDamage(damage);
        Debug.Log("Beetle attacks!");
    }

    public void Knockback(Vector3 direction)
    {
        StopMoving();
        chargeTimer = 0f;
        beetleRb.AddForce(direction.normalized * knockbackForce, ForceMode.Impulse);
        knockbackTimer = knockbackDuration;
    }
}
