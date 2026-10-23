
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Transform playerVisual;

    [Header("Normal Camera")]
    [SerializeField]
    private Vector3 cameraOffset =
        new Vector3(7f, 5f, -5f);

    [SerializeField] private float horizontalSmoothTime = 0.1f;
    [SerializeField] private float verticalSmoothTime = 0.1f;

    [Header("Flip Target Tracking")]
    [SerializeField] private bool enableFlipTracking = true;
    [SerializeField] private float trackingRange = 150f;
    [SerializeField] private bool allowMouseRetargeting = true;

    [Header("Gradual Camera Rotation - Flip Only")]
    [SerializeField] private float flipRotationSmoothTime = 0.4f;
    [SerializeField] private float maxFlipRotationSpeed = 540f;
    [SerializeField] private float crosshairFreedomRadius = 100f;

    [Header("Flip Camera Movement")]
    [SerializeField] private float flipCameraLift = 0.65f;
    [SerializeField] private float flipCameraForwardTravel = 0.35f;
    [SerializeField] private float flipSmoothTime = 0.06f;

    [Header("Gradual Player Rotation")]
    [Range(0f, 1f)]
    [SerializeField] private float bodyFlipStrength = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float targetFacingStrength = 1f;

    [Tooltip("How quickly the character can turn toward the target.")]
    [SerializeField] private float bodyTurnSpeed = 540f;

    [Header("Camera Collision")]
    [SerializeField] private bool enableCameraCollision = true;
    [SerializeField] private float cameraRadius = 0.3f;
    [SerializeField] private float wallOffset = 0.1f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Header("FOV")]
    [SerializeField] private float normalFOV = 90f;
    [SerializeField] private float jumpFOV = 65f;
    [SerializeField] private float dashFOV = 120f;
    [SerializeField] private float dashThreshold = 15f;
    [SerializeField] private float jumpThreshold = 1f;
    [SerializeField] private float fovSpeed = 6f;

    private Camera cam;
    private PlayerController controller;
    private CameraLook cameraLook;
    private Rigidbody playerRb;
    private Transform playerRoot;

    private Vector3 followPosition;
    private Vector3 followVelocity;

    private Quaternion originalVisualLocalRotation;

    private Vector3 lockedHitPoint;

    private bool flipTrackingActive;

    private float previousMouseYaw;
    private float previousMousePitch;

    private Quaternion flipStartFacing;
    private Quaternion currentBodyFacing;

    private float rotationSpeedVelocity;
    private float currentRotationSpeed;

    public bool IsTrackingFlip => flipTrackingActive;

    public bool IsOverheadActive => false;
    public float OverheadBlend => 0f;

    public Vector3 LockedHitPoint => lockedHitPoint;

    public float CrosshairFreedomRadius =>
        crosshairFreedomRadius;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        ResolveReferences();
    }

    private void Start()
    {
        if (cameraPivot == null)
        {
            Debug.LogError(
                "CameraFollow: Camera Pivot is missing.",
                this
            );

            enabled = false;
            return;
        }

        followPosition = cameraPivot.position;

        transform.SetPositionAndRotation(
            followPosition +
                cameraPivot.rotation * cameraOffset,
            cameraPivot.rotation
        );

        cam.fieldOfView = normalFOV;
    }

    private void ResolveReferences()
    {
        if (cameraPivot == null)
            return;

        controller =
            cameraPivot.GetComponentInParent<PlayerController>();

        cameraLook =
            cameraPivot.GetComponent<CameraLook>();

        playerRb =
            cameraPivot.GetComponentInParent<Rigidbody>();

        if (controller != null)
        {
            playerRoot = controller.transform;

            if (playerVisual == null)
                playerVisual = controller.VisualModel;
        }
        else
        {
            playerRoot = cameraPivot.root;
        }

        if (playerVisual != null)
        {
            originalVisualLocalRotation =
                playerVisual.localRotation;
        }
    }

    private void LateUpdate()
    {
        if (cameraPivot == null)
            return;

        bool flipping =
            enableFlipTracking &&
            controller != null &&
            controller.IsFlipping;

        if (flipping && !flipTrackingActive)
        {
            BeginFlipTracking();
        }

        UpdateFollowPosition(flipping);

        if (flipping)
        {
            UpdateMouseRetargeting();
            UpdateSoftFlipCamera();
            UpdatePlayerVisual();
        }
        else
        {
            if (flipTrackingActive)
            {
                EndFlipTracking();
            }

            UpdateNormalCamera();
        }

        UpdateFOV();
    }

    // ==================================================
    // TARGET CAPTURE
    // ==================================================

    private void BeginFlipTracking()
    {
        Ray ray = new Ray(
            transform.position,
            transform.forward
        );

        lockedHitPoint = FindRaycastTarget(ray);

        flipTrackingActive = true;

        previousMouseYaw = controller.MouseYaw;

        previousMousePitch =
            cameraLook != null
            ? cameraLook.Pitch
            : 0f;

        currentRotationSpeed = 0f;
        rotationSpeedVelocity = 0f;

        if (playerVisual != null)
        {
            flipStartFacing =
                playerVisual.rotation *
                Quaternion.Inverse(
                    originalVisualLocalRotation
                );

            currentBodyFacing = flipStartFacing;
        }
        else
        {
            flipStartFacing =
                controller.transform.rotation;

            currentBodyFacing = flipStartFacing;
        }
    }

    private Vector3 FindRaycastTarget(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            trackingRange,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        float nearest = trackingRange;

        Vector3 point =
            ray.GetPoint(trackingRange);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (playerRoot != null &&
                hit.collider.transform.IsChildOf(playerRoot))
            {
                continue;
            }

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                point = hit.point;
            }
        }

        return point;
    }

    // ==================================================
    // MOUSE RETARGETING
    // ==================================================

    private void UpdateMouseRetargeting()
    {
        float yaw = controller.MouseYaw;

        float pitch =
            cameraLook != null
            ? cameraLook.Pitch
            : previousMousePitch;

        float yawDelta =
            Mathf.DeltaAngle(
                previousMouseYaw,
                yaw
            );

        float pitchDelta =
            pitch - previousMousePitch;

        previousMouseYaw = yaw;
        previousMousePitch = pitch;

        if (!allowMouseRetargeting)
            return;

        if (Mathf.Abs(yawDelta) < 0.00001f &&
            Mathf.Abs(pitchDelta) < 0.00001f)
        {
            return;
        }

        Vector3 stick =
            lockedHitPoint - transform.position;

        float distance = stick.magnitude;

        if (distance < 0.001f)
            return;

        Quaternion yawRotation =
            Quaternion.AngleAxis(
                yawDelta,
                Vector3.up
            );

        Quaternion pitchRotation =
            Quaternion.AngleAxis(
                pitchDelta,
                transform.right
            );

        Vector3 direction =
            yawRotation *
            pitchRotation *
            stick.normalized;

        lockedHitPoint =
            transform.position +
            direction.normalized * distance;
    }

    // ==================================================
    // CAMERA POSITION
    // ==================================================

    private void UpdateFollowPosition(bool flipping)
    {
        Vector3 target = cameraPivot.position;

        float horizontalTime =
            flipping
            ? flipSmoothTime
            : horizontalSmoothTime;

        float verticalTime =
            flipping
            ? flipSmoothTime
            : verticalSmoothTime;

        horizontalTime =
            Mathf.Max(0.0001f, horizontalTime);

        verticalTime =
            Mathf.Max(0.0001f, verticalTime);

        followPosition.x = Mathf.SmoothDamp(
            followPosition.x,
            target.x,
            ref followVelocity.x,
            horizontalTime
        );

        followPosition.y = Mathf.SmoothDamp(
            followPosition.y,
            target.y,
            ref followVelocity.y,
            verticalTime
        );

        followPosition.z = Mathf.SmoothDamp(
            followPosition.z,
            target.z,
            ref followVelocity.z,
            horizontalTime
        );
    }

    private void UpdateNormalCamera()
    {
        Quaternion rotation =
            cameraPivot.rotation;

        Vector3 position =
            followPosition +
            rotation * cameraOffset;

        position = ResolveCameraCollision(
            followPosition,
            position
        );

        transform.SetPositionAndRotation(
            position,
            rotation
        );
    }

    // ==================================================
    // SMOOTH CAMERA ROTATION
    // ==================================================

    private void UpdateSoftFlipCamera()
    {
        float progress = controller.FlipProgress;

        float arc =
            Mathf.Sin(progress * Mathf.PI);

        Vector3 normalOffset =
            cameraPivot.rotation * cameraOffset;

        Vector3 forward =
            cameraPivot.rotation * Vector3.forward;

        Vector3 desiredPosition =
            followPosition +
            normalOffset +
            Vector3.up *
                (arc * flipCameraLift) +
            forward *
                (arc * flipCameraForwardTravel);

        desiredPosition = ResolveCameraCollision(
            followPosition,
            desiredPosition
        );

        Vector3 toTarget =
            lockedHitPoint - desiredPosition;

        if (toTarget.sqrMagnitude < 0.0001f)
            toTarget = transform.forward;

        Quaternion desiredRotation =
            Quaternion.LookRotation(
                toTarget.normalized,
                Vector3.up
            );

        float smoothTime =
            Mathf.Max(
                0.01f,
                flipRotationSmoothTime
            );

        float angularError =
            Quaternion.Angle(
                transform.rotation,
                desiredRotation
            );

        currentRotationSpeed =
            Mathf.SmoothDamp(
                currentRotationSpeed,
                angularError / smoothTime,
                ref rotationSpeedVelocity,
                smoothTime
            );

        float maxStep =
            Mathf.Min(
                maxFlipRotationSpeed,
                Mathf.Max(
                    0f,
                    currentRotationSpeed
                )
            ) * Time.deltaTime;

        Quaternion newRotation =
            Quaternion.RotateTowards(
                transform.rotation,
                desiredRotation,
                maxStep
            );

        // Give the crosshair room to move.
        // Increase camera catch-up if it reaches
        // the edge of its freedom zone.
        Vector3 screenPoint =
            cam.WorldToScreenPoint(lockedHitPoint);

        if (screenPoint.z > 0f)
        {
            Vector2 screenCenter =
                new Vector2(
                    Screen.width * 0.5f,
                    Screen.height * 0.5f
                );

            float drift =
                Vector2.Distance(
                    new Vector2(
                        screenPoint.x,
                        screenPoint.y
                    ),
                    screenCenter
                );

            if (drift > crosshairFreedomRadius)
            {
                float extra =
                    Mathf.Clamp01(
                        (drift - crosshairFreedomRadius) /
                        Mathf.Max(
                            1f,
                            crosshairFreedomRadius
                        )
                    );

                newRotation =
                    Quaternion.Slerp(
                        newRotation,
                        desiredRotation,
                        extra
                    );
            }
        }

        transform.SetPositionAndRotation(
            desiredPosition,
            newRotation
        );
    }

    // ==================================================
    // GRADUAL PLAYER ROTATION
    // ==================================================

    private void UpdatePlayerVisual()
    {
        if (playerVisual == null ||
            controller == null)
        {
            return;
        }

        float progress = controller.FlipProgress;

        Vector3 toTarget =
            lockedHitPoint - playerVisual.position;

        if (toTarget.sqrMagnitude < 0.0001f)
            toTarget = transform.forward;

        Quaternion targetFacing =
            Quaternion.LookRotation(
                toTarget.normalized,
                Vector3.up
            );

        // Gradually change facing throughout
        // the flip instead of waiting until
        // the final frame.
        float facingBlend =
            Mathf.SmoothStep(
                0f,
                targetFacingStrength,
                progress
            );

        Quaternion desiredBodyFacing =
            Quaternion.Slerp(
                flipStartFacing,
                targetFacing,
                facingBlend
            );

        // Prevent sudden changes if the target
        // direction changes during the flip.
        currentBodyFacing =
            Quaternion.RotateTowards(
                currentBodyFacing,
                desiredBodyFacing,
                bodyTurnSpeed * Time.deltaTime
            );

        // Continuous gymnastics rotation.
        // This is based on an angle rather than
        // interpolating between two different
        // 360-degree quaternion rotations.
        Vector3 flipAxis;

        switch (controller.CurrentTrick)
        {
            case PlayerController.TrickType.CartwheelLeft:
            case PlayerController.TrickType.CartwheelRight:
                flipAxis = Vector3.forward;
                break;

            default:
                flipAxis = Vector3.right;
                break;
        }

        float angle =
            controller.FlipAngle *
            bodyFlipStrength;

        Quaternion flipRotation =
            Quaternion.AngleAxis(
                angle,
                flipAxis
            );

        playerVisual.rotation =
            currentBodyFacing *
            flipRotation *
            originalVisualLocalRotation;
    }

    // ==================================================
    // END FLIP
    // ==================================================

    private void EndFlipTracking()
    {
        // Keep the final camera direction when
        // normal mouse control resumes.
        Vector3 forward = transform.forward;

        float yaw =
            Mathf.Atan2(
                forward.x,
                forward.z
            ) * Mathf.Rad2Deg;

        float pitch =
            -Mathf.Asin(
                Mathf.Clamp(
                    forward.y,
                    -1f,
                    1f
                )
            ) * Mathf.Rad2Deg;

        controller.SetMouseYaw(yaw);

        if (cameraLook != null)
            cameraLook.SetPitch(pitch);

        cameraPivot.rotation =
            Quaternion.Euler(0f, yaw, 0f) *
            Quaternion.Euler(pitch, 0f, 0f);

        flipTrackingActive = false;
    }

    // ==================================================
    // COLLISION
    // ==================================================

    private Vector3 ResolveCameraCollision(
        Vector3 origin,
        Vector3 desired)
    {
        if (!enableCameraCollision)
            return desired;

        Vector3 offset = desired - origin;

        float distance = offset.magnitude;

        if (distance < 0.0001f)
            return desired;

        Vector3 direction = offset / distance;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(
                0.01f,
                cameraRadius
            ),
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

            if (playerRoot != null &&
                hit.collider.transform.IsChildOf(playerRoot))
            {
                continue;
            }

            if (hit.distance < nearest)
            {
                nearest = hit.distance;
                blocked = true;
            }
        }

        if (!blocked)
            return desired;

        return origin +
            direction *
            Mathf.Max(
                0f,
                nearest - wallOffset
            );
    }

    // ==================================================
    // FIELD OF VIEW
    // ==================================================

    private void UpdateFOV()
    {
        if (cam == null)
            return;

        float targetFOV = normalFOV;

        if (playerRb != null)
        {
            Vector3 velocity =
                playerRb.linearVelocity;

            if (velocity.magnitude > dashThreshold)
            {
                targetFOV = dashFOV;
            }
            else if (
                Mathf.Abs(velocity.y) > jumpThreshold)
            {
                targetFOV = jumpFOV;
            }
        }

        float blend =
            1f - Mathf.Exp(
                -Mathf.Max(0f, fovSpeed) *
                Time.deltaTime
            );

        cam.fieldOfView =
            Mathf.Lerp(
                cam.fieldOfView,
                targetFOV,
                blend
            );
    }
}
