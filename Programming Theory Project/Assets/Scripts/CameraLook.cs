
using UnityEngine;

public class CameraLook : MonoBehaviour
{
    [SerializeField] float mouseSensitivity = 2f;
    [SerializeField] float minRotation = -60f;
    [SerializeField] float maxRotation = 60f;

    float mouseY;
    float verticalRotation;

    void Update()
    {
        mouseY = Input.GetAxis("Mouse Y");

        verticalRotation -= mouseY * mouseSensitivity;

        verticalRotation = Mathf.Clamp(verticalRotation, minRotation, maxRotation);

        transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
    }
}
