
using UnityEngine;

public class CameraLook : MonoBehaviour
{
    [Header("Mouse Settings")]
    [SerializeField] float mouseSensitivity = 2f;
    [SerializeField] float minRotation = -60f;
    [SerializeField] float maxRotation = 60f;

    float mouseY;
    float verticalRotation;

    void Update()
    {
        // Get vertical mouse movement
        mouseY = Input.GetAxis("Mouse Y");

        // Rotate camera up and down
        verticalRotation -= mouseY * mouseSensitivity;

        // Limit how far the camera can rotate
        verticalRotation = Mathf.Clamp(verticalRotation, minRotation, maxRotation);

        transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }
}
