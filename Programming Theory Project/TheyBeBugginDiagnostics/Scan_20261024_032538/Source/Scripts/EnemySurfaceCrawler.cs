
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class EnemySurfaceCrawler : MonoBehaviour
{
    [Header("Climbing / Solid Geometry")]
    [SerializeField] private LayerMask climbableLayers = ~0;
    [SerializeField] private LayerMask solidLayers = ~0;
    [SerializeField, Min(0.01f)] private float skin = 0.035f;
    [SerializeField, Min(0.1f)] private float surfaceProbeDistance = 1.5f;
    [SerializeField, Min(0.1f)] private float wallProbeDistance = 1.1f;
    [SerializeField, Min(0.05f)] private float attachmentRange = 0.45f;
    [SerializeField, Min(0.1f)] private float rotationSpeed = 10f;
    [SerializeField, Min(0.1f)] private float gravity = 22f;
    [SerializeField, Min(1f)] private float terminalSpeed = 35f;
    [SerializeField, Min(0.1f)] private float stuckSeconds = 1.5f;

    [Header("Surface Attachment")]
    [SerializeField] private bool allowWallClimbing = true;
    [SerializeField, Min(0.01f)] private float surfaceSnapSpeed = 6f;

    private Rigidbody body;
    private Collider shape;
    private CapsuleCollider capsule;

    private Vector3 requestedDirection;
    private float requestedSpeed;
    private Vector3 velocity;
    private Vector3 normal = Vector3.up;
    private Vector3 lastPosition;
    private float stuckTime;

    private bool attached;
    private bool jumping;

    private float jumpClock;
    private float jumpDuration;
    private Vector3 jumpStart;
    private Vector3 jumpVelocity;
    private Vector3 jumpNormal;

    private readonly RaycastHit[] casts = new RaycastHit[48];
    private readonly Collider[] overlaps = new Collider[64];

    public Vector3 SurfaceNormal => normal;
    public bool HasSurface => attached;
    public bool IsJumping => jumping;
    public bool IsStuck => stuckTime >= stuckSeconds;
    public LayerMask ClimbableLayers => climbableLayers;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        shape = GetComponent<Collider>();
        capsule = shape as CapsuleCollider;

        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        lastPosition = body.position;
    }

    private void Start()
    {
        // Always try the floor first when spawning.
        if (TryFindSurface(
            body.position,
            Vector3.up,
            out RaycastHit hit))
        {
            attached = true;
            normal = hit.normal;

            // Do not immediately flip the enemy into a wall
            // just because a wall happens to be nearby.
            if (Vector3.Dot(normal, Vector3.up) > 0.5f)
            {
                normal = Vector3.up;
            }
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

        velocity += direction.normalized *
                    Mathf.Max(0f, strength);

        if (attached)
        {
            attached = false;
        }
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (jumping)
            UpdateJump(dt);
        else if (attached)
            Crawl(dt);
        else
            Fall(dt);

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

        // Check for a climbable wall in the travel direction.
        if (allowWallClimbing &&
            direction.sqrMagnitude > 0.001f &&
            RayClimbable(
                ColliderCenter(body.position),
                direction,
                wallProbeDistance,
                out RaycastHit wall) &&
            Vector3.Dot(wall.normal, normal) < 0.5f)
        {
            Quaternion wallRotation =
                SurfaceRotation(wall.normal, direction);

            if (PoseClear(body.position, wallRotation))
            {
                normal = wall.normal;
            }
        }

        Vector3 next =
            SweepAndSlide(body.position, travel);

        // Find support under the proposed new position.
        if (TryFindSurface(next, normal, out RaycastHit support))
        {
            Vector3 supportNormal = support.normal;

            Quaternion targetRotation =
                SurfaceRotation(
                    supportNormal,
                    direction);

            Quaternion nextRotation =
                Quaternion.Slerp(
                    body.rotation,
                    targetRotation,
                    Mathf.Clamp01(rotationSpeed * dt));

            if (PoseClear(next, nextRotation))
            {
                body.MoveRotation(nextRotation);
                normal = supportNormal;
            }

            // Move toward the surface only when there is
            // a small gap. Never snap through solid geometry.
            float extent = ExtentAlong(normal);
            float gap = support.distance - extent;

            if (gap > skin &&
                gap < attachmentRange)
            {
                Vector3 snap =
                    -normal *
                    Mathf.Min(
                        gap - skin,
                        surfaceSnapSpeed * dt);

                next = SweepAndSlide(next, snap);
            }

            body.MovePosition(next);
            velocity = Vector3.zero;
        }
        else
        {
            body.MovePosition(next);

            attached = false;
            velocity = direction * requestedSpeed;
        }
    }

    private void Fall(float dt)
    {
        velocity += Vector3.down * gravity * dt;

        velocity.y =
            Mathf.Max(velocity.y, -terminalSpeed);

        Vector3 next =
            SweepAndSlide(
                body.position,
                velocity * dt);

        body.MovePosition(next);

        // Falling beetles should prioritize landing
        // on a floor instead of rotating onto a wall.
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
                velocity = Vector3.zero;
                body.MoveRotation(target);
            }
        }
    }

    private Vector3 SweepAndSlide(
        Vector3 start,
        Vector3 displacement)
    {
        Vector3 position = start;
        Vector3 remaining = displacement;

        for (int pass = 0; pass < 3; pass++)
        {
            if (remaining.sqrMagnitude < 0.0000001f)
                break;

            float distance = remaining.magnitude;
            Vector3 direction = remaining / distance;

            // Rigidbody has SweepTestAll, not
            // SweepTestNonAlloc.
            RaycastHit[] hits =
                body.SweepTestAll(
                    direction,
                    distance + skin,
                    QueryTriggerInteraction.Ignore);

            float nearest = distance + skin;
            Vector3 hitNormal = Vector3.zero;

            foreach (RaycastHit hit in hits)
            {
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
            {
                position += remaining;
                break;
            }

            float allowed =
                Mathf.Max(0f, nearest - skin);

            position += direction * allowed;

            remaining =
                Vector3.ProjectOnPlane(
                    remaining - direction * allowed,
                    hitNormal);

            if (Vector3.Dot(
                remaining,
                displacement) <= 0f)
                break;
        }

        return position;
    }

    private Vector3 ColliderCenter(Vector3 rootPosition)
    {
        if (capsule != null)
        {
            return rootPosition +
                body.rotation *
                (capsule.transform.localPosition +
                 capsule.transform.localRotation *
                 capsule.center);
        }

        return rootPosition +
               (shape.bounds.center - body.position);
    }

    private float ExtentAlong(Vector3 direction)
    {
        direction.Normalize();

        if (capsule != null)
        {
            Vector3 scale =
                capsule.transform.lossyScale;

            float sx = Mathf.Abs(scale.x);
            float sy = Mathf.Abs(scale.y);
            float sz = Mathf.Abs(scale.z);

            Vector3 axis;

            float axisScale;
            float radialScale;

            switch (capsule.direction)
            {
                case 0:
                    axis = capsule.transform.right;
                    axisScale = sx;
                    radialScale = Mathf.Max(sy, sz);
                    break;

                case 2:
                    axis = capsule.transform.forward;
                    axisScale = sz;
                    radialScale = Mathf.Max(sx, sy);
                    break;

                default:
                    axis = capsule.transform.up;
                    axisScale = sy;
                    radialScale = Mathf.Max(sx, sz);
                    break;
            }

            float radius =
                capsule.radius * radialScale;

            float halfHeight =
                Mathf.Max(
                    radius,
                    capsule.height * axisScale * 0.5f);

            float segment =
                Mathf.Max(0f, halfHeight - radius);

            return radius +
                   segment *
                   Mathf.Abs(
                       Vector3.Dot(
                           axis.normalized,
                           direction));
        }

        Bounds bounds = shape.bounds;

        return
            Mathf.Abs(direction.x) * bounds.extents.x +
            Mathf.Abs(direction.y) * bounds.extents.y +
            Mathf.Abs(direction.z) * bounds.extents.z;
    }

    private bool IsOwnCollider(Collider other)
    {
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

        int count =
            Physics.RaycastNonAlloc(
                origin,
                direction.normalized,
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
                hit.collider.isTrigger ||
                IsOwnCollider(hit.collider))
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

        // Check preferred support first, then floor.
        // Avoid grabbing unrelated side surfaces.
        Vector3[] directions =
        {
            -preferredNormal.normalized,
            Vector3.down
        };

        float closest = float.PositiveInfinity;
        bool found = false;

        Vector3 center = ColliderCenter(position);

        for (int i = 0; i < directions.Length; i++)
        {
            Vector3 direction = directions[i];

            if (direction.sqrMagnitude < 0.001f)
                continue;

            float extent = ExtentAlong(direction);

            float distance =
                Mathf.Max(
                    surfaceProbeDistance,
                    extent + attachmentRange + skin);

            if (!RayClimbable(
                center,
                direction,
                distance,
                out RaycastHit hit))
                continue;

            // The surface should be facing the probe.
            if (Vector3.Dot(
                hit.normal,
                -direction) < 0.4f)
                continue;

            float gap = hit.distance - extent;

            if (gap > attachmentRange ||
                gap < -skin * 2f)
                continue;

            float score = Mathf.Abs(gap);

            if (score < closest)
            {
                closest = score;
                best = hit;
                found = true;
            }
        }

        return found;
    }

    private Quaternion SurfaceRotation(
        Vector3 up,
        Vector3 direction)
    {
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
            forward = Vector3.Cross(up, Vector3.right);

        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.Cross(up, Vector3.forward);

        return Quaternion.LookRotation(
            forward.normalized,
            up);
    }

    private bool PoseClear(
        Vector3 position,
        Quaternion rotation)
    {
        Vector3 offset =
            shape.bounds.center - body.position;

        Vector3 center =
            position +
            rotation *
            (Quaternion.Inverse(body.rotation) * offset);

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
                other.isTrigger ||
                IsOwnCollider(other))
                continue;

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

    public bool TryBeginSpiderJump(
        Vector3 landingPoint,
        Vector3 landingNormal,
        float duration,
        float maxDistance)
    {
        // Beetles cannot jump.
        if (GetComponent<Spider>() == null)
            return false;

        if (!attached ||
            jumping ||
            duration <= 0f)
            return false;

        if (Vector3.Distance(
            body.position,
            landingPoint) >
            Mathf.Min(15f, maxDistance))
            return false;

        jumpStart = body.position;
        jumpNormal = landingNormal.normalized;
        jumpDuration = duration;
        jumpClock = 0f;

        jumpVelocity =
            (landingPoint - jumpStart -
             0.5f * Vector3.down *
             gravity * duration * duration) /
            duration;

        jumping = true;
        attached = false;

        return true;
    }

    private void UpdateJump(float dt)
    {
        jumpClock += dt;

        float t =
            Mathf.Min(jumpClock, jumpDuration);

        Vector3 destination =
            jumpStart +
            jumpVelocity * t +
            0.5f * Vector3.down *
            gravity * t * t;

        Vector3 next =
            SweepAndSlide(
                body.position,
                destination - body.position);

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
                    body.MoveRotation(target);
                }
            }
        }
    }
}
