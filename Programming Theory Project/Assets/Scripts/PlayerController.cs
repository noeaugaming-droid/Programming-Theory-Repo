
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

    [Header("Visual Model")]
    [SerializeField] private Transform visualModel;

    [Header("Camera Pivot")]
    [Tooltip("Assign Player/Pivot here. The pivot stays parented to Player.")]
    [SerializeField] private Transform cameraPivot;

    [Header("Flip Aim Steering")]
    [Tooltip("Steer flip travel toward mouse aim while continuing the flip.")]
    [SerializeField] private bool steerFlipTravelWithAim = true;
    [SerializeField] private float flipSteeringSpeed = 720f;

    [Header("Double Tap Flip")]
    [SerializeField] private float doubleTapWindow = 0.7f;
    [SerializeField] private float flipUpwardVelocity = 9f;
    [SerializeField] private float flipHorizontalSpeed = 12f;
    [SerializeField] private float flipDuration = 0.8f;
    [SerializeField] private float flipGravityMultiplier = 1f;
    [SerializeField] private float apexVelocityDamping = 1f;
    [SerializeField] private float apexWindow = 0.5f;

    public bool IsFlipping { get; private set; }
    public TrickType CurrentTrick { get; private set; }
    public float FlipAngle { get; private set; }
    public float FlipProgress { get; private set; }

    public float MouseYaw => mouseYaw;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private Quaternion originalVisualRotation;
    private Quaternion originalPivotLocalRotation;

    private Vector2 moveInput;
    private float mouseYaw;

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

    private const float MinimumTime = 0.0001f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();

        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints |= RigidbodyConstraints.FreezeRotation;
        rb.angularVelocity = Vector3.zero;

        mouseYaw = transform.eulerAngles.y;

        if (visualModel != null)
        {
            originalVisualRotation =
                visualModel.localRotation;
        }

        if (cameraPivot == null)
        {
            CameraLook look =
                GetComponentInChildren<CameraLook>();

            if (look != null)
                cameraPivot = look.transform;
        }

        if (cameraPivot != null)
        {
            originalPivotLocalRotation =
                cameraPivot.localRotation;
        }
    }

    private void Update()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            );

            moveInput =
                Vector2.ClampMagnitude(moveInput, 1f);

            mouseYaw +=
                Input.GetAxisRaw("Mouse X") *
                mouseSensitivity;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (Time.time - lastSpaceTime <=
                    doubleTapWindow)
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

        UpdateFlipVisual();
    }

    private void FixedUpdate()
    {
        rb.MoveRotation(
            Quaternion.Euler(0f, mouseYaw, 0f)
        );

        rb.angularVelocity = Vector3.zero;

        UpdateGroundedState();

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

            if (isGrounded && !IsFlipping)
                Jump();
        }

        if (dashQueued)
        {
            dashQueued = false;
            TryStartDash();
        }

        if (dashTimer > 0f)
        {
            dashTimer = Mathf.Max(
                0f,
                dashTimer - Time.fixedDeltaTime
            );
        }

        if (IsFlipping)
            ApplyFlipMovement();
        else
            ApplyNormalMovement();

        float gravityMultiplier =
            IsFlipping ? flipGravityMultiplier : 1f;

        rb.AddForce(
            Vector3.down * gravity * gravityMultiplier,
            ForceMode.Acceleration
        );
    }

    private void LateUpdate()
    {
        Quaternion immediateWorldYaw =
            Quaternion.Euler(0f, mouseYaw, 0f);

        Quaternion correction =
            Quaternion.Inverse(transform.rotation) *
            immediateWorldYaw;

        if (visualModel != null)
        {
            Quaternion flipRotation =
                IsFlipping
                ? Quaternion.AngleAxis(
                    FlipAngle,
                    flipAxis
                )
                : Quaternion.identity;

            visualModel.localRotation =
                correction *
                originalVisualRotation *
                flipRotation;
        }

        if (cameraPivot != null)
        {
            Vector3 localEuler =
                cameraPivot.localEulerAngles;

            float pitch =
                Mathf.DeltaAngle(0f, localEuler.x);

            cameraPivot.rotation =
                immediateWorldYaw *
                Quaternion.Euler(pitch, 0f, 0f);
        }
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
            center.y - Mathf.Max(
                height * 0.5f,
                radius
            );

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

        isGrounded = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.normal.y > 0.5f)
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
    }

    private void TryStartDash()
    {
        if (Time.time - lastDashTime < dashCooldown)
            return;

        lastDashTime = Time.time;
        dashTimer = dashDuration;
    }

    private Vector3 GetMovementDirection()
    {
        Quaternion yawRotation =
            Quaternion.Euler(0f, mouseYaw, 0f);

        Vector3 direction =
            yawRotation *
            new Vector3(
                moveInput.x,
                0f,
                moveInput.y
            );

        return Vector3.ClampMagnitude(
            direction,
            1f
        );
    }

    private void ApplyNormalMovement()
    {
        Vector3 direction =
            GetMovementDirection();

        float currentSpeed =
            dashTimer > 0f
            ? dashSpeed
            : speed;

        Vector3 velocity = rb.linearVelocity;

        velocity.x =
            direction.x * currentSpeed;

        velocity.z =
            direction.z * currentSpeed;

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

        IsFlipping = true;
        flipTimer = 0f;
        FlipProgress = 0f;
        FlipAngle = 0f;

        flipDirection =
            GetMovementDirection();

        if (flipDirection.sqrMagnitude < 0.01f)
        {
            flipDirection =
                Quaternion.Euler(
                    0f,
                    mouseYaw,
                    0f
                ) * Vector3.forward;
        }

        Vector3 velocity =
            flipDirection * flipHorizontalSpeed;

        velocity.y = flipUpwardVelocity;

        rb.linearVelocity = velocity;
        isGrounded = false;
    }

    private void ApplyFlipMovement()
    {
        if (steerFlipTravelWithAim)
        {
            Vector3 targetDirection = GetMovementDirection();

            if (targetDirection.sqrMagnitude < 0.01f)
            {
                targetDirection =
                    Quaternion.Euler(
                        0f,
                        mouseYaw,
                        0f
                    ) * Vector3.forward;
            }

            flipDirection = Vector3.RotateTowards(
                flipDirection,
                targetDirection.normalized,
                Mathf.Max(0f, flipSteeringSpeed) *
                    Mathf.Deg2Rad * Time.fixedDeltaTime,
                0f
            );
        }

        Vector3 velocity = rb.linearVelocity;

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

    private void UpdateFlipVisual()
    {
        if (!IsFlipping)
            return;

        flipTimer += Time.deltaTime;

        float duration =
            Mathf.Max(
                MinimumTime,
                flipDuration
            );

        float progress =
            Mathf.Clamp01(
                flipTimer / duration
            );

        float eased =
            progress * progress *
            (3f - 2f * progress);

        FlipProgress = progress;

        FlipAngle =
            360f * eased * flipSign;

        if (progress >= 1f)
        {
            IsFlipping = false;
            CurrentTrick = TrickType.None;

            FlipProgress = 0f;
            FlipAngle = 0f;
        }
    }

    private void OnDisable()
    {
        if (visualModel != null)
        {
            visualModel.localRotation =
                originalVisualRotation;
        }

        if (cameraPivot != null)
        {
            cameraPivot.localRotation =
                originalPivotLocalRotation;
        }
    }
}
