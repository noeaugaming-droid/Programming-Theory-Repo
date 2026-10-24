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

    #region Spider_Jump_Variables
    [Header("Surface Jumping")]
    [SerializeField] private float maximumJumpDistance = 15f;
    [SerializeField] private float jumpCooldown = 2.5f;
    [SerializeField] private float jumpDuration = 0.9f;
    [SerializeField] private float jumpSearchInterval = 0.5f;
    [SerializeField] private float jumpProgressThreshold = 0.5f;
    private float jumpCooldownTimer;
    private float jumpSearchTimer;
    private float jumpProgressTimer;
    private float lastJumpProgressDistance;
    #endregion

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
        lastJumpProgressDistance = Vector3.Distance(transform.position, player.position);
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

        jumpCooldownTimer -= Time.deltaTime;
        jumpSearchTimer -= Time.deltaTime;
        jumpProgressTimer += Time.deltaTime;
        if (crawler.IsJumping)
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

        if (jumpProgressTimer >= 1f)
        {
            if (distance >= lastJumpProgressDistance - jumpProgressThreshold && jumpCooldownTimer <= 0f)
                TryFindJumpTarget();
            lastJumpProgressDistance = distance;
            jumpProgressTimer = 0f;
        }
        if (crawler.IsStuck && jumpCooldownTimer <= 0f && jumpSearchTimer <= 0f)
            TryFindJumpTarget();

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

    #region Spider_Jump_Methods
    // Search for an accessible landing surface within 15 units.
    private void TryFindJumpTarget()
    {
        jumpSearchTimer = Mathf.Max(0.1f, jumpSearchInterval);
        if (player == null || crawler.IsJumping || !crawler.HasSurface) return;

        Vector3 origin = transform.position;
        Vector3 toward = (player.position - origin).normalized;
        Vector3 bestPoint = Vector3.zero;
        Vector3 bestNormal = Vector3.up;
        float bestScore = float.NegativeInfinity;
        bool found = false;

        // Sample candidate landing locations around the player and between us.
        for (int ring = 1; ring <= 3; ring++)
        {
            float fraction = ring / 3f;
            Vector3 center = Vector3.Lerp(origin, player.position, fraction);
            for (int sample = 0; sample < 12; sample++)
            {
                float angle = sample * Mathf.PI * 2f / 12f;
                Vector3 sideways = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 probe = center + sideways * (ring == 3 ? 2f : 1f);
                // Cast downward first, then sideways for walls and ceilings.
                Vector3[] directions = { Vector3.down, Vector3.up, toward, -toward };
                foreach (Vector3 direction in directions)
                {
                    if (!Physics.Raycast(probe - direction * 4f, direction,
                        out RaycastHit hit, 8f, crawler.ClimbableLayers,
                        QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.GetComponentInParent<Enemy>() != null) continue;
                    Vector3 destination = hit.point + hit.normal * 0.55f;
                    float jumpDistance = Vector3.Distance(origin, destination);
                    if (jumpDistance < 2f || jumpDistance > maximumJumpDistance) continue;
                    float improvement = Vector3.Distance(origin, player.position) -
                        Vector3.Distance(destination, player.position);
                    if (improvement < 1.5f) continue;
                    float score = improvement - jumpDistance * 0.12f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPoint = hit.point;
                        bestNormal = hit.normal;
                        found = true;
                    }
                }
            }
        }
        if (found && crawler.TryBeginSpiderJump(bestPoint, bestNormal,
            jumpDuration, maximumJumpDistance))
            jumpCooldownTimer = jumpCooldown;
    }
    #endregion

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