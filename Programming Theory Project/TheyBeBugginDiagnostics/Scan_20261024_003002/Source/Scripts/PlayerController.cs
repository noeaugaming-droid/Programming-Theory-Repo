
using System.Collections.Generic;
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

    #region Physics_And_Movement_Variables
    // Values and references used by this part of the script.
    #endregion
    #region Player_Variables
    // Values and references used by this part of the script.
    #endregion
    #region Physics_And_Movement_Variables
    // Values and references used by this part of the script.
    [Header("Movement")]
    [SerializeField] private float speed = 10f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float gravity = 20f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    #endregion
    #region Player_Variables
    // Values and references used by this part of the script.
    [Header("Visual Model")]
    [SerializeField] private Transform visualModel;

    #endregion
    #region Camera_And_Aiming_Variables
    // Values and references used by this part of the script.
    [Header("Camera Pivot")]
    [SerializeField] private Transform cameraPivot;

    #endregion
    #region Physics_And_Movement_Variables
    // Values and references used by this part of the script.
    [Header("Flip Aim Steering")]
    [SerializeField] private bool steerFlipTravelWithAim = true;
    [SerializeField] private float flipSteeringSpeed = 720f;

    #endregion
    #region Player_Variables
    // Values and references used by this part of the script.
    #endregion
    #region Physics_And_Movement_Variables
    // Values and references used by this part of the script.
    #endregion
    #region Player_Variables
    // Values and references used by this part of the script.
    [Header("Double Tap Flip")]
    [SerializeField] private float doubleTapWindow = 0.7f;
    [SerializeField] private float flipUpwardVelocity = 9f;
    [SerializeField] private float flipHorizontalSpeed = 12f;
    [SerializeField] private float flipDuration = 0.8f;
    [SerializeField] private float flipGravityMultiplier = 1f;
    [SerializeField] private float apexVelocityDamping = 1f;
    [SerializeField] private float apexWindow = 0.5f;

    #endregion
    #region Physics_And_Movement_Variables
    // Values and references used by this part of the script.
    [Header("Landing Detection")]
    [Tooltip("Minimum upward surface normal to count as ground.")]
    [Range(0f, 1f)]
    [SerializeField] private float minimumGroundNormalY = 0.5f;


    public bool IsFlipping { get; private set; }
    public TrickType CurrentTrick { get; private set; }
    public float FlipAngle { get; private set; }
    public float FlipProgress { get; private set; }

    #endregion
    #region Camera_And_Aiming_Variables
    // Values and references used by this part of the script.
    public float MouseYaw => mouseYaw;

    // True from flip takeoff until physical landing contact.
    // CameraLook uses this to select the flip aim clamps.
    public bool UseFlipAimClamp { get; private set; }

    #endregion
    #region Physics_And_Movement_Variables
    // Values and references used by this part of the script.
    public bool IsGrounded => isGrounded;

    #endregion
    #region Player_Variables
    // Values and references used by this part of the script.
    private Rigidbody rb;
    private CapsuleCollider capsule;

    private Quaternion originalVisualRotation;
    private Quaternion originalPivotLocalRotation;

    #endregion
    #region Physics_And_Movement_Variables
    // Values and references used by this part of the script.
    private Vector2 moveInput;
    #endregion
    #region Camera_And_Aiming_Variables
    // Values and references used by this part of the script.
    private float mouseYaw;

    #endregion
    #region Player_Variables
    // Values and references used by this part of the script.
    private float lastSpaceTime = -100f;
    #endregion
    #region Physics_And_Movement_Variables
    // Values and references used by this part of the script.
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

    // Prevents the ground beneath the player at takeoff
    // from being mistaken for a new landing.
    private bool leftGroundSinceFlip;

    // Tracks actual collision contacts, not just a raycast.
    private readonly HashSet<Collider> groundContacts =
        new HashSet<Collider>();

    private readonly ContactPoint[] contactBuffer =
        new ContactPoint[32];

    #endregion
    #region Player_Variables
    // Values and references used by this part of the script.
    private const float MinimumTime = 0.0001f;
    #endregion
    #region Physics_And_Movement_Variables
    // Values and references used by this part of the script.
    private bool flipAvailable = true;

    public bool CanFlip => flipAvailable && !IsFlipping;

    #endregion
    #region Unity_Lifecycle_Methods
    // Methods Unity calls automatically during the object's lifetime.
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

        // The player must leave the ground before
        // another ground contact can count as landing.
        if (UseFlipAimClamp && !isGrounded)
        {
            leftGroundSinceFlip = true;
        }

        if (flipQueued)
        {
            flipQueued = false;
            jumpQueued = false;

            // Only flip if a previous landing
            // has made another flip available.
            if (CanFlip)
            {
                StartFlip();
            }
        }
        else if (jumpQueued)
        {
            jumpQueued = false;

            if (isGrounded && !IsFlipping)
            {
                Jump();
            }
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
        {
            ApplyFlipMovement();
        }
        else
        {
            ApplyNormalMovement();
        }

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
            isGrounded = groundContacts.Count > 0;
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

            if (hit.normal.y > minimumGroundNormalY)
            {
                isGrounded = true;
                break;
            }
        }

        if (groundContacts.Count > 0)
            isGrounded = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        ProcessGroundContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        ProcessGroundContact(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        groundContacts.Remove(collision.collider);
    }

    #endregion
    #region Physics_And_Movement_Methods
    // Movement, jumping, collisions, and other physics behavior.
    private void ProcessGroundContact(Collision collision)
    {
        if (collision == null ||
            collision.collider == null)
            return;

        if (collision.collider.transform.IsChildOf(transform))
            return;

        int count = collision.GetContacts(contactBuffer);

        bool validGroundContact = false;

        for (int i = 0; i < count; i++)
        {
            if (contactBuffer[i].normal.y >
                minimumGroundNormalY)
            {
                validGroundContact = true;
                break;
            }
        }

        if (!validGroundContact)
        {
            groundContacts.Remove(collision.collider);
            return;
        }

        groundContacts.Add(collision.collider);
        isGrounded = true;

        // Normal aim clamps activate at physical landing,
        // regardless of whether the flip animation has ended.
        if (UseFlipAimClamp &&
            leftGroundSinceFlip &&
            rb.linearVelocity.y <= 0.1f)
        {
            UseFlipAimClamp = false;
            leftGroundSinceFlip = false;
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

    #endregion
    #region Unity_Lifecycle_Methods
    // Methods Unity calls automatically during the object's lifetime.
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

        // Activate flip clamps at takeoff.
        UseFlipAimClamp = true;

        // Wait until physically airborne before
        // accepting any ground collision as landing.
        leftGroundSinceFlip = !isGrounded;

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

    #endregion
    #region Physics_And_Movement_Methods
    // Movement, jumping, collisions, and other physics behavior.
    private void ApplyFlipMovement()
    {
        if (steerFlipTravelWithAim)
        {
            Vector3 targetDirection =
                GetMovementDirection();

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

    #endregion
    #region Unity_Lifecycle_Methods
    // Methods Unity calls automatically during the object's lifetime.
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

            // Intentionally do not change UseFlipAimClamp.
            // It changes only when the collider lands.
        }
    }

    #endregion
    #region Camera_And_Aiming_Methods
    // Camera positioning, looking, and aiming behavior.
    public void AlignFacingToCamera(float worldYaw)
    {
        mouseYaw = worldYaw;

        Quaternion facing =
            Quaternion.Euler(0f, worldYaw, 0f);

        transform.rotation = facing;

        if (rb != null)
            rb.MoveRotation(facing);
    }

    #endregion
    #region Unity_Lifecycle_Methods
    // Methods Unity calls automatically during the object's lifetime.
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

        groundContacts.Clear();
        UseFlipAimClamp = false;
        leftGroundSinceFlip = false;
    }
    #endregion
}
