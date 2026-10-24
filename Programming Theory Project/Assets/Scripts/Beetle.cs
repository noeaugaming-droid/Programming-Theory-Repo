using UnityEngine;

[RequireComponent(typeof(EnemySurfaceCrawler))]
public class Beetle : Enemy
{
    [Header("Chase")]
    [SerializeField] private float minimumSpeed = 5f;
    [SerializeField] private float maximumSpeed = 8f;
    [SerializeField] private float attackRange = 2.5f;
    [SerializeField] private float attackCooldown = 2f;
    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float knockbackDuration = 0.3f;

    private EnemySurfaceCrawler crawler;
    private Transform player;
    private PlayerHealth playerHealth;
    private float attackTimer;
    private float individualSpeed;
    public float IndividualSpeed => individualSpeed;

    private void Start()
    {
        crawler = GetComponent<EnemySurfaceCrawler>();
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
        {
            player = target.transform;
            playerHealth = target.GetComponent<PlayerHealth>();
        }
        individualSpeed = Random.Range(minimumSpeed, Mathf.Max(minimumSpeed, maximumSpeed));
    }

    private void Update()
    {
        if (player == null || playerHealth == null || playerHealth.isDead)
        {
            crawler.SetMovement(Vector3.zero, 0f);
            return;
        }
        attackTimer -= Time.deltaTime;
        Vector3 toward = player.position - transform.position;
        float distance = toward.magnitude;
        crawler.SetMovement(toward, individualSpeed);
        if (distance <= attackRange && attackTimer <= 0f)
        {
            Attack();
            attackTimer = attackCooldown;
        }
    }

    public void Knockback(Vector3 direction) =>
        crawler.ApplyKnockback(direction, knockbackForce, knockbackDuration);

    public override void Attack()
    {
        if (playerHealth != null && !playerHealth.isDead)
            playerHealth.TakeDamage(damage);
    }
}
