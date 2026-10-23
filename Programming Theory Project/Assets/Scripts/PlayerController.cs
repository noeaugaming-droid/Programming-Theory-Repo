
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

    [Header("References")]
    [SerializeField] private Transform visualModel;
    [SerializeField] private Transform cameraPivot;

    [Header("Movement")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float gravity = 20f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Flip")]
    [SerializeField] private float doubleTapWindow = 0.7f;
    [SerializeField] private float flipUpwardVelocity = 9f;
    [SerializeField] private float flipHorizontalSpeed = 12f;
    [SerializeField] private float flipDuration = 0.8f;
    [SerializeField] private float flipGravityMultiplier = 1f;
    [SerializeField] private float apexVelocityDamping = 1f;
    [SerializeField] private float apexWindow = 0.5f;

    [Header("Movement Direction Lock")]
    [Tooltip(
        "Keep traveling in the original world direction " +
        "until the movement keys used for the flip are released."
    )]
    [SerializeField] private bool preserveFlipTravelDirection = true;

    [Tooltip(
        "If enabled, pressing a different movement combination " +
        "after landing cancels the original direction lock."
    )]
    [SerializeField] private bool newInputCancelsDirectionLock = true;

    public bool IsFlipping { get; private set; }
    public TrickType CurrentTrick { get; private set; }
    public float FlipAngle { get; private set; }
    public float FlipProgress { get; private set; }
    public float MouseYaw => mouseYaw;
    public Transform VisualModel => visualModel;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private CameraFollow cameraFollow;

    private Vector2 moveInput;
    private float mouseYaw;
    private bool grounded;

    private bool jumpQueued;
    private bool flipQueued;
    private bool dashQueued;

    private float lastSpaceTime = -100f;
    private float lastDashTime = -100f;
    private float dashTimer;

    private float flipTimer;
    private float flipSign;

    private Vector3 flipDirection;
    private Quaternion originalVisualLocalRotation;

    // The movement latch.
    private bool travelDirectionLocked;
    private Vector3 lockedTravelDirection;
    private Vector2 lockedMovementInput;

    private const float InputDeadZone = 0.1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints |= RigidbodyConstraints.FreezeRotation;

        mouseYaw = transform.eulerAngles.y;

        if (visualModel != null)
        {
            originalVisualLocalRotation =
                visualModel.localRotation;
        }

        if (cameraPivot == null)
        {
            CameraLook look =
                GetComponentInChildren<CameraLook>();

            if (look != null)
                cameraPivot = look.transform;
        }

        cameraFollow =
            FindAnyObjectByType<CameraFollow>();
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

        UpdateMovementDirectionLock();
        UpdateFlipAnimation();
    }

    private void FixedUpdate()
    {
        // Facing and travel direction are separate.
        rb.MoveRotation(
            Quaternion.Euler(0f, mouseYaw, 0f)
        );

        rb.angularVelocity = Vector3.zero;

        CheckGrounded();

        if (flipQueued)
        {
            flipQueued = false;
            jumpQueued = false;

            if (!IsFlipping)
                StartFlip();
        }
        else if (jumpQueued)
        {
            jumpQueued = false;

            if (grounded && !IsFlipping)
                Jump();
        }

        if (dashQueued)
        {
            dashQueued = false;
            StartDash();
        }

        dashTimer = Mathf.Max(
            0f,
            dashTimer - Time.fixedDeltaTime
        );

        if (IsFlipping)
        {
            MoveDuringFlip();
        }
        else
        {
            MoveNormally();
        }

        rb.AddForce(
            Vector3.down *
            gravity *
            (IsFlipping ? flipGravityMultiplier : 1f),
            ForceMode.Acceleration
        );
    }

    private void LateUpdate()
    {
        bool cameraControlsVisual =
            cameraFollow != null &&
            cameraFollow.IsTrackingFlip;

        if (visualModel != null && !cameraControlsVisual)
        {
            Quaternion desiredYaw =
                Quaternion.Euler(0f, mouseYaw, 0f);

            Quaternion correction =
                Quaternion.Inverse(transform.rotation) *
                desiredYaw;

            visualModel.localRotation =
                correction *
                originalVisualLocalRotation;
        }

        if (cameraPivot != null)
        {
            float pitch = Mathf.DeltaAngle(
                0f,
                cameraPivot.localEulerAngles.x
            );

            cameraPivot.rotation =
                Quaternion.Euler(0f, mouseYaw, 0f) *
                Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    public void SetMouseYaw(float yaw)
    {
        mouseYaw = yaw;
    }

    // --------------------------------------------------
    // MOVEMENT DIRECTION LOCK
    // --------------------------------------------------

    private void UpdateMovementDirectionLock()
    {
        if (!travelDirectionLocked)
            return;

        // Do not cancel the latch while the flip
        // is still playing.
        if (IsFlipping)
            return;

        // Releasing all movement keys ends the latch.
        if (moveInput.sqrMagnitude <
            InputDeadZone * InputDeadZone)
        {
            ReleaseTravelDirectionLock();
            return;
        }

        if (!newInputCancelsDirectionLock)
            return;

        // W held continuously remains W, even if
        // the character or camera turns around.
        //
        // Switching to S, A, D, or a different
        // combination creates a new movement intent.
        Vector2 currentInput =
            moveInput.normalized;

        Vector2 originalInput =
            lockedMovementInput.normalized;

        float similarity =
            Vector2.Dot(currentInput, originalInput);

        if (similarity < 0.99f)
        {
            ReleaseTravelDirectionLock();
        }
    }

    private void LockTravelDirection(
        Vector3 worldDirection)
    {
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude < 0.0001f)
        {
            worldDirection =
                Quaternion.Euler(0f, mouseYaw, 0f) *
                Vector3.forward;
        }

        lockedTravelDirection =
            worldDirection.normalized;

        lockedMovementInput = moveInput;

        travelDirectionLocked =
            preserveFlipTravelDirection &&
            moveInput.sqrMagnitude >
                InputDeadZone * InputDeadZone;
    }

    private void ReleaseTravelDirectionLock()
    {
        travelDirectionLocked = false;
        lockedMovementInput = Vector2.zero;
    }

    private Vector3 GetMovementDirection()
    {
        if (travelDirectionLocked)
        {
            // The original world direction wins.
            // Camera rotation cannot change it.
            return lockedTravelDirection *
                Mathf.Clamp01(moveInput.magnitude);
        }

        Quaternion yaw =
            Quaternion.Euler(0f, mouseYaw, 0f);

        return Vector3.ClampMagnitude(
            yaw * new Vector3(
                moveInput.x,
                0f,
                moveInput.y
            ),
            1f
        );
    }

    // --------------------------------------------------
    // GROUND CHECK
    // --------------------------------------------------

    private void CheckGrounded()
    {
        if (capsule == null)
        {
            grounded = false;
            return;
        }

        Vector3 center =
            transform.TransformPoint(capsule.center);

        float radius =
            capsule.radius *
            Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.z)
            );

        float height =
            capsule.height *
            Mathf.Abs(transform.lossyScale.y);

        float bottom =
            center.y -
            Mathf.Max(height * 0.5f, radius);

        Vector3 origin = new Vector3(
            center.x,
            bottom + radius + 0.08f,
            center.z
        );

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            Mathf.Max(0.01f, radius * 0.85f),
            Vector3.down,
            0.16f,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        grounded = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.normal.y > 0.5f)
            {
                grounded = true;
                break;
            }
        }
    }

    // --------------------------------------------------
    // JUMP AND DASH
    // --------------------------------------------------

    private void Jump()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = jumpForce;
        rb.linearVelocity = velocity;
        grounded = false;
    }

    private void StartDash()
    {
        if (Time.time - lastDashTime < dashCooldown)
            return;

        lastDashTime = Time.time;
        dashTimer = dashDuration;
    }

    // --------------------------------------------------
    // NORMAL MOVEMENT
    // --------------------------------------------------

    private void MoveNormally()
    {
        Vector3 direction =
            GetMovementDirection();

        Vector3 velocity =
            rb.linearVelocity;

        float currentSpeed =
            dashTimer > 0f
            ? dashSpeed
            : speed;

        velocity.x =
            direction.x * currentSpeed;

        velocity.z =
            direction.z * currentSpeed;

        rb.linearVelocity = velocity;
    }

    // --------------------------------------------------
    // FLIP
    // --------------------------------------------------

    private void StartFlip()
    {
        if (moveInput.y > InputDeadZone)
        {
            CurrentTrick = TrickType.FrontFlip;
            flipSign = 1f;
        }
        else if (moveInput.y < -InputDeadZone)
        {
            CurrentTrick = TrickType.BackFlip;
            flipSign = -1f;
        }
        else if (moveInput.x < -InputDeadZone)
        {
            CurrentTrick = TrickType.CartwheelLeft;
            flipSign = 1f;
        }
        else if (moveInput.x > InputDeadZone)
        {
            CurrentTrick = TrickType.CartwheelRight;
            flipSign = -1f;
        }
        else
        {
            CurrentTrick = TrickType.FrontFlip;
            flipSign = 1f;
        }

        // Capture the world-space direction BEFORE
        // the camera and body begin rotating.
        Vector3 initialDirection =
            GetMovementDirection();

        LockTravelDirection(initialDirection);

        flipDirection =
            initialDirection.sqrMagnitude > 0.0001f
            ? initialDirection.normalized
            : Quaternion.Euler(
                0f,
                mouseYaw,
                0f
            ) * Vector3.forward;

        IsFlipping = true;
        flipTimer = 0f;
        FlipAngle = 0f;
        FlipProgress = 0f;

        Vector3 velocity =
            flipDirection * flipHorizontalSpeed;

        velocity.y = flipUpwardVelocity;

        rb.linearVelocity = velocity;
        grounded = false;
    }

    private void MoveDuringFlip()
    {
        // Do NOT rotate flipDirection toward
        // the camera's current forward direction.
        //
        // The player continues traveling in the
        // same world-space direction, regardless
        // of where the body or camera faces.

        Vector3 velocity =
            rb.linearVelocity;

        velocity.x =
            flipDirection.x * flipHorizontalSpeed;

        velocity.z =
            flipDirection.z * flipHorizontalSpeed;

        if (Mathf.Abs(velocity.y) < apexWindow)
        {
            velocity.y *=
                Mathf.Clamp01(apexVelocityDamping);
        }

        rb.linearVelocity = velocity;
    }

    private void UpdateFlipAnimation()
    {
        if (!IsFlipping)
            return;

        flipTimer += Time.deltaTime;

        FlipProgress = Mathf.Clamp01(
            flipTimer /
            Mathf.Max(0.0001f, flipDuration)
        );

        float eased =
            FlipProgress *
            FlipProgress *
            (3f - 2f * FlipProgress);

        FlipAngle =
            360f * eased * flipSign;

        if (FlipProgress >= 1f)
        {
            IsFlipping = false;
            CurrentTrick = TrickType.None;
            FlipAngle = 0f;
            FlipProgress = 0f;

            // IMPORTANT:
            // Do NOT release the movement latch.
            //
            // It stays active until the player
            // releases W or changes movement input.
        }
    }
}
