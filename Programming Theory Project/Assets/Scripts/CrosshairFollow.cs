
using UnityEngine;

[DefaultExecutionOrder(300)]
[RequireComponent(typeof(RectTransform))]
public class CrosshairFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private CameraFollow cameraFollow;

    [Header("Flip Crosshair")]
    [SerializeField] private bool enableFlipCrosshairFreedom = true;

    private RectTransform crosshairRect;
    private Canvas parentCanvas;

    private void Awake()
    {
        crosshairRect = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (cameraFollow == null && aimCamera != null)
        {
            cameraFollow =
                aimCamera.GetComponent<CameraFollow>();
        }
    }

    private void LateUpdate()
    {
        if (crosshairRect == null ||
            aimCamera == null ||
            parentCanvas == null)
            return;

        Vector2 screenCenter = new Vector2(
            Screen.width * 0.5f,
            Screen.height * 0.5f
        );

        Vector2 desiredScreenPosition = screenCenter;

        bool flipTracking =
            enableFlipCrosshairFreedom &&
            cameraFollow != null &&
            cameraFollow.IsTrackingFlip;

        if (flipTracking)
        {
            Vector3 projected =
                aimCamera.WorldToScreenPoint(
                    cameraFollow.LockedHitPoint
                );

            if (projected.z > 0f)
            {
                Vector2 targetScreenPosition =
                    new Vector2(
                        projected.x,
                        projected.y
                    );

                Vector2 offset =
                    targetScreenPosition -
                    screenCenter;

                // Crosshair may drift only within
                // the configured freedom radius.
                offset = Vector2.ClampMagnitude(
                    offset,
                    cameraFollow.CrosshairFreedomRadius
                );

                desiredScreenPosition =
                    screenCenter + offset;
            }
        }

        SetCrosshairScreenPosition(
            desiredScreenPosition
        );
    }

    private void SetCrosshairScreenPosition(
        Vector2 screenPosition)
    {
        if (parentCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay)
        {
            crosshairRect.position = screenPosition;
            return;
        }

        RectTransform canvasRect =
            parentCanvas.transform as RectTransform;

        if (canvasRect == null)
            return;

        Camera canvasCamera =
            parentCanvas.renderMode ==
                RenderMode.ScreenSpaceCamera
            ? parentCanvas.worldCamera
            : aimCamera;

        Vector2 localPoint;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            canvasCamera,
            out localPoint))
        {
            crosshairRect.position =
                canvasRect.TransformPoint(localPoint);
        }
    }
}
