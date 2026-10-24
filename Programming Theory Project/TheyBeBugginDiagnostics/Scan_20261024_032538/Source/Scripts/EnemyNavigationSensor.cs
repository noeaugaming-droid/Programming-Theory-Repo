
using UnityEngine;

/// <summary>
/// Detects obstacles, edges, and nearby enemies.
/// Gives each enemy its own preferred navigation direction.
///
/// Attach this to the same GameObject as EnemySurfaceCrawler.
///
/// This component does NOT move the enemy.
/// EnemySurfaceCrawler remains responsible for movement.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemySurfaceCrawler))]
public class EnemyNavigationSensor : MonoBehaviour
{
    #region Surface_Detection_Variables

    [Header("Surface Detection")]

    [Tooltip("Layers containing floors, walls, and climbable terrain.")]
    [SerializeField]
    private LayerMask environmentLayers = ~0;

    [Tooltip("How far ahead the enemy checks for obstacles.")]
    [SerializeField]
    private float obstacleDetectionDistance = 2.5f;

    [Tooltip("How far ahead the enemy checks for platform edges.")]
    [SerializeField]
    private float edgeDetectionDistance = 1.2f;

    [Tooltip("How far toward a surface the support ray travels.")]
    [SerializeField]
    private float supportDetectionDistance = 2.0f;

    [Tooltip("Height of forward ray origins above the surface.")]
    [SerializeField]
    private float rayOriginOffset = 0.25f;

    [Tooltip("How frequently the enemy updates its raycast decisions.")]
    [SerializeField]
    private float scanInterval = 0.15f;

    #endregion


    #region Navigation_Variables

    [Header("Navigation")]

    [Tooltip("How strongly the enemy prefers the player direction.")]
    [SerializeField]
    private float targetDirectionWeight = 2.0f;

    [Tooltip("How strongly the enemy avoids obstacles.")]
    [SerializeField]
    private float obstaclePenalty = 4.0f;

    [Tooltip("Penalty for moving toward an unsupported edge.")]
    [SerializeField]
    private float gapPenalty = 5.0f;

    [Tooltip("How strongly the enemy avoids nearby enemies.")]
    [SerializeField]
    private float separationWeight = 2.0f;

    [Tooltip("How strongly the enemy maintains its previous route.")]
    [SerializeField]
    private float directionContinuityWeight = 0.6f;

    [Tooltip("How long a failed direction remains discouraged.")]
    [SerializeField]
    private float blockedDirectionMemory = 2.5f;

    [Tooltip("Distance the enemy must move during a progress check.")]
    [SerializeField]
    private float minimumMovementProgress = 0.35f;

    [Tooltip("Time between progress checks.")]
    [SerializeField]
    private float progressCheckInterval = 1.0f;

    #endregion


    #region Enemy_Avoidance_Variables

    [Header("Enemy Separation")]

    [SerializeField]
    private float separationRadius = 1.75f;

    [SerializeField]
    private LayerMask enemyLayers = ~0;

    private readonly Collider[] nearbyColliders = new Collider[32];

    #endregion


    #region Private_Class_Variables

    private EnemySurfaceCrawler crawler;

    private Vector3 preferredDirection;
    private Vector3 previousDirection;
    private Vector3 lastProgressPosition;
    private Vector3 blockedDirection;

    private float scanTimer;
    private float progressTimer;
    private float blockedMemoryTimer;

    private int preferredSide;

    private bool initialized;
    private bool obstacleAhead;
    private bool gapAhead;
    private bool climbableWallAhead;
    private bool isStuck;

    #endregion


    #region Public_Properties

    public Vector3 PreferredDirection => preferredDirection;

    public bool ObstacleAhead => obstacleAhead;

    public bool GapAhead => gapAhead;

    public bool ClimbableWallAhead => climbableWallAhead;

    public bool IsStuck => isStuck;

    #endregion


    #region Unity_Lifecycle_Methods

    private void Awake()
    {
        crawler = GetComponent<EnemySurfaceCrawler>();

        preferredSide = Random.value < 0.5f ? -1 : 1;

        preferredDirection = transform.forward;
        previousDirection = transform.forward;

        lastProgressPosition = transform.position;

        // Give enemies slightly different scan timing.
        scanTimer = Random.Range(0f, Mathf.Max(0.02f, scanInterval));

        initialized = true;
    }

