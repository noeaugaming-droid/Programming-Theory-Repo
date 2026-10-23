
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Beetle : Enemy
{
    [Header("Attack")]
    [SerializeField] private float attackRange = 3f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float chargeForce = 15f;
    [SerializeField] private float chargeDuration = 0.3f;

    [Header("Individual Clone Speed")]
    [Min(0f)][SerializeField] private float minimumSpeed = 5f;
    [Min(0f)][SerializeField] private float maximumSpeed = 8f;

    [Header("Erratic Movement")]
    [SerializeField] private float directionChangeMin = 0.4f;
    [SerializeField] private float directionChangeMax = 1.2f;
    [Range(0f, 1f)][SerializeField] private float strafeStrength = 0.6f;
    [Range(0f, 1f)][SerializeField] private float zigzagStrength = 0.35f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Independent Chase")]
    [SerializeField] private float preferredPlayerDistance = 4f;
    [SerializeField] private float orbitStrength = 0.75f;
    [SerializeField] private float orbitChangeMin = 2f;
    [SerializeField] private float orbitChangeMax = 5f;

    [Header("Enemy Separation")]
    [SerializeField] private float separationRadius = 4.5f;
    [SerializeField] private float separationStrength = 2.8f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    [Header("Occasional Grouping")]
    [Range(0f, 1f)]
    [SerializeField] private float groupingChance = 0.2f;
    [SerializeField] private float groupedSeparationMultiplier = 0.25f;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 10f;
    [SerializeField] private float knockbackDuration = 0.3f;

    private Transform player;
    private PlayerHealth playerHealth;
    private Rigidbody beetleRb;

    private float attackTimer;
    private float knockbackTimer;
    private float chargeTimer;

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
        beetleRb = GetComponent<Rigidbody>();

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            Debug.LogError(
                "Beetle needs a GameObject tagged Player.",
                this
            );
            enabled = false;
            return;
        }

        player = playerObject.transform;
        playerHealth = playerObject.GetComponent<PlayerHealth>();

        // Every beetle clone independently chooses a speed.
        // The permitted speed range never exceeds 3f.
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
        zigzagFrequency = Random.Range(2f, 5f);

        ChooseNewMovement();
        ChooseNewOrbit();

        Debug.Log(
            $"{name} spawned with speed {individualSpeed:F2}",
            this
        );
    }

    private void FixedUpdate()
    {
        if (playerHealth == null ||
            playerHealth.isDead ||
            beetleRb == null)
            return;

        attackTimer -= Time.fixedDeltaTime;

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        if (chargeTimer > 0f)
        {
            chargeTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector3 toPlayer =
            player.position - beetleRb.position;

        toPlayer.y = 0f;

        float distance = toPlayer.magnitude;

        if (distance <= attackRange &&
            attackTimer <= 0f)
        {
            Attack();
            attackTimer = attackCooldown;
            return;
        }

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
            // Move around the player instead of stacking
            // in the same position as other enemies.
            movementDirection =
                right * orbitDirection +
                separation * individualSeparationStrength;

            if (distance > attackRange)
                movementDirection += forward * 0.8f;
        }

        movementDirection.y = 0f;

        if (movementDirection.sqrMagnitude > 0.001f)
            movementDirection.Normalize();

        float movementSpeed = individualSpeed;

        if (distance <= individualPreferredDistance)
            movementSpeed *= 0.7f;

        Vector3 movement =
            movementDirection * movementSpeed;

        beetleRb.linearVelocity = new Vector3(
            movement.x,
            beetleRb.linearVelocity.y,
            movement.z
        );

        if (forward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(forward) *
                Quaternion.Euler(90f, 0f, 0f);

            beetleRb.MoveRotation(
                Quaternion.Slerp(
                    beetleRb.rotation,
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
            beetleRb.position,
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
                beetleRb.position -
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

        zigzagFrequency = Random.Range(2f, 5f);
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
        beetleRb.linearVelocity = new Vector3(
            0f,
            beetleRb.linearVelocity.y,
            0f
        );
    }

    public override void Attack()
    {
        if (playerHealth == null || playerHealth.isDead)
            return;

        Vector3 direction =
            player.position - beetleRb.position;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            beetleRb.AddForce(
                direction.normalized * chargeForce,
                ForceMode.Impulse
            );

            chargeTimer = chargeDuration;
        }

        playerHealth.TakeDamage(damage);
        Debug.Log("Beetle attacks!");
    }

    public void Knockback(Vector3 direction)
    {
        if (beetleRb == null)
            return;

        StopMoving();
        chargeTimer = 0f;

        beetleRb.AddForce(
            direction.normalized * knockbackForce,
            ForceMode.Impulse
        );

        knockbackTimer = knockbackDuration;

        ChooseNewMovement();
        ChooseNewOrbit();
    }
}
