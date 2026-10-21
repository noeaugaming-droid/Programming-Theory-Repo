using UnityEngine;

public class CameraLook : MonoBehaviour
{
    [Header("Mouse Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minRotation = -60f;
    [SerializeField] private float maxRotation = 60f;

    private float verticalRotation;

    void Update()
    {
        float mouseY = Input.GetAxis("Mouse Y");
        verticalRotation -= mouseY * mouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, minRotation, maxRotation);
        transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
    }
}
