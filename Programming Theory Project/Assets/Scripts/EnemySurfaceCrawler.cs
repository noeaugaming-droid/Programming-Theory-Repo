
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class EnemySurfaceCrawler : MonoBehaviour
{
    [Header("Climbing / Solid Geometry")]
    [SerializeField] private LayerMask climbableLayers = ~0;
    [SerializeField] private LayerMask solidLayers = ~0;

    [SerializeField, Min(0.01f)]
    private float skin = 0.035f;

    [SerializeField, Min(0.1f)]
    private float surfaceProbeDistance = 1.5f;

    [SerializeField, Min(0.1f)]
    private float wallProbeDistance = 1.1f;

    [SerializeField, Min(0.05f)]
    private float attachmentRange = 0.45f;

    [SerializeField, Min(0.1f)]
    private float rotationSpeed = 10f;

    [SerializeField, Min(0.1f)]
    private float gravity = 22f;

    [SerializeField, Min(1f)]
    private float terminalSpeed = 35f;

    [SerializeField, Min(0.1f)]
    private float stuckSeconds = 1.5f;

    [Header("Mesh Collider Seam Handling")]
    [SerializeField, Min(0.01f)]
    private float seamProbeOffset = 0.3f;

    [SerializeField, Min(0f)]
    private float seamHeightTolerance = 0.2f;

    [SerializeField, Range(0f, 1f)]
    private float minimumFloorNormalY = 0.55f;

    [SerializeField, Min(0f)]
    private float attachmentGraceTime = 0.15f;

    [SerializeField, Min(0.1f)]
    private float surfaceNormalSmoothSpeed = 14f;

    [SerializeField]
    private bool showDebugProbes = false;

    private bool beetleMode;
    private CapsuleCollider beetleCapsule;
    private float beetleVerticalSpeed;

    private Rigidbody body;
    private Collider shape;

    private Vector3 requestedDirection;
    private float requestedSpeed;

    private Vector3 velocity;
    private Vector3 normal = Vector3.up;
    private Vector3 lastPosition;

    private float stuckTime;
    private float missingSurfaceTime;

    private bool attached;
    private bool jumping;

    private float jumpClock;
    private float jumpDuration;

    private Vector3 jumpStart;
    private Vector3 jumpVelocity;
    private Vector3 jumpNormal;
    private Vector3 jumpTarget;

    private readonly RaycastHit[] casts =
        new RaycastHit[64];

    private readonly Collider[] overlaps =
        new Collider[64];

    public Vector3 SurfaceNormal => normal;
    public bool HasSurface => attached;
    public bool IsJumping => jumping;
    public bool IsStuck => stuckTime >= stuckSeconds;
    public LayerMask ClimbableLayers => climbableLayers;

    private void Awake()
    {
        beetleMode = GetComponent<Beetle>() != null;

        beetleCapsule = GetComponent<CapsuleCollider>();

        body = GetComponent<Rigidbody>();
        shape = GetComponent<Collider>();

        body.isKinematic = true;
        body.useGravity = false;

        body.interpolation =
            RigidbodyInterpolation.Interpolate;

        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        lastPosition = body.position;
    }

    private void Start()
    {
        if (beetleMode && beetleCapsule != null)
        {
            normal = Vector3.up;
            attached = false;
            return;
        }

        attached = TryFindSurface(
            body.position,
            transform.up,
            out RaycastHit hit);

        if (attached)
        {
            normal = hit.normal;
            missingSurfaceTime = 0f;
        }
    }

    public void SetMovement(Vector3 direction, float speed)
    {
        requestedDirection =
            direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.zero;

        requestedSpeed = Mathf.Max(0f, speed);
    }

    public void ApplyKnockback(
        Vector3 direction,
        float strength,
        float duration)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        velocity +=
            direction.normalized * Mathf.Max(0f, strength);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (beetleMode && beetleCapsule != null)
        {
            UpdateBeetle(dt);
        }
        else if (jumping)
        {
            UpdateJump(dt);
        }
        else if (attached)
        {
            Crawl(dt);
        }
        else
        {
            Fall(dt);
        }

        if (requestedSpeed > 0.1f &&
            !jumping &&
            Vector3.Distance(
                lastPosition,
                body.position) < 0.015f)
        {
            stuckTime += dt;
        }
        else
        {
            stuckTime = 0f;
        }

        lastPosition = body.position;
    }

    // =====================================================
    // BEETLE MOTOR
    // Preserves upright, floor-only beetle movement.
    // =====================================================

    private void UpdateBeetle(float dt)
    {
        Vector3 currentForward =
            Vector3.ProjectOnPlane(
                body.rotation * Vector3.forward,
                Vector3.up);

        if (currentForward.sqrMagnitude < 0.0001f)
            currentForward = Vector3.forward;

        Quaternion upright =
            Quaternion.LookRotation(
                currentForward.normalized,
                Vector3.up);

        Vector3 direction =
            Vector3.ProjectOnPlane(
                requestedDirection,
                Vector3.up);

        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();

            upright = Quaternion.LookRotation(
                direction,
                Vector3.up);
        }

        float bottom = CapsuleVerticalExtent(upright);

        Vector3 worldCenterOffset =
            upright * Vector3.Scale(
                beetleCapsule.center,
                transform.lossyScale);

        Vector3 position = body.position;

        float castHeight = Mathf.Max(
            2f,
            bottom + surfaceProbeDistance + 0.5f);

        float rayLength =
            castHeight + bottom + attachmentRange + 1f;

        bool found = TryFindBeetleFloor(
            position,
            worldCenterOffset,
            direction,
            castHeight,
            rayLength,
            out RaycastHit floor);

        float desiredY = found
            ? floor.point.y + bottom + skin -
              worldCenterOffset.y
            : position.y;

        float gap = position.y - desiredY;

        bool groundedNow =
            found &&
            gap < attachmentRange +
                  seamHeightTolerance + 0.3f &&
            gap > -Mathf.Max(
                1f,
                bottom * 2f);

        normal = Vector3.up;

        if (groundedNow)
        {
            attached = true;
            missingSurfaceTime = 0f;
            beetleVerticalSpeed = 0f;

            // Avoid snapping violently when crossing
            // a small height difference at a seam.
            if (Mathf.Abs(gap) <=
                seamHeightTolerance + skin)
            {
                position.y = Mathf.MoveTowards(
                    position.y,
                    desiredY,
                    Mathf.Max(
                        0.05f,
                        requestedSpeed * dt));
            }
            else
            {
                // Preserve original spawn-penetration
                // recovery behavior.
                position.y = desiredY;
            }
        }
        else
        {
            missingSurfaceTime += dt;

            if (attached &&
                missingSurfaceTime <= attachmentGraceTime)
            {
                // Short gaps between adjacent meshes
                // should not immediately start a fall.
                beetleVerticalSpeed = 0f;
            }
            else
            {
                attached = false;

                beetleVerticalSpeed = Mathf.Max(
                    -terminalSpeed,
                    beetleVerticalSpeed - gravity * dt);

                position.y += beetleVerticalSpeed * dt;

                if (found && position.y <= desiredY)
                {
                    position.y = desiredY;
                    attached = true;
                    beetleVerticalSpeed = 0f;
                    missingSurfaceTime = 0f;
                }
            }
        }

        Vector3 motion =
            direction * requestedSpeed * dt;

        if (motion.sqrMagnitude > 0.0000001f)
        {
            GetBeetleCapsule(
                position,
                upright,
                out Vector3 a,
                out Vector3 b,
                out float radius);

            float distance = motion.magnitude;
            Vector3 travelDir = motion / distance;

            // Check all hits rather than trusting the
            // first collider. This avoids accidentally
            // treating the beetle's own child colliders
            // as blocking geometry.
            int count = Physics.CapsuleCastNonAlloc(
                a,
                b,
                radius * 0.97f,
                travelDir,
                casts,
                distance + skin,
                solidLayers,
                QueryTriggerInteraction.Ignore);

            float allowedDistance = distance;

            for (int i = 0; i < count; i++)
            {
                RaycastHit obstacle = casts[i];

                if (obstacle.collider == null ||
                    IsOwnCollider(obstacle.collider))
                    continue;

                // A predominantly upward-facing normal
                // is usually floor geometry, not a wall.
                // Only ignore such a hit when a supporting
                // floor has already been detected.
                if (attached &&
                    obstacle.normal.y >=
                    minimumFloorNormalY)
                {
                    continue;
                }

                allowedDistance = Mathf.Min(
                    allowedDistance,
                    Mathf.Max(
                        0f,
                        obstacle.distance - skin));
            }

            position += travelDir * allowedDistance;
        }

        body.MoveRotation(upright);
        body.MovePosition(position);
    }

    private bool TryFindBeetleFloor(
        Vector3 position,
        Vector3 centerOffset,
        Vector3 direction,
        float castHeight,
        float rayLength,
        out RaycastHit best)
    {
        best = default;

        Vector3 forward =
            direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.ProjectOnPlane(
                    body.rotation * Vector3.forward,
                    Vector3.up).normalized;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        Vector3 side =
            Vector3.Cross(Vector3.up, forward).normalized;

        // Center, forward, backward, left and right.
        // This allows one probe to cross a seam while
        // other probes still detect valid ground.
        Vector3[] offsets =
        {
            Vector3.zero,
            forward * seamProbeOffset,
            -forward * seamProbeOffset * 0.5f,
            side * seamProbeOffset,
            -side * seamProbeOffset
        };

        bool found = false;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 origin =
                position +
                centerOffset +
                offsets[i] +
                Vector3.up * castHeight;

            if (showDebugProbes)
            {
                Debug.DrawRay(
                    origin,
                    Vector3.down * rayLength,
                    Color.yellow);
            }

            if (!RayClimbable(
                origin,
                Vector3.down,
                rayLength,
                out RaycastHit hit))
            {
                continue;
            }

            if (hit.normal.y < minimumFloorNormalY)
                continue;

            // Favor nearby floor heights, not simply
            // whichever mesh happens to be hit first.
            float expectedBottom =
                position.y -
                CapsuleVerticalExtent(body.rotation);

            float heightDifference =
                Mathf.Abs(hit.point.y - expectedBottom);

            float score =
                heightDifference +
                offsets[i].magnitude * 0.15f;

            if (score < bestScore)
            {
                bestScore = score;
                best = hit;
                found = true;
            }
        }

        return found;
    }

    private float CapsuleVerticalExtent(
        Quaternion rotation)
    {
        GetBeetleCapsule(
            body.position,
            rotation,
            out Vector3 a,
            out Vector3 b,
            out float radius);

        return Mathf.Abs(a.y - b.y) * 0.5f +
               radius;
    }

    private void GetBeetleCapsule(
        Vector3 rootPosition,
        Quaternion rotation,
        out Vector3 a,
        out Vector3 b,
        out float radius)
    {
        Vector3 scale = transform.lossyScale;

        Vector3 absScale = new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z));

        Vector3 axis =
            beetleCapsule.direction == 0
                ? Vector3.right
                : beetleCapsule.direction == 2
                    ? Vector3.forward
                    : Vector3.up;

        float axisScale =
            beetleCapsule.direction == 0
                ? absScale.x
                : beetleCapsule.direction == 2
                    ? absScale.z
                    : absScale.y;

        float radialScale =
            beetleCapsule.direction == 0
                ? Mathf.Max(absScale.y, absScale.z)
                : beetleCapsule.direction == 2
                    ? Mathf.Max(absScale.x, absScale.y)
                    : Mathf.Max(absScale.x, absScale.z);

        radius =
            beetleCapsule.radius * radialScale;

        float halfSegment = Mathf.Max(
            0f,
            beetleCapsule.height *
            axisScale * 0.5f - radius);

        Vector3 center =
            rootPosition +
            rotation * Vector3.Scale(
                beetleCapsule.center,
                scale);

        Vector3 offset =
            rotation * axis * halfSegment;

        a = center + offset;
        b = center - offset;
    }

    // =====================================================
    // SPIDER SURFACE CRAWLING
    // =====================================================

    private void Crawl(float dt)
    {
        Vector3 direction =
            Vector3.ProjectOnPlane(
                requestedDirection,
                normal);

        if (direction.sqrMagnitude > 0.0001f)
            direction.Normalize();

        Vector3 travel =
            direction * requestedSpeed * dt;

        Vector3 candidateNormal = normal;

        // Detect upcoming walls or sharp corners.
        if (direction.sqrMagnitude > 0.001f &&
            RayClimbable(
                body.position,
                direction,
                wallProbeDistance,
                out RaycastHit wall))
        {
            float alignment =
                Vector3.Dot(wall.normal, normal);

            if (alignment < 0.75f)
            {
                Quaternion wallRotation =
                    SurfaceRotation(
                        wall.normal,
                        direction);

                if (PoseClear(
                    body.position,
                    wallRotation))
                {
                    candidateNormal = wall.normal;
                }
            }
        }

        Vector3 next =
            SweepAndSlide(
                body.position,
                travel);

        // Look at the next position AND a short
        // distance ahead. This helps find the
        // adjacent collider before losing support.
        bool found =
            TryFindSurface(
                next,
                candidateNormal,
                out RaycastHit support);

        if (!found &&
            direction.sqrMagnitude > 0.001f)
        {
            found = TryFindSurface(
                next +
                direction * seamProbeOffset,
                candidateNormal,
                out support);
        }

        if (found)
        {
            missingSurfaceTime = 0f;
            attached = true;

            Vector3 targetNormal = support.normal;

            // Smooth transitions across mesh triangles
            // and between nearly coplanar colliders.
            if (Vector3.Dot(
                normal,
                targetNormal) > 0.65f)
            {
                normal = Vector3.Slerp(
                    normal,
                    targetNormal,
                    Mathf.Clamp01(
                        surfaceNormalSmoothSpeed * dt))
                    .normalized;
            }
            else
            {
                // A sharp wall/ceiling transition needs
                // the new support normal, not a blended
                // direction pointing through geometry.
                normal = targetNormal;
            }

            Quaternion target =
                SurfaceRotation(
                    normal,
                    direction);

            Quaternion rotated =
                Quaternion.Slerp(
                    body.rotation,
                    target,
                    Mathf.Clamp01(
                        rotationSpeed * dt));

            if (PoseClear(next, rotated))
                body.MoveRotation(rotated);

            velocity = Vector3.MoveTowards(
                velocity,
                Vector3.zero,
                12f * dt);
        }
        else
        {
            missingSurfaceTime += dt;

            if (missingSurfaceTime >
                attachmentGraceTime)
            {
                attached = false;
                velocity =
                    direction * requestedSpeed;
            }
        }

        body.MovePosition(next);
    }

    private void Fall(float dt)
    {
        velocity +=
            Vector3.down * gravity * dt;

        velocity.y = Mathf.Max(
            velocity.y,
            -terminalSpeed);

        Vector3 next =
            SweepAndSlide(
                body.position,
                velocity * dt);

        body.MovePosition(next);

        if (TryFindSurface(
            next,
            Vector3.up,
            out RaycastHit hit))
        {
            Quaternion target =
                SurfaceRotation(
                    hit.normal,
                    requestedDirection);

            if (PoseClear(next, target))
            {
                normal = hit.normal;
                attached = true;

                missingSurfaceTime = 0f;
                velocity = Vector3.zero;

                body.MoveRotation(target);
            }
        }
    }

    // =====================================================
    // COLLISION AND SURFACE DETECTION
    // =====================================================

    private Vector3 SweepAndSlide(
        Vector3 start,
        Vector3 displacement)
    {
        if (displacement.sqrMagnitude <
            0.0000001f)
        {
            return start;
        }

        float distance = displacement.magnitude;
        Vector3 direction =
            displacement / distance;

        // Sweep from the Rigidbody's current pose.
        // Avoid repeatedly sweeping from that same
        // pose while pretending it has already moved.
        RaycastHit[] hits =
            body.SweepTestAll(
                direction,
                distance + skin,
                QueryTriggerInteraction.Ignore);

        float nearest = distance + skin;
        Vector3 hitNormal = Vector3.zero;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];

            if (hit.collider == null ||
                hit.collider.isTrigger ||
                IsOwnCollider(hit.collider))
                continue;

            if ((solidLayers.value &
                (1 << hit.collider.gameObject.layer)) == 0)
                continue;

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                hitNormal = hit.normal;
            }
        }

        if (hitNormal == Vector3.zero)
            return start + displacement;

        float allowed = Mathf.Clamp(
            nearest - skin,
            0f,
            distance);

        Vector3 position =
            start + direction * allowed;

        Vector3 remaining =
            displacement -
            direction * allowed;

        Vector3 slide =
            Vector3.ProjectOnPlane(
                remaining,
                hitNormal);

        // A conservative slide prevents a seam from
        // producing a large sideways displacement.
        if (slide.sqrMagnitude > 0.0000001f)
        {
            Vector3 slideTarget =
                position + slide;

            if (PoseClear(
                slideTarget,
                body.rotation))
            {
                position = slideTarget;
            }
        }

        return position;
    }

    private bool IsOwnCollider(Collider other)
    {
        if (other == null)
            return false;

        return other == shape ||
               other.transform == transform ||
               other.transform.IsChildOf(transform);
    }

    private bool RayClimbable(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit best)
    {
        best = default;

        if (direction.sqrMagnitude < 0.0001f)
            return false;

        direction.Normalize();

        int count =
            Physics.RaycastNonAlloc(
                origin,
                direction,
                casts,
                distance,
                climbableLayers,
                QueryTriggerInteraction.Ignore);

        float nearest = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = casts[i];

            if (hit.collider == null ||
                IsOwnCollider(hit.collider) ||
                hit.collider.isTrigger)
                continue;

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                best = hit;
                found = true;
            }
        }

        return found;
    }

    private bool TryFindSurface(
        Vector3 position,
        Vector3 preferredNormal,
        out RaycastHit best)
    {
        best = default;

        if (preferredNormal.sqrMagnitude < 0.0001f)
            preferredNormal = Vector3.up;

        preferredNormal.Normalize();

        Vector3 tangent =
            Vector3.ProjectOnPlane(
                requestedDirection,
                preferredNormal);

        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent = Vector3.ProjectOnPlane(
                body.rotation * Vector3.forward,
                preferredNormal);
        }

        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent =
                Vector3.Cross(
                    preferredNormal,
                    Vector3.right);
        }

        if (tangent.sqrMagnitude < 0.0001f)
        {
            tangent =
                Vector3.Cross(
                    preferredNormal,
                    Vector3.forward);
        }

        tangent.Normalize();

        Vector3 side =
            Vector3.Cross(
                preferredNormal,
                tangent).normalized;

        Vector3[] directions =
        {
            -preferredNormal,
            Vector3.down,
            Vector3.left,
            Vector3.right,
            Vector3.forward,
            Vector3.back,
            Vector3.up
        };

        Vector3[] offsets =
        {
            Vector3.zero,
            tangent * seamProbeOffset,
            -tangent * seamProbeOffset,
            side * seamProbeOffset,
            -side * seamProbeOffset
        };

        bool found = false;
        float bestScore = float.PositiveInfinity;

        float maxDistance =
            surfaceProbeDistance +
            attachmentRange;

        for (int p = 0; p < offsets.Length; p++)
        {
            Vector3 origin =
                position + offsets[p];

            for (int d = 0; d < directions.Length; d++)
            {
                Vector3 castDirection =
                    directions[d].normalized;

                if (showDebugProbes &&
                    d == 0)
                {
                    Debug.DrawRay(
                        origin,
                        castDirection * maxDistance,
                        Color.cyan);
                }

                if (!RayClimbable(
                    origin,
                    castDirection,
                    maxDistance,
                    out RaycastHit hit))
                {
                    continue;
                }

                float clearance =
                    ProjectedExtent(hit.normal);

                float gap =
                    Mathf.Abs(
                        hit.distance - clearance);

                if (gap >
                    attachmentRange +
                    seamHeightTolerance)
                {
                    continue;
                }

                // Prefer the currently attached surface
                // when neighboring mesh colliders offer
                // nearly equivalent support.
                float alignment =
                    Vector3.Dot(
                        hit.normal,
                        preferredNormal);

                float normalPenalty =
                    (1f - alignment) * 0.12f;

                float offsetPenalty =
                    offsets[p].magnitude * 0.1f;

                float score =
                    gap +
                    normalPenalty +
                    offsetPenalty;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = hit;
                    found = true;
                }
            }
        }

        return found;
    }

    private float ProjectedExtent(Vector3 axis)
    {
        Bounds bounds = shape.bounds;

        axis.Normalize();

        return
            Mathf.Abs(axis.x) * bounds.extents.x +
            Mathf.Abs(axis.y) * bounds.extents.y +
            Mathf.Abs(axis.z) * bounds.extents.z;
    }

    private Quaternion SurfaceRotation(
        Vector3 up,
        Vector3 direction)
    {
        if (up.sqrMagnitude < 0.0001f)
            up = Vector3.up;

        up.Normalize();

        Vector3 forward =
            Vector3.ProjectOnPlane(
                direction,
                up);

        if (forward.sqrMagnitude < 0.001f)
        {
            forward =
                Vector3.ProjectOnPlane(
                    body.rotation * Vector3.forward,
                    up);
        }

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.Cross(
                up,
                Vector3.right);

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.Cross(
                up,
                Vector3.forward);

        return Quaternion.LookRotation(
            forward.normalized,
            up);
    }

    private bool PoseClear(
        Vector3 position,
        Quaternion rotation)
    {
        Vector3 center =
            position +
            rotation *
            (Quaternion.Inverse(body.rotation) *
             (shape.bounds.center - body.position));

        int count =
            Physics.OverlapSphereNonAlloc(
                center,
                shape.bounds.extents.magnitude + skin,
                overlaps,
                solidLayers,
                QueryTriggerInteraction.Ignore);

        Vector3 colliderPosition =
            position +
            rotation *
            (Quaternion.Inverse(body.rotation) *
             (shape.transform.position - body.position));

        Quaternion colliderRotation =
            rotation *
            (Quaternion.Inverse(body.rotation) *
             shape.transform.rotation);

        for (int i = 0; i < count; i++)
        {
            Collider other = overlaps[i];

            if (other == null ||
                IsOwnCollider(other) ||
                other.isTrigger)
            {
                continue;
            }

            if (Physics.ComputePenetration(
                shape,
                colliderPosition,
                colliderRotation,
                other,
                other.transform.position,
                other.transform.rotation,
                out _,
                out float depth))
            {
                if (depth > skin + 0.015f)
                    return false;
            }
        }

        return true;
    }

    // =====================================================
    // SPIDER JUMPING
    // Public API preserved for Spider.cs.
    // =====================================================

    public bool TryBeginSpiderJump(
        Vector3 landingPoint,
        Vector3 landingNormal,
        float duration,
        float maxDistance)
    {
        if (GetComponent<Spider>() == null ||
            !attached ||
            jumping ||
            duration <= 0f ||
            Vector3.Distance(
                body.position,
                landingPoint) >
            Mathf.Min(15f, maxDistance))
        {
            return false;
        }

        jumpStart = body.position;
        jumpTarget = landingPoint;

        jumpNormal =
            landingNormal.sqrMagnitude > 0.0001f
                ? landingNormal.normalized
                : Vector3.up;

        jumpDuration = duration;
        jumpClock = 0f;

        jumpVelocity =
            (landingPoint -
             jumpStart -
             0.5f *
             Vector3.down *
             gravity *
             duration *
             duration) / duration;

        jumping = true;
        attached = false;
        missingSurfaceTime = 0f;

        return true;
    }

    private void UpdateJump(float dt)
    {
        jumpClock += dt;

        float t =
            Mathf.Min(
                jumpClock,
                jumpDuration);

        Vector3 destination =
            jumpStart +
            jumpVelocity * t +
            0.5f *
            Vector3.down *
            gravity *
            t * t;

        Vector3 start = body.position;

        Vector3 next =
            SweepAndSlide(
                start,
                destination - start);

        body.MovePosition(next);

        velocity =
            jumpVelocity +
            Vector3.down * gravity * t;

        if (Vector3.Distance(
                next,
                destination) > 0.08f ||
            jumpClock >= jumpDuration)
        {
            jumping = false;
            attached = false;

            if (TryFindSurface(
                next,
                jumpNormal,
                out RaycastHit hit))
            {
                Quaternion target =
                    SurfaceRotation(
                        hit.normal,
                        requestedDirection);

                if (PoseClear(next, target))
                {
                    attached = true;
                    normal = hit.normal;

                    velocity = Vector3.zero;
                    missingSurfaceTime = 0f;

                    body.MoveRotation(target);
                }
            }
        }
    }

    private void OnValidate()
    {
        skin = Mathf.Max(0.01f, skin);

        surfaceProbeDistance =
            Mathf.Max(0.1f, surfaceProbeDistance);

        wallProbeDistance =
            Mathf.Max(0.1f, wallProbeDistance);

        attachmentRange =
            Mathf.Max(0.05f, attachmentRange);

        seamProbeOffset =
            Mathf.Max(0.01f, seamProbeOffset);

        seamHeightTolerance =
            Mathf.Max(0f, seamHeightTolerance);

        attachmentGraceTime =
            Mathf.Max(0f, attachmentGraceTime);

        surfaceNormalSmoothSpeed =
            Mathf.Max(0.1f, surfaceNormalSmoothSpeed);
    }
}
