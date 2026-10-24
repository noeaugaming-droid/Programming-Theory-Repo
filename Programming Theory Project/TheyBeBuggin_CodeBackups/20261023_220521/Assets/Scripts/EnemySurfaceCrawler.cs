
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemySurfaceCrawler : MonoBehaviour
{
    [Header("Surface Detection")]
    [SerializeField] private LayerMask climbableLayers = ~0;
    [SerializeField] private float surfaceOffset = 0.55f;
    [SerializeField] private float surfaceProbeDistance = 1.5f;
    [SerializeField] private float wallDetectionDistance = 1.2f;
    [SerializeField] private float edgeProbeDistance = 1.5f;

    [Header("Movement")]
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField] private float surfaceSnapSpeed = 12f;
    [SerializeField] private float surfaceNormalSpeed = 10f;

    [Header("Recovery")]
    [SerializeField] private float lostSurfaceGravity = 15f;
    [SerializeField] private float maximumRecoveryDistance = 3f;

    private Rigidbody rb;
    private Collider bodyCollider;

    private Vector3 surfaceNormal = Vector3.up;
    private Vector3 desiredDirection;
    private Vector3 knockbackVelocity;

    private float desiredSpeed;
    private float knockbackTimer;

    private bool hasSurface;

    public Vector3 SurfaceNormal => surfaceNormal;
    public bool HasSurface => hasSurface;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        surfaceNormal = transform.up;
    }

    public void SetMovement(Vector3 direction, float speed)
    {
        desiredDirection = direction;
        desiredSpeed = Mathf.Max(0f, speed);
    }

    public void ApplyKnockback(
        Vector3 direction,
        float strength,
        float duration)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return;

        knockbackVelocity = direction.normalized * strength;
        knockbackTimer = Mathf.Max(0f, duration);
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        if (knockbackTimer > 0f)
        {
            knockbackTimer -= dt;

            Vector3 displacement = knockbackVelocity * dt;

            rb.MovePosition(rb.position + displacement);

            knockbackVelocity = Vector3.Lerp(
                knockbackVelocity,
                Vector3.zero,
                dt * 6f
            );

            return;
        }

        Vector3 position = rb.position;

        Vector3 travelDirection =
            Vector3.ProjectOnPlane(
                desiredDirection,
                surfaceNormal
            ).normalized;

        // Detect a wall directly ahead.
        // Its normal becomes the new climbing orientation.
        if (travelDirection.sqrMagnitude > 0.001f)
        {
            Vector3 wallOrigin =
                position + surfaceNormal * 0.05f;

            if (Physics.Raycast(
                wallOrigin,
                travelDirection,
                out RaycastHit wallHit,
                wallDetectionDistance,
                climbableLayers,
                QueryTriggerInteraction.Ignore))
            {
                if (!IsOwnCollider(wallHit.collider))
                {
                    surfaceNormal = Vector3.Slerp(
                        surfaceNormal,
                        wallHit.normal,
                        Mathf.Clamp01(surfaceNormalSpeed * dt)
                    ).normalized;
                }
            }
        }

        // Look toward the surface beneath the enemy's
        // current local up axis.
        Vector3 probeOrigin =
            position + surfaceNormal * surfaceProbeDistance;

        bool foundSurface = TrySurfaceRay(
            probeOrigin,
            -surfaceNormal,
            surfaceProbeDistance * 2f +
                maximumRecoveryDistance,
            out RaycastHit surfaceHit
        );

        // Probe ahead to help transition around edges.
        if (!foundSurface &&
            travelDirection.sqrMagnitude > 0.001f)
        {
            Vector3 ahead =
                position +
                travelDirection * edgeProbeDistance +
                surfaceNormal * surfaceProbeDistance;

            foundSurface = TrySurfaceRay(
                ahead,
                -surfaceNormal,
                surfaceProbeDistance * 2f +
                    maximumRecoveryDistance,
                out surfaceHit
            );
        }

        // Fallback: search nearby directions when detached.
        if (!foundSurface)
        {
            Vector3[] directions =
            {
                Vector3.down,
                Vector3.up,
                Vector3.left,
                Vector3.right,
                Vector3.forward,
                Vector3.back
            };

            float closest = float.MaxValue;
            RaycastHit closestHit = default;

            foreach (Vector3 direction in directions)
            {
                if (TrySurfaceRay(
                    position,
                    direction,
                    maximumRecoveryDistance,
                    out RaycastHit hit))
                {
                    if (hit.distance < closest)
                    {
                        closest = hit.distance;
                        closestHit = hit;
                        foundSurface = true;
                    }
                }
            }

            if (foundSurface)
                surfaceHit = closestHit;
        }

        hasSurface = foundSurface;

        if (foundSurface)
        {
            surfaceNormal = Vector3.Slerp(
                surfaceNormal,
                surfaceHit.normal,
                Mathf.Clamp01(surfaceNormalSpeed * dt)
            ).normalized;

            Vector3 destination =
                surfaceHit.point +
                surfaceNormal * surfaceOffset;

            position = Vector3.Lerp(
                position,
                destination,
                Mathf.Clamp01(surfaceSnapSpeed * dt)
            );
        }
        else
        {
            position += Vector3.down *
                lostSurfaceGravity * dt * dt;
        }

        Vector3 movement =
            Vector3.ProjectOnPlane(
                desiredDirection,
                surfaceNormal
            );

        if (movement.sqrMagnitude > 0.001f)
        {
            movement.Normalize();

            position += movement * desiredSpeed * dt;

            Quaternion targetRotation =
                Quaternion.LookRotation(
                    movement,
                    surfaceNormal
                );

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    Mathf.Clamp01(rotationSpeed * dt)
                )
            );
        }
        else
        {
            Quaternion targetRotation =
                Quaternion.FromToRotation(
                    transform.up,
                    surfaceNormal
                ) * rb.rotation;

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    Mathf.Clamp01(rotationSpeed * dt)
                )
            );
        }

        rb.MovePosition(position);
    }

    private bool TrySurfaceRay(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit result)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            distance,
            climbableLayers,
            QueryTriggerInteraction.Ignore
        );

        float closest = float.MaxValue;
        result = default;

        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            if (IsOwnCollider(hit.collider))
                continue;

            if (hit.distance < closest)
            {
                closest = hit.distance;
                result = hit;
                found = true;
            }
        }

        return found;
    }

    private bool IsOwnCollider(Collider candidate)
    {
        return candidate == bodyCollider ||
            candidate.transform == transform ||
            candidate.transform.IsChildOf(transform);
    }
}
