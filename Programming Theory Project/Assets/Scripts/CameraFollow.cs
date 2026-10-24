
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform cameraPivot;

    [Header("Camera Position")]
    [SerializeField]
    private Vector3 cameraOffset =
        new Vector3(7f, 5f, -5f);

    [SerializeField] private float horizontalSmoothTime = 0.1f;
    [SerializeField] private float verticalSmoothTime = 0.1f;
    [SerializeField] private float dashSmoothTime = 0.18f;

    [SerializeField] private float maxFollowDistance = 15f;
    [SerializeField] private float teleportDetectionDistance = 8f;

    [Header("Movement Detection")]
    [SerializeField] private float dashThreshold = 15f;
    [SerializeField] private float jumpThreshold = 1f;

    [Header("Field Of View")]
    [SerializeField] private float normalFOV = 90f;
    [SerializeField] private float jumpFOV = 65f;
    [SerializeField] private float dashFOV = 120f;
    [SerializeField] private float fovSpeed = 6f;

    [Header("Camera Collision")]
    [SerializeField] private bool enableCameraCollision = true;
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private float wallOffset = 0.1f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Header("Front / Back Flip Camera")]
    [SerializeField] private bool enableFlipTopView = true;
    [SerializeField] private float flipTopHeight = 7f;
    [SerializeField] private float flipTopBackDistance = 2.5f;
    [SerializeField] private float flipLookDownDistance = 3f;
    [SerializeField] private float flipTransitionInTime = 0.35f;
    [SerializeField] private float flipTransitionOutTime = 0.35f;
    [SerializeField] private float flipPositionSmoothTime = 0.08f;

    [Header("Flip Mouse Aim")]
    [Tooltip("When enabled, the mouse controls camera rotation during flips instead of locking the view onto the player.")]
    [SerializeField] private bool freeAimDuringFlips = true;

    private Camera attachedCamera;
    private Rigidbody playerRb;
    private PlayerController playerController;
    private Transform playerTransform;

    private Vector3 smoothedFollowPosition;
    private Vector3 followVelocity;

    private Vector3 previousPivotPosition;
    private bool hasPreviousPivotPosition;

    private Vector3 lastValidPosition;
    private Quaternion lastValidRotation;

    private float flipBlend;
    private float flipBlendVelocity;

    public float OverheadBlend => flipBlend;
    public bool IsOverheadActive => flipBlend > 0.001f;

    [Header("Overhead Look Orientation")]
    [Tooltip("Downward base angle for overhead view. 90 means straight down before mouse pitch is added.")]
    [SerializeField] private float overheadBasePitch = 90f;

    private CameraLook cameraLook;
    private bool initialized;
    private bool warnedInvalid;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();
        ResolvePlayerReferences();
        InitializeCamera();
    }

    private void OnEnable()
    {
        if (attachedCamera == null)
            attachedCamera = GetComponent<Camera>();

        if (cameraPivot != null)
        {
            ResolvePlayerReferences();
            InitializeCamera();
        }
    }

    private void ResolvePlayerReferences()
    {
        if (cameraPivot == null)
            return;

        cameraLook = cameraPivot.GetComponent<CameraLook>();

        playerRb =
            cameraPivot.GetComponentInParent<Rigidbody>();

        playerController =
            cameraPivot.GetComponentInParent<PlayerController>();

        if (playerRb != null)
            playerTransform = playerRb.transform;
        else if (playerController != null)
            playerTransform = playerController.transform;
        else
            playerTransform = cameraPivot.root;
    }

    private void InitializeCamera()
    {
        if (cameraPivot == null)
            return;

        Vector3 pivotPosition = cameraPivot.position;
        Quaternion pivotRotation = cameraPivot.rotation;

        if (!IsFinite(pivotPosition) ||
            !IsFinite(pivotRotation))
            return;

        smoothedFollowPosition = pivotPosition;
        followVelocity = Vector3.zero;

        previousPivotPosition = pivotPosition;
        hasPreviousPivotPosition = true;

        flipBlend = 0f;
        flipBlendVelocity = 0f;

        Vector3 initialPosition =
            smoothedFollowPosition +
            pivotRotation * cameraOffset;

        if (!IsFinite(initialPosition))
            return;

        transform.SetPositionAndRotation(
            initialPosition,
            pivotRotation
        );

        lastValidPosition = initialPosition;
        lastValidRotation = pivotRotation;

        if (attachedCamera != null)
            attachedCamera.fieldOfView = normalFOV;

        initialized = true;
    }

    private void LateUpdate()
    {
        if (cameraPivot == null)
            return;

        if (!initialized)
        {
            ResolvePlayerReferences();
            InitializeCamera();

            if (!initialized)
                return;
        }

        Vector3 pivotPosition = cameraPivot.position;
        Quaternion pivotRotation = cameraPivot.rotation;

        if (!IsFinite(pivotPosition) ||
            !IsFinite(pivotRotation))
        {
            RestoreLastValidCamera();
            return;
        }

        UpdateFlipBlend();
        UpdateSmoothedFollowPosition(pivotPosition);

        Vector3 normalPosition =
            smoothedFollowPosition +
            pivotRotation * cameraOffset;

        Quaternion normalRotation = pivotRotation;

        Vector3 desiredPosition = normalPosition;
        Quaternion desiredRotation = normalRotation;

        if (enableFlipTopView &&
            playerTransform != null &&
            flipBlend > 0.0001f)
        {
            float yaw = playerController != null
                ? playerController.MouseYaw
                : playerTransform.eulerAngles.y;

            Quaternion yawRotation =
                Quaternion.Euler(0f, yaw, 0f);

            Vector3 topPosition =
                smoothedFollowPosition +
                Vector3.up * flipTopHeight +
                yawRotation *
                (Vector3.back * flipTopBackDistance);

            desiredPosition = Vector3.Lerp(
                normalPosition,
                topPosition,
                flipBlend
            );

            float mousePitch = cameraLook != null
                ? cameraLook.Pitch
                : Mathf.DeltaAngle(
                    0f,
                    cameraPivot.localEulerAngles.x
                );

            if (freeAimDuringFlips)
            {
                desiredRotation =
                    yawRotation *
                    Quaternion.Euler(
                        mousePitch +
                        overheadBasePitch * flipBlend,
                        0f,
                        0f
                    );
            }
            else
            {
                Vector3 lookTarget =
                    playerTransform.position -
                    Vector3.up * flipLookDownDistance;

                Vector3 direction =
                    lookTarget - topPosition;

                if (direction.sqrMagnitude > 0.0001f)
                {
                    desiredRotation = Quaternion.Slerp(
                        normalRotation,
                        Quaternion.LookRotation(
                            direction,
                            Vector3.up
                        ),
                        flipBlend
                    );
                }
            }
        }

        if (!IsFinite(desiredPosition) ||
            !IsFinite(desiredRotation))
        {
            RestoreLastValidCamera();
            return;
        }

        if (enableCameraCollision)
        {
            desiredPosition = ResolveCameraCollision(
                smoothedFollowPosition,
                desiredPosition
            );
        }

        if (!IsFinite(desiredPosition))
        {
            RestoreLastValidCamera();
            return;
        }

        transform.SetPositionAndRotation(
            desiredPosition,
            desiredRotation
        );

        lastValidPosition = desiredPosition;
        lastValidRotation = desiredRotation;

        warnedInvalid = false;

        UpdateFieldOfView();
    }

    private void UpdateSmoothedFollowPosition(
        Vector3 targetPosition)
    {
        bool teleported = false;

        if (hasPreviousPivotPosition)
        {
            float pivotTravel =
                Vector3.Distance(
                    targetPosition,
                    previousPivotPosition
                );

            teleported =
                pivotTravel > teleportDetectionDistance;
        }

        previousPivotPosition = targetPosition;
        hasPreviousPivotPosition = true;

        float followDistance =
            Vector3.Distance(
                smoothedFollowPosition,
                targetPosition
            );

        if (teleported ||
            (maxFollowDistance > 0f &&
             followDistance > maxFollowDistance))
        {
            smoothedFollowPosition = targetPosition;
            followVelocity = Vector3.zero;
            return;
        }

        float horizontalTime =
            Mathf.Max(
                0.0001f,
                horizontalSmoothTime
            );

        float verticalTime =
            Mathf.Max(
                0.0001f,
                verticalSmoothTime
            );

        if (playerRb != null &&
            playerRb.linearVelocity.magnitude > dashThreshold)
        {
            horizontalTime =
                Mathf.Max(
                    0.0001f,
                    dashSmoothTime
                );
        }

        if (flipBlend > 0.001f)
        {
            float flipTime =
                Mathf.Max(
                    0.0001f,
                    flipPositionSmoothTime
                );

            horizontalTime = flipTime;
            verticalTime = flipTime;
        }

        float x = Mathf.SmoothDamp(
            smoothedFollowPosition.x,
            targetPosition.x,
            ref followVelocity.x,
            horizontalTime,
            Mathf.Infinity,
            Time.deltaTime
        );

        float y = Mathf.SmoothDamp(
            smoothedFollowPosition.y,
            targetPosition.y,
            ref followVelocity.y,
            verticalTime,
            Mathf.Infinity,
            Time.deltaTime
        );

        float z = Mathf.SmoothDamp(
            smoothedFollowPosition.z,
            targetPosition.z,
            ref followVelocity.z,
            horizontalTime,
            Mathf.Infinity,
            Time.deltaTime
        );

        Vector3 nextPosition =
            new Vector3(x, y, z);

        if (IsFinite(nextPosition))
        {
            smoothedFollowPosition = nextPosition;
        }
        else
        {
            smoothedFollowPosition = targetPosition;
            followVelocity = Vector3.zero;
        }
    }

    private void UpdateFlipBlend()
    {
        bool overheadFlip = false;

        if (enableFlipTopView &&
            playerController != null &&
            playerController.IsFlipping)
        {
            PlayerController.TrickType trick =
                playerController.CurrentTrick;

            overheadFlip =
                trick == PlayerController.TrickType.FrontFlip ||
                trick == PlayerController.TrickType.BackFlip;
        }

        float targetBlend =
            overheadFlip ? 1f : 0f;

        float smoothTime = overheadFlip
            ? flipTransitionInTime
            : flipTransitionOutTime;

        flipBlend = Mathf.SmoothDamp(
            flipBlend,
            targetBlend,
            ref flipBlendVelocity,
            Mathf.Max(0.0001f, smoothTime),
            Mathf.Infinity,
            Time.deltaTime
        );

        flipBlend = Mathf.Clamp01(flipBlend);
    }

    private Vector3 ResolveCameraCollision(
        Vector3 origin,
        Vector3 target)
    {
        Vector3 displacement =
            target - origin;

        float distance = displacement.magnitude;

        if (distance < 0.0001f)
            return target;

        Vector3 direction =
            displacement / distance;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.01f, cameraRadius),
            direction,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        float closestDistance = distance;
        bool blocked = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (playerTransform != null &&
                hit.collider.transform.IsChildOf(
                    playerTransform
                ))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                blocked = true;
            }
        }

        if (!blocked)
            return target;

        return origin +
            direction *
            Mathf.Max(
                0f,
                closestDistance - wallOffset
            );
    }

    private void UpdateFieldOfView()
    {
        if (attachedCamera == null)
            return;

        float targetFOV = normalFOV;

        if (playerRb != null)
        {
            Vector3 velocity =
                playerRb.linearVelocity;

            if (velocity.magnitude > dashThreshold)
                targetFOV = dashFOV;
            else if (Mathf.Abs(velocity.y) > jumpThreshold)
                targetFOV = jumpFOV;
        }

        float blend =
            1f - Mathf.Exp(
                -Mathf.Max(0f, fovSpeed) *
                Time.deltaTime
            );

        attachedCamera.fieldOfView = Mathf.Lerp(
            attachedCamera.fieldOfView,
            targetFOV,
            blend
        );
    }

    private void RestoreLastValidCamera()
    {
        if (!warnedInvalid)
        {
            Debug.LogWarning(
                "CameraFollow detected an invalid " +
                "camera target and restored its " +
                "last valid transform.",
                this
            );

            warnedInvalid = true;
        }

        if (IsFinite(lastValidPosition) &&
            IsFinite(lastValidRotation))
        {
            transform.SetPositionAndRotation(
                lastValidPosition,
                lastValidRotation
            );
        }

        followVelocity = Vector3.zero;
    }

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
}
