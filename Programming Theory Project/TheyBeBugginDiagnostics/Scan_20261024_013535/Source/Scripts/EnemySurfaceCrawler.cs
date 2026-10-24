
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemySurfaceCrawler : MonoBehaviour
{
    [Header("Surface Detection")]
    [SerializeField] private LayerMask climbableLayers = ~0;
    [SerializeField, Min(0.05f)] private float surfaceOffset = 0.7f;
    [SerializeField, Min(0.1f)] private float surfaceProbeDistance = 1.5f;
    [SerializeField, Min(0.1f)] private float wallDetectionDistance = 1.2f;
    [SerializeField, Min(0.1f)] private float edgeProbeDistance = 1.5f;
    [SerializeField, Min(0.05f)] private float maximumSurfaceGap = 0.85f;

    [Header("Movement")]
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float surfaceSnapSpeed = 12f;
    [SerializeField] private float surfaceNormalSpeed = 10f;

    [Header("Airborne Recovery")]
    [SerializeField] private float lostSurfaceGravity = 15f;
    [SerializeField] private float maximumRecoveryDistance = 3f;

    [Header("Navigation")]
    [SerializeField] private float obstacleProbeDistance = 1.4f;
    [SerializeField] private float enemyAvoidanceRadius = 1.3f;
    [SerializeField] private float stuckCheckInterval = 1f;
    [SerializeField] private float minimumProgress = 0.4f;
    [SerializeField] private float recoveryDuration = 2f;
    [SerializeField] private float edgeTurnSearchDistance = 1.2f;

    [Header("Airborne Physics")]
    [SerializeField, Min(0f)] private float airDrag = 0.4f;
    [SerializeField, Min(1f)] private float maximumFallSpeed = 30f;
    [SerializeField, Min(0f)] private float collisionSkin = 0.025f;
    [SerializeField, Min(0.01f)] private float maximumMovementStep = 0.12f;
    [SerializeField, Min(0f)] private float landingNormalThreshold = 0.35f;

    private Rigidbody rb;
    private Collider bodyCollider;
    private EnemyNavigationSensor navigationSensor;
    private EnemyApproachVariation approachVariation;
    private Transform player;

    private Vector3 surfaceNormal = Vector3.up;
    private Vector3 lastAttachedNormal = Vector3.up;
    private Vector3 desiredDirection;
    private Vector3 airborneVelocity;
    private Vector3 knockbackVelocity;
    private Vector3 lastPosition;

    private float desiredSpeed;
    private float knockbackTimer;
    private float stuckTimer;
    private float recoveryTimer;
    private float fallSpeed;

    private bool hasSurface;
    private bool jumping;
    private bool spiderMode;

    private Vector3 jumpStart;
    private Vector3 jumpVelocity;
    private Vector3 jumpTarget;
    private Vector3 jumpLandingNormal;
    private float jumpElapsed;
    private float jumpDuration;

    private readonly Collider[] overlapResults = new Collider[96];
    private readonly RaycastHit[] rayResults = new RaycastHit[64];

    public Vector3 SurfaceNormal => surfaceNormal;
    public bool HasSurface => hasSurface;
    public bool IsJumping => jumping;
    public bool IsStuck =>
        recoveryTimer > 0f ||
        (navigationSensor != null && navigationSensor.IsStuck);

    public LayerMask ClimbableLayers => climbableLayers;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        navigationSensor = GetComponent<EnemyNavigationSensor>();
        approachVariation = GetComponent<EnemyApproachVariation>();

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        spiderMode = GetComponent<Spider>() != null;

        surfaceNormal = transform.up.normalized;
        lastAttachedNormal = surfaceNormal;
        lastPosition = rb.position;
    }

    private void Start()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;

        if (TryAcquireSurface(rb.position, out RaycastHit hit))
            AttachToSurface(hit);
        else
            EnterAirborne(Vector3.zero);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (knockbackTimer > 0f)
        {
            knockbackTimer =
                Mathf.Max(0f, knockbackTimer - dt);

            knockbackVelocity = Vector3.MoveTowards(
                knockbackVelocity,
                Vector3.zero,
                dt * 12f
            );
        }

        if (recoveryTimer > 0f)
            recoveryTimer =
                Mathf.Max(0f, recoveryTimer - dt);

        if (jumping)
        {
            UpdateJump(dt);
            return;
        }

        if (!hasSurface)
        {
            UpdateAirborne(dt);
            return;
        }

        UpdateAttached(dt);
    }

    public void SetMovement(Vector3 direction, float speed)
    {
        desiredDirection =
            direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.zero;

        desiredSpeed = Mathf.Max(0f, speed);
    }

    public void ApplyKnockback(
        Vector3 direction,
        float strength,
        float duration)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        knockbackVelocity =
            direction.normalized * Mathf.Max(0f, strength);

        knockbackTimer = Mathf.Max(0f, duration);

        if (!hasSurface)
            airborneVelocity += knockbackVelocity;
    }

    private void UpdateAttached(float dt)
    {
        Vector3 position = rb.position;

        Vector3 movement =
            Vector3.ProjectOnPlane(
                desiredDirection,
                surfaceNormal
            );

        if (movement.sqrMagnitude > 0.0001f)
            movement.Normalize();

        if (approachVariation != null &&
            movement.sqrMagnitude > 0.0001f)
        {
            movement = approachVariation.AdjustDirection(
                movement,
                position,
                surfaceNormal
            );
        }

        if (navigationSensor != null &&
            navigationSensor.PreferredDirection.sqrMagnitude >
            0.0001f)
        {
            movement = Vector3.ProjectOnPlane(
                navigationSensor.PreferredDirection,
                surfaceNormal
            ).normalized;
        }

        Vector3 velocity =
            movement * desiredSpeed +
            Vector3.ProjectOnPlane(
                knockbackVelocity,
                surfaceNormal
            );

        Vector3 travel =
            velocity.sqrMagnitude > 0.0001f
                ? velocity.normalized
                : Vector3.zero;

        // Confirm that the current surface still exists.
        if (!FindConnectedSurface(
            position,
            travel,
            out RaycastHit currentSurface))
        {
            EnterAirborne(velocity);
            UpdateAirborne(dt);
            return;
        }

        Vector3 proposed = position + velocity * dt;

        // Do not move into unsupported space.
        if (FindConnectedSurface(
            proposed,
            travel,
            out RaycastHit nextSurface))
        {
            Vector3 targetNormal = nextSurface.normal;

            Quaternion targetRotation =
                BuildSurfaceRotation(
                    targetNormal,
                    travel
                );

            Vector3 targetPosition =
                SurfaceTarget(
                    nextSurface,
                    targetRotation
                );

            // Check rotation and position together.
            Quaternion nextRotation = Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                Mathf.Clamp01(rotationSpeed * dt)
            );

            Vector3 snapped = Vector3.Lerp(
                proposed,
                targetPosition,
                Mathf.Clamp01(surfaceSnapSpeed * dt)
            );

            if (TryMoveToPose(
                snapped,
                nextRotation,
                out Vector3 actualPosition))
            {
                surfaceNormal = Vector3.Slerp(
                    surfaceNormal,
                    targetNormal,
                    Mathf.Clamp01(surfaceNormalSpeed * dt)
                ).normalized;

                lastAttachedNormal = targetNormal;
                rb.MovePosition(actualPosition);
                rb.MoveRotation(nextRotation);
            }
            else
            {
                // Blocked by geometry: remain at a safe pose.
                RegisterBlockedMovement(dt);
            }
        }
        else
        {
            // Stay at the supported edge rather than
            // walking into empty space.
            if (FindConnectedSurface(
                position,
                Vector3.zero,
                out RaycastHit edgeSupport))
            {
                Vector3 target =
                    SurfaceTarget(edgeSupport, rb.rotation);

                MoveCollisionAware(target);
                RegisterBlockedMovement(dt);
            }
            else
            {
                EnterAirborne(velocity);
                UpdateAirborne(dt);
                return;
            }
        }

        UpdateProgress(dt);
    }

    private void EnterAirborne(Vector3 inheritedVelocity)
    {
        if (!hasSurface)
            return;

        hasSurface = false;
        fallSpeed = 0f;

        airborneVelocity = inheritedVelocity;

        lastAttachedNormal = surfaceNormal;
    }

    private void UpdateAirborne(float dt)
    {
        hasSurface = false;

        airborneVelocity +=
            Vector3.down *
            Mathf.Max(0f, lostSurfaceGravity) * dt;

        airborneVelocity = Vector3.ClampMagnitude(
            airborneVelocity,
            Mathf.Max(1f, maximumFallSpeed)
        );

        airborneVelocity *=
            1f / (1f + Mathf.Max(0f, airDrag) * dt);

        fallSpeed = Mathf.Max(0f, -airborneVelocity.y);

        Vector3 start = rb.position;
        Vector3 delta = airborneVelocity * dt;

        // Look for an actual landing along the path.
        if (delta.sqrMagnitude > 0.000001f &&
            TrySurfaceRay(
                start,
                delta.normalized,
                delta.magnitude +
                BodyClearance(-delta.normalized, rb.rotation) +
                collisionSkin,
                out RaycastHit landing))
        {
            if (IsValidLanding(landing, airborneVelocity))
            {
                Quaternion landingRotation =
                    BuildSurfaceRotation(
                        landing.normal,
                        Vector3.ProjectOnPlane(
                            airborneVelocity,
                            landing.normal
                        )
                    );

                Vector3 landingPosition =
                    SurfaceTarget(
                        landing,
                        landingRotation
                    );

                if (CanOccupyPose(
                    landingPosition,
                    landingRotation))
                {
                    rb.MovePosition(landingPosition);
                    rb.MoveRotation(landingRotation);
                    AttachState(landing.normal);
                    return;
                }
            }
        }

        Vector3 desiredPosition = start + delta;

        // Kinematic physics: move only as far as
        // collision geometry permits.
        Vector3 actualPosition =
            MoveCollisionAware(desiredPosition);

        if (delta.sqrMagnitude > 0.000001f &&
            Vector3.Distance(
                actualPosition,
                desiredPosition
            ) > 0.05f)
        {
            Vector3 blockedDirection =
                desiredPosition - actualPosition;

            if (blockedDirection.sqrMagnitude > 0.0001f)
            {
                airborneVelocity = Vector3.ProjectOnPlane(
                    airborneVelocity,
                    blockedDirection.normalized
                );
            }
        }

        // Search nearby surfaces only after movement.
        if (TryAcquireSurface(
            actualPosition,
            out RaycastHit nearby))
        {
            Quaternion landingRotation =
                BuildSurfaceRotation(
                    nearby.normal,
                    airborneVelocity
                );

            Vector3 landingPosition =
                SurfaceTarget(
                    nearby,
                    landingRotation
                );

            if (Vector3.Distance(
                    actualPosition,
                    landingPosition
                ) <= Mathf.Max(0.15f, maximumSurfaceGap) &&
                CanOccupyPose(
                    landingPosition,
                    landingRotation))
            {
                rb.MovePosition(landingPosition);
                rb.MoveRotation(landingRotation);
                AttachState(nearby.normal);
                return;
            }
        }

        // Keep a stable orientation while falling.
        // Never rotate toward an unconfirmed wall.
        Quaternion upright =
            Quaternion.FromToRotation(
                rb.rotation * Vector3.up,
                Vector3.up
            ) * rb.rotation;

        Quaternion nextRotation = Quaternion.Slerp(
            rb.rotation,
            upright,
            Mathf.Clamp01(rotationSpeed * 0.35f * dt)
        );

        if (CanOccupyPose(actualPosition, nextRotation))
            rb.MoveRotation(nextRotation);
    }

    private bool IsValidLanding(
        RaycastHit hit,
        Vector3 velocity)
    {
        if (!IsClimbable(hit.collider))
            return false;

        if (hit.normal.sqrMagnitude < 0.001f)
            return false;

        // Must be moving into the surface.
        if (Vector3.Dot(
            velocity.normalized,
            hit.normal
        ) >= -0.05f)
            return false;

        // Floor landing is preferred when falling.
        if (velocity.y < -0.1f &&
            hit.normal.y >= landingNormalThreshold)
            return true;

        // Walls and ceilings can also be reacquired
        // when genuinely contacted.
        return true;
    }

    private void AttachToSurface(RaycastHit hit)
    {
        Quaternion rotation =
            BuildSurfaceRotation(
                hit.normal,
                desiredDirection
            );

        Vector3 target =
            SurfaceTarget(hit, rotation);

        if (!CanOccupyPose(target, rotation))
            return;

        rb.MovePosition(target);
        rb.MoveRotation(rotation);
        AttachState(hit.normal);
    }

    private void AttachState(Vector3 normal)
    {
        hasSurface = true;
        jumping = false;

        surfaceNormal = normal.normalized;
        lastAttachedNormal = surfaceNormal;

        airborneVelocity = Vector3.zero;
        fallSpeed = 0f;

        stuckTimer = 0f;
        lastPosition = rb.position;
    }

    private bool FindConnectedSurface(
        Vector3 position,
        Vector3 travel,
        out RaycastHit hit)
    {
        hit = default;

        Vector3 normal =
            surfaceNormal.sqrMagnitude > 0.001f
                ? surfaceNormal.normalized
                : Vector3.up;

        float distance = Mathf.Max(
            surfaceProbeDistance,
            BodyClearance(normal, rb.rotation) +
            maximumSurfaceGap + 0.2f
        );

        if (TrySurfaceRay(
            position + normal * 0.15f,
            -normal,
            distance,
            out RaycastHit support))
        {
            Vector3 target =
                SurfaceTarget(support, rb.rotation);

            if (Vector3.Distance(
                position,
                target
            ) <= maximumSurfaceGap + 0.35f)
            {
                hit = support;

                // A wall transition is allowed only
                // when the wall is close and reachable.
                if (travel.sqrMagnitude > 0.001f &&
                    TrySurfaceRay(
                        position,
                        travel.normalized,
                        Mathf.Max(
                            wallDetectionDistance,
                            edgeTurnSearchDistance
                        ),
                        out RaycastHit wall))
                {
                    if (Vector3.Dot(
                        wall.normal,
                        normal
                    ) < 0.5f)
                    {
                        Quaternion wallRotation =
                            BuildSurfaceRotation(
                                wall.normal,
                                travel
                            );

                        Vector3 wallTarget =
                            SurfaceTarget(
                                wall,
                                wallRotation
                            );

                        if (Vector3.Distance(
                            position,
                            wallTarget
                        ) <= maximumSurfaceGap +
                            BodyClearance(normal, rb.rotation) &&
                            CanOccupyPose(
                                wallTarget,
                                wallRotation))
                        {
                            hit = wall;
                        }
                    }
                }

                return true;
            }
        }

        if (travel.sqrMagnitude > 0.001f &&
            TrySurfaceRay(
                position,
                travel.normalized,
                Mathf.Max(
                    wallDetectionDistance,
                    edgeProbeDistance
                ),
                out RaycastHit corner))
        {
            Quaternion cornerRotation =
                BuildSurfaceRotation(
                    corner.normal,
                    travel
                );

            Vector3 cornerTarget =
                SurfaceTarget(
                    corner,
                    cornerRotation
                );

            if (Vector3.Distance(
                position,
                cornerTarget
            ) <= maximumSurfaceGap + 0.35f &&
                CanOccupyPose(
                    cornerTarget,
                    cornerRotation))
            {
                hit = corner;
                return true;
            }
        }

        return false;
    }

    private bool TryAcquireSurface(
        Vector3 position,
        out RaycastHit hit)
    {
        hit = default;

        Vector3[] directions =
        {
            Vector3.down,
            -lastAttachedNormal,
            Vector3.right,
            Vector3.left,
            Vector3.forward,
            Vector3.back,
            Vector3.up
        };

        float bestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 direction = directions[i];

            if (!TrySurfaceRay(
                position,
                direction,
                Mathf.Max(
                    surfaceProbeDistance,
                    maximumRecoveryDistance
                ),
                out RaycastHit candidate))
                continue;

            Quaternion rotation =
                BuildSurfaceRotation(
                    candidate.normal,
                    desiredDirection
                );

            Vector3 target =
                SurfaceTarget(
                    candidate,
                    rotation
                );

            float distance =
                Vector3.Distance(position, target);

            if (distance >
                Mathf.Max(0.2f, maximumSurfaceGap))
                continue;

            if (!CanOccupyPose(target, rotation))
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                hit = candidate;
                found = true;
            }
        }

        return found;
    }

    private bool TrySurfaceRay(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit bestHit)
    {
        bestHit = default;

        if (climbableLayers.value == 0 ||
            direction.sqrMagnitude < 0.0001f)
            return false;

        int count = Physics.RaycastNonAlloc(
            origin,
            direction.normalized,
            rayResults,
            Mathf.Max(0.01f, distance),
            climbableLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearest = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = rayResults[i];

            if (!IsClimbable(hit.collider))
                continue;

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        return found;
    }

    private bool IsClimbable(Collider candidate)
    {
        if (candidate == null ||
            candidate.isTrigger ||
            candidate.transform == transform ||
            candidate.transform.IsChildOf(transform))
            return false;

        return
            (climbableLayers.value &
            (1 << candidate.gameObject.layer)) != 0;
    }

    private Quaternion BuildSurfaceRotation(
        Vector3 normal,
        Vector3 direction)
    {
        Vector3 up = normal.normalized;

        Vector3 forward =
            Vector3.ProjectOnPlane(direction, up);

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.ProjectOnPlane(
                rb.rotation * Vector3.forward,
                up
            );
        }

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.Cross(up, Vector3.right);

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.Cross(up, Vector3.forward);

        return Quaternion.LookRotation(
            forward.normalized,
            up
        );
    }

    private float BodyClearance(
        Vector3 normal,
        Quaternion rotation)
    {
        Vector3 n = normal.normalized;

        // Conservative projected body bounds.
        Bounds bounds = bodyCollider.bounds;

        float extent =
            Mathf.Abs(n.x) * bounds.extents.x +
            Mathf.Abs(n.y) * bounds.extents.y +
            Mathf.Abs(n.z) * bounds.extents.z;

        return Mathf.Max(
            surfaceOffset,
            extent + collisionSkin
        );
    }

    private Vector3 SurfaceTarget(
        RaycastHit hit,
        Quaternion rotation)
    {
        return hit.point +
            hit.normal *
            BodyClearance(hit.normal, rotation);
    }

    private bool CanOccupyPose(
        Vector3 position,
        Quaternion rotation)
    {
        Vector3 center = ColliderWorldCenter(
            position,
            rotation
        );

        float radius = Mathf.Max(
            0.05f,
            bodyCollider.bounds.extents.magnitude
        );

        int count = Physics.OverlapSphereNonAlloc(
            center,
            radius + collisionSkin,
            overlapResults,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            Collider other = overlapResults[i];

            if (other == null ||
                other == bodyCollider ||
                other.isTrigger ||
                other.transform == transform ||
                other.transform.IsChildOf(transform))
                continue;

            if (Physics.GetIgnoreLayerCollision(
                gameObject.layer,
                other.gameObject.layer))
                continue;

            if (Physics.ComputePenetration(
                bodyCollider,
                ColliderPosePosition(
                    position,
                    rotation
                ),
                ColliderPoseRotation(rotation),
                other,
                other.transform.position,
                other.transform.rotation,
                out Vector3 separation,
                out float penetration))
            {
                if (penetration > collisionSkin)
                    return false;
            }
        }

        return true;
    }

    private Vector3 ColliderWorldCenter(
        Vector3 position,
        Quaternion rotation)
    {
        Vector3 offset =
            Quaternion.Inverse(rb.rotation) *
            (bodyCollider.bounds.center - rb.position);

        return position + rotation * offset;
    }

    private Vector3 ColliderPosePosition(
        Vector3 position,
        Quaternion rotation)
    {
        Vector3 offset =
            Quaternion.Inverse(rb.rotation) *
            (bodyCollider.transform.position - rb.position);

        return position + rotation * offset;
    }

    private Quaternion ColliderPoseRotation(
        Quaternion rotation)
    {
        Quaternion relative =
            Quaternion.Inverse(rb.rotation) *
            bodyCollider.transform.rotation;

        return rotation * relative;
    }

    private bool TryMoveToPose(
        Vector3 targetPosition,
        Quaternion targetRotation,
        out Vector3 result)
    {
        Vector3 start = rb.position;
        Quaternion startRotation = rb.rotation;

        float distance =
            Vector3.Distance(start, targetPosition);

        float angle =
            Quaternion.Angle(startRotation, targetRotation);

        int steps = Mathf.Clamp(
            Mathf.Max(
                Mathf.CeilToInt(
                    distance /
                    Mathf.Max(0.01f, maximumMovementStep)
                ),
                Mathf.CeilToInt(angle / 8f)
            ),
            1,
            128
        );

        result = start;

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;

            Vector3 candidate = Vector3.Lerp(
                start,
                targetPosition,
                t
            );

            Quaternion candidateRotation =
                Quaternion.Slerp(
                    startRotation,
                    targetRotation,
                    t
                );

            if (!CanOccupyPose(
                candidate,
                candidateRotation))
                return false;

            result = candidate;
        }

        return true;
    }

    private Vector3 MoveCollisionAware(
        Vector3 destination)
    {
        Vector3 start = rb.position;
        Vector3 delta = destination - start;

        if (delta.sqrMagnitude < 0.0000001f)
            return start;

        int steps = Mathf.Clamp(
            Mathf.CeilToInt(
                delta.magnitude /
                Mathf.Max(0.01f, maximumMovementStep)
            ),
            1,
            256
        );

        Vector3 safe = start;

        for (int i = 1; i <= steps; i++)
        {
            Vector3 candidate =
                Vector3.Lerp(
                    start,
                    destination,
                    (float)i / steps
                );

            if (!CanOccupyPose(
                candidate,
                rb.rotation))
                break;

            safe = candidate;
        }

        rb.MovePosition(safe);
        return safe;
    }

    private void RegisterBlockedMovement(float dt)
    {
        stuckTimer += dt;

        if (stuckTimer >=
            Mathf.Max(0.1f, stuckCheckInterval))
        {
            recoveryTimer =
                Mathf.Max(0.1f, recoveryDuration);

            stuckTimer = 0f;
        }
    }

    private void UpdateProgress(float dt)
    {
        stuckTimer += dt;

        if (stuckTimer <
            Mathf.Max(0.1f, stuckCheckInterval))
            return;

        float distance =
            Vector3.Distance(
                rb.position,
                lastPosition
            );

        if (desiredSpeed > 0.1f &&
            distance < minimumProgress)
        {
            recoveryTimer =
                Mathf.Max(0.1f, recoveryDuration);
        }

        lastPosition = rb.position;
        stuckTimer = 0f;
    }

    public bool TryBeginSpiderJump(
        Vector3 landingPoint,
        Vector3 landingNormal,
        float duration,
        float maxDistance)
    {
        if (!spiderMode ||
            !hasSurface ||
            jumping ||
            duration <= 0f)
            return false;

        Vector3 start = rb.position;
        float distance =
            Vector3.Distance(start, landingPoint);

        if (distance >
            Mathf.Min(15f, maxDistance))
            return false;

        if (landingNormal.sqrMagnitude < 0.001f)
            return false;

        float gravity =
            Mathf.Max(1f, lostSurfaceGravity);

        Vector3 velocity =
            (landingPoint - start -
             0.5f * Vector3.down *
             gravity * duration * duration) /
            duration;

        if (!ArcIsClear(
            start,
            velocity,
            gravity,
            duration))
            return false;

        jumpStart = start;
        jumpVelocity = velocity;
        jumpTarget = landingPoint;
        jumpLandingNormal = landingNormal.normalized;

        jumpDuration = duration;
        jumpElapsed = 0f;

        jumping = true;
        hasSurface = false;

        airborneVelocity = velocity;

        return true;
    }

    private bool ArcIsClear(
        Vector3 start,
        Vector3 velocity,
        float gravity,
        float duration)
    {
        float length =
            velocity.magnitude * duration +
            0.5f * gravity * duration * duration;

        int steps = Mathf.Clamp(
            Mathf.CeilToInt(
                length /
                Mathf.Max(0.05f, maximumMovementStep)
            ),
            16,
            256
        );

        for (int i = 1; i < steps; i++)
        {
            float t =
                duration * (float)i / steps;

            Vector3 position =
                start + velocity * t +
                0.5f * Vector3.down *
                gravity * t * t;

            if (!CanOccupyPose(
                position,
                rb.rotation))
                return false;
        }

        return true;
    }

    private void UpdateJump(float dt)
    {
        jumpElapsed += dt;

        float t =
            Mathf.Min(jumpElapsed, jumpDuration);

        float gravity =
            Mathf.Max(1f, lostSurfaceGravity);

        Vector3 desiredPosition =
            jumpStart + jumpVelocity * t +
            0.5f * Vector3.down *
            gravity * t * t;

        Vector3 actualPosition =
            MoveCollisionAware(desiredPosition);

        airborneVelocity =
            jumpVelocity +
            Vector3.down * gravity * t;

        if (Vector3.Distance(
            actualPosition,
            desiredPosition
        ) > 0.05f)
        {
            jumping = false;
            hasSurface = false;
            return;
        }

        if (jumpElapsed < jumpDuration)
            return;

        jumping = false;

        if (TrySurfaceRay(
            jumpTarget +
            jumpLandingNormal * surfaceProbeDistance,
            -jumpLandingNormal,
            surfaceProbeDistance +
            maximumSurfaceGap +
            BodyClearance(
                jumpLandingNormal,
                rb.rotation
            ),
            out RaycastHit landing))
        {
            Quaternion landingRotation =
                BuildSurfaceRotation(
                    landing.normal,
                    desiredDirection
                );

            Vector3 landingPosition =
                SurfaceTarget(
                    landing,
                    landingRotation
                );

            if (Vector3.Distance(
                actualPosition,
                landingPosition
            ) <= maximumSurfaceGap + 0.5f &&
                CanOccupyPose(
                    landingPosition,
                    landingRotation))
            {
                rb.MovePosition(landingPosition);
                rb.MoveRotation(landingRotation);
                AttachState(landing.normal);
                return;
            }
        }

        // No valid landing: keep falling.
        hasSurface = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = hasSurface
            ? Color.green
            : Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            0.12f
        );

        Gizmos.color = Color.cyan;

        Gizmos.DrawRay(
            transform.position,
            surfaceNormal * 0.8f
        );

        if (!hasSurface)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawRay(
                transform.position,
                airborneVelocity * 0.2f
            );
        }
    }
}
