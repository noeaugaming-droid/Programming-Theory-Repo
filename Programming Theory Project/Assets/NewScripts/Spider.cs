using UnityEngine;

// INHERITANCE: Spider gets health, speed, damage and TakeDamage from Enemy.
public class Spider : Enemy
{
    [Header("Attack")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackDelay = 2f;

    [Header("Knockback")]
    [SerializeField] private float knockbackDuration = 0.3f;
    [SerializeField] private float knockbackForce = 10f;

    private Transform player;
    private PlayerHealth playerHealth;
    private Rigidbody spiderRb;
    private float attackTimer;
    private float knockbackTimer;

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            Debug.LogError("Spider needs a GameObject tagged Player.");
            enabled = false;
            return;
        }

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();
        spiderRb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (playerHealth == null || playerHealth.isDead) return;

        attackTimer += Time.deltaTime;
        if (Vector3.Distance(transform.position, player.position) <= attackRange &&
            attackTimer >= attackDelay)
        {
            Attack();
            attackTimer = 0f;
        }
    }

    void FixedUpdate()
    {
        if (playerHealth == null || playerHealth.isDead) return;

        // Let physics handle the knockback before chasing again.
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector3 direction = player.position - spiderRb.position;
        direction.y = 0f;

        if (direction.magnitude > attackRange)
        {
            spiderRb.MoveRotation(Quaternion.LookRotation(direction));
            // Velocity respects solid collisions better than repeatedly moving
            // a Rigidbody into another collider with MovePosition.
            Vector3 movement = direction.normalized * speed;
            spiderRb.linearVelocity = new Vector3(
                movement.x, spiderRb.linearVelocity.y, movement.z);
        }
        else
        {
            StopMoving();
        }
    }

    private void StopMoving()
    {
        spiderRb.linearVelocity = new Vector3(0f, spiderRb.linearVelocity.y, 0f);
    }

    public void Knockback(Vector3 direction)
    {
        StopMoving();
        spiderRb.AddForce(direction.normalized * knockbackForce, ForceMode.Impulse);
        knockbackTimer = knockbackDuration;
    }

    public override void Attack()
    {
        if (playerHealth != null && !playerHealth.isDead)
        {
            playerHealth.TakeDamage(damage);
            Debug.Log("Spider attacks!");
        }
    }
}
