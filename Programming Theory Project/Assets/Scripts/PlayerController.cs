
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    public enum TrickType
    {
        None,
        CartwheelLeft,
        CartwheelRight,
        FrontFlip,
        BackFlip
    }

    [Header("Movement")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float gravity = 20f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("References")]
    [SerializeField] private Transform visualModel;
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private CameraLook cameraLook;

    [Header("Double Tap Tricks")]
    [SerializeField] private float doubleTapWindow = 0.7f;
    [SerializeField] private float flipUpwardVelocity = 9f;
    [SerializeField] private float flipHorizontalSpeed = 12f;
    [SerializeField] private float flipDuration = 0.8f;
    [SerializeField] private float flipGravityMultiplier = 1f;
    [SerializeField] private float apexVelocityDamping = 1f;
    [SerializeField] private float apexWindow = 0.5f;

    [Header("Flip Restriction")]
    [Tooltip("Only one flip is allowed until the player lands.")]
    [SerializeField] private bool oneFlipPerLanding = true;

    [Tooltip("Ground contact time required to unlock the next flip.")]
    [SerializeField] private float groundedResetTime = 0.08f;

    [Header("Target Orbit Cartwheels")]
    [SerializeField] private bool enableAimOrbit = true;
    [SerializeField] private float minimumOrbitRadius = 2.5f;
    [SerializeField] private float maximumOrbitRadius = 25f;
    [SerializeField] private float orbitSteering = 14f;
    [SerializeField] private float orbitFacingSpeed = 18f;
    [SerializeField] private float orbitRadiusCorrection = 0.65f;

    [Header("Front / Back Somersaults")]
    [SerializeField] private bool enableInvertedHalfTurn = true;

    [Range(0.05f, 0.45f)]
    [SerializeField] private float halfTurnStart = 0.38f;

    [Range(0.55f, 0.95f)]
    [SerializeField] private float halfTurnEnd = 0.62f;

    [SerializeField] private float frontFlipSteeringSpeed = 9f;
    [SerializeField] private float frontFlipLandingSpeed = 10f;

    public bool IsFlipping { get; private set; }
    public TrickType CurrentTrick { get; private set; }
    public float FlipAngle { get; private set; }
    public float FlipProgress { get; private set; }
    public float FlipTurnAngle { get; private set; }
    public float MouseYaw => mouseYaw;

    public bool IsCartwheeling =>
        IsFlipping &&
        (CurrentTrick == TrickType.CartwheelLeft ||
         CurrentTrick == TrickType.CartwheelRight);

    public bool IsFrontFlipping =>
        IsFlipping && CurrentTrick == TrickType.FrontFlip;

    public bool IsVerticalFlipping =>
        IsFlipping &&
        (CurrentTrick == TrickType.FrontFlip ||
         CurrentTrick == TrickType.BackFlip);

    public Quaternion FlipBodyRotation { get; private set; }
        = Quaternion.identity;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Quaternion originalVisualRotation;

    private Vector2 moveInput;
    private float mouseYaw;
    private float facingYaw;

    private float lastSpaceTime = -100f;
    private float lastDashTime = -100f;
    private float dashTimer;

    private bool isGrounded;
    private bool jumpQueued;
    private bool flipQueued;
    private bool dashQueued;

    private float flipTimer;
    private Vector3 flipDirection;
    private Vector3 flipAxis;
    private float flipSign;

    private float orbitRadius;
    private bool orbitActive;

    private float flipStartingYaw;
    private float flipEndingYaw;

    // True after the first flip starts.
    // Resets only after the player lands.
    private bool flipUsedSinceLanding;
    private float groundedTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        mouseYaw = transform.eulerAngles.y;
        facingYaw = mouseYaw;

        if (visualModel != null)
            originalVisualRotation = visualModel.localRotation;

        if (cameraLook == null)
            cameraLook = GetComponentInChildren<CameraLook>();

        if (cameraPivot == null && cameraLook != null)
            cameraPivot = cameraLook.transform;
    }

    private void Update()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            moveInput = Vector2.ClampMagnitude(
                new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical")
                ),
                1f
            );

            mouseYaw +=
                Input.GetAxisRaw("Mouse X") *
                mouseSensitivity;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (Time.time - lastSpaceTime <= doubleTapWindow)
                {
                    flipQueued = true;
                    jumpQueued = false;
                    lastSpaceTime = -100f;
                }
                else
                {
                    jumpQueued = true;
                    lastSpaceTime = Time.time;
                }
            }

            if (Input.GetKeyDown(KeyCode.Q))
                dashQueued = true;
        }
        else
        {
            moveInput = Vector2.zero;
        }

        UpdateFlipProgress();
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
        UpdateFlipAvailability();

        if (flipQueued)
        {
            flipQueued = false;
            jumpQueued = false;

            if (CanStartFlip())
                StartFlip();
        }
        else if (jumpQueued)
        {
            jumpQueued = false;

            if (isGrounded && !IsFlipping)
                Jump();
        }

        if (dashQueued)
        {
            dashQueued = false;
            TryDash();
        }

        dashTimer = Mathf.Max(
            0f,
            dashTimer - Time.fixedDeltaTime
        );

        if (IsFlipping)
            ApplyFlipMovement();
        else
            ApplyNormalMovement();

        rb.MoveRotation(
            Quaternion.Euler(0f, facingYaw, 0f)
        );

        rb.angularVelocity = Vector3.zero;

        rb.AddForce(
            Vector3.down * gravity *
            (IsFlipping ? flipGravityMultiplier : 1f),
            ForceMode.Acceleration
        );
    }

    private bool CanStartFlip()
    {
        if (IsFlipping)
            return false;

        if (oneFlipPerLanding && flipUsedSinceLanding)
            return false;

        // The FIRST flip is allowed in the air.
        return true;
    }

    private void UpdateFlipAvailability()
    {
        if (!flipUsedSinceLanding)
            return;

        // Do not reset while the flip is active.
        if (IsFlipping)
        {
            groundedTimer = 0f;
            return;
        }

        // Only landing can unlock another flip.
        if (isGrounded && rb.linearVelocity.y <= 0.1f)
        {
            groundedTimer += Time.fixedDeltaTime;

            if (groundedTimer >= groundedResetTime)
            {
                flipUsedSinceLanding = false;
                groundedTimer = 0f;
            }
        }
        else
        {
            groundedTimer = 0f;
        }
    }

    private void LateUpdate()
    {
        if (visualModel == null)
            return;

        Quaternion desiredYaw =
            Quaternion.Euler(0f, facingYaw, 0f);

        Quaternion correction =
            Quaternion.Inverse(transform.rotation) *
            desiredYaw;

        Quaternion trickRotation = Quaternion.identity;

        if (IsCartwheeling)
        {
            // Preserve cartwheel rotation.
            trickRotation =
                Quaternion.AngleAxis(FlipAngle, flipAxis);
        }
        else if (IsVerticalFlipping)
        {
            trickRotation =
                Quaternion.AngleAxis(
                    FlipAngle,
                    Vector3.right
                ) *
                Quaternion.AngleAxis(
                    FlipTurnAngle,
                    Vector3.up
                );
        }

        visualModel.localRotation =
            correction *
            originalVisualRotation *
            trickRotation;
    }

    private void UpdateGroundedState()
    {
        if (capsule == null)
        {
            isGrounded = false;
            return;
        }

        Vector3 center =
            transform.TransformPoint(capsule.center);

        float radius = capsule.radius *
            Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.z)
            );

        float height = capsule.height *
            Mathf.Abs(transform.lossyScale.y);

        Vector3 origin = center +
            Vector3.down *
            (Mathf.Max(radius, height * 0.5f) -
             radius - 0.08f);

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.01f, radius * 0.85f),
            Vector3.down,
            0.16f,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        isGrounded = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null &&
                !hit.collider.transform.IsChildOf(transform) &&
                hit.normal.y > 0.5f)
            {
                isGrounded = true;
                break;
            }
        }
    }

    private void Jump()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = jumpForce;
        rb.linearVelocity = velocity;

        isGrounded = false;
        groundedTimer = 0f;
    }

    private void TryDash()
    {
        if (Time.time - lastDashTime < dashCooldown)
            return;

        lastDashTime = Time.time;
        dashTimer = dashDuration;
    }

    private Vector3 GetMoveDirection()
    {
        Vector3 direction =
            Quaternion.Euler(0f, mouseYaw, 0f) *
            new Vector3(moveInput.x, 0f, moveInput.y);

        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void ApplyNormalMovement()
    {
        facingYaw = mouseYaw;

        Vector3 direction = GetMoveDirection();
        Vector3 velocity = rb.linearVelocity;

        float movementSpeed =
            dashTimer > 0f ? dashSpeed : speed;

        velocity.x = direction.x * movementSpeed;
        velocity.z = direction.z * movementSpeed;

        rb.linearVelocity = velocity;
    }

    private void StartFlip()
    {
        if (moveInput.y > 0.1f)
        {
            CurrentTrick = TrickType.FrontFlip;
            flipAxis = Vector3.right;
            flipSign = 1f;
        }
        else if (moveInput.y < -0.1f)
        {
            CurrentTrick = TrickType.BackFlip;
            flipAxis = Vector3.right;
            flipSign = -1f;
        }
        else if (moveInput.x < -0.1f)
        {
            CurrentTrick = TrickType.CartwheelLeft;
            flipAxis = Vector3.forward;
            flipSign = 1f;
        }
        else if (moveInput.x > 0.1f)
        {
            CurrentTrick = TrickType.CartwheelRight;
            flipAxis = Vector3.forward;
            flipSign = -1f;
        }
        else
        {
            CurrentTrick = TrickType.FrontFlip;
            flipAxis = Vector3.right;
            flipSign = 1f;
        }

        // Lock further flips until landing.
        flipUsedSinceLanding = true;
        groundedTimer = 0f;

        IsFlipping = true;
        flipTimer = 0f;
        FlipProgress = 0f;
        FlipAngle = 0f;
        FlipTurnAngle = 0f;

        flipStartingYaw = facingYaw;
        flipEndingYaw = flipStartingYaw + 180f;

        flipDirection = GetMoveDirection();

        if (flipDirection.sqrMagnitude < 0.01f)
        {
            flipDirection =
                Quaternion.Euler(0f, mouseYaw, 0f) *
                Vector3.forward;
        }

        orbitActive = false;

        if (enableAimOrbit &&
            IsCartwheeling &&
            cameraLook != null)
        {
            Vector3 offset =
                cameraLook.AimPoint - rb.position;

            offset.y = 0f;

            float distance = offset.magnitude;

            if (distance >= minimumOrbitRadius &&
                distance <= maximumOrbitRadius)
            {
                orbitActive = true;
                orbitRadius = distance;
            }
        }

        Vector3 velocity =
            flipDirection * flipHorizontalSpeed;

        velocity.y = flipUpwardVelocity;
        rb.linearVelocity = velocity;
        isGrounded = false;
    }

    private void ApplyFlipMovement()
    {
        Vector3 desiredDirection = flipDirection;

        if (IsCartwheeling && orbitActive && cameraLook != null)
        {
            Vector3 toTarget =
                cameraLook.AimPoint - rb.position;

            toTarget.y = 0f;

            float distance = toTarget.magnitude;

            if (distance > 0.1f)
            {
                Vector3 inward = toTarget / distance;

                float side =
                    CurrentTrick == TrickType.CartwheelRight
                    ? 1f : -1f;

                Vector3 tangent =
                    Vector3.Cross(Vector3.up, inward) * side;

                float radiusError = distance - orbitRadius;

                float correction = Mathf.Clamp(
                    radiusError * orbitRadiusCorrection,
                    -0.6f,
                    0.6f
                );

                desiredDirection =
                    (tangent + inward * correction).normalized;

                float targetYaw =
                    Mathf.Atan2(inward.x, inward.z) *
                    Mathf.Rad2Deg;

                facingYaw = Mathf.LerpAngle(
                    facingYaw,
                    targetYaw,
                    1f - Mathf.Exp(
                        -orbitFacingSpeed * Time.fixedDeltaTime
                    )
                );
            }
        }
        else if (IsVerticalFlipping)
        {
            facingYaw = flipStartingYaw;

            if (IsFrontFlipping && cameraLook != null)
            {
                Vector3 toAim =
                    cameraLook.AimPoint - rb.position;

                toAim.y = 0f;

                if (toAim.sqrMagnitude > 0.001f)
                {
                    float steering =
                        FlipProgress >= 0.5f
                        ? frontFlipLandingSpeed
                        : frontFlipSteeringSpeed;

                    desiredDirection = Vector3.Slerp(
                        flipDirection,
                        toAim.normalized,
                        1f - Mathf.Exp(
                            -steering * Time.fixedDeltaTime
                        )
                    ).normalized;
                }
            }
        }
        else
        {
            facingYaw = mouseYaw;
        }

        flipDirection = Vector3.Slerp(
            flipDirection,
            desiredDirection,
            1f - Mathf.Exp(
                -orbitSteering * Time.fixedDeltaTime
            )
        ).normalized;

        Vector3 velocity = rb.linearVelocity;

        velocity.x = flipDirection.x * flipHorizontalSpeed;
        velocity.z = flipDirection.z * flipHorizontalSpeed;

        if (Mathf.Abs(velocity.y) < apexWindow)
        {
            velocity.y *=
                Mathf.Clamp01(apexVelocityDamping);
        }

        rb.linearVelocity = velocity;
    }

    private void UpdateFlipProgress()
    {
        if (!IsFlipping)
            return;

        flipTimer += Time.deltaTime;

        FlipProgress = Mathf.Clamp01(
            flipTimer / Mathf.Max(0.0001f, flipDuration)
        );

        FlipAngle =
            360f * Smooth01(FlipProgress) * flipSign;

        if (IsVerticalFlipping)
        {
            float turnStart = Mathf.Clamp(
                halfTurnStart, 0.05f, 0.45f
            );

            float turnEnd = Mathf.Clamp(
                halfTurnEnd, 0.55f, 0.95f
            );

            float turnProgress = Mathf.InverseLerp(
                turnStart,
                turnEnd,
                FlipProgress
            );

            FlipTurnAngle = enableInvertedHalfTurn
                ? 180f * Smooth01(turnProgress)
                : 0f;

            FlipBodyRotation =
                Quaternion.Euler(0f, flipStartingYaw, 0f) *
                Quaternion.AngleAxis(
                    FlipAngle, Vector3.right
                ) *
                Quaternion.AngleAxis(
                    FlipTurnAngle, Vector3.up
                );
        }
        else
        {
            FlipTurnAngle = 0f;
            FlipBodyRotation = Quaternion.identity;
        }

        if (FlipProgress >= 1f)
        {
            bool wasVertical = IsVerticalFlipping;

            IsFlipping = false;
            CurrentTrick = TrickType.None;
            FlipProgress = 0f;
            FlipAngle = 0f;
            FlipTurnAngle = 0f;
            orbitActive = false;

            if (wasVertical)
            {
                facingYaw = enableInvertedHalfTurn
                    ? flipEndingYaw
                    : flipStartingYaw;

                mouseYaw = facingYaw;
            }

            FlipBodyRotation = Quaternion.identity;

            // Do NOT reset flipUsedSinceLanding here.
            // The player must touch the ground first.
        }
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp01(value);
        return t * t * (3f - 2f * t);
    }

    private void OnDisable()
    {
        if (visualModel != null)
            visualModel.localRotation = originalVisualRotation;
    }
}
