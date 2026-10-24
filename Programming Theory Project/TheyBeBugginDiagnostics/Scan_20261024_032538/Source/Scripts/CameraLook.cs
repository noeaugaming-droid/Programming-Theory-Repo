
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class CameraLook : MonoBehaviour
{
    #region Camera_And_Aiming_Variables
    // Values and references used by this part of the script.
    [Header("Mouse Settings")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Header("Vertical Aim Limits")]
    [SerializeField] private float minRotation = -60f;
    [SerializeField] private float maxRotation = 60f;

    [Header("Shooting Aim")]
    [SerializeField] private float aimDistance = 1000f;
    [SerializeField] private LayerMask aimLayers = ~0;

    private float verticalRotation;
    private Camera mainCamera;

    public float Pitch => verticalRotation;

    public Vector3 AimPoint { get; private set; }

    public float AimYaw => transform.eulerAngles.y;

    public Quaternion AimRotation => transform.rotation;

    public Vector3 AimDirection => transform.forward;

    #endregion
    #region Unity_Lifecycle_Methods
    // Methods Unity calls automatically during the object's lifetime.
    private void Awake()
    {
        mainCamera = Camera.main;

        verticalRotation = Mathf.DeltaAngle(
            0f,
            transform.localEulerAngles.x
        );

        verticalRotation = Mathf.Clamp(
            verticalRotation,
            Mathf.Max(-89f, Mathf.Min(minRotation, maxRotation)),
            Mathf.Min(89f, Mathf.Max(minRotation, maxRotation))
        );

        ApplyPivotRotation();

        AimPoint =
            transform.position +
            transform.forward * aimDistance;
    }

    private void Update()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseY = Input.GetAxis("Mouse Y");

            verticalRotation -= mouseY * mouseSensitivity;
        }

        float minimumPitch = Mathf.Max(
            -89f,
            Mathf.Min(minRotation, maxRotation)
        );

        float maximumPitch = Mathf.Min(
            89f,
            Mathf.Max(minRotation, maxRotation)
        );

        verticalRotation = Mathf.Clamp(
            verticalRotation,
            minimumPitch,
            maximumPitch
        );

        ApplyPivotRotation();

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    #endregion
    #region Camera_And_Aiming_Methods
    // Camera positioning, looking, and aiming behavior.
    private void ApplyPivotRotation()
    {
        transform.localRotation = Quaternion.Euler(
            verticalRotation,
            0f,
            0f
        );
    }

    #endregion
    #region Unity_Lifecycle_Methods
    // Methods Unity calls automatically during the object's lifetime.
    private void LateUpdate()
    {
        if (mainCamera != null)
            RefreshAimPoint(mainCamera);
    }

    #endregion
    #region Camera_And_Aiming_Methods
    // Camera positioning, looking, and aiming behavior.
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
    #endregion
}
