
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CameraLook cameraLook;

    [Header("Normal Third Person Camera")]
    [SerializeField]
    private Vector3 cameraOffset =
        new Vector3(7f, 5f, -5f);

    [SerializeField] private float followSmoothTime = 0.08f;
    [SerializeField] private float maximumFollowDistance = 15f;

    [Header("Aim Tracking")]
    [SerializeField] private bool rotateCameraTowardAimPoint = true;
    [SerializeField] private float aimRotationSpeed = 18f;

    [Header("Cartwheel Camera - Existing Behavior")]
    [SerializeField] private bool rollWithCartwheel = true;

    [Range(0f, 1f)]
    [SerializeField] private float rollAmount = 1f;

    [Header("First Person Somersault Camera")]
    [SerializeField] private bool enableFirstPersonFlips = true;

    [SerializeField]
    private Vector3 eyeOffset =
        new Vector3(0f, 1.65f, 0.12f);

    [Tooltip("Optional visual-model head or eye anchor.")]
    [SerializeField] private Transform headTransform;

    [Range(0.01f, 0.45f)]
    [SerializeField] private float enterFirstPersonAt = 0.18f;

    [Range(0.55f, 0.99f)]
    [SerializeField] private float exitFirstPersonAt = 0.82f;

    [SerializeField] private float flipCameraPitchOffset = 0f;

    [Header("Camera Collision")]
    [SerializeField] private bool enableCameraCollision = true;
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private float wallOffset = 0.1f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Header("Field Of View")]
    [SerializeField] private float normalFOV = 90f;
    [SerializeField] private float firstPersonFlipFOV = 100f;
    [SerializeField] private float jumpFOV = 65f;
    [SerializeField] private float dashFOV = 120f;
    [SerializeField] private float dashThreshold = 15f;
    [SerializeField] private float jumpThreshold = 1f;
    [SerializeField] private float fovSpeed = 6f;

    private Camera attachedCamera;
    private Rigidbody playerRb;

    private Vector3 smoothedPivotPosition;
    private Vector3 followVelocity;

    private Quaternion unrolledRotation;
    private Vector3 unrolledCameraPosition;

    private bool initialized;

    public Camera AttachedCamera => attachedCamera;

    private void Awake()
    {
        attachedCamera = GetComponent<Camera>();

        ResolveReferences();
        InitializeCamera();
    }

    private void ResolveReferences()
    {
        if (playerController == null && cameraPivot != null)
        {
            playerController =
                cameraPivot.GetComponentInParent<PlayerController>();
        }

        if (cameraLook == null && cameraPivot != null)
        {
            cameraLook =
                cameraPivot.GetComponent<CameraLook>();
        }

        if (playerController == null)
        {
            playerController =
                FindAnyObjectByType<PlayerController>();
        }

        if (cameraLook == null)
        {
            cameraLook =
                FindAnyObjectByType<CameraLook>();
        }

        if (playerRb == null && playerController != null)
        {
            playerRb =
                playerController.GetComponent<Rigidbody>();
        }
    }

    private void InitializeCamera()
    {
        if (cameraPivot == null)
        {
            Debug.LogWarning(
                "CameraFollow: Camera Pivot is not assigned.",
                this
            );
            return;
        }

        smoothedPivotPosition = cameraPivot.position;

        unrolledRotation = cameraLook != null
            ? cameraLook.AimRotation
            : cameraPivot.rotation;

        unrolledCameraPosition =
            smoothedPivotPosition +
            unrolledRotation * cameraOffset;

        transform.SetPositionAndRotation(
            unrolledCameraPosition,
            unrolledRotation
        );

        initialized = true;
    }

    public Vector3 GetUnrolledAimOrigin()
    {
        return initialized
            ? unrolledCameraPosition
            : transform.position;
    }

    private bool IsVerticalFlip()
    {
        return playerController != null &&
               playerController.IsVerticalFlipping;
    }

    private float GetFirstPersonBlend()
    {
        if (!enableFirstPersonFlips || !IsVerticalFlip())
            return 0f;

        float progress = playerController.FlipProgress;

        float enter = Mathf.Clamp(
            enterFirstPersonAt, 0.01f, 0.45f
        );

        float exit = Mathf.Clamp(
            exitFirstPersonAt, 0.55f, 0.99f
        );

        if (progress < enter)
            return Smooth01(progress / enter);

        if (progress > exit)
        {
            return 1f - Smooth01(
                (progress - exit) / (1f - exit)
            );
        }

        return 1f;
    }

    private Vector3 GetEyePosition()
    {
        if (headTransform != null)
            return headTransform.position;

        if (playerController == null)
            return cameraPivot.position;

        // Eye anchor relative to the rotating body.
        // This rotates the eye position around the
        // character's root during the somersault.
        return playerController.transform.position +
            playerController.FlipBodyRotation * eyeOffset;
    }

    private Quaternion GetFirstPersonRotation(
        Quaternion aimRotation)
    {
        if (playerController == null)
            return aimRotation;

        // Aim pitch remains independently controlled.
        // The player supplies the actual somersault
        // and the inverted 180-degree Y-axis turn.
        float pitch = NormalizeAngle(
            aimRotation.eulerAngles.x
        );

        Quaternion pitchRotation =
            Quaternion.Euler(pitch, 0f, 0f);

        Quaternion extraPitch =
            Quaternion.Euler(
                flipCameraPitchOffset, 0f, 0f
            );

        return playerController.FlipBodyRotation *
               pitchRotation *
               extraPitch;
    }

    private void LateUpdate()
    {
        if (cameraPivot == null)
            return;

        if (!initialized)
        {
            ResolveReferences();
            InitializeCamera();

            if (!initialized)
                return;
        }

        Vector3 pivotPosition = cameraPivot.position;

        if (Vector3.Distance(
            smoothedPivotPosition,
            pivotPosition
        ) > maximumFollowDistance)
        {
            smoothedPivotPosition = pivotPosition;
            followVelocity = Vector3.zero;
        }
        else
        {
            smoothedPivotPosition = Vector3.SmoothDamp(
                smoothedPivotPosition,
                pivotPosition,
                ref followVelocity,
                Mathf.Max(0.001f, followSmoothTime)
            );
        }

        Quaternion baseRotation = cameraLook != null
            ? cameraLook.AimRotation
            : cameraPivot.rotation;

        // Normal third-person position.
        Vector3 normalPosition =
            smoothedPivotPosition +
            baseRotation * cameraOffset;

        Quaternion normalRotation = baseRotation;

        if (rotateCameraTowardAimPoint &&
            cameraLook != null)
        {
            Vector3 aimDirection =
                cameraLook.AimPoint - normalPosition;

            if (aimDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(
                        aimDirection.normalized,
                        Vector3.up
                    );

                normalRotation = Quaternion.Slerp(
                    unrolledRotation,
                    targetRotation,
                    1f - Mathf.Exp(
                        -aimRotationSpeed * Time.deltaTime
                    )
                );
            }
        }

        Vector3 finalPosition = normalPosition;
        Quaternion finalRotation = normalRotation;

        bool verticalFlip = IsVerticalFlip();

        if (verticalFlip && enableFirstPersonFlips)
        {
            float blend = GetFirstPersonBlend();

            Vector3 eyePosition = GetEyePosition();

            Quaternion firstPersonRotation =
                GetFirstPersonRotation(baseRotation);

            // Move into the character's eyes.
            finalPosition = Vector3.Lerp(
                normalPosition,
                eyePosition,
                blend
            );

            // Rotate with the entire body maneuver:
            // somersault + inverted Y-axis half-turn.
            finalRotation = Quaternion.Slerp(
                normalRotation,
                firstPersonRotation,
                blend
            );
        }
        else if (playerController != null &&
                 playerController.IsCartwheeling &&
                 rollWithCartwheel)
        {
            // Preserve existing cartwheel roll.
            finalRotation *= Quaternion.Euler(
                0f,
                0f,
                playerController.FlipAngle * rollAmount
            );
        }

        // Independent aim reference for CameraLook.
        // Do not apply the flip rotation here.
        unrolledCameraPosition =
            verticalFlip && enableFirstPersonFlips
            ? GetEyePosition()
            : normalPosition;

        unrolledRotation = normalRotation;

        // Keep first-person flips from being pushed
        // away from the head by camera collision.
        if (enableCameraCollision &&
            (!verticalFlip || !enableFirstPersonFlips))
        {
            finalPosition = ResolveCollision(
                smoothedPivotPosition,
                finalPosition
            );
        }

        transform.SetPositionAndRotation(
            finalPosition,
            finalRotation
        );

        UpdateFOV(verticalFlip);
    }

    private Vector3 ResolveCollision(
        Vector3 origin,
        Vector3 target)
    {
        Vector3 displacement = target - origin;
        float distance = displacement.magnitude;

        if (distance < 0.001f)
            return target;

        Vector3 direction = displacement / distance;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.01f, cameraRadius),
            direction,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearest = distance;
        bool blocked = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (playerController != null &&
                hit.collider.transform.IsChildOf(
                    playerController.transform
                ))
                continue;

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                blocked = true;
            }
        }

        if (!blocked)
            return target;

        return origin +
            direction *
            Mathf.Max(0f, nearest - wallOffset);
    }

    private void UpdateFOV(bool verticalFlip)
    {
        if (attachedCamera == null)
            return;

        float targetFOV = normalFOV;

        if (verticalFlip && enableFirstPersonFlips)
        {
            targetFOV = Mathf.Lerp(
                normalFOV,
                firstPersonFlipFOV,
                GetFirstPersonBlend()
            );
        }
        else if (playerRb != null)
        {
            Vector3 velocity = playerRb.linearVelocity;

            if (velocity.magnitude > dashThreshold)
                targetFOV = dashFOV;
            else if (Mathf.Abs(velocity.y) > jumpThreshold)
                targetFOV = jumpFOV;
        }

        attachedCamera.fieldOfView = Mathf.Lerp(
            attachedCamera.fieldOfView,
            targetFOV,
            1f - Mathf.Exp(
                -fovSpeed * Time.deltaTime
            )
        );
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private static float NormalizeAngle(float angle)
    {
        return Mathf.DeltaAngle(0f, angle);
    }
}
