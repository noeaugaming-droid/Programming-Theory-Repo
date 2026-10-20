using UnityEngine;

public class PlayerController : MonoBehaviour
{

    //==_Floats_==//
    [Header("Movement")]
    [SerializeField] float speed = 10f;
    [Header("Mouse")]
    [SerializeField] float mouseSensitivity = 2f;
    
    private float horIn;
    private float vertIn;
    private float mouseX;

    //==_Type_References_==//
    private Rigidbody playerRb;

    void Start()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        playerRb = GetComponent<Rigidbody>();
    }


    void Update()
    {
        PlayerInputs();
        MouseLookH();
    }

    void FixedUpdate()
    {
        PlayerMove();
    }

    void PlayerInputs()
    {
        horIn = Input.GetAxis("Horizontal");
        vertIn = Input.GetAxis("Vertical");
        mouseX = Input.GetAxis("Mouse X");
    }

    void PlayerMove()
    {
        Vector3 forwardMovement = transform.forward * vertIn;
        Vector3 sidewaysMovement = transform.right * horIn;
        Vector3 direction = forwardMovement + sidewaysMovement;
        direction = direction.normalized;

        playerRb.linearVelocity = new Vector3(direction.x * speed, playerRb.linearVelocity.y, direction.z * speed);
    }

    void MouseLookH()
    {
        Vector3 horizontalLook = new Vector3(0, mouseX, 0);
        playerRb.transform.Rotate(horizontalLook * mouseSensitivity);
       
    }



}
