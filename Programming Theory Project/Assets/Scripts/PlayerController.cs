using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float speed = 10f;
    
    private Rigidbody playerRb;


    private float horIn;
    private float vertIn;
    private float mouseX;
    private float mouseY;


    void Start()
    {
        playerRb = GetComponent<Rigidbody>();
    }


    void Update()
    {
        PlayerInputs();
    }

    void FixedUpdate()
    {

    }

    void PlayerInputs()
    {
        horIn = Input.GetAxis("Horizontal");
        vertIn = Input.GetAxis("Vertical");
        mouseX = Input.GetAxis("Mouse X");
        mouseY = Input.GetAxis("Mouse Y");
    }

    void PlayerMove()
    {

        Vector3 direction = new Vector3(horIn, transform.position.y, vertIn);
        playerRb.linearVelocity = direction * speed;
    }

}
