
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField]
    private Vector3 cameraOffset =
        new Vector3(0f, 1f, -5f);

    [Header("Follow Smoothing")]
    [Range(0.01f, 0.5f)]
    [SerializeField] private float horizontalSmoothTime = 0.035f;
    [Range(0.01f, 0.5f)]
    [SerializeField] private float verticalSmoothTime = 0.06f;
    [Range(0.01f, 0.5f)]
    [SerializeField] private float dashSmoothTime = 0.10f;

    [Header("Automatic Recovery")]
    [SerializeField] private float maxFollowDistance = 15f;
    [SerializeField] private float teleportDetectionDistance = 8f;

    [Header("Movement Effects")]
    [SerializeField] private float dashThreshold = 15f;
    [SerializeField] private float jumpThreshold = 1f;

    [Header("Field of View")]
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float jumpFOV = 65f;
    [SerializeField] private float dashFOV = 75f;
    [SerializeField] private float fovSpeed = 6f;

    [Header("Collision")]
    [SerializeField] private bool enableCameraCollision = true;
    [SerializeField] private float cameraRadius = 0.25f;
    [SerializeField] private float wallOffset = 0.1f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Header("Front / Back Flip Top View")]
    [SerializeField] private bool enableFlipTopView = true;
    [SerializeField] private float flipTopHeight = 7f;
    [SerializeField] private float flipTopBackDistance = 2.5f;
    [SerializeField] private float flipLookDownDistance = 3f;

    [Range(0.05f, 1f)]
    [SerializeField] private float flipTransitionInTime = 0.35f;

    [Range(0.05f, 1f)]
    [SerializeField] private float flipTransitionOutTime = 0.35f;

    [Range(0.01f, 0.3f)]
    [SerializeField] private float flipPositionSmoothTime = 0.04f;

    private Rigidbody playerRb;
    private PlayerController playerController;
    private Camera playerCamera;

    private Vector3 smoothedPosition;
    private Vector3 positionVelocity;

    private float flipBlend;
    private float flipBlendVelocity;

    private Vector3 lastPivotPosition;
    private Vector3 lastValidCameraPosition;
    private Quaternion lastValidCameraRotation;

    private bool initialized;
    private bool warnedInvalidTarget;

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z);
    }

    private static bool IsFinite(Quaternion value)
    {
        return IsFinite(value.x) &&
               IsFinite(value.y) &&
               IsFinite(value.z) &&
               IsFinite(value.w);
    }

    private void Awake()
    {
        playerCamera = GetComponent<Camera>();
    }

    private void Start()
    {
        InitializeCamera();
    }

    private void OnEnable()
    {
        initialized = false;
        warnedInvalidTarget = false;
        positionVelocity = Vector3.zero;
        flipBlendVelocity = 0f;
    }

    private void InitializeCamera()
    {
        if (cameraPivot == null)
        {
            Debug.LogError(
                "CameraFollow: Assign Camera Pivot."
            );
            return;
        }

        playerRb =
            cameraPivot.GetComponentInParent<Rigidbody>();

        playerController =
            cameraPivot.GetComponentInParent<PlayerController>();

        if (playerRb == null)
        {
            Debug.LogError(
                "CameraFollow: Player Rigidbody missing."
            );
            return;
        }

        if (playerCamera == null)
            playerCamera = GetComponent<Camera>();

        Vector3 pivotPosition = cameraPivot.position;

        if (!IsFinite(pivotPosition) ||
            !IsFinite(cameraPivot.rotation))
        {
            Debug.LogError(
                "CameraFollow: Player CameraPivot has " +
                "invalid position or rotation."
            );
            return;
        }

        Vector3 desiredPosition =
            cameraPivot.TransformPoint(cameraOffset);

        if (!IsFinite(desiredPosition))
        {
            Debug.LogError(
                "CameraFollow: Invalid Camera Offset or " +
                "CameraPivot transform."
            );
            return;
        }

        positionVelocity = Vector3.zero;
        flipBlend = 0f;
        flipBlendVelocity = 0f;

        lastPivotPosition = pivotPosition;

        smoothedPosition = desiredPosition;

        Vector3 safePosition = GetSafeCameraPosition(
            pivotPosition,
            desiredPosition
        );

        if (!IsFinite(safePosition))
            safePosition = desiredPosition;

        transform.position = safePosition;
        transform.rotation = cameraPivot.rotation;

        lastValidCameraPosition = safePosition;
        lastValidCameraRotation = cameraPivot.rotation;

        playerCamera.fieldOfView = normalFOV;

        warnedInvalidTarget = false;
        initialized = true;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            InitializeCamera();

            if (!initialized)
                return;
        }

        if (cameraPivot == null || playerRb == null)
        {
            initialized = false;
            return;
        }

        Vector3 pivotPosition = cameraPivot.position;

        // Reject invalid player transforms before doing
        // any camera position calculations.
        if (!IsFinite(pivotPosition) ||
            !IsFinite(cameraPivot.rotation) ||
            !IsFinite(playerRb.transform.position) ||
            !IsFinite(playerRb.transform.rotation))
        {
            RecoverFromInvalidTarget();
            return;
        }

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

        float targetBlend = frontOrBackFlip ? 1f : 0f;

        float transitionTime = frontOrBackFlip
            ? flipTransitionInTime
            : flipTransitionOutTime;

        transitionTime = Mathf.Max(
            0.01f,
            transitionTime
        );

        // Reset any corrupted smoothing state.
        if (!IsFinite(flipBlend) ||
            !IsFinite(flipBlendVelocity))
        {
            flipBlend = 0f;
            flipBlendVelocity = 0f;
        }

        flipBlend = Mathf.SmoothDamp(
            flipBlend,
            targetBlend,
            ref flipBlendVelocity,
            transitionTime
        );

        if (!IsFinite(flipBlend) ||
            !IsFinite(flipBlendVelocity))
        {
            flipBlend = targetBlend;
            flipBlendVelocity = 0f;
        }

        flipBlend = Mathf.Clamp01(flipBlend);

        // NORMAL CAMERA
        Vector3 normalPosition =
            cameraPivot.TransformPoint(cameraOffset);

        Quaternion normalRotation =
            cameraPivot.rotation;

        // FRONT/BACK FLIP OVERHEAD CAMERA
        Transform playerTransform = playerRb.transform;

        Vector3 topPosition =
            pivotPosition +
            Vector3.up * flipTopHeight -
            playerTransform.forward * flipTopBackDistance;

        Vector3 topLookTarget =
            playerTransform.position -
            Vector3.up * flipLookDownDistance;

        Vector3 topDirection =
            topLookTarget - topPosition;

        Quaternion topRotation = normalRotation;

        if (IsFinite(topDirection) &&
            topDirection.sqrMagnitude > 0.0001f)
        {
            topRotation = Quaternion.LookRotation(
                topDirection,
                Vector3.up
            );
        }

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

        if (!IsFinite(desiredPosition) ||
            !IsFinite(desiredRotation))
        {
            RecoverFromInvalidTarget();
            return;
        }

        warnedInvalidTarget = false;

        // Detect teleports and large camera separations.
        bool teleported =
            Vector3.Distance(
                pivotPosition,
                lastPivotPosition
            ) > Mathf.Max(0.1f, teleportDetectionDistance);

        lastPivotPosition = pivotPosition;

        bool tooFar =
            !IsFinite(transform.position) ||
            Vector3.Distance(
                transform.position,
                desiredPosition
            ) > Mathf.Max(1f, maxFollowDistance);

        if (teleported || tooFar)
        {
            SnapToTarget(
                pivotPosition,
                desiredPosition,
                desiredRotation
            );

            UpdateFOV(frontOrBackFlip);
            return;
        }

        // Check smoothing state before SmoothDamp.
        if (!IsFinite(smoothedPosition) ||
            !IsFinite(positionVelocity))
        {
            smoothedPosition = desiredPosition;
            positionVelocity = Vector3.zero;
        }

        Vector3 horizontalMovement =
            playerRb.linearVelocity;

        horizontalMovement.y = 0f;

        bool isDashing =
            IsFinite(horizontalMovement) &&
            horizontalMovement.magnitude > dashThreshold;

        float followTime = isDashing
            ? dashSmoothTime
            : horizontalSmoothTime;

        followTime = Mathf.Lerp(
            Mathf.Max(0.01f, followTime),
            Mathf.Max(0.01f, flipPositionSmoothTime),
            flipBlend
        );

        float verticalTime = Mathf.Lerp(
            Mathf.Max(0.01f, verticalSmoothTime),
            Mathf.Max(0.01f, flipPositionSmoothTime),
            flipBlend
        );

        // One consistent smoothing velocity avoids
        // competing SmoothDamp calculations.
        Vector3 nextPosition = new Vector3(
            Mathf.SmoothDamp(
                smoothedPosition.x,
                desiredPosition.x,
                ref positionVelocity.x,
                followTime
            ),
            Mathf.SmoothDamp(
                smoothedPosition.y,
                desiredPosition.y,
                ref positionVelocity.y,
                verticalTime
            ),
            Mathf.SmoothDamp(
                smoothedPosition.z,
                desiredPosition.z,
                ref positionVelocity.z,
                followTime
            )
        );

        // Never allow NaN into the Transform.
        if (!IsFinite(nextPosition) ||
            !IsFinite(positionVelocity))
        {
            smoothedPosition = desiredPosition;
            positionVelocity = Vector3.zero;
            nextPosition = desiredPosition;
        }

        smoothedPosition = nextPosition;

        Vector3 safePosition = GetSafeCameraPosition(
            pivotPosition,
            nextPosition
        );

        if (!IsFinite(safePosition))
        {
            SnapToTarget(
                pivotPosition,
                desiredPosition,
                desiredRotation
            );

            UpdateFOV(frontOrBackFlip);
            return;
        }

        transform.position = safePosition;

        // Normal mouse aiming has no rotation smoothing.
        // Flip transitions remain smooth through flipBlend.
        transform.rotation = desiredRotation;

        lastValidCameraPosition = safePosition;
        lastValidCameraRotation = desiredRotation;

        UpdateFOV(frontOrBackFlip);
    }

    private Vector3 GetSafeCameraPosition(
        Vector3 origin,
        Vector3 desiredPosition
    )
    {
        if (!IsFinite(origin) ||
            !IsFinite(desiredPosition))
        {
            return lastValidCameraPosition;
        }

        if (!enableCameraCollision)
            return desiredPosition;

        Vector3 direction = desiredPosition - origin;

        float distance = direction.magnitude;

        if (!IsFinite(distance) || distance < 0.001f)
            return desiredPosition;

        direction /= distance;

        float radius = Mathf.Max(0.01f, cameraRadius);

        if (Physics.SphereCast(
            origin,
            radius,
            direction,
            out RaycastHit hit,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore))
        {
            float safeDistance = Mathf.Max(
                0f,
                hit.distance - Mathf.Max(0f, wallOffset)
            );

            if (IsFinite(safeDistance))
                return origin + direction * safeDistance;
        }

        return desiredPosition;
    }

    private void SnapToTarget(
        Vector3 origin,
        Vector3 desiredPosition,
        Quaternion desiredRotation
    )
    {
        positionVelocity = Vector3.zero;
        smoothedPosition = desiredPosition;

        Vector3 safePosition = GetSafeCameraPosition(
            origin,
            desiredPosition
        );

        if (!IsFinite(safePosition))
            safePosition = lastValidCameraPosition;

        if (IsFinite(safePosition))
        {
            transform.position = safePosition;
            lastValidCameraPosition = safePosition;
        }

        if (IsFinite(desiredRotation))
        {
            transform.rotation = desiredRotation;
            lastValidCameraRotation = desiredRotation;
        }
    }

    private void RecoverFromInvalidTarget()
    {
        positionVelocity = Vector3.zero;
        flipBlendVelocity = 0f;
        flipBlend = 0f;

        if (IsFinite(lastValidCameraPosition))
            transform.position = lastValidCameraPosition;

        if (IsFinite(lastValidCameraRotation))
            transform.rotation = lastValidCameraRotation;

        if (!warnedInvalidTarget)
        {
            Debug.LogWarning(
                "CameraFollow: Invalid camera target detected. " +
                "Camera held at its last valid position. " +
                "Check Player, CameraPivot, and camera settings."
            );

            warnedInvalidTarget = true;
        }
    }

    private void UpdateFOV(bool frontOrBackFlip)
    {
        if (playerCamera == null)
            return;

        Vector3 velocity = playerRb.linearVelocity;

        bool validVelocity = IsFinite(velocity);

        bool isDashing = false;
        bool isJumping = false;

        if (validVelocity)
        {
            Vector3 horizontal = velocity;
            horizontal.y = 0f;

            isDashing =
                horizontal.magnitude > dashThreshold;

            isJumping =
                velocity.y > jumpThreshold;
        }

        float targetFOV = normalFOV;

        if (isDashing)
            targetFOV = dashFOV;
        else if (isJumping || frontOrBackFlip)
            targetFOV = jumpFOV;

        if (!IsFinite(playerCamera.fieldOfView))
            playerCamera.fieldOfView = normalFOV;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            Mathf.Clamp01(
                Mathf.Max(0f, fovSpeed) * Time.deltaTime
            )
        );
    }
}
