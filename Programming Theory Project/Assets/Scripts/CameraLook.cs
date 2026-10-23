
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class CameraLook : MonoBehaviour
{
    [Header("Mouse Aim")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -60f;
    [SerializeField] private float maxPitch = 60f;

    [Header("Front Flip Aim")]
    [SerializeField] private float frontFlipMinPitch = -85f;
    [SerializeField] private float frontFlipMaxPitch = 85f;

    [Header("Aim Target")]
    [SerializeField] private float aimRange = 150f;
    [SerializeField] private LayerMask aimLayers = ~0;
    [SerializeField] private float mouseReleaseThreshold = 0.05f;

    [Header("References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CameraFollow cameraFollow;

    private float pitch;
    private Transform anchoredTransform;
    private Vector3 anchoredLocalPoint;
    private Vector3 aimPoint;
    private bool hasAnchor;

    public float Pitch => pitch;
    public Vector3 AimPoint => aimPoint;
    public bool HasAnchor => hasAnchor;

    public Quaternion AimRotation
    {
        get
        {
            float yaw = playerController != null
                ? playerController.MouseYaw
                : transform.eulerAngles.y;

            return Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private void Awake()
    {
        if (playerController == null)
            playerController =
                GetComponentInParent<PlayerController>();

        if (cameraFollow == null)
            cameraFollow =
                FindAnyObjectByType<CameraFollow>();

        pitch = Mathf.DeltaAngle(
            0f,
            transform.localEulerAngles.x
        );

        aimPoint =
            transform.position +
            transform.forward * aimRange;
    }

    private void Update()
    {
        bool frontFlipping =
            playerController != null &&
            playerController.IsFrontFlipping;

        float lowerLimit = frontFlipping
            ? frontFlipMinPitch
            : minPitch;

        float upperLimit = frontFlipping
            ? frontFlipMaxPitch
            : maxPitch;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseY = Input.GetAxisRaw("Mouse Y");
            float mouseX = Input.GetAxisRaw("Mouse X");

            pitch -= mouseY * mouseSensitivity;

            bool mouseMoved =
                Mathf.Abs(mouseX) > mouseReleaseThreshold ||
                Mathf.Abs(mouseY) > mouseReleaseThreshold;

            if (mouseMoved || frontFlipping)
                ReleaseAnchor();
        }

        pitch = Mathf.Clamp(
            pitch,
            lowerLimit,
            upperLimit
        );

        transform.localRotation =
            Quaternion.Euler(pitch, 0f, 0f);
    }

    private void LateUpdate()
    {
        bool flipping =
            playerController != null &&
            playerController.IsFlipping;

        bool frontFlipping =
            playerController != null &&
            playerController.IsFrontFlipping;

        if (!flipping || frontFlipping)
        {
            ReleaseAnchor();
            UpdateAimPoint(false);
        }
        else if (hasAnchor && anchoredTransform != null)
        {
            aimPoint =
                anchoredTransform.TransformPoint(
                    anchoredLocalPoint
                );
        }
        else
        {
            UpdateAimPoint(true);
        }
    }

    private void UpdateAimPoint(bool allowAnchor)
    {
        Vector3 origin = cameraFollow != null
            ? cameraFollow.GetUnrolledAimOrigin()
            : transform.position;

        Vector3 direction =
            AimRotation * Vector3.forward;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            aimRange,
            aimLayers,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = aimRange;
        RaycastHit nearestHit = default;
        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (playerController != null &&
                hit.collider.transform.IsChildOf(
                    playerController.transform
                ))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
                found = true;
            }
        }

        if (found)
        {
            aimPoint = nearestHit.point;

            if (allowAnchor)
            {
                anchoredTransform =
                    nearestHit.collider.transform;

                anchoredLocalPoint =
                    anchoredTransform.InverseTransformPoint(
                        nearestHit.point
                    );

                hasAnchor = true;
            }
        }
        else
        {
            aimPoint = origin + direction * aimRange;
        }
    }

    public void ReleaseAnchor()
    {
        hasAnchor = false;
        anchoredTransform = null;
    }
}