    private void FixedUpdate()
    {
        if (!initialized || crawler == null)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;

        progressTimer += dt;
        scanTimer -= dt;

        blockedMemoryTimer =
            Mathf.Max(0f, blockedMemoryTimer - dt);

        CheckMovementProgress();

        if (scanTimer <= 0f)
        {
            scanTimer = Mathf.Max(0.02f, scanInterval);

            RefreshForwardDetection();
        }
    }

    #endregion


    #region Public_Navigation_Methods

    /// <summary>
    /// Returns a direction selected from several raycast-tested
    /// movement possibilities.
    ///
    /// Call this from EnemySurfaceCrawler before moving.
    /// </summary>
    public Vector3 GetNavigationDirection(Vector3 desiredDirection)
    {
        if (crawler == null)
        {
            return desiredDirection;
        }

        Vector3 normal = GetSurfaceNormal();

        Vector3 forward =
            Vector3.ProjectOnPlane(desiredDirection, normal);

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward =
                Vector3.ProjectOnPlane(transform.forward, normal);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        forward.Normalize();

        Vector3 separation = CalculateEnemySeparation(normal);

        float bestScore = float.NegativeInfinity;

        Vector3 bestDirection = forward;

        // Test 13 directions from -180 to +180 degrees.
        for (int i = 0; i < 13; i++)
        {
            float angle = (i - 6) * 30f;

            Vector3 candidate =
                Quaternion.AngleAxis(angle, normal) * forward;

            candidate.Normalize();

            float score =
                Vector3.Dot(candidate, forward)
                * targetDirectionWeight;

            score +=
                Vector3.Dot(candidate, separation)
                * separationWeight;

            score +=
                Vector3.Dot(candidate, previousDirection)
                * directionContinuityWeight;

            bool blocked = DetectObstacle(candidate);

            bool supported = DetectSupportAhead(candidate);

            bool wall = DetectClimbableWall(candidate);

            if (blocked && !wall)
            {
                score -= obstaclePenalty;
            }

            // A connected wall may be a valid climbing route.
            if (wall)
            {
                score += 1.0f;
            }

            // Unsupported edges should not be ordinary
            // crawling destinations.
            if (!supported && !wall)
            {
                score -= gapPenalty;
            }

            // Temporarily discourage failed directions.
            if (blockedMemoryTimer > 0f &&
                blockedDirection.sqrMagnitude > 0.001f)
            {
                float similarity =
                    Vector3.Dot(candidate, blockedDirection);

                if (similarity > 0.5f)
                {
                    score -= similarity * 5f;
                }
            }

            // Individual enemies favor different sides
            // when choosing between otherwise similar routes.
            Vector3 side =
                Vector3.Cross(normal, forward).normalized;

            score +=
                Vector3.Dot(candidate, side)
                * preferredSide
                * 0.12f;

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = candidate;
            }
        }

        preferredDirection = bestDirection.normalized;

        previousDirection = preferredDirection;

