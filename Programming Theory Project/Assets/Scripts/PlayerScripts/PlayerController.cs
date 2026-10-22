
using UnityEngine;

public class PlayerController : MonoBehaviour
{
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

    private float horIn;
    private float vertIn;
    private float mouseX;

    private bool jumpKeyPressed;
    private bool dashKeyPressed;
    private bool isGrounded;
    private bool isDashing;

    private float dashTimer;
    private float cooldownTimer;

    private Vector3 dashDirection;

    private Rigidbody playerRb;

    void Awake()
    {
        playerRb = GetComponent<Rigidbody>();

        // Custom gravity replaces Unity's default gravity.
        playerRb.useGravity = false;
    }

    void Update()
    {
        horIn = Input.GetAxis("Horizontal");
        vertIn = Input.GetAxis("Vertical");

        mouseX += Input.GetAxis("Mouse X") * mouseSensitivity;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpKeyPressed = true;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            dashKeyPressed = true;
        }
    }

    void FixedUpdate()
    {
        // Rotate player.
        playerRb.MoveRotation(
            playerRb.rotation * Quaternion.Euler(0f, mouseX, 0f)
        );

        mouseX = 0f;

        // Calculate movement direction.
        Vector3 direction = transform.forward * vertIn +
                            transform.right * horIn;

        direction = direction.normalized;

        // Dash cooldown.
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.fixedDeltaTime;
        }

        // Start dash.
        if (dashKeyPressed && !isDashing && cooldownTimer <= 0f)
        {
            isDashing = true;
            dashTimer = dashDuration;
            cooldownTimer = dashCooldown;

            dashDirection = direction;

            // Dash forward if no movement key is pressed.
            if (dashDirection.magnitude == 0f)
            {
                dashDirection = transform.forward;
            }
        }

        dashKeyPressed = false;

        // Apply dash or normal movement.
        if (isDashing)
        {
            playerRb.linearVelocity = new Vector3(
                dashDirection.x * dashSpeed,
                playerRb.linearVelocity.y,
                dashDirection.z * dashSpeed
            );

            dashTimer -= Time.fixedDeltaTime;

            if (dashTimer <= 0f)
            {
                isDashing = false;
            }
        }
        else
        {
            playerRb.linearVelocity = new Vector3(
                direction.x * speed,
                playerRb.linearVelocity.y,
                direction.z * speed
            );
        }

        // Jump.
        if (jumpKeyPressed && isGrounded)
        {
            playerRb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
        }

        jumpKeyPressed = false;

        // Apply custom gravity.
        playerRb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
    }

    void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isGrounded = true;
                break;
            }
        }
    }

    void OnCollisionExit(Collision collision)
    {
        isGrounded = false;
    }
}
