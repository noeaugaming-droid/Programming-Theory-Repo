
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1f, -5f);
    [SerializeField] private float followSpeed = 15f;

    [Header("Movement Effects")]
    [SerializeField] private float verticalFollowSpeed = 5f;
    [SerializeField] private float dashFollowSpeed = 4f;
    [SerializeField] private float dashThreshold = 15f;
    [SerializeField] private float jumpThreshold = 1f;

    [Header("Field of View")]
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float jumpFOV = 65f;
    [SerializeField] private float dashFOV = 75f;
    [SerializeField] private float fovSpeed = 6f;

    [Header("Collision")]
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private float wallOffset = 0.1f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    private Rigidbody playerRb;
    private Camera playerCamera;

    void Start()
    {
        playerRb = cameraPivot.GetComponentInParent<Rigidbody>();
        playerCamera = GetComponent<Camera>();

        playerCamera.fieldOfView = normalFOV;
    }

    void LateUpdate()
    {
        if (cameraPivot == null || playerRb == null) return;

        // Match the camera pivot rotation.
        transform.rotation = cameraPivot.rotation;

        Vector3 start = cameraPivot.position;
        Vector3 desiredPosition = cameraPivot.TransformPoint(cameraOffset);

        // Detect horizontal movement speed.
        Vector3 horizontalVelocity = playerRb.linearVelocity;
        horizontalVelocity.y = 0f;

        // Detect dash and jump.
        bool isDashing = horizontalVelocity.magnitude > dashThreshold;
        bool isJumping = playerRb.linearVelocity.y > jumpThreshold;

        float currentFollowSpeed = isDashing ? dashFollowSpeed : followSpeed;

        // Horizontal camera follow.
        Vector3 targetPosition = transform.position;

        targetPosition.x = Mathf.Lerp(
            transform.position.x,
            desiredPosition.x,
            currentFollowSpeed * Time.deltaTime
        );

        targetPosition.z = Mathf.Lerp(
            transform.position.z,
            desiredPosition.z,
            currentFollowSpeed * Time.deltaTime
        );

        // Vertical camera follow with jump lag.
        targetPosition.y = Mathf.Lerp(
            transform.position.y,
            desiredPosition.y,
            verticalFollowSpeed * Time.deltaTime
        );

        // Detect obstacles between the player and camera.
        Vector3 direction = targetPosition - start;
        float distance = direction.magnitude;

        if (distance > 0f)
        {
            direction.Normalize();

            if (Physics.SphereCast(
                start,
                cameraRadius,
                direction,
                out RaycastHit hit,
                distance,
                collisionLayers,
                QueryTriggerInteraction.Ignore))
            {
                targetPosition = start + direction *
                    Mathf.Max(0f, hit.distance - wallOffset);
            }
        }

        // Prevent the camera from moving through walls.
        Vector3 movement = targetPosition - transform.position;
        float movementDistance = movement.magnitude;

        if (movementDistance > 0f)
        {
            if (Physics.SphereCast(
                transform.position,
                cameraRadius,
                movement.normalized,
                out RaycastHit movementHit,
                movementDistance,
                collisionLayers,
                QueryTriggerInteraction.Ignore))
            {
                targetPosition = transform.position +
                    movement.normalized *
                    Mathf.Max(0f, movementHit.distance - wallOffset);
            }
        }

        // Prevent camera overlap with solid objects.
        if (!Physics.CheckSphere(
            targetPosition,
            cameraRadius,
            collisionLayers,
            QueryTriggerInteraction.Ignore))
        {
            transform.position = targetPosition;
        }

        // Camera FOV effects.
        float targetFOV = normalFOV;

        if (isDashing)
        {
            targetFOV = dashFOV;
        }
        else if (isJumping)
        {
            targetFOV = jumpFOV;
        }

        // Smoothly transition between FOV values.
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            fovSpeed * Time.deltaTime
        );
    }
}
