
using UnityEngine;

/// <summary>
/// Shared surface movement controller for spiders and beetles.
///
/// Beetles:
/// - Crawl on connected floors, walls, and ceilings.
/// - Cannot initiate jumps.
///
/// Spiders:
/// - Crawl and climb.
/// - Can initiate validated jumps up to 15 units.
///
/// Both:
/// - Detect valid climbable surfaces.
/// - Avoid nearby enemies.
/// - Use EnemyNavigationSensor when available.
/// - Stop normal crawling when airborne.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemySurfaceCrawler : MonoBehaviour
{
    #region Surface_Detection_Variables

    [Header("Surface Detection")]

    [SerializeField]
    private LayerMask climbableLayers = ~0;

    [SerializeField, Min(0.05f)]
    private float surfaceOffset = 0.7f;

    [SerializeField, Min(0.1f)]
    private float surfaceProbeDistance = 1.5f;

    [SerializeField, Min(0.1f)]
    private float wallDetectionDistance = 1.2f;

    [SerializeField, Min(0.1f)]
    private float edgeProbeDistance = 1.5f;

    [SerializeField, Min(0.05f)]
    private float maximumSurfaceGap = 0.85f;

    #endregion


    #region Physics_Movement_Variables

    [Header("Movement")]

    [SerializeField]
    private float rotationSpeed = 12f;

    [SerializeField]
    private float surfaceSnapSpeed = 12f;

    [SerializeField]
    private float surfaceNormalSpeed = 10f;

    [Header("Airborne Recovery")]

    [SerializeField]
    private float lostSurfaceGravity = 15f;

    [SerializeField]
    private float maximumRecoveryDistance = 3f;

    #endregion


    #region Navigation_Variables

    [Header("Navigation")]

    [SerializeField]
    private float obstacleProbeDistance = 1.4f;

    [SerializeField]
    private float enemyAvoidanceRadius = 1.3f;

    [SerializeField]
    private float stuckCheckInterval = 1f;

    [SerializeField]
    private float minimumProgress = 0.4f;

    [SerializeField]
    private float recoveryDuration = 2f;

    [SerializeField]
    private float edgeTurnSearchDistance = 1.2f;

    #endregion


    #region Private_Class_Variables

    private Rigidbody rb;
    private Collider bodyCollider;

    private Transform player;

    private EnemyApproachVariation approachVariation;

    // NEW: Optional raycast navigation component.
    private EnemyNavigationSensor navigationSensor;

    private Vector3 surfaceNormal = Vector3.up;
    private Vector3 desiredDirection;
    private Vector3 knockbackVelocity;

    private Vector3 lastPosition;

    private Vector3 jumpStart;
    private Vector3 jumpVelocity;
    private Vector3 jumpTarget;
    private Vector3 jumpLandingNormal;

    private float desiredSpeed;
    private float knockbackTimer;

    private float stuckTimer;
    private float recoveryTimer;
    private float lastPlayerDistance;

    private float fallSpeed;

    private float jumpElapsed;
    private float jumpDuration;

    private int recoverySide = 1;

    private bool hasSurface;
    private bool spiderMode;
    private bool jumping;

    private readonly Collider[] neighbors = new Collider[32];

    #endregion


    #region Public_Properties

    public Vector3 SurfaceNormal => surfaceNormal;

    public bool HasSurface => hasSurface;

    public bool IsJumping => jumping;

    public bool IsStuck =>
        recoveryTimer > 0f ||
        (navigationSensor != null && navigationSensor.IsStuck);

    public LayerMask ClimbableLayers => climbableLayers;

    #endregion


    #region Unity_Lifecycle_Methods

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        bodyCollider = GetComponent<Collider>();

        rb.useGravity = false;
        rb.isKinematic = true;

        rb.interpolation =
            RigidbodyInterpolation.Interpolate;

        surfaceNormal = transform.up;

        lastPosition = rb.position;

        // Only objects with Spider can initiate jumps.
        spiderMode = GetComponent<Spider>() != null;

        approachVariation =
            GetComponent<EnemyApproachVariation>();

        // Find the new sensor if attached.
        navigationSensor =
            GetComponent<EnemyNavigationSensor>();
    }


    private void Start()
    {
        GameObject found =
            GameObject.FindGameObjectWithTag("Player");

        if (found != null)
        {
            player = found.transform;
        }

        lastPlayerDistance =
            player != null
            ? Vector3.Distance(rb.position, player.position)
            : 0f;
    }


    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // Nothing means no valid climbable surfaces.
        if (climbableLayers.value == 0)
        {
            jumping = false;
            hasSurface = false;

            Fall(dt);
            return;
        }

        // Jumping is separate from crawling.
        if (jumping)
        {
            UpdateJump(dt);
            return;
        }

        Vector3 position = rb.position;

        // Update knockback.
        if (knockbackTimer > 0f)
        {
            knockbackTimer =
                Mathf.Max(0f, knockbackTimer - dt);

            knockbackVelocity = Vector3.Lerp(
                knockbackVelocity,
                Vector3.zero,
                Mathf.Clamp01(dt * 6f)
            );
        }

        // Do not crawl through empty air.
        if (!hasSurface)
        {
            if (!TryAcquireSurface(
                position,
                out RaycastHit initialHit))
            {
                Fall(dt);
                return;
            }

            AttachToSurface(initialHit);

            position = rb.position;
        }

        // Choose an individual movement direction.
        Vector3 movement =
            ChooseDirection(position, dt);

        if (knockbackTimer > 0f)
        {
            movement +=
                Vector3.ProjectOnPlane(
                    knockbackVelocity,
                    surfaceNormal
                ) / Mathf.Max(0.01f, desiredSpeed);
        }

        movement =
            Vector3.ProjectOnPlane(
                movement,
                surfaceNormal
            ).normalized;

        // Confirm that we remain near a real surface.
        if (!TryFindConnectedSurface(
            position,
            movement,
            out RaycastHit currentHit))
        {
            hasSurface = false;

            Fall(dt);
            return;
        }

        // Adjust to the detected surface.
        surfaceNormal = Vector3.Slerp(
            surfaceNormal,
            currentHit.normal,
            Mathf.Clamp01(surfaceNormalSpeed * dt)
        ).normalized;

        Vector3 snapped =
            currentHit.point +
            currentHit.normal * surfaceOffset;

        position = Vector3.Lerp(
            position,
            snapped,
            Mathf.Clamp01(surfaceSnapSpeed * dt)
        );

        fallSpeed = 0f;
        hasSurface = true;

        // Apply normal crawling movement.
        if (movement.sqrMagnitude > 0.0001f &&
            desiredSpeed > 0f)
        {
            Vector3 travel =
                Vector3.ProjectOnPlane(
                    movement,
                    surfaceNormal
                ).normalized;

            Vector3 proposed =
                position + travel * desiredSpeed * dt;

            // Do not move beyond an unsupported edge.
            if (TryFindConnectedSurface(
                proposed,
                travel,
                out RaycastHit nextHit))
            {
                Vector3 target =
                    nextHit.point +
                    nextHit.normal * surfaceOffset;

                position = Vector3.Lerp(
                    proposed,
                    target,
                    Mathf.Clamp01(surfaceSnapSpeed * dt)
                );

                surfaceNormal = Vector3.Slerp(
                    surfaceNormal,
                    nextHit.normal,
                    Mathf.Clamp01(surfaceNormalSpeed * dt)
                ).normalized;
            }

            Vector3 forward =
                Vector3.ProjectOnPlane(
                    travel,
                    surfaceNormal
                ).normalized;

            if (forward.sqrMagnitude > 0.001f)
            {
                RotateToward(
                    Quaternion.LookRotation(
                        forward,
                        surfaceNormal
                    ),
                    dt
                );
            }
        }
        else
        {
            Quaternion targetRotation =
                Quaternion.FromToRotation(
                    transform.up,
                    surfaceNormal
                ) * rb.rotation;

            RotateToward(targetRotation, dt);
        }

        rb.MovePosition(position);
    }

    #endregion


    #region Public_Movement_Methods

    /// <summary>
    /// Receives movement instructions from Spider or Beetle.
    /// </summary>
    public void SetMovement(
        Vector3 direction,
        float speed)
    {
        desiredDirection = direction;

        desiredSpeed = Mathf.Max(0f, speed);
    }


    /// <summary>
    /// Applies knockback from damage or attacks.
    /// </summary>
    public void ApplyKnockback(
        Vector3 direction,
        float strength,
        float duration)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        knockbackVelocity =
            direction.normalized *
            Mathf.Max(0f, strength);

        knockbackTimer =
            Mathf.Max(0f, duration);
    }


    /// <summary>
    /// Starts a spider jump toward a validated landing.
    /// Beetles cannot use this successfully.
    /// </summary>
    public bool TryBeginSpiderJump(
        Vector3 landingPoint,
        Vector3 landingNormal,
        float duration,
        float maxDistance)
    {
        if (!spiderMode ||
            jumping ||
            !hasSurface ||
            climbableLayers.value == 0 ||
            knockbackTimer > 0f ||
            duration <= 0.1f)
        {
            return false;
        }

        Vector3 normal = landingNormal.normalized;

        if (normal.sqrMagnitude < 0.5f)
        {
            return false;
        }

        Vector3 destination =
            landingPoint + normal * surfaceOffset;

        // Never jump farther than 15 Unity units.
        if (Vector3.Distance(
            rb.position,
            destination) > Mathf.Min(15f, maxDistance))
        {
            return false;
        }

        // Validate the landing surface.
        if (!TrySurfaceRay(
            destination + normal * surfaceProbeDistance,
            -normal,
            surfaceProbeDistance +
            maximumSurfaceGap +
            surfaceOffset,
            out RaycastHit landingHit))
        {
            return false;
        }

        if (Vector3.Distance(
            landingHit.point,
            landingPoint) > 1.25f)
        {
            return false;
        }

        float gravity =
            Mathf.Max(1f, lostSurfaceGravity);

        Vector3 velocity =
            (destination - rb.position
            - 0.5f * Vector3.down *
            gravity * duration * duration)
            / duration;

        if (!ArcIsClear(
            rb.position,
            velocity,
            gravity,
            duration,
            landingHit.collider))
        {
            return false;
        }

        jumpStart = rb.position;

        jumpTarget =
            landingHit.point +
            landingHit.normal * surfaceOffset;

        jumpLandingNormal = landingHit.normal;

        jumpVelocity =
            (jumpTarget - jumpStart
            - 0.5f * Vector3.down *
            gravity * duration * duration)
            / duration;

        jumpDuration = duration;
        jumpElapsed = 0f;

        jumping = true;
        hasSurface = false;
        fallSpeed = 0f;

        return true;
    }

    #endregion


    #region Navigation_Methods

    /// <summary>
    /// Chooses the enemy's movement direction.
    ///
    /// 1. Starts with Spider/Beetle movement.
    /// 2. Applies individual approach variation.
    /// 3. Detects stuck behavior.
    /// 4. Scores local movement directions.
    /// 5. Uses EnemyNavigationSensor if available.
    /// </summary>
    private Vector3 ChooseDirection(
        Vector3 position,
        float dt)
    {
        Vector3 toward = desiredDirection;

        // Preserve individual enemy approach variation.
        if (approachVariation != null)
        {
            toward =
                approachVariation.AdjustDirection(
                    toward,
                    position,
                    surfaceNormal
                );
        }

        toward =
            Vector3.ProjectOnPlane(
                toward,
                surfaceNormal
            ).normalized;

        if (toward.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        // Check progress toward the player.
        stuckTimer += dt;

        recoveryTimer =
            Mathf.Max(0f, recoveryTimer - dt);

        if (stuckTimer >=
            Mathf.Max(0.2f, stuckCheckInterval))
        {
            float distance =
                player != null
                ? Vector3.Distance(
                    position,
                    player.position)
                : 0f;

            bool notMoving =
                Vector3.Distance(
                    position,
                    lastPosition
                ) < minimumProgress;

            bool notApproaching =
                player != null &&
                distance >
                lastPlayerDistance - minimumProgress;

            if (desiredSpeed > 0.1f &&
                (notMoving || notApproaching))
            {
                recoveryTimer = recoveryDuration;

                recoverySide *= -1;
            }

            lastPosition = position;
            lastPlayerDistance = distance;

            stuckTimer = 0f;
        }

        Vector3 avoidance =
            GetAvoidance(position);

        Vector3 right =
            Vector3.Cross(
                surfaceNormal,
                toward
            ).normalized;

        Vector3 best = Vector3.zero;

        float bestScore =
            float.NegativeInfinity;

        // Evaluate nine possible directions.
        for (int i = 0; i < 9; i++)
        {
            float degrees = (i - 4) * 35f;

            Vector3 candidate =
                Quaternion.AngleAxis(
                    degrees,
                    surfaceNormal
                ) * toward;

            float score =
                Vector3.Dot(candidate, toward) * 2f
                + Vector3.Dot(candidate, avoidance) * 2f;

            // Check for an obstacle or climbable wall.
            if (TrySurfaceRay(
                position + surfaceNormal * 0.12f,
                candidate,
                obstacleProbeDistance,
                out RaycastHit obstacle))
            {
                score +=
                    Vector3.Dot(
                        obstacle.normal,
                        surfaceNormal
                    ) < 0.65f
                    ? 0.8f
                    : -3f;
            }

            // Penalize unsupported directions.
            if (!TryFindConnectedSurface(
                position +
                candidate * edgeTurnSearchDistance,
                candidate,
                out _))
            {
                score -= 8f;
            }

            // Favor an alternate direction when stuck.
            if (recoveryTimer > 0f)
            {
                score +=
                    Vector3.Dot(
                        candidate,
                        right * recoverySide
                    ) * 3f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        // NEW: Let the raycast sensor refine
        // the chosen movement direction.
        //
        // Each enemy's sensor has its own
        // steering preferences and route memory.
        if (navigationSensor != null && hasSurface)
        {
            best =
                navigationSensor.GetNavigationDirection(
                    best
                );
        }

        return best.normalized;
    }


    /// <summary>
    /// Calculates a direction away from nearby enemies.
    /// </summary>
    private Vector3 GetAvoidance(Vector3 position)
    {
        Vector3 result = Vector3.zero;

        int count =
            Physics.OverlapSphereNonAlloc(
                position,
                enemyAvoidanceRadius,
                neighbors,
                ~0,
                QueryTriggerInteraction.Ignore
            );

        for (int i = 0; i < count; i++)
        {
            Collider other = neighbors[i];

            if (other == null ||
                IsOwnCollider(other) ||
                other.GetComponentInParent<Enemy>() == null)
            {
                continue;
            }

            Vector3 away =
                position -
                other.ClosestPoint(position);

            if (away.sqrMagnitude < 0.0001f)
            {
                away =
                    position -
                    other.transform.position;
            }

            if (away.sqrMagnitude > 0.0001f)
            {
                result +=
                    away.normalized /
                    Mathf.Max(0.2f, away.magnitude);
            }
        }

        return Vector3.ProjectOnPlane(
            result,
            surfaceNormal
        ).normalized;
    }

    #endregion


    #region Surface_Detection_Methods

    /// <summary>
    /// Attempts to find ground beneath an airborne enemy.
    /// </summary>
    private bool TryAcquireSurface(
        Vector3 position,
        out RaycastHit hit)
    {
        return TrySurfaceRay(
            position + Vector3.up * 0.1f,
            Vector3.down,
            Mathf.Max(0.2f, maximumRecoveryDistance),
            out hit
        ) &&
        Vector3.Dot(
            hit.normal,
            Vector3.up
        ) > 0.35f;
    }


    /// <summary>
    /// Finds a nearby connected surface.
    /// Does not intentionally attach to distant geometry.
    /// </summary>
    private bool TryFindConnectedSurface(
        Vector3 position,
        Vector3 travel,
        out RaycastHit hit)
    {
        hit = default;

        if (climbableLayers.value == 0)
        {
            return false;
        }

        // Check for a wall directly ahead.
        if (travel.sqrMagnitude > 0.001f &&
            TrySurfaceRay(
                position + surfaceNormal * 0.1f,
                travel,
                wallDetectionDistance,
                out RaycastHit wall) &&
            Vector3.Dot(
                wall.normal,
                surfaceNormal
            ) < 0.65f &&
            wall.distance <= maximumSurfaceGap)
        {
            hit = wall;
            return true;
        }

        // Check the current surface.
        Vector3 origin =
            position +
            surfaceNormal * surfaceProbeDistance;

        if (TrySurfaceRay(
            origin,
            -surfaceNormal,
            surfaceProbeDistance +
            surfaceOffset +
            maximumSurfaceGap,
            out RaycastHit current) &&
            Vector3.Distance(
                position,
                current.point +
                current.normal * surfaceOffset
            ) <= maximumSurfaceGap + 0.15f)
        {
            hit = current;
            return true;
        }

        // Look for a connected wall around an edge.
        if (travel.sqrMagnitude > 0.001f)
        {
            Vector3 ahead =
                position +
                travel.normalized * edgeProbeDistance;

            Vector3 cornerOrigin =
                ahead -
                surfaceNormal * (surfaceOffset + 0.15f);

            if (TrySurfaceRay(
                cornerOrigin,
                -travel.normalized,
                edgeProbeDistance + maximumSurfaceGap,
                out RaycastHit edgeWall) &&
                Vector3.Distance(
                    position,
                    edgeWall.point +
                    edgeWall.normal * surfaceOffset
                ) <= edgeProbeDistance + maximumSurfaceGap &&
                Vector3.Dot(
                    edgeWall.normal,
                    surfaceNormal
                ) < 0.7f)
            {
                hit = edgeWall;
                return true;
            }
        }

        return false;
    }


    /// <summary>
    /// Casts a ray against climbable layers.
    /// Ignores the enemy's own colliders.
    /// </summary>
    private bool TrySurfaceRay(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit result)
    {
        result = default;

        if (climbableLayers.value == 0 ||
            distance <= 0f ||
            direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        RaycastHit[] hits =
            Physics.RaycastAll(
                origin,
                direction.normalized,
                distance,
                climbableLayers,
                QueryTriggerInteraction.Ignore
            );

        float closest = float.MaxValue;

        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            if (IsOwnCollider(hit.collider))
            {
                continue;
            }

            // Other enemies are not climbable terrain.
            if (hit.collider.GetComponentInParent<Enemy>() != null)
            {
                continue;
            }

            if (hit.distance < closest)
            {
                closest = hit.distance;
                result = hit;
                found = true;
            }
        }

        return found;
    }


    /// <summary>
    /// Checks whether a collider belongs to this enemy.
    /// </summary>
    private bool IsOwnCollider(Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        return
            candidate == bodyCollider ||
            candidate.transform == transform ||
            candidate.transform.IsChildOf(transform);
    }


    /// <summary>
    /// Attaches the enemy to a detected surface.
    /// </summary>
    private void AttachToSurface(RaycastHit hit)
    {
        surfaceNormal = hit.normal;

        fallSpeed = 0f;

        hasSurface = true;

        rb.MovePosition(
            hit.point +
            hit.normal * surfaceOffset
        );
    }

    #endregion


    #region Airborne_And_Jump_Methods

    /// <summary>
    /// Applies downward movement when no surface is found.
    /// </summary>
    private void Fall(float dt)
    {
        hasSurface = false;

        fallSpeed +=
            Mathf.Max(0f, lostSurfaceGravity) * dt;

        Vector3 start = rb.position;

        Vector3 next =
            start +
            Vector3.down * fallSpeed * dt;

        Vector3 delta = next - start;

        // Look for a landing surface along the fall.
        if (delta.sqrMagnitude > 0.000001f &&
            TrySurfaceRay(
                start,
                Vector3.down,
                delta.magnitude + surfaceOffset,
                out RaycastHit landing) &&
            Vector3.Dot(
                landing.normal,
                Vector3.up
            ) > 0.35f)
        {
            AttachToSurface(landing);
            return;
        }

        rb.MovePosition(next);
    }


    /// <summary>
    /// Tests the proposed spider jump trajectory
    /// for collisions with level geometry.
    /// </summary>
    private bool ArcIsClear(
        Vector3 start,
        Vector3 velocity,
        float gravity,
        float duration,
        Collider landingCollider)
    {
        Vector3 previous = start;

        for (int i = 1; i <= 24; i++)
        {
            float t = duration * i / 24f;

            Vector3 next =
                start +
                velocity * t +
                0.5f * Vector3.down *
                gravity * t * t;

            Vector3 delta = next - previous;

            if (delta.sqrMagnitude > 0.000001f)
            {
                RaycastHit[] hits =
                    Physics.SphereCastAll(
                        previous,
                        0.12f,
                        delta.normalized,
                        delta.magnitude,
                        climbableLayers,
                        QueryTriggerInteraction.Ignore
                    );

                foreach (RaycastHit hit in hits)
                {
                    if (IsOwnCollider(hit.collider))
                    {
                        continue;
                    }

                    // Permit the intended landing surface
                    // at the very end of the trajectory.
                    if (i >= 23 &&
                        hit.collider == landingCollider)
                    {
                        continue;
                    }

                    return false;
                }
            }

            previous = next;
        }

        return true;
    }


    /// <summary>
    /// Moves a spider through its jump trajectory.
    /// </summary>
    private void UpdateJump(float dt)
    {
        if (climbableLayers.value == 0)
        {
            jumping = false;

            Fall(dt);
            return;
        }

        jumpElapsed += dt;

        float t =
            Mathf.Min(jumpElapsed, jumpDuration);

        float gravity =
            Mathf.Max(1f, lostSurfaceGravity);

        Vector3 position =
            jumpStart +
            jumpVelocity * t +
            0.5f * Vector3.down *
            gravity * t * t;

        rb.MovePosition(position);

        if (jumpElapsed < jumpDuration)
        {
            return;
        }

        jumping = false;

        // Confirm the intended landing still exists.
        if (TrySurfaceRay(
            jumpTarget +
            jumpLandingNormal * surfaceProbeDistance,
            -jumpLandingNormal,
            surfaceProbeDistance +
            surfaceOffset +
            maximumSurfaceGap,
            out RaycastHit landing) &&
            Vector3.Distance(
                landing.point +
                landing.normal * surfaceOffset,
                jumpTarget
            ) < 1.25f)
        {
            AttachToSurface(landing);
        }
        else
        {
            hasSurface = false;

            fallSpeed = Mathf.Max(
                0f,
                -jumpVelocity.y +
                gravity * jumpDuration
            );
        }
    }


    /// <summary>
    /// Smoothly rotates the enemy toward a target rotation.
    /// </summary>
    private void RotateToward(
        Quaternion target,
        float dt)
    {
        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                target,
                Mathf.Clamp01(rotationSpeed * dt)
            )
        );
    }

    #endregion
}
