
using UnityEngine;

public class CameraCollisions : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1f, -5f);

    [Header("Collision")]
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private float wallOffset = 0.1f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    private Vector3 lastSafePosition;

    void Start()
    {
        lastSafePosition = cameraPivot.position;
    }

    void LateUpdate()
    {
        Vector3 start = cameraPivot.position;
        Vector3 desiredPosition = cameraPivot.TransformPoint(cameraOffset);

        Vector3 direction = desiredPosition - start;
        float distance = direction.magnitude;

        if (distance <= 0f) return;

        direction.Normalize();

        Vector3 targetPosition = desiredPosition;

        if (Physics.SphereCast(
            start,
            cameraRadius,
            direction,
            out RaycastHit hit,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - wallOffset);

            Vector3 blockedPosition = start + direction * safeDistance;

            Vector3 remainingMovement = desiredPosition - blockedPosition;

            Vector3 slideDirection =
                Vector3.ProjectOnPlane(remainingMovement, hit.normal);

            targetPosition = blockedPosition;

            float slideDistance = slideDirection.magnitude;

            if (slideDistance > 0f)
            {
                if (Physics.SphereCast(
                    blockedPosition,
                    cameraRadius,
                    slideDirection.normalized,
                    out RaycastHit slideHit,
                    slideDistance,
                    collisionLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    targetPosition += slideDirection.normalized *
                        Mathf.Max(0f, slideHit.distance - wallOffset);
                }
                else
                {
                    targetPosition += slideDirection;
                }
            }
        }

        // Never deliberately place the camera inside a solid object.
        if (!Physics.CheckSphere(
            targetPosition,
            cameraRadius,
            collisionLayers,
            QueryTriggerInteraction.Ignore))
        {
            // Also check the path from the last safe position.
            Vector3 movement = targetPosition - lastSafePosition;
            float movementDistance = movement.magnitude;

            if (movementDistance > 0f &&
                Physics.SphereCast(
                    lastSafePosition,
                    cameraRadius,
                    movement.normalized,
                    out RaycastHit movementHit,
                    movementDistance,
                    collisionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                targetPosition = lastSafePosition +
                    movement.normalized *
                    Mathf.Max(0f, movementHit.distance - wallOffset);
            }

            if (!Physics.CheckSphere(
                targetPosition,
                cameraRadius,
                collisionLayers,
                QueryTriggerInteraction.Ignore))
            {
                lastSafePosition = targetPosition;
            }
        }

        transform.position = lastSafePosition;
    }
}
