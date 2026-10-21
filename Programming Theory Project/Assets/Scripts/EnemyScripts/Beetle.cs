using UnityEngine;

public class Beetle : Enemy
{
    [Header("Attack")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float chargeForce = 15f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float knockbackDuration = 0.3f;

    private Transform player;
    private PlayerHealth playerHealth;
    private Rigidbody beetleRb;
    private float attackTimer;
    private float knockbackTimer;

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();
        beetleRb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (playerHealth.isDead) return;

        attackTimer -= Time.fixedDeltaTime;

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector3 direction = player.position - beetleRb.position;
        direction.y = 0f;

        if (direction.magnitude > attackRange)
        {
            beetleRb.MoveRotation(Quaternion.LookRotation(direction) * Quaternion.Euler(90f, 0f, 0f));
            beetleRb.MovePosition(beetleRb.position + direction.normalized * speed * Time.fixedDeltaTime);
        }
        else if (attackTimer <= 0f)
        {
            // POLYMORPHISM: An Enemy reference calls Attack(), but the Beetle
            // override runs because this object is a Beetle.
            Enemy enemy = this;
            enemy.Attack();
            attackTimer = attackCooldown;
        }
    }

    public override void Attack()
    {
        Vector3 direction = player.position - beetleRb.position;
        direction.y = 0f;
        beetleRb.AddForce(direction.normalized * chargeForce, ForceMode.Impulse);
        playerHealth.TakeDamage(damage);
        Debug.Log("Beetle attacks!");
    }

    public void Knockback(Vector3 direction)
    {
        beetleRb.AddForce(direction.normalized * knockbackForce, ForceMode.Impulse);
        knockbackTimer = knockbackDuration;
    }
}
