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

    private Rigidbody body;
    private Collider shape;
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
    private Vector3 jumpTarget;

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
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        lastPosition = body.position;
    }

    private void Start()
    {
        attached = TryFindSurface(body.position, transform.up, out RaycastHit hit);
        if (attached) normal = hit.normal;
    }

    public void SetMovement(Vector3 direction, float speed)
    {
        requestedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        requestedSpeed = Mathf.Max(0f, speed);
    }

    public void ApplyKnockback(Vector3 direction, float strength, float duration)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        velocity += direction.normalized * Mathf.Max(0f, strength);
        // Momentum naturally decays when attached; airborne momentum follows gravity.
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (jumping) UpdateJump(dt);
        else if (attached) Crawl(dt);
        else Fall(dt);

        if (requestedSpeed > 0.1f && !jumping &&
            Vector3.Distance(lastPosition, body.position) < 0.015f)
            stuckTime += dt;
        else
            stuckTime = 0f;
        lastPosition = body.position;
    }

    private void Crawl(float dt)
    {
        Vector3 direction = Vector3.ProjectOnPlane(requestedDirection, normal);
        if (direction.sqrMagnitude > 0.0001f) direction.Normalize();
        Vector3 travel = direction * requestedSpeed * dt;

        // Detect a wall before entering it; a wall is a new support, not an obstacle to tunnel through.
        if (direction.sqrMagnitude > 0.001f &&
            RayClimbable(body.position, direction, wallProbeDistance, out RaycastHit wall) &&
            Vector3.Dot(wall.normal, normal) < 0.75f)
        {
            Quaternion wallRotation = SurfaceRotation(wall.normal, direction);
            if (PoseClear(body.position, wallRotation))
                normal = wall.normal;
        }

        Vector3 next = SweepAndSlide(body.position, travel);
        body.MovePosition(next);

        if (TryFindSurface(next, normal, out RaycastHit support))
        {
            normal = support.normal;
            Quaternion target = SurfaceRotation(normal, direction);
            Quaternion rotated = Quaternion.Slerp(body.rotation, target, rotationSpeed * dt);
            if (PoseClear(next, rotated)) body.MoveRotation(rotated);
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, 12f * dt);
        }
        else
        {
            attached = false;
            velocity = direction * requestedSpeed;
        }
    }

    private void Fall(float dt)
    {
        velocity += Vector3.down * gravity * dt;
        velocity.y = Mathf.Max(velocity.y, -terminalSpeed);
        Vector3 next = SweepAndSlide(body.position, velocity * dt);
        body.MovePosition(next);
        if (TryFindSurface(next, Vector3.up, out RaycastHit hit))
        {
            Quaternion target = SurfaceRotation(hit.normal, requestedDirection);
            if (PoseClear(next, target))
            {
                normal = hit.normal;
                attached = true;
                velocity = Vector3.zero;
                body.MoveRotation(target);
            }
        }
    }

    // Sweep the Rigidbody collider and slide along solid obstacles.
    // Note: SweepTestAll casts from the Rigidbody current physics pose;
    // subsequent slide passes are conservative approximations.
    // No transform teleportation is used for ordinary locomotion.
    private Vector3 SweepAndSlide(Vector3 start, Vector3 displacement)
    {
        Vector3 position = start;
        Vector3 remaining = displacement;
        for (int pass = 0; pass < 3 && remaining.sqrMagnitude > 0.0000001f; pass++)
        {
            float distance = remaining.magnitude;
            Vector3 direction = remaining / distance;
            RaycastHit[] sweepHits = body.SweepTestAll(
                direction,
                distance + skin,
                QueryTriggerInteraction.Ignore
            );
            int count = sweepHits.Length;
            float nearest = distance + skin;
            Vector3 hitNormal = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = sweepHits[i];
                if (hit.collider == null || hit.collider.isTrigger || IsOwnCollider(hit.collider)) continue;
                if ((solidLayers.value & (1 << hit.collider.gameObject.layer)) == 0) continue;
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
            float allowed = Mathf.Max(0f, nearest - skin);
            position += direction * allowed;
            remaining = Vector3.ProjectOnPlane(remaining - direction * allowed, hitNormal);
            if (Vector3.Dot(remaining, displacement) <= 0f) break;
        }
        return position;
    }

    private bool IsOwnCollider(Collider other) =>
        other == shape || other.transform == transform || other.transform.IsChildOf(transform);

    private bool RayClimbable(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
    {
        best = default;
        if (direction.sqrMagnitude < 0.0001f) return false;
        int count = Physics.RaycastNonAlloc(origin, direction.normalized, casts,
            distance, climbableLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = casts[i];
            if (hit.collider == null || IsOwnCollider(hit.collider) || hit.collider.isTrigger) continue;
            if (hit.distance < nearest) { nearest = hit.distance; best = hit; found = true; }
        }
        return found;
    }

    private bool TryFindSurface(Vector3 position, Vector3 preferredNormal, out RaycastHit best)
    {
        best = default;
        Vector3[] directions = { -preferredNormal, Vector3.down, Vector3.left,
            Vector3.right, Vector3.forward, Vector3.back, Vector3.up };
        float closest = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < directions.Length; i++)
        {
            if (!RayClimbable(position, directions[i], surfaceProbeDistance + attachmentRange,
                out RaycastHit hit)) continue;
            // Distance from the collider center to its surface varies by shape.
            float clearance = ProjectedExtent(hit.normal);
            float gap = Mathf.Abs(hit.distance - clearance);
            if (gap > attachmentRange || gap >= closest) continue;
            closest = gap;
            best = hit;
            found = true;
        }
        return found;
    }

    private float ProjectedExtent(Vector3 axis)
    {
        Bounds b = shape.bounds;
        axis = axis.normalized;
        return Mathf.Abs(axis.x) * b.extents.x +
               Mathf.Abs(axis.y) * b.extents.y +
               Mathf.Abs(axis.z) * b.extents.z;
    }

    private Quaternion SurfaceRotation(Vector3 up, Vector3 direction)
    {
        Vector3 forward = Vector3.ProjectOnPlane(direction, up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, up);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.Cross(up, Vector3.right);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.Cross(up, Vector3.forward);
        return Quaternion.LookRotation(forward.normalized, up.normalized);
    }

    private bool PoseClear(Vector3 position, Quaternion rotation)
    {
        // Use actual collider penetration rather than rejecting normal surface contact.
        Vector3 center = position + rotation *
            (Quaternion.Inverse(body.rotation) * (shape.bounds.center - body.position));
        int count = Physics.OverlapSphereNonAlloc(center,
            shape.bounds.extents.magnitude + skin, overlaps, solidLayers,
            QueryTriggerInteraction.Ignore);
        Vector3 colliderPosition = position + rotation *
            (Quaternion.Inverse(body.rotation) * (shape.transform.position - body.position));
        Quaternion colliderRotation = rotation *
            (Quaternion.Inverse(body.rotation) * shape.transform.rotation);
        for (int i = 0; i < count; i++)
        {
            Collider other = overlaps[i];
            if (other == null || IsOwnCollider(other) || other.isTrigger) continue;
            if (Physics.ComputePenetration(shape, colliderPosition, colliderRotation,
                other, other.transform.position, other.transform.rotation,
                out _, out float depth) && depth > skin + 0.015f)
                return false;
        }
        return true;
    }

    public bool TryBeginSpiderJump(Vector3 landingPoint, Vector3 landingNormal,
        float duration, float maxDistance)
    {
        if (GetComponent<Spider>() == null || !attached || jumping || duration <= 0f ||
            Vector3.Distance(body.position, landingPoint) > Mathf.Min(15f, maxDistance)) return false;
        jumpStart = body.position;
        jumpTarget = landingPoint;
        jumpNormal = landingNormal.normalized;
        jumpDuration = duration;
        jumpClock = 0f;
        jumpVelocity = (landingPoint - jumpStart -
            0.5f * Vector3.down * gravity * duration * duration) / duration;
        jumping = true;
        attached = false;
        return true;
    }

    private void UpdateJump(float dt)
    {
        jumpClock += dt;
        float t = Mathf.Min(jumpClock, jumpDuration);
        Vector3 destination = jumpStart + jumpVelocity * t +
            0.5f * Vector3.down * gravity * t * t;
        Vector3 start = body.position;
        Vector3 next = SweepAndSlide(start, destination - start);
        body.MovePosition(next);
        velocity = jumpVelocity + Vector3.down * gravity * t;
        if (Vector3.Distance(next, destination) > 0.08f || jumpClock >= jumpDuration)
        {
            jumping = false;
            attached = false;
            if (TryFindSurface(next, jumpNormal, out RaycastHit hit))
            {
                Quaternion target = SurfaceRotation(hit.normal, requestedDirection);
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
