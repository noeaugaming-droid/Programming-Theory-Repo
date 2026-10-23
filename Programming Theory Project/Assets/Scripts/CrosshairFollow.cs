
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public class CrosshairFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraLook cameraLook;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private RectTransform crosshairRect;
    [SerializeField] private Canvas canvas;

    [Header("Screen Behavior")]
    [SerializeField] private bool clampToScreen = true;
    [SerializeField] private float screenPadding = 30f;

    [Header("Flip Crosshair")]
    [SerializeField] private bool alwaysVisibleDuringFlips = true;

    private Graphic crosshairGraphic;

    private void Awake()
    {
        if (cameraLook == null)
            cameraLook = FindAnyObjectByType<CameraLook>();

        if (cameraFollow == null)
            cameraFollow = FindAnyObjectByType<CameraFollow>();

        if (playerController == null)
            playerController = FindAnyObjectByType<PlayerController>();

        if (crosshairRect == null)
            crosshairRect = GetComponent<RectTransform>();

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        crosshairGraphic = GetComponent<Graphic>();
    }

    private void LateUpdate()
    {
        if (cameraLook == null ||
            cameraFollow == null ||
            crosshairRect == null ||
            canvas == null)
            return;

        Camera cam = cameraFollow.AttachedCamera;

        if (cam == null)
            return;

        bool verticalFlip =
            playerController != null &&
            playerController.IsFlipping &&
            (
                playerController.CurrentTrick ==
                    PlayerController.TrickType.FrontFlip ||
                playerController.CurrentTrick ==
                    PlayerController.TrickType.BackFlip
            );

        Vector3 screenPoint =
            cam.WorldToScreenPoint(cameraLook.AimPoint);

        bool behindCamera = screenPoint.z <= 0f;

        if (behindCamera)
        {
            if (verticalFlip && alwaysVisibleDuringFlips)
            {
                screenPoint = new Vector3(
                    Screen.width * 0.5f,
                    Screen.height * 0.5f,
                    1f
                );
            }
            else
            {
                if (crosshairGraphic != null)
                    crosshairGraphic.enabled = false;

                return;
            }
        }

        if (crosshairGraphic != null)
            crosshairGraphic.enabled = true;

        if (clampToScreen)
        {
            float padding = Mathf.Max(
                0f,
                screenPadding
            );

            screenPoint.x = Mathf.Clamp(
                screenPoint.x,
                padding,
                Screen.width - padding
            );

            screenPoint.y = Mathf.Clamp(
                screenPoint.y,
                padding,
                Screen.height - padding
            );
        }

        RectTransform canvasRect =
            canvas.transform as RectTransform;

        if (canvasRect == null)
            return;

        Camera uiCamera =
            canvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        if (RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                uiCamera,
                out Vector2 localPoint))
        {
            crosshairRect.anchoredPosition = localPoint;
        }
    }
}
