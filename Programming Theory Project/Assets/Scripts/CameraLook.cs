
using UnityEngine;

public class CameraLook : MonoBehaviour
{
    [Header("Mouse Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minRotation = -60f;
    [SerializeField] private float maxRotation = 60f;

    private float verticalRotation;

    private void Start()
    {
        float startingX = transform.localEulerAngles.x;

        if (startingX > 180f)
            startingX -= 360f;

        verticalRotation = Mathf.Clamp(
            startingX,
            minRotation,
            maxRotation
        );
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        float mouseY = Input.GetAxis("Mouse Y");

        verticalRotation -= mouseY * mouseSensitivity;

        verticalRotation = Mathf.Clamp(
            verticalRotation,
            minRotation,
            maxRotation
        );

        transform.localRotation = Quaternion.Euler(
            verticalRotation,
            0f,
            0f
        );
    }
}
