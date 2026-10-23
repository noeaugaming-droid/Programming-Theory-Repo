
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Spider : Enemy
{
    [Header("Attack")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackDelay = 2f;

    [Header("Individual Clone Speed")]
    [Min(0f)][SerializeField] private float minimumSpeed = 4f;
    [Min(0f)][SerializeField] private float maximumSpeed = 7f;

    [Header("Erratic Movement")]
    [SerializeField] private float directionChangeMin = 0.25f;
    [SerializeField] private float directionChangeMax = 0.85f;
    [Range(0f, 1f)][SerializeField] private float strafeStrength = 0.7f;
    [Range(0f, 1f)][SerializeField] private float zigzagStrength = 0.4f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Independent Chase")]
    [SerializeField] private float preferredPlayerDistance = 2.8f;
    [SerializeField] private float orbitStrength = 0.65f;
    [SerializeField] private float orbitChangeMin = 1.5f;
    [SerializeField] private float orbitChangeMax = 4f;

    [Header("Enemy Separation")]
    [SerializeField] private float separationRadius = 3.5f;
    [SerializeField] private float separationStrength = 2.5f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Occasional Grouping")]
    [Range(0f, 1f)]
    [SerializeField] private float groupingChance = 0.15f;
    [SerializeField] private float groupedSeparationMultiplier = 0.25f;

    [Header("Knockback")]
    [SerializeField] private float knockbackDuration = 0.3f;
    [SerializeField] private float knockbackForce = 10f;

    private Transform player;
    private PlayerHealth playerHealth;
    private Rigidbody spiderRb;

    private float attackTimer;
    private float knockbackTimer;

    private float individualSpeed;
    private float movementChangeTimer;
    private float currentStrafe;
    private float currentZigzag;
    private float zigzagPhase;
    private float zigzagFrequency;

    private float orbitTimer;
    private float orbitDirection;
    private float individualOrbitStrength;
    private float individualPreferredDistance;
    private float individualSeparationStrength;
    private bool toleratesGrouping;

    private readonly Collider[] nearbyEnemies = new Collider[32];

    public float IndividualSpeed => individualSpeed;

    private void Start()
    {
        spiderRb = GetComponent<Rigidbody>();

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            Debug.LogError(
                "Spider needs a GameObject tagged Player.",
                this
            );
            enabled = false;
            return;
        }

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();

        // Every spawned clone rolls its own speed.
        // The range is always capped to a width of 3f.
        float min = Mathf.Max(0f, minimumSpeed);
        float max = Mathf.Clamp(maximumSpeed, min, min + 3f);

        individualSpeed = Random.Range(min, max);

        toleratesGrouping =
            Random.value < groupingChance;

        individualSeparationStrength =
            separationStrength * Random.Range(0.85f, 1.2f);

        if (toleratesGrouping)
        {
            individualSeparationStrength *=
                Mathf.Clamp01(groupedSeparationMultiplier);
        }

        individualPreferredDistance =
            Mathf.Max(
                attackRange,
                preferredPlayerDistance +
                Random.Range(-0.5f, 1.5f)
            );

        individualOrbitStrength =
            orbitStrength * Random.Range(0.7f, 1.3f);

        orbitDirection = Random.value < 0.5f ? -1f : 1f;

        zigzagPhase = Random.Range(0f, Mathf.PI * 2f);
        zigzagFrequency = Random.Range(3f, 7f);

        ChooseNewMovement();
        ChooseNewOrbit();

        Debug.Log(
            $"{name} spawned with speed {individualSpeed:F2}",
            this
        );
    }

    private void Update()
    {
        if (playerHealth == null || playerHealth.isDead)
            return;

        attackTimer += Time.deltaTime;

        if (Vector3.Distance(
            transform.position,
            player.position
        ) <= attackRange &&
            attackTimer >= attackDelay)
        {
            Attack();
            attackTimer = 0f;
        }
    }

    private void FixedUpdate()
    {
        if (playerHealth == null ||
            playerHealth.isDead ||
            spiderRb == null)
            return;

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector3 toPlayer =
            player.position - spiderRb.position;

        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;

        movementChangeTimer -= Time.fixedDeltaTime;
        orbitTimer -= Time.fixedDeltaTime;

        if (movementChangeTimer <= 0f)
            ChooseNewMovement();

        if (orbitTimer <= 0f)
            ChooseNewOrbit();

        Vector3 forward =
            distance > 0.001f
            ? toPlayer / distance
            : transform.forward;

        forward.y = 0f;
        forward.Normalize();

        Vector3 right =
            Vector3.Cross(Vector3.up, forward);

        float zigzag =
            Mathf.Sin(
                Time.time * zigzagFrequency + zigzagPhase
            ) * currentZigzag;

        Vector3 separation = CalculateSeparation();

        Vector3 movementDirection;

        if (distance > individualPreferredDistance)
        {
            movementDirection =
                forward +
                right * (
                    currentStrafe +
                    zigzag +
                    orbitDirection * individualOrbitStrength
                ) +
                separation * individualSeparationStrength;
        }
        else
        {
            // Close to the player: circle or reposition
            // rather than piling onto the same spot.
            movementDirection =
                right * orbitDirection +
                separation * individualSeparationStrength;

            // Avoid drifting too far outside attack range.
            if (distance > attackRange)
                movementDirection += forward * 0.6f;
        }

        movementDirection.y = 0f;

        if (movementDirection.sqrMagnitude > 0.001f)
            movementDirection.Normalize();

        float movementSpeed = individualSpeed;

        // Slow down a little while circling near the player.
        if (distance <= individualPreferredDistance)
            movementSpeed *= 0.65f;

        Vector3 movement =
            movementDirection * movementSpeed;

        spiderRb.linearVelocity = new Vector3(
            movement.x,
            spiderRb.linearVelocity.y,
            movement.z
        );

        if (forward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(forward);

            spiderRb.MoveRotation(
                Quaternion.Slerp(
                    spiderRb.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime
                )
            );
        }
    }

    private Vector3 CalculateSeparation()
    {
        if (separationRadius <= 0f)
            return Vector3.zero;

        int count = Physics.OverlapSphereNonAlloc(
            spiderRb.position,
            separationRadius,
            nearbyEnemies,
            enemyLayers,
            QueryTriggerInteraction.Ignore
        );

        Vector3 separation = Vector3.zero;
        int validNeighbors = 0;

        for (int i = 0; i < count; i++)
        {
            Collider otherCollider = nearbyEnemies[i];

            if (otherCollider == null)
                continue;

            Enemy otherEnemy =
                otherCollider.GetComponentInParent<Enemy>();

            if (otherEnemy == null || otherEnemy == this)
                continue;

            Vector3 away =
                spiderRb.position -
                otherEnemy.transform.position;

            away.y = 0f;

            float distance = away.magnitude;

            if (distance < 0.001f ||
                distance >= separationRadius)
                continue;

            float weight =
                1f - distance / separationRadius;

            separation +=
                away.normalized * weight;

            validNeighbors++;
        }

        if (validNeighbors > 0)
            separation /= validNeighbors;

        return separation;
    }

    private void ChooseNewMovement()
    {
        float min = Mathf.Max(0.05f, directionChangeMin);
        float max = Mathf.Max(min, directionChangeMax);

        movementChangeTimer = Random.Range(min, max);

        currentStrafe = Random.Range(
            -strafeStrength,
            strafeStrength
        );

        currentZigzag = Random.Range(
            0f,
            zigzagStrength
        );

        zigzagFrequency = Random.Range(3f, 7f);
    }

    private void ChooseNewOrbit()
    {
        float min = Mathf.Max(0.1f, orbitChangeMin);
        float max = Mathf.Max(min, orbitChangeMax);

        orbitTimer = Random.Range(min, max);

        if (Random.value < 0.35f)
            orbitDirection *= -1f;
    }

    private void StopMoving()
    {
        spiderRb.linearVelocity = new Vector3(
            0f,
            spiderRb.linearVelocity.y,
            0f
        );
    }

    public void Knockback(Vector3 direction)
    {
        if (spiderRb == null)
            return;

        StopMoving();

        spiderRb.AddForce(
            direction.normalized * knockbackForce,
            ForceMode.Impulse
        );

        knockbackTimer = knockbackDuration;

        ChooseNewMovement();
        ChooseNewOrbit();
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
