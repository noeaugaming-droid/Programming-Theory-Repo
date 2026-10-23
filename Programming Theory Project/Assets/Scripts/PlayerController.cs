
using UnityEngine;

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

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 7f;

    [Header("Gravity")]
    [SerializeField] private float gravity = 20f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 25f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1f;

    [Header("Flip Visual")]
    [SerializeField] private Transform visualModel;

    [Header("Double Tap")]
    [Tooltip("Maximum seconds between Space presses.")]
    [Range(0.1f, 0.8f)]
    [SerializeField] private float doubleTapWindow = 0.3f;

    [Header("Flip Movement")]
    [SerializeField] private float flipUpwardVelocity = 9f;
    [SerializeField] private float flipHorizontalSpeed = 12f;
    [SerializeField] private float flipDuration = 0.8f;

    [Header("Midair Slowdown")]
    [Range(0.05f, 1f)]
    [SerializeField] private float flipGravityMultiplier = 0.35f;

    [Range(0f, 1f)]
    [SerializeField] private float apexVelocityDamping = 0.8f;

    [Range(0.05f, 0.5f)]
    [SerializeField] private float apexWindow = 0.18f;

    private float horIn;
    private float vertIn;

    private bool jumpKeyPressed;
    private bool dashKeyPressed;
    private bool isGrounded;
    private bool isDashing;
    private bool isFlipping;

    private float dashTimer;
    private float cooldownTimer;
    private float lastJumpTime = -100f;
    private float flipTimer;
    private float flipAngle;

    private Vector3 dashDirection;
    private Vector3 flipDirection;

    private Rigidbody playerRb;
    private Quaternion originalVisualRotation;

    private TrickType currentTrick = TrickType.None;

    public bool IsFlipping => isFlipping;
    public TrickType CurrentTrick => currentTrick;
    public float FlipAngle => flipAngle;

    public float FlipProgress =>
        isFlipping
            ? Mathf.Clamp01(
                flipTimer / Mathf.Max(0.01f, flipDuration)
            )
            : 0f;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();
        playerRb.useGravity = false;

        if (visualModel != null)
        {
            originalVisualRotation = visualModel.localRotation;
        }
        else
        {
            Debug.LogWarning(
                "PlayerController: Assign Visual Model to see flips."
            );
        }
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            horIn = 0f;
            vertIn = 0f;
            return;
        }

        horIn = Input.GetAxis("Horizontal");
        vertIn = Input.GetAxis("Vertical");

        float mouseX =
            Input.GetAxis("Mouse X") * mouseSensitivity;

        playerRb.MoveRotation(
            playerRb.rotation *
            Quaternion.Euler(0f, mouseX, 0f)
        );

        if (Input.GetKeyDown(KeyCode.Space))
        {
            HandleSpacePress();
        }

        if (Input.GetKeyDown(KeyCode.Q) && !isFlipping)
        {
            dashKeyPressed = true;
        }

        UpdateFlipVisual();
    }

    private void HandleSpacePress()
    {
        if (isFlipping)
            return;

        float timeSinceLastJump =
            Time.time - lastJumpTime;

        if (lastJumpTime >= 0f &&
            timeSinceLastJump <= doubleTapWindow)
        {
            lastJumpTime = -100f;

            if (!isGrounded || jumpKeyPressed)
            {
                StartFlip();
            }

            return;
        }

        if (isGrounded)
        {
            jumpKeyPressed = true;
            lastJumpTime = Time.time;
        }
    }

    private void StartFlip()
    {
        isFlipping = true;
        flipTimer = 0f;
        flipAngle = 0f;

        isDashing = false;
        dashKeyPressed = false;
        jumpKeyPressed = false;

        if (Mathf.Abs(horIn) > Mathf.Abs(vertIn))
        {
            if (horIn < 0f)
            {
                currentTrick = TrickType.CartwheelLeft;
                flipDirection = -transform.right;
            }
            else
            {
                currentTrick = TrickType.CartwheelRight;
                flipDirection = transform.right;
            }
        }
        else if (vertIn < -0.1f)
        {
            currentTrick = TrickType.BackFlip;
            flipDirection = -transform.forward;
        }
        else
        {
            currentTrick = TrickType.FrontFlip;
            flipDirection = transform.forward;
        }

        playerRb.linearVelocity = new Vector3(
            flipDirection.x * flipHorizontalSpeed,
            flipUpwardVelocity,
            flipDirection.z * flipHorizontalSpeed
        );
    }

    private void UpdateFlipVisual()
    {
        if (!isFlipping)
            return;

        flipTimer += Time.deltaTime;

        float progress = Mathf.Clamp01(
            flipTimer / Mathf.Max(0.01f, flipDuration)
        );

        float easedProgress =
            progress * progress * (3f - 2f * progress);

        flipAngle = easedProgress * 360f;

        if (visualModel != null)
        {
            Vector3 axis = Vector3.right;
            float signedAngle = flipAngle;

            switch (currentTrick)
            {
                case TrickType.CartwheelLeft:
                    axis = Vector3.forward;
                    signedAngle = flipAngle;
                    break;

                case TrickType.CartwheelRight:
                    axis = Vector3.forward;
                    signedAngle = -flipAngle;
                    break;

                case TrickType.FrontFlip:
                    axis = Vector3.right;
                    signedAngle = flipAngle;
                    break;

                case TrickType.BackFlip:
                    axis = Vector3.right;
                    signedAngle = -flipAngle;
                    break;
            }

            visualModel.localRotation =
                originalVisualRotation *
                Quaternion.AngleAxis(
                    signedAngle,
                    axis
                );
        }

        if (progress >= 1f)
        {
            FinishFlip();
        }
    }

    private void FinishFlip()
    {
        isFlipping = false;
        currentTrick = TrickType.None;
        flipAngle = 0f;

        if (visualModel != null)
        {
            visualModel.localRotation =
                originalVisualRotation;
        }
    }

    private void FixedUpdate()
    {
        bool groundedLastStep = isGrounded;
        isGrounded = false;

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.fixedDeltaTime;
        }

        if (isFlipping)
        {
            HandleFlipPhysics();

            jumpKeyPressed = false;
            dashKeyPressed = false;

            return;
        }

        Vector3 direction =
            (
                transform.forward * vertIn +
                transform.right * horIn
            ).normalized;

        if (dashKeyPressed &&
            !isDashing &&
            cooldownTimer <= 0f)
        {
            isDashing = true;
            dashTimer = dashDuration;
            cooldownTimer = dashCooldown;

            dashDirection = direction;

            if (dashDirection.sqrMagnitude == 0f)
            {
                dashDirection = transform.forward;
            }
        }

        dashKeyPressed = false;

        Vector3 movement = direction * speed;

        if (isDashing)
        {
            movement = dashDirection * dashSpeed;

            dashTimer -= Time.fixedDeltaTime;

            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
        }

        playerRb.linearVelocity = new Vector3(
            movement.x,
            playerRb.linearVelocity.y,
            movement.z
        );

        if (jumpKeyPressed && groundedLastStep)
        {
            playerRb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );

            isGrounded = false;
        }

        jumpKeyPressed = false;

        playerRb.AddForce(
            Vector3.down * gravity,
            ForceMode.Acceleration
        );
    }

    private void HandleFlipPhysics()
    {
        Vector3 velocity = playerRb.linearVelocity;

        velocity.x =
            flipDirection.x * flipHorizontalSpeed;

        velocity.z =
            flipDirection.z * flipHorizontalSpeed;

        if (Mathf.Abs(velocity.y) < apexWindow)
        {
            velocity.y *= apexVelocityDamping;
        }

        playerRb.linearVelocity = velocity;

        playerRb.AddForce(
            Vector3.down *
            gravity *
            flipGravityMultiplier,
            ForceMode.Acceleration
        );
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isGrounded = true;
                return;
            }
        }
    }

    private void OnDisable()
    {
        if (visualModel != null)
        {
            visualModel.localRotation =
                originalVisualRotation;
        }

        isFlipping = false;
        currentTrick = TrickType.None;

        jumpKeyPressed = false;
        dashKeyPressed = false;
    }
}
