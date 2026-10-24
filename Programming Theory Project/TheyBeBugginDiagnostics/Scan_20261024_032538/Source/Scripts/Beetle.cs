
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

    [Header("Targeting")]
    [SerializeField, Min(0.1f)]
    private float targetSearchInterval = 1f;

    private EnemySurfaceCrawler crawler;
    private Transform player;
    private PlayerHealth playerHealth;

    private float attackTimer;
    private float searchTimer;
    private float individualSpeed;

    public float IndividualSpeed => individualSpeed;

    private void Awake()
    {
        crawler = GetComponent<EnemySurfaceCrawler>();

        individualSpeed = Random.Range(
            minimumSpeed,
            Mathf.Max(minimumSpeed, maximumSpeed));
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (crawler == null)
            return;

        if (player == null || playerHealth == null)
        {
            searchTimer -= Time.deltaTime;

            if (searchTimer <= 0f)
            {
                FindPlayer();
                searchTimer = targetSearchInterval;
            }
        }

        if (player == null ||
            playerHealth == null ||
            playerHealth.isDead)
        {
            crawler.SetMovement(Vector3.zero, 0f);
            return;
        }

        attackTimer -= Time.deltaTime;

        Vector3 toward =
            player.position - transform.position;

        float distance = toward.magnitude;

        if (distance > 0.001f)
        {
            crawler.SetMovement(
                toward.normalized,
                individualSpeed);
        }
        else
        {
            crawler.SetMovement(
                Vector3.zero,
                0f);
        }

        if (distance <= attackRange &&
            attackTimer <= 0f)
        {
            Attack();
            attackTimer = attackCooldown;
        }
    }

    private void FindPlayer()
    {
        GameObject target =
            GameObject.FindGameObjectWithTag("Player");

        if (target == null)
        {
            player = null;
            playerHealth = null;
            return;
        }

        player = target.transform;

        playerHealth =
            target.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            playerHealth =
                target.GetComponentInChildren<PlayerHealth>();
        }
    }

    public void Knockback(Vector3 direction)
    {
        if (crawler == null)
            return;

        crawler.ApplyKnockback(
            direction,
            knockbackForce,
            knockbackDuration);
    }

    public override void Attack()
    {
        if (playerHealth != null &&
            !playerHealth.isDead)
        {
            playerHealth.TakeDamage(damage);
        }
    }
}
