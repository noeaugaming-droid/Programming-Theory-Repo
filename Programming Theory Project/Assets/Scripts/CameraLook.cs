
using UnityEngine;

[DefaultExecutionOrder(10)]
public class CameraLook : MonoBehaviour
{
    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;

    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 60f;

    private float pitch;

    private void Awake()
    {
        pitch = Mathf.DeltaAngle(
            0f,
            transform.localEulerAngles.x
        );

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        // Raw input: no additional mouse-axis filtering.
        float mouseY =
            Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

        pitch -= mouseY;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch
        );

        transform.localRotation =
            Quaternion.Euler(pitch, 0f, 0f);
    }
}
