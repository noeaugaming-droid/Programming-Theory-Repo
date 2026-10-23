
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

    private bool jumpKeyPressed;
    private bool dashKeyPressed;
    private bool isGrounded;
    private bool isDashing;

    private float dashTimer;
    private float cooldownTimer;

    private Vector3 dashDirection;
    private Rigidbody playerRb;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();
        playerRb.useGravity = false;
    }

    private void Update()
    {
        bool cursorLocked =
            Cursor.lockState == CursorLockMode.Locked;

        if (cursorLocked)
        {
            horIn = Input.GetAxis("Horizontal");
            vertIn = Input.GetAxis("Vertical");

            float mouseX =
                Input.GetAxis("Mouse X") * mouseSensitivity;

            playerRb.MoveRotation(
                playerRb.rotation *
                Quaternion.Euler(0f, mouseX, 0f)
            );

            if (Input.GetKeyDown(KeyCode.Space))
                jumpKeyPressed = true;

            if (Input.GetKeyDown(KeyCode.Q))
                dashKeyPressed = true;
        }
        else
        {
            horIn = 0f;
            vertIn = 0f;
        }
    }

    private void FixedUpdate()
    {
        bool groundedLastStep = isGrounded;
        isGrounded = false;

        Vector3 direction =
            transform.forward * vertIn +
            transform.right * horIn;

        direction = direction.normalized;

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.fixedDeltaTime;

        if (dashKeyPressed &&
            !isDashing &&
            cooldownTimer <= 0f)
        {
            isDashing = true;
            dashTimer = dashDuration;
            cooldownTimer = dashCooldown;

            dashDirection = direction;

            if (dashDirection.sqrMagnitude == 0f)
                dashDirection = transform.forward;
        }

        dashKeyPressed = false;

        Vector3 movement = direction * speed;

        if (isDashing)
        {
            movement = dashDirection * dashSpeed;
            dashTimer -= Time.fixedDeltaTime;

            if (dashTimer <= 0f)
                isDashing = false;
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
}
