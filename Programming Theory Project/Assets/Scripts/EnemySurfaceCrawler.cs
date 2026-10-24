using UnityEngine;

// Shared surface movement for beetles and spiders.
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class EnemySurfaceCrawler : MonoBehaviour
{
    #region Surface_Detection_Variables
    [Header("Surface Detection")]
    [SerializeField] private LayerMask climbableLayers = ~0;
    [SerializeField] private float surfaceOffset = 0.55f;
    [SerializeField] private float surfaceProbeDistance = 1.5f;
    [SerializeField] private float wallDetectionDistance = 1.2f;
    [SerializeField] private float edgeProbeDistance = 1.5f;
    #endregion

    #region Physics_Movement_Variables
    [Header("Movement")]
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float surfaceSnapSpeed = 12f;
    [SerializeField] private float surfaceNormalSpeed = 10f;
    [Header("Recovery")]
    [SerializeField] private float lostSurfaceGravity = 15f;
    [SerializeField] private float maximumRecoveryDistance = 3f;
    [Header("Navigation")]
    [SerializeField] private float obstacleProbeDistance = 1.4f;
    [SerializeField] private float enemyAvoidanceRadius = 1.3f;
    [SerializeField] private float stuckCheckInterval = 1f;
    [SerializeField] private float minimumProgress = 0.4f;
    [SerializeField] private float recoveryDuration = 2f;
    [SerializeField] private float edgeTurnSearchDistance = 1.2f;
    #endregion

    #region Private_Variables
    private Rigidbody rb;
    private Collider bodyCollider;
    private Transform player;
    private Vector3 surfaceNormal = Vector3.up;
    private Vector3 desiredDirection;
    private Vector3 knockbackVelocity;
    private Vector3 lastPosition;
    private float desiredSpeed;
    private float knockbackTimer;
    private float stuckTimer;
    private float recoveryTimer;
    private float lastPlayerDistance;
    private int recoverySide = 1;
    private bool hasSurface;
    private bool spiderMode;
    private bool jumping;
    private Vector3 jumpVelocity;
    private Vector3 jumpTarget;
    private float jumpElapsed;
    private float jumpDuration;
    private readonly Collider[] neighbors = new Collider[32];
    #endregion

    #region Public_Properties
    public Vector3 SurfaceNormal => surfaceNormal;
    public bool HasSurface => hasSurface;
    public bool IsJumping => jumping;
    public bool IsStuck => recoveryTimer > 0f;
    public LayerMask ClimbableLayers => climbableLayers;
    #endregion

    #region Unity_Lifecycle_Methods
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        surfaceNormal = transform.up;
        lastPosition = transform.position;
        spiderMode = GetComponent<Spider>() != null;
    }

    private void Start()
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found != null) player = found.transform;
        lastPlayerDistance = player != null ? Vector3.Distance(transform.position, player.position) : 0f;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (jumping) { UpdateJump(dt); return; }
        if (knockbackTimer > 0f)
        {
            knockbackTimer -= dt;
            Vector3 pushed = rb.position + knockbackVelocity * dt;
            // Beetles remain attached: knockback is tangent to their surface.
            if (!spiderMode) pushed = rb.position + Vector3.ProjectOnPlane(knockbackVelocity, surfaceNormal) * dt;
            rb.MovePosition(pushed);
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, dt * 6f);
            return;
        }

        Vector3 position = rb.position;
        Vector3 movement = ChooseDirection(position, dt);
        RaycastHit hit;
        bool found = FindAttachedSurface(position, movement, out hit);
        hasSurface = found;

        if (found)
        {
            Vector3 targetNormal = hit.normal;
            surfaceNormal = Vector3.Slerp(surfaceNormal, targetNormal,
                Mathf.Clamp01(surfaceNormalSpeed * dt)).normalized;
            Vector3 snapped = hit.point + targetNormal * surfaceOffset;
            position = Vector3.Lerp(position, snapped, Mathf.Clamp01(surfaceSnapSpeed * dt));
        }
        else if (spiderMode)
        {
            // Spiders may fall when detached; beetles do not intentionally drop.
            position += Vector3.down * lostSurfaceGravity * dt * dt;
        }
        else
        {
            // A beetle without a connected surface holds position and searches again.
            movement = Vector3.zero;
        }

        movement = Vector3.ProjectOnPlane(movement, surfaceNormal).normalized;
        if (movement.sqrMagnitude > 0.001f)
        {
            Vector3 candidate = position + movement * desiredSpeed * dt;
            if (spiderMode || CanRemainAttached(candidate, movement)) position = candidate;
            Quaternion rotation = Quaternion.LookRotation(movement, surfaceNormal);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, rotation, Mathf.Clamp01(rotationSpeed * dt)));
        }
        else
        {
            Quaternion rotation = Quaternion.FromToRotation(transform.up, surfaceNormal) * rb.rotation;
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, rotation, Mathf.Clamp01(rotationSpeed * dt)));
        }
        rb.MovePosition(position);
    }
    #endregion

    #region Public_Movement_Methods
    public void SetMovement(Vector3 direction, float speed)
    {
        desiredDirection = direction;
        desiredSpeed = Mathf.Max(0f, speed);
    }

    public void ApplyKnockback(Vector3 direction, float strength, float duration)
    {
        if (direction.sqrMagnitude < 0.0001f) return;
        knockbackVelocity = direction.normalized * strength;
        knockbackTimer = Mathf.Max(0f, duration);
    }

    // Called only by Spider. Uses a timed ballistic arc and checks the landing zone.
    public bool TryBeginSpiderJump(Vector3 landingPoint, Vector3 landingNormal,
        float duration, float maxDistance)
    {
        if (!spiderMode || jumping || !hasSurface || knockbackTimer > 0f) return false;
        Vector3 start = rb.position;
        Vector3 destination = landingPoint + landingNormal.normalized * surfaceOffset;
        if (Vector3.Distance(start, destination) > maxDistance) return false;
        if (duration <= 0.1f) return false;
        float gravity = Mathf.Max(1f, lostSurfaceGravity);
        Vector3 velocity = (destination - start - 0.5f * Vector3.down * gravity * duration * duration) / duration;
        if (!ArcIsClear(start, velocity, gravity, duration)) return false;
        jumpVelocity = velocity;
        jumpTarget = destination;
        jumpDuration = duration;
        jumpElapsed = 0f;
        jumping = true;
        hasSurface = false;
        return true;
    }
    #endregion

    #region Navigation_Methods
    private Vector3 ChooseDirection(Vector3 position, float dt)
    {
        Vector3 toward = desiredDirection;
        if (player != null && desiredSpeed > 0f)
        {
            Vector3 projected = Vector3.ProjectOnPlane(player.position - position, surfaceNormal);
            if (projected.sqrMagnitude > 0.04f) toward = projected.normalized;
        }
        toward = Vector3.ProjectOnPlane(toward, surfaceNormal).normalized;
        if (toward.sqrMagnitude < 0.001f) return Vector3.zero;

        stuckTimer += dt;
        recoveryTimer = Mathf.Max(0f, recoveryTimer - dt);
        if (stuckTimer >= Mathf.Max(0.2f, stuckCheckInterval))
        {
            float distance = player != null ? Vector3.Distance(position, player.position) : 0f;
            bool notMoving = Vector3.Distance(position, lastPosition) < minimumProgress;
            bool notApproaching = player != null && distance > lastPlayerDistance - minimumProgress;
            if (desiredSpeed > 0.1f && (notMoving || notApproaching))
            {
                recoveryTimer = recoveryDuration;
                recoverySide *= -1;
            }
            lastPosition = position;
            lastPlayerDistance = distance;
            stuckTimer = 0f;
        }

        Vector3 avoidance = GetAvoidance(position);
        Vector3 right = Vector3.Cross(surfaceNormal, toward).normalized;
        Vector3 best = Vector3.zero;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 9; i++)
        {
            float degrees = (i - 4) * 35f;
            Vector3 candidate = Quaternion.AngleAxis(degrees, surfaceNormal) * toward;
            float score = Vector3.Dot(candidate, toward) * 2f + Vector3.Dot(candidate, avoidance) * 2f;
            if (TrySurfaceRay(position + surfaceNormal * 0.12f, candidate,
                obstacleProbeDistance, out RaycastHit obstacle))
            {
                // Perpendicular surfaces are useful wall transitions.
                score += Vector3.Dot(obstacle.normal, surfaceNormal) < 0.65f ? 0.8f : -3f;
            }
            if (!spiderMode && !CanRemainAttached(position + candidate * edgeTurnSearchDistance, candidate))
                score -= 8f;
            if (recoveryTimer > 0f)
                score += Vector3.Dot(candidate, right * recoverySide) * 3f;
            if (score > bestScore) { bestScore = score; best = candidate; }
        }
        return best.normalized;
    }

    private Vector3 GetAvoidance(Vector3 position)
    {
        Vector3 result = Vector3.zero;
        int count = Physics.OverlapSphereNonAlloc(position, enemyAvoidanceRadius,
            neighbors, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider other = neighbors[i];
            if (other == null || IsOwnCollider(other) || other.GetComponentInParent<Enemy>() == null) continue;
            Vector3 away = position - other.ClosestPoint(position);
            if (away.sqrMagnitude < 0.0001f) away = position - other.transform.position;
            if (away.sqrMagnitude > 0.0001f) result += away.normalized / Mathf.Max(0.2f, away.magnitude);
        }
        return Vector3.ProjectOnPlane(result, surfaceNormal).normalized;
    }
    #endregion

    #region Surface_Detection_Methods
    private bool FindAttachedSurface(Vector3 position, Vector3 travel, out RaycastHit hit)
    {
        // First test a wall directly ahead, then the current surface, then the edge.
        if (travel.sqrMagnitude > 0.001f &&
            TrySurfaceRay(position + surfaceNormal * 0.1f, travel,
                wallDetectionDistance, out RaycastHit wall) &&
            Vector3.Dot(wall.normal, surfaceNormal) < 0.65f)
        {
            hit = wall;
            return true;
        }
        Vector3 origin = position + surfaceNormal * surfaceProbeDistance;
        if (TrySurfaceRay(origin, -surfaceNormal,
            surfaceProbeDistance * 2f + maximumRecoveryDistance, out hit)) return true;
        if (travel.sqrMagnitude > 0.001f)
        {
            Vector3 ahead = position + travel * edgeProbeDistance;
            if (TrySurfaceRay(ahead + surfaceNormal * surfaceProbeDistance,
                -surfaceNormal, surfaceProbeDistance * 2f + maximumRecoveryDistance, out hit)) return true;
            // Search for the vertical face just beyond a platform edge.
            if (TrySurfaceRay(ahead + Vector3.down * 0.4f, -travel,
                edgeProbeDistance + maximumRecoveryDistance, out hit)) return true;
        }
        return FindNearbySurface(position, out hit);
    }

    private bool CanRemainAttached(Vector3 position, Vector3 travel)
    {
        return FindAttachedSurface(position, travel, out _);
    }

    private bool FindNearbySurface(Vector3 position, out RaycastHit bestHit)
    {
        Vector3[] directions = { -surfaceNormal, Vector3.down, Vector3.up,
            Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        bestHit = default;
        float closest = float.MaxValue;
        bool found = false;
        foreach (Vector3 direction in directions)
        {
            if (TrySurfaceRay(position, direction, maximumRecoveryDistance, out RaycastHit hit)
                && hit.distance < closest)
            {
                closest = hit.distance;
                bestHit = hit;
                found = true;
            }
        }
        return found;
    }

    private bool TrySurfaceRay(Vector3 origin, Vector3 direction, float distance, out RaycastHit result)
    {
        result = default;
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance,
            climbableLayers, QueryTriggerInteraction.Ignore);
        float closest = float.MaxValue;
        bool found = false;
        foreach (RaycastHit hit in hits)
        {
            if (IsOwnCollider(hit.collider)) continue;
            if (hit.distance < closest) { closest = hit.distance; result = hit; found = true; }
        }
        return found;
    }

    private bool IsOwnCollider(Collider candidate)
    {
        return candidate == bodyCollider || candidate.transform == transform ||
            candidate.transform.IsChildOf(transform);
    }
    #endregion

    #region Spider_Jump_Methods
    private bool ArcIsClear(Vector3 start, Vector3 velocity, float gravity, float duration)
    {
        Vector3 previous = start;
        for (int i = 1; i <= 16; i++)
        {
            float t = duration * i / 16f;
            Vector3 next = start + velocity * t + 0.5f * Vector3.down * gravity * t * t;
            Vector3 delta = next - previous;
            if (Physics.SphereCast(previous, 0.15f, delta.normalized,
                out RaycastHit obstacle, delta.magnitude, climbableLayers,
                QueryTriggerInteraction.Ignore) && !IsOwnCollider(obstacle.collider))
            {
                // The final segment may legitimately touch the destination surface.
                if (i < 16) return false;
            }
            previous = next;
        }
        return true;
    }

    private void UpdateJump(float dt)
    {
        jumpElapsed += dt;
        float t = Mathf.Min(jumpElapsed, jumpDuration);
        float gravity = Mathf.Max(1f, lostSurfaceGravity);
        Vector3 start = jumpTarget - jumpVelocity * jumpDuration -
            0.5f * Vector3.down * gravity * jumpDuration * jumpDuration;
        Vector3 position = start + jumpVelocity * t + 0.5f * Vector3.down * gravity * t * t;
        rb.MovePosition(position);
        if (jumpElapsed < jumpDuration) return;
        jumping = false;
        if (FindNearbySurface(position, out RaycastHit landing))
        {
            surfaceNormal = landing.normal;
            rb.MovePosition(landing.point + surfaceNormal * surfaceOffset);
            hasSurface = true;
        }
    }
    #endregion
}
