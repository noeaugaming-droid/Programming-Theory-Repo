using UnityEngine;

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
        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();
        spiderRb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (playerHealth.isDead) return;

        attackTimer += Time.deltaTime;
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= attackRange && attackTimer >= attackDelay)
        {
            Attack();
            attackTimer = 0f;
        }
    }

    void FixedUpdate()
    {
        if (playerHealth.isDead) return;

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
            spiderRb.MovePosition(spiderRb.position + direction.normalized * speed * Time.fixedDeltaTime);
        }
    }

    public void Knockback(Vector3 direction)
    {
        spiderRb.AddForce(direction.normalized * knockbackForce, ForceMode.Impulse);
        knockbackTimer = knockbackDuration;
    }

    public override void Attack()
    {
        playerHealth.TakeDamage(damage);
        Debug.Log("Spider attacks!");
    }
}
