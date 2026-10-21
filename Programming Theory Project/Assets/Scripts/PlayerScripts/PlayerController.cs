using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float speed = 10f;

    [Header("Mouse")]
    [SerializeField] private float mouseSensitivity = 2f;

    private float horIn;
    private float vertIn;
    private float mouseX;
    private Rigidbody playerRb;

    void Awake()
    {
        playerRb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        horIn = Input.GetAxis("Horizontal");
        vertIn = Input.GetAxis("Vertical");
        mouseX += Input.GetAxis("Mouse X") * mouseSensitivity;
    }

    void FixedUpdate()
    {
        // Rigidbody rotation happens with physics, not directly on its Transform.
        playerRb.MoveRotation(playerRb.rotation * Quaternion.Euler(0f, mouseX, 0f));
        mouseX = 0f;

        Vector3 direction = transform.forward * vertIn + transform.right * horIn;
        direction = direction.normalized;

        playerRb.linearVelocity = new Vector3(
            direction.x * speed,
            playerRb.linearVelocity.y,
            direction.z * speed
        );
    }
}
