
using UnityEngine;

[DefaultExecutionOrder(100)]
public class CameraFollow : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField]
    private Vector3 cameraOffset =
        new Vector3(0f, 1f, -5f);

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

    [Header("Front / Back Flip Top View")]
    [SerializeField] private bool enableFlipTopView = true;

    [Tooltip("How high the camera moves above the player.")]
    [SerializeField] private float flipTopHeight = 7f;

    [Tooltip("How far behind the player the camera stays.")]
    [SerializeField] private float flipTopBackDistance = 2.5f;

    [Tooltip("How far below the player the camera aims.")]
    [SerializeField] private float flipLookDownDistance = 3f;

    [Tooltip("Time to transition into the overhead view.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float flipTransitionInTime = 0.35f;

    [Tooltip("Time to transition back to normal view.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float flipTransitionOutTime = 0.35f;

    [Tooltip("Camera position smoothing while flipping.")]
    [Range(0.01f, 0.3f)]
    [SerializeField] private float flipPositionSmoothTime = 0.08f;

    [Tooltip("Camera rotation smoothing speed.")]
    [SerializeField] private float flipRotationSpeed = 10f;

    private Rigidbody playerRb;
    private PlayerController playerController;
    private Camera playerCamera;

    private float horizontalVelocityX;
    private float horizontalVelocityZ;
    private float verticalVelocity;

    private Vector3 cameraVelocity;

    private float flipBlend;
    private float flipBlendVelocity;

    private void Start()
    {
        if (cameraPivot == null)
        {
            Debug.LogError(
                "CameraFollow: Assign Camera Pivot."
            );

            enabled = false;
            return;
        }

        playerRb =
            cameraPivot.GetComponentInParent<Rigidbody>();

        playerController =
            cameraPivot.GetComponentInParent<PlayerController>();

        playerCamera = GetComponent<Camera>();

        if (playerRb == null || playerCamera == null)
        {
            Debug.LogError(
                "CameraFollow requires a Player Rigidbody and Camera."
            );

            enabled = false;
            return;
        }

        playerCamera.fieldOfView = normalFOV;

        transform.position =
            cameraPivot.TransformPoint(cameraOffset);

        transform.rotation = cameraPivot.rotation;
    }

    private void LateUpdate()
    {
        if (playerRb == null || playerCamera == null)
            return;

        // Only front and back flips activate top view.
        bool frontOrBackFlip = false;

        if (enableFlipTopView &&
            playerController != null &&
            playerController.IsFlipping)
        {
            frontOrBackFlip =
                playerController.CurrentTrick ==
                    PlayerController.TrickType.FrontFlip ||
                playerController.CurrentTrick ==
                    PlayerController.TrickType.BackFlip;
        }

        // Gradually enter or exit the top view.
        float targetBlend = frontOrBackFlip ? 1f : 0f;

        float transitionTime = frontOrBackFlip
            ? flipTransitionInTime
            : flipTransitionOutTime;

        flipBlend = Mathf.SmoothDamp(
            flipBlend,
            targetBlend,
            ref flipBlendVelocity,
            transitionTime
        );

        flipBlend = Mathf.Clamp01(flipBlend);

        // Normal third-person camera target.
        Vector3 normalPosition =
            cameraPivot.TransformPoint(cameraOffset);

        Quaternion normalRotation =
            cameraPivot.rotation;

        // Top-view position stays relative to player yaw,
        // not the PlayerVisual's flipping rotation.
        Transform playerTransform = playerRb.transform;

        Vector3 topPosition =
            cameraPivot.position +
            Vector3.up * flipTopHeight -
            playerTransform.forward * flipTopBackDistance;

        // Aim below the player so enemies on the ground
        // remain visible.
        Vector3 topLookTarget =
            playerTransform.position -
            Vector3.up * flipLookDownDistance;

        Vector3 topLookDirection =
            topLookTarget - topPosition;

        Quaternion topRotation = normalRotation;

        if (topLookDirection.sqrMagnitude > 0.0001f)
        {
            topRotation = Quaternion.LookRotation(
                topLookDirection.normalized,
                Vector3.up
            );
        }

        // Blend camera position and orientation.
        Vector3 desiredPosition = Vector3.Lerp(
            normalPosition,
            topPosition,
            flipBlend
        );

        Quaternion desiredRotation = Quaternion.Slerp(
            normalRotation,
            topRotation,
            flipBlend
        );

        // Existing dash / jump effects.
        Vector3 horizontalMovement =
            playerRb.linearVelocity;

        horizontalMovement.y = 0f;

        bool isDashing =
            horizontalMovement.magnitude > dashThreshold;

        bool isJumping =
            playerRb.linearVelocity.y > jumpThreshold;

        float horizontalTime = isDashing
            ? dashSmoothTime
            : horizontalSmoothTime;

        // Normal follow smoothing when not in top view.
        Vector3 normalSmoothedPosition = new Vector3(
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

        // Faster response in overhead mode.
        Vector3 flipSmoothedPosition =
            Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref cameraVelocity,
                flipPositionSmoothTime
            );

        Vector3 targetPosition = Vector3.Lerp(
            normalSmoothedPosition,
            flipSmoothedPosition,
            flipBlend
        );

        // Collision between player and camera.
        Vector3 start = cameraPivot.position;
        Vector3 direction = targetPosition - start;
        float distance = direction.magnitude;

        if (distance > 0.001f &&
            Physics.SphereCast(
                start,
                cameraRadius,
                direction.normalized,
                out RaycastHit hit,
                distance,
                collisionLayers,
                QueryTriggerInteraction.Ignore))
        {
            targetPosition =
                start +
                direction.normalized *
                Mathf.Max(
                    0f,
                    hit.distance - wallOffset
                );

            ResetSmoothing();
        }

        // Prevent camera from passing through walls.
        Vector3 movement =
            targetPosition - transform.position;

        float movementDistance = movement.magnitude;

        if (movementDistance > 0.001f &&
            Physics.SphereCast(
                transform.position,
                cameraRadius,
                movement.normalized,
                out RaycastHit movementHit,
                movementDistance,
                collisionLayers,
                QueryTriggerInteraction.Ignore))
        {
            targetPosition =
                transform.position +
                movement.normalized *
                Mathf.Max(
                    0f,
                    movementHit.distance - wallOffset
                );

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

        // Smooth camera rotation.
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            Mathf.Clamp01(
                flipRotationSpeed * Time.deltaTime
            )
        );

        // Field of view.
        float targetFOV = normalFOV;

        if (isDashing)
        {
            targetFOV = dashFOV;
        }
        else if (isJumping || frontOrBackFlip)
        {
            targetFOV = jumpFOV;
        }

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
        cameraVelocity = Vector3.zero;
    }
}
