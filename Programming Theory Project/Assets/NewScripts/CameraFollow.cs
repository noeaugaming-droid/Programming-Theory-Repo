using UnityEngine;

// Put this on Main Camera, which is NOT a child of the player.
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
        if (cameraPivot == null)
        {
            Debug.LogError("Assign Camera Pivot on CameraFollow.");
            enabled = false;
            return;
        }

        playerRb = cameraPivot.GetComponentInParent<Rigidbody>();
        playerCamera = GetComponent<Camera>();
        if (playerCamera != null) playerCamera.fieldOfView = normalFOV;
    }

    void LateUpdate()
    {
        if (playerRb == null || playerCamera == null) return;

        transform.rotation = cameraPivot.rotation;
        Vector3 start = cameraPivot.position;
        Vector3 desiredPosition = cameraPivot.TransformPoint(cameraOffset);

        Vector3 horizontalVelocity = playerRb.linearVelocity;
        horizontalVelocity.y = 0f;
        bool isDashing = horizontalVelocity.magnitude > dashThreshold;
        bool isJumping = playerRb.linearVelocity.y > jumpThreshold;
        float currentSpeed = isDashing ? dashFollowSpeed : followSpeed;

        Vector3 targetPosition = transform.position;
        targetPosition.x = Mathf.Lerp(transform.position.x, desiredPosition.x,
            currentSpeed * Time.deltaTime);
        targetPosition.z = Mathf.Lerp(transform.position.z, desiredPosition.z,
            currentSpeed * Time.deltaTime);
        targetPosition.y = Mathf.Lerp(transform.position.y, desiredPosition.y,
            verticalFollowSpeed * Time.deltaTime);

        // Move the camera closer if a wall blocks the view of the player.
        Vector3 direction = targetPosition - start;
        float distance = direction.magnitude;
        if (distance > 0f && Physics.SphereCast(start, cameraRadius,
            direction.normalized, out RaycastHit hit, distance,
            collisionLayers, QueryTriggerInteraction.Ignore))
        {
            targetPosition = start + direction.normalized *
                Mathf.Max(0f, hit.distance - wallOffset);
        }

        // Check the path from the camera's current position too.
        Vector3 movement = targetPosition - transform.position;
        float movementDistance = movement.magnitude;
        if (movementDistance > 0f && Physics.SphereCast(transform.position,
            cameraRadius, movement.normalized, out RaycastHit movementHit,
            movementDistance, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            targetPosition = transform.position + movement.normalized *
                Mathf.Max(0f, movementHit.distance - wallOffset);
        }

        if (!Physics.CheckSphere(targetPosition, cameraRadius,
            collisionLayers, QueryTriggerInteraction.Ignore))
        {
            transform.position = targetPosition;
        }

        float targetFOV = normalFOV;
        if (isDashing) targetFOV = dashFOV;
        else if (isJumping) targetFOV = jumpFOV;

        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView,
            targetFOV, fovSpeed * Time.deltaTime);
    }
}