        return preferredDirection;
    }

    /// <summary>
    /// Lets another script ask whether a direction
    /// contains a detected obstacle.
    /// </summary>
    public bool IsDirectionBlocked(Vector3 direction)
    {
        return DetectObstacle(direction);
    }

    /// <summary>
    /// Lets another script ask whether a direction
    /// has a supporting surface ahead.
    /// </summary>
    public bool HasSurfaceAhead(Vector3 direction)
    {
        return DetectSupportAhead(direction);
    }

    #endregion


    #region Raycast_Detection_Methods

    private void RefreshForwardDetection()
    {
        Vector3 direction = preferredDirection;

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = transform.forward;
        }

        obstacleAhead = DetectObstacle(direction);

        gapAhead = !DetectSupportAhead(direction);

        climbableWallAhead = DetectClimbableWall(direction);
    }

    private bool DetectObstacle(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Vector3 normal = GetSurfaceNormal();

        Vector3 origin =
            transform.position + normal * rayOriginOffset;

        return CastEnvironmentRay(
            origin,
            direction.normalized,
            obstacleDetectionDistance,
            out _
        );
    }

    private bool DetectSupportAhead(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Vector3 normal = GetSurfaceNormal();

        Vector3 origin =
            transform.position
            + direction.normalized * edgeDetectionDistance
            + normal * rayOriginOffset;

        return CastEnvironmentRay(
            origin,
            -normal,
            supportDetectionDistance,
            out _
        );
    }

    private bool DetectClimbableWall(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Vector3 normal = GetSurfaceNormal();

        Vector3 origin =
            transform.position + normal * rayOriginOffset;

        if (!CastEnvironmentRay(
            origin,
            direction.normalized,
            obstacleDetectionDistance,
            out RaycastHit hit))
        {
            return false;
        }

        // A significantly different surface normal
        // suggests a wall or corner transition.
        float alignment =
            Vector3.Dot(hit.normal, normal);

        return alignment < 0.75f;
    }

    private bool CastEnvironmentRay(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit closestHit)
    {
        closestHit = default;

        if (environmentLayers.value == 0)
        {
            return false;
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction.normalized,
            distance,
            environmentLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = float.MaxValue;

        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            // Never treat this enemy's own colliders
            // as terrain or obstacles.
            if (hit.collider.transform == transform ||
                hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            // Never treat other enemies as climbable terrain.
            if (hit.collider.GetComponentInParent<Enemy>() != null)
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;

                closestHit = hit;

                found = true;
            }
        }

        return found;
    }

    #endregion


    #region Enemy_Separation_Methods

    private Vector3 CalculateEnemySeparation(Vector3 normal)
    {
        Vector3 separation = Vector3.zero;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            separationRadius,
            nearbyColliders,
            enemyLayers,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < count; i++)
        {
            Collider other = nearbyColliders[i];

            if (other == null)
            {
                continue;
            }

            if (other.transform == transform ||
                other.transform.IsChildOf(transform))
            {
                continue;
            }

            if (other.GetComponentInParent<Enemy>() == null)
            {
                continue;
            }

            Vector3 away =
                transform.position - other.ClosestPoint(transform.position);

            if (away.sqrMagnitude < 0.0001f)
            {
                away =
                    transform.position - other.transform.position;
            }

            float distance = Mathf.Max(0.2f, away.magnitude);

            if (away.sqrMagnitude > 0.0001f)
            {
                separation += away.normalized / distance;
            }
        }

        return Vector3.ProjectOnPlane(
            separation,
            normal
        ).normalized;
    }

    #endregion


    #region Stuck_Recovery_Methods

    private void CheckMovementProgress()
    {
        if (progressTimer < Mathf.Max(0.2f, progressCheckInterval))
        {
            return;
        }

        float distance =
            Vector3.Distance(
                transform.position,
                lastProgressPosition
            );

        // Only consider the enemy stuck when its crawler
        // is currently attached to a surface.
        isStuck =
            crawler.HasSurface &&
            distance < minimumMovementProgress;

        if (isStuck)
        {
            blockedDirection = preferredDirection;

            blockedMemoryTimer = blockedDirectionMemory;

            preferredSide *= -1;
        }

        lastProgressPosition = transform.position;

        progressTimer = 0f;
    }

    #endregion


    #region Surface_Helper_Methods

    private Vector3 GetSurfaceNormal()
    {
        if (crawler != null &&
            crawler.SurfaceNormal.sqrMagnitude > 0.001f)
        {
            return crawler.SurfaceNormal.normalized;
        }

        return transform.up;
    }

    #endregion


    #region Debug_Gizmo_Methods

    private void OnDrawGizmosSelected()
    {
        EnemySurfaceCrawler currentCrawler =
            GetComponent<EnemySurfaceCrawler>();

        Vector3 normal =
            currentCrawler != null
            ? currentCrawler.SurfaceNormal
            : transform.up;

        if (normal.sqrMagnitude < 0.001f)
        {
            normal = Vector3.up;
        }

        normal.Normalize();

        Vector3 direction =
            preferredDirection.sqrMagnitude > 0.001f
            ? preferredDirection
            : transform.forward;

        Vector3 origin =
            transform.position + normal * rayOriginOffset;

        Gizmos.color = Color.yellow;

        Gizmos.DrawRay(
            origin,
            direction.normalized * obstacleDetectionDistance
        );

        Vector3 edgeOrigin =
            transform.position
            + direction.normalized * edgeDetectionDistance
            + normal * rayOriginOffset;

        Gizmos.color = Color.cyan;

        Gizmos.DrawRay(
            edgeOrigin,
            -normal * supportDetectionDistance
        );

        Gizmos.color = Color.magenta;

        Gizmos.DrawWireSphere(
            transform.position,
            separationRadius
        );

        Gizmos.color = Color.green;

        Gizmos.DrawRay(
            transform.position,
            normal * 1.5f
        );
    }

    #endregion
}
