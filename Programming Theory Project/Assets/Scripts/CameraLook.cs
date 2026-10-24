
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class CameraLook : MonoBehaviour
{
    [Header("Mouse Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minRotation = -60f;
    [SerializeField] private float maxRotation = 60f;

    [Header("Shooting Aim")]
    [SerializeField] private float aimDistance = 1000f;
    [SerializeField] private LayerMask aimLayers = ~0;

    private float verticalRotation;

    private Camera mainCamera;

    // Used by the restored CameraFollow.
    public float Pitch => verticalRotation;

    // Used by BulletShooter.
    public Vector3 AimPoint { get; private set; }

    // Compatibility properties for other scripts.
    public float AimYaw => transform.eulerAngles.y;

    public Quaternion AimRotation => transform.rotation;

    public Vector3 AimDirection =>
        transform.forward;

    private void Awake()
    {
        mainCamera = Camera.main;

        verticalRotation = Mathf.DeltaAngle(
            0f,
            transform.localEulerAngles.x
        );

        AimPoint =
            transform.position +
            transform.forward * aimDistance;
    }

    private void Update()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseY =
                Input.GetAxis("Mouse Y");

            verticalRotation -=
                mouseY * mouseSensitivity;

            verticalRotation = Mathf.Clamp(
                verticalRotation,
                minRotation,
                maxRotation
            );

            transform.localRotation =
                Quaternion.Euler(
                    verticalRotation,
                    0f,
                    0f
                );
        }

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (mainCamera != null)
            RefreshAimPoint(mainCamera);
    }

    public void RefreshAimPoint(Camera cam)
    {
        if (cam == null)
            return;

        Ray ray = cam.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            aimDistance,
            aimLayers,
            QueryTriggerInteraction.Ignore
        );

        float closestDistance = aimDistance;

        Vector3 targetPoint =
            ray.origin +
            ray.direction * aimDistance;

        Transform playerRoot = transform.root;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            // Don't aim at the player's own collider.
            if (hit.collider.transform.IsChildOf(playerRoot))
                continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                targetPoint = hit.point;
            }
        }

        AimPoint = targetPoint;
    }
}
