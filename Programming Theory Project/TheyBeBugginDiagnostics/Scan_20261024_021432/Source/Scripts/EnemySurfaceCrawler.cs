
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

    [Header("Collision and Falling")]
    [SerializeField, Min(0f)] private float airDrag = 0.4f;
    [SerializeField, Min(1f)] private float maximumFallSpeed = 30f;
    [SerializeField, Min(0.001f)] private float collisionSkin = 0.025f;
    [SerializeField, Min(0.01f)] private float maximumMovementStep = 0.12f;
    [SerializeField, Min(0f)] private float landingNormalThreshold = 0.35f;

    private Rigidbody rb;
    private Collider bodyCollider;
    private EnemyNavigationSensor navigationSensor;
    private EnemyApproachVariation approachVariation;

    private Vector3 surfaceNormal = Vector3.up;
    private Vector3 desiredDirection;
    private Vector3 airVelocity;
    private Vector3 knockbackVelocity;
    private Vector3 progressPosition;

    private float desiredSpeed;
    private float knockbackTimer;
    private float progressTimer;
    private float recoveryTimer;

    private bool hasSurface;
    private bool jumping;
    private bool spiderMode;

    private Vector3 jumpStart;
    private Vector3 jumpVelocity;
    private Vector3 jumpTarget;
    private Vector3 jumpNormal;
    private float jumpTime;
    private float jumpDuration;

    private readonly RaycastHit[] hits = new RaycastHit[64];
    private readonly Collider[] overlaps = new Collider[96];

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

        spiderMode = GetComponent<Spider>() != null;

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        surfaceNormal = transform.up.normalized;
        progressPosition = rb.position;
    }

    private void Start()
    {
        if (FindNearbySurface(rb.position, out RaycastHit hit))
            Attach(hit);
        else
        {
            hasSurface = false;
            airVelocity = Vector3.zero;
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= dt;
            knockbackVelocity = Vector3.MoveTowards(
                knockbackVelocity,
                Vector3.zero,
                12f * dt
            );
        }
        else
        {
            knockbackVelocity = Vector3.zero;
        }

        recoveryTimer = Mathf.Max(0f, recoveryTimer - dt);

        if (jumping)
            UpdateJump(dt);
        else if (hasSurface)
            UpdateAttached(dt);
        else
            UpdateAirborne(dt);
    }

    public void SetMovement(Vector3 direction, float speed)
    {
        desiredDirection = direction.sqrMagnitude > 0.0001f
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

        if (!hasSurface && !jumping)
            airVelocity += knockbackVelocity;
    }

    private void UpdateAttached(float dt)
    {
        Vector3 start = rb.position;

        Vector3 direction = Vector3.ProjectOnPlane(
            desiredDirection,
            surfaceNormal
        );

        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();

        if (approachVariation != null &&
            direction.sqrMagnitude > 0.0001f)
        {
            Vector3 adjusted = approachVariation.AdjustDirection(
                direction,
                start,
                surfaceNormal
            );

            if (adjusted.sqrMagnitude > 0.0001f)
                direction = adjusted.normalized;
        }

        if (navigationSensor != null &&
            navigationSensor.PreferredDirection.sqrMagnitude >
            0.0001f)
        {
            Vector3 preferred = Vector3.ProjectOnPlane(
                navigationSensor.PreferredDirection,
                surfaceNormal
            );

            // Never overwrite a valid movement direction
            // with a direction that projects to zero.
            if (preferred.sqrMagnitude > 0.0001f)
                direction = preferred.normalized;
        }

        Vector3 velocity =
            direction * desiredSpeed +
            Vector3.ProjectOnPlane(
                knockbackVelocity,
                surfaceNormal
            );

        Vector3 travel = velocity.sqrMagnitude > 0.0001f
            ? velocity.normalized
            : Vector3.zero;

        if (!FindSupport(start, travel, out RaycastHit support))
        {
            EnterAirborne(velocity);
            UpdateAirborne(dt);
            return;
        }

        Vector3 proposed = start + velocity * dt;

        if (!FindSupport(proposed, travel, out RaycastHit next))
        {
            // Keep contact with the current surface.
            // Do not rotate toward unsupported empty space.
            Vector3 safeTarget = SurfacePosition(
                support,
                rb.rotation
            );

            MoveSafely(safeTarget, rb.rotation);
            CheckProgress(dt);
            return;
        }

        Quaternion targetRotation = SurfaceRotation(
            next.normal,
            travel
        );

        Quaternion smoothRotation = Quaternion.Slerp(
            rb.rotation,
            targetRotation,
            Mathf.Clamp01(rotationSpeed * dt)
        );

        Vector3 surfaceTarget = SurfacePosition(
            next,
            smoothRotation
        );

        Vector3 targetPosition = Vector3.Lerp(
            proposed,
            surfaceTarget,
            Mathf.Clamp01(surfaceSnapSpeed * dt)
        );

        Vector3 safePosition = MoveSafely(
            targetPosition,
            rb.rotation
        );

        // Translation must not fail just because
        // the new rotation is obstructed.
        if (CanOccupy(safePosition, smoothRotation))
            rb.MoveRotation(smoothRotation);

        surfaceNormal = Vector3.Slerp(
            surfaceNormal,
            next.normal,
            Mathf.Clamp01(surfaceNormalSpeed * dt)
        ).normalized;

        CheckProgress(dt);
    }

    private void EnterAirborne(Vector3 inheritedVelocity)
    {
        hasSurface = false;
        airVelocity = inheritedVelocity;
    }

    private void UpdateAirborne(float dt)
    {
        airVelocity += Vector3.down *
            Mathf.Max(0f, lostSurfaceGravity) * dt;

        airVelocity *= 1f /
            (1f + Mathf.Max(0f, airDrag) * dt);

        if (airVelocity.magnitude > maximumFallSpeed)
            airVelocity = airVelocity.normalized *
                maximumFallSpeed;

        Vector3 start = rb.position;
        Vector3 displacement = airVelocity * dt;

        Vector3 actual = MoveSafely(
            start + displacement,
            rb.rotation
        );

        // Remove velocity pointing into a blocked surface.
        if ((actual - (start + displacement)).sqrMagnitude >
            0.0004f)
        {
            Vector3 blocked = start + displacement - actual;

            if (blocked.sqrMagnitude > 0.0001f)
            {
                airVelocity = Vector3.ProjectOnPlane(
                    airVelocity,
                    blocked.normalized
                );
            }
        }

        if (FindNearbySurface(actual, out RaycastHit landing))
        {
            Quaternion rotation = SurfaceRotation(
                landing.normal,
                airVelocity
            );

            Vector3 target = SurfacePosition(
                landing,
                rotation
            );

            float allowedDistance = Mathf.Max(
                maximumSurfaceGap,
                collisionSkin + 0.1f
            );

            if (Vector3.Distance(actual, target) <=
                allowedDistance &&
                CanOccupy(target, rotation))
            {
                rb.MovePosition(target);
                rb.MoveRotation(rotation);

                surfaceNormal = landing.normal;
                hasSurface = true;
                airVelocity = Vector3.zero;
                recoveryTimer = 0f;
                return;
            }
        }

        // While falling, rotate toward world-up only
        // if the rotation is physically unobstructed.
        Quaternion upright = Quaternion.FromToRotation(
            rb.rotation * Vector3.up,
            Vector3.up
        ) * rb.rotation;

        Quaternion nextRotation = Quaternion.Slerp(
            rb.rotation,
            upright,
            Mathf.Clamp01(rotationSpeed * 0.3f * dt)
        );

        if (CanOccupy(actual, nextRotation))
            rb.MoveRotation(nextRotation);
    }

    private bool FindSupport(
        Vector3 position,
        Vector3 travel,
        out RaycastHit result)
    {
        result = default;

        Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f
            ? surfaceNormal.normalized
            : Vector3.up;

        float probe = Mathf.Max(
            surfaceProbeDistance,
            Clearance(normal) + maximumSurfaceGap + 0.2f
        );

        // Prefer the surface already supporting the enemy.
        if (RaySurface(
            position + normal * 0.1f,
            -normal,
            probe,
            out RaycastHit support))
        {
            Vector3 target = SurfacePosition(
                support,
                rb.rotation
            );

            if (Vector3.Distance(position, target) <=
                maximumSurfaceGap + 0.4f)
            {
                result = support;

                // A wall transition is only allowed when
                // its destination can hold the whole collider.
                if (travel.sqrMagnitude > 0.001f &&
                    RaySurface(
                        position,
                        travel,
                        Mathf.Max(
                            wallDetectionDistance,
                            edgeTurnSearchDistance
                        ),
                        out RaycastHit wall))
                {
                    Quaternion rotation = SurfaceRotation(
                        wall.normal,
                        travel
                    );

                    Vector3 wallTarget = SurfacePosition(
                        wall,
                        rotation
                    );

                    if (Vector3.Dot(
                        wall.normal,
                        normal
                    ) < 0.6f &&
                        Vector3.Distance(
                            position,
                            wallTarget
                        ) <= maximumSurfaceGap +
                            Clearance(normal) &&
                        CanOccupy(wallTarget, rotation))
                    {
                        result = wall;
                    }
                }

                return true;
            }
        }

        if (travel.sqrMagnitude > 0.001f &&
            RaySurface(
                position,
                travel,
                Mathf.Max(
                    wallDetectionDistance,
                    edgeProbeDistance
                ),
                out RaycastHit corner))
        {
            Quaternion rotation = SurfaceRotation(
                corner.normal,
                travel
            );

            Vector3 target = SurfacePosition(
                corner,
                rotation
            );

            if (Vector3.Distance(position, target) <=
                maximumSurfaceGap + 0.4f &&
                CanOccupy(target, rotation))
            {
                result = corner;
                return true;
            }
        }

        return false;
    }

    private bool FindNearbySurface(
        Vector3 position,
        out RaycastHit result)
    {
        result = default;

        Vector3[] directions =
        {
            Vector3.down,
            -surfaceNormal,
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left,
            Vector3.up
        };

        float best = float.PositiveInfinity;
        bool found = false;

        float distance = Mathf.Max(
            surfaceProbeDistance,
            maximumRecoveryDistance
        );

        for (int i = 0; i < directions.Length; i++)
        {
            if (!RaySurface(
                position,
                directions[i],
                distance,
                out RaycastHit hit))
                continue;

            Quaternion rotation = SurfaceRotation(
                hit.normal,
                desiredDirection
            );

            Vector3 target = SurfacePosition(
                hit,
                rotation
            );

            float separation = Vector3.Distance(
                position,
                target
            );

            if (separation >
                maximumSurfaceGap + 0.2f)
                continue;

            if (!CanOccupy(target, rotation))
                continue;

            if (separation < best)
            {
                best = separation;
                result = hit;
                found = true;
            }
        }

        return found;
    }

    private bool RaySurface(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit result)
    {
        result = default;

        if (direction.sqrMagnitude < 0.0001f ||
            climbableLayers.value == 0)
            return false;

        int count = Physics.RaycastNonAlloc(
            origin,
            direction.normalized,
            hits,
            distance,
            climbableLayers,
            QueryTriggerInteraction.Ignore
        );

        float closest = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider candidate = hits[i].collider;

            if (candidate == null ||
                candidate.isTrigger ||
                candidate.transform == transform ||
                candidate.transform.IsChildOf(transform))
                continue;

            if (hits[i].distance < closest)
            {
                closest = hits[i].distance;
                result = hits[i];
                found = true;
            }
        }

        return found;
    }

    private Quaternion SurfaceRotation(
        Vector3 normal,
        Vector3 movement)
    {
        Vector3 up = normal.normalized;

        Vector3 forward = Vector3.ProjectOnPlane(
            movement,
            up
        );

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

    private float Clearance(Vector3 normal)
    {
        // Project the current collider bounds onto the
        // surface normal. Retain surfaceOffset as a minimum.
        Bounds bounds = bodyCollider.bounds;
        Vector3 n = normal.normalized;

        float projected =
            Mathf.Abs(n.x) * bounds.extents.x +
            Mathf.Abs(n.y) * bounds.extents.y +
            Mathf.Abs(n.z) * bounds.extents.z;

        return Mathf.Max(
            surfaceOffset,
            projected + collisionSkin
        );
    }

    private Vector3 SurfacePosition(
        RaycastHit hit,
        Quaternion rotation)
    {
        return hit.point +
            hit.normal * Clearance(hit.normal);
    }

    private bool CanOccupy(
        Vector3 position,
        Quaternion rotation)
    {
        Vector3 colliderPosition = position +
            rotation *
            (Quaternion.Inverse(rb.rotation) *
             (bodyCollider.transform.position - rb.position));

        Quaternion colliderRotation = rotation *
            (Quaternion.Inverse(rb.rotation) *
             bodyCollider.transform.rotation);

        Vector3 centerOffset =
            Quaternion.Inverse(rb.rotation) *
            (bodyCollider.bounds.center - rb.position);

        Vector3 center = position +
            rotation * centerOffset;

        float radius = Mathf.Max(
            0.05f,
            bodyCollider.bounds.extents.magnitude
        );

        int count = Physics.OverlapSphereNonAlloc(
            center,
            radius + collisionSkin,
            overlaps,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            Collider other = overlaps[i];

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
                colliderPosition,
                colliderRotation,
                other,
                other.transform.position,
                other.transform.rotation,
                out Vector3 separation,
                out float depth))
            {
                if (depth > Mathf.Max(
                    collisionSkin,
                    0.045f
                ))
                    return false;
            }
        }

        return true;
    }

    private Vector3 MoveSafely(
        Vector3 destination,
        Quaternion rotation)
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
            Vector3 candidate = Vector3.Lerp(
                start,
                destination,
                (float)i / steps
            );

            if (!CanOccupy(candidate, rotation))
                break;

            safe = candidate;
        }

        rb.MovePosition(safe);
        return safe;
    }

    private void Attach(RaycastHit hit)
    {
        Quaternion rotation = SurfaceRotation(
            hit.normal,
            desiredDirection
        );

        Vector3 position = SurfacePosition(
            hit,
            rotation
        );

        if (!CanOccupy(position, rotation))
            return;

        rb.MovePosition(position);
        rb.MoveRotation(rotation);

        surfaceNormal = hit.normal.normalized;
        hasSurface = true;
        jumping = false;
        airVelocity = Vector3.zero;
        recoveryTimer = 0f;
    }

    private void CheckProgress(float dt)
    {
        progressTimer += dt;

        if (progressTimer < Mathf.Max(
            0.1f,
            stuckCheckInterval
        ))
            return;

        float moved = Vector3.Distance(
            rb.position,
            progressPosition
        );

        if (desiredSpeed > 0.1f &&
            moved < minimumProgress)
        {
            recoveryTimer = Mathf.Max(
                0.1f,
                recoveryDuration
            );
        }

        progressPosition = rb.position;
        progressTimer = 0f;
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
            duration <= 0f ||
            landingNormal.sqrMagnitude < 0.001f)
            return false;

        float distance = Vector3.Distance(
            rb.position,
            landingPoint
        );

        if (distance > Mathf.Min(
            15f,
            maxDistance
        ))
            return false;

        float gravity = Mathf.Max(
            1f,
            lostSurfaceGravity
        );

        Vector3 start = rb.position;

        Vector3 velocity =
            (landingPoint - start -
             0.5f * Vector3.down *
             gravity * duration * duration) /
            duration;

        if (!JumpArcClear(
            start,
            velocity,
            gravity,
            duration
        ))
            return false;

        jumpStart = start;
        jumpVelocity = velocity;
        jumpTarget = landingPoint;
        jumpNormal = landingNormal.normalized;
        jumpDuration = duration;
        jumpTime = 0f;

        jumping = true;
        hasSurface = false;
        airVelocity = velocity;

        return true;
    }

    private bool JumpArcClear(
        Vector3 start,
        Vector3 velocity,
        float gravity,
        float duration)
    {
        float approximateDistance =
            velocity.magnitude * duration +
            0.5f * gravity * duration * duration;

        int steps = Mathf.Clamp(
            Mathf.CeilToInt(
                approximateDistance /
                Mathf.Max(0.05f, maximumMovementStep)
            ),
            16,
            256
        );

        for (int i = 1; i < steps; i++)
        {
            float t = duration * i / steps;

            Vector3 point =
                start +
                velocity * t +
                0.5f * Vector3.down *
                gravity * t * t;

            if (!CanOccupy(point, rb.rotation))
                return false;
        }

        return true;
    }

    private void UpdateJump(float dt)
    {
        jumpTime += dt;

        float t = Mathf.Min(
            jumpTime,
            jumpDuration
        );

        float gravity = Mathf.Max(
            1f,
            lostSurfaceGravity
        );

        Vector3 destination =
            jumpStart +
            jumpVelocity * t +
            0.5f * Vector3.down *
            gravity * t * t;

        Vector3 actual = MoveSafely(
            destination,
            rb.rotation
        );

        airVelocity =
            jumpVelocity +
            Vector3.down * gravity * t;

        if (Vector3.Distance(
            actual,
            destination
        ) > 0.05f)
        {
            jumping = false;
            hasSurface = false;
            return;
        }

        if (jumpTime < jumpDuration)
            return;

        jumping = false;

        if (RaySurface(
            jumpTarget +
            jumpNormal * surfaceProbeDistance,
            -jumpNormal,
            surfaceProbeDistance +
            maximumSurfaceGap +
            Clearance(jumpNormal),
            out RaycastHit landing))
        {
            Quaternion rotation = SurfaceRotation(
                landing.normal,
                desiredDirection
            );

            Vector3 target = SurfacePosition(
                landing,
                rotation
            );

            if (Vector3.Distance(
                actual,
                target
            ) <= maximumSurfaceGap + 0.5f &&
                CanOccupy(target, rotation))
            {
                rb.MovePosition(target);
                rb.MoveRotation(rotation);

                surfaceNormal = landing.normal.normalized;
                hasSurface = true;
                airVelocity = Vector3.zero;
                recoveryTimer = 0f;
                return;
            }
        }

        // Failed jump landing: continue under gravity.
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
                airVelocity * 0.2f
            );
        }
    }
}
