
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class CameraLook : MonoBehaviour
{
    [Header("Mouse")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Header("Normal Pitch Limits")]
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("Flip Pitch Limits")]
    [SerializeField] private float flipMinPitch = -170f;
    [SerializeField] private float flipMaxPitch = 170f;

    [Header("References")]
    [SerializeField] private PlayerController playerController;

    private float pitch;

    public float Pitch => pitch;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController =
                GetComponentInParent<PlayerController>();
        }

        pitch = Mathf.DeltaAngle(
            0f,
            transform.localEulerAngles.x
        );
    }

    private void Update()
    {
        bool flipping =
            playerController != null &&
            playerController.IsFlipping;

        float minimum =
            flipping ? flipMinPitch : minPitch;

        float maximum =
            flipping ? flipMaxPitch : maxPitch;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            pitch -=
                Input.GetAxisRaw("Mouse Y") *
                mouseSensitivity;
        }

        pitch = Mathf.Clamp(
            pitch,
            minimum,
            maximum
        );

        transform.localRotation =
            Quaternion.Euler(pitch, 0f, 0f);
    }

    public void SetPitch(float newPitch)
    {
        pitch = newPitch;

        transform.localRotation =
            Quaternion.Euler(pitch, 0f, 0f);
    }
}
