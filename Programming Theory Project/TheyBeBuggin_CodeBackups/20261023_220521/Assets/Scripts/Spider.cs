
using UnityEngine;

[RequireComponent(typeof(EnemySurfaceCrawler))]
public class Spider : Enemy
{
    [Header("Attack")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackDelay = 2f;

    [Header("Individual Speed")]
    [SerializeField] private float minimumSpeed = 4f;
    [SerializeField] private float maximumSpeed = 7f;

    [Header("Movement")]
    [SerializeField] private float preferredPlayerDistance = 2.8f;
    [SerializeField] private float strafeStrength = 0.7f;
    [SerializeField] private float zigzagStrength = 0.4f;
    [SerializeField] private float orbitStrength = 0.65f;
    [SerializeField] private float directionChangeInterval = 0.6f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float knockbackDuration = 0.3f;

    private EnemySurfaceCrawler crawler;
    private Transform player;
    private PlayerHealth playerHealth;

    private float individualSpeed;
    private float attackTimer;
    private float movementTimer;

    private float strafeDirection;
    private float orbitDirection;
    private float zigzagPhase;

    public float IndividualSpeed => individualSpeed;

    private void Start()
    {
        crawler = GetComponent<EnemySurfaceCrawler>();

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            Debug.LogError(
                "Spider requires a Player-tagged object.",
                this
            );

            enabled = false;
            return;
        }

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();

        float min = Mathf.Max(0f, minimumSpeed);
        float max = Mathf.Clamp(
            maximumSpeed,
            min,
            min + 3f
        );

        individualSpeed = Random.Range(min, max);

        orbitDirection = Random.value < 0.5f ? -1f : 1f;
        zigzagPhase = Random.Range(0f, Mathf.PI * 2f);

        ChooseMovement();
    }

    private void Update()
    {
        if (player == null ||
            playerHealth == null ||
            playerHealth.isDead)
        {
            crawler.SetMovement(Vector3.zero, 0f);
            return;
        }

        attackTimer += Time.deltaTime;
        movementTimer -= Time.deltaTime;

        if (movementTimer <= 0f)
            ChooseMovement();

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance <= attackRange &&
            attackTimer >= attackDelay)
        {
            Attack();
            attackTimer = 0f;
        }

        Vector3 normal = crawler.SurfaceNormal;

        Vector3 towardPlayer =
            player.position - transform.position;

        Vector3 forward =
            Vector3.ProjectOnPlane(
                towardPlayer,
                normal
            ).normalized;

        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        Vector3 right =
            Vector3.Cross(normal, forward).normalized;

        float zigzag = Mathf.Sin(
            Time.time * 5f + zigzagPhase
        ) * zigzagStrength;

        Vector3 direction;

        if (distance > preferredPlayerDistance)
        {
            direction =
                forward +
                right * (
                    strafeDirection +
                    zigzag +
                    orbitDirection * orbitStrength
                );
        }
        else
        {
            direction =
                right * orbitDirection +
                forward * 0.25f;
        }

        float speedMultiplier =
            distance <= preferredPlayerDistance
                ? 0.65f
                : 1f;

        crawler.SetMovement(
            direction.normalized,
            individualSpeed * speedMultiplier
        );
    }

    private void ChooseMovement()
    {
        movementTimer = Random.Range(
            Mathf.Max(0.1f, directionChangeInterval * 0.5f),
            Mathf.Max(0.2f, directionChangeInterval * 1.5f)
        );

        strafeDirection = Random.Range(
            -strafeStrength,
            strafeStrength
        );

        if (Random.value < 0.35f)
            orbitDirection *= -1f;
    }

    public void Knockback(Vector3 direction)
    {
        if (crawler != null)
        {
            crawler.ApplyKnockback(
                direction,
                knockbackForce,
                knockbackDuration
            );
        }
    }

    public override void Attack()
    {
        if (playerHealth == null || playerHealth.isDead)
            return;

        playerHealth.TakeDamage(damage);

        Debug.Log("Spider attacks!");
    }
}
