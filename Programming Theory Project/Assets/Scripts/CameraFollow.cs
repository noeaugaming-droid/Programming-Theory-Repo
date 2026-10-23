
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 1f, -5f);

    [Header("Camera Follow Delay")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float horizontalSmoothTime = 0.08f;

    [Range(0.01f, 0.5f)]
    [SerializeField] private float verticalSmoothTime = 0.12f;

    [Range(0.01f, 0.5f)]
    [SerializeField] private float dashSmoothTime = 0.18f;

    [Header("Movement Effects")]
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

    private float horizontalVelocityX;
    private float horizontalVelocityZ;
    private float verticalVelocity;

    void Start()
    {
        if (cameraPivot == null)
        {
            Debug.LogError("Assign Camera Pivot on CameraFollow.");
            enabled = false;
            return;
        }

        playerRb = cameraPivot.GetComponentInParent<Rigidbody>();
        playerCamera = GetComponent<Camera>();

        if (playerRb == null || playerCamera == null)
        {
            Debug.LogError("CameraFollow requires a Player Rigidbody and Camera.");
            enabled = false;
            return;
        }

        playerCamera.fieldOfView = normalFOV;

        transform.position = cameraPivot.TransformPoint(cameraOffset);
        transform.rotation = cameraPivot.rotation;
    }

    void LateUpdate()
    {
        if (playerRb == null || playerCamera == null)
            return;

        transform.rotation = cameraPivot.rotation;

        Vector3 start = cameraPivot.position;
        Vector3 desiredPosition = cameraPivot.TransformPoint(cameraOffset);

        Vector3 horizontalMovement = playerRb.linearVelocity;
        horizontalMovement.y = 0f;

        bool isDashing = horizontalMovement.magnitude > dashThreshold;
        bool isJumping = playerRb.linearVelocity.y > jumpThreshold;

        float horizontalTime = isDashing
            ? dashSmoothTime
            : horizontalSmoothTime;

        Vector3 targetPosition = new Vector3(
            Mathf.SmoothDamp(
                transform.position.x,
                desiredPosition.x,
                ref horizontalVelocityX,
                horizontalTime
            ),
            Mathf.SmoothDamp(
                transform.position.y,
                desiredPosition.y,
                ref verticalVelocity,
                verticalSmoothTime
            ),
            Mathf.SmoothDamp(
                transform.position.z,
                desiredPosition.z,
                ref horizontalVelocityZ,
                horizontalTime
            )
        );

        // Camera collision detection.
        Vector3 direction = targetPosition - start;
        float distance = direction.magnitude;

        if (distance > 0f &&
            Physics.SphereCast(
                start,
                cameraRadius,
                direction.normalized,
                out RaycastHit hit,
                distance,
                collisionLayers,
                QueryTriggerInteraction.Ignore))
        {
            targetPosition = start + direction.normalized *
                Mathf.Max(0f, hit.distance - wallOffset);

            ResetSmoothing();
        }

        Vector3 movement = targetPosition - transform.position;
        float movementDistance = movement.magnitude;

        if (movementDistance > 0f &&
            Physics.SphereCast(
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

            ResetSmoothing();
        }

        if (!Physics.CheckSphere(
            targetPosition,
            cameraRadius,
            collisionLayers,
            QueryTriggerInteraction.Ignore))
        {
            transform.position = targetPosition;
        }
        else
        {
            ResetSmoothing();
        }

        float targetFOV = normalFOV;

        if (isDashing)
            targetFOV = dashFOV;
        else if (isJumping)
            targetFOV = jumpFOV;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            Mathf.Clamp01(fovSpeed * Time.deltaTime)
        );
    }

    private void ResetSmoothing()
    {
        horizontalVelocityX = 0f;
        horizontalVelocityZ = 0f;
        verticalVelocity = 0f;
    }
}
