
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(200)]
public class CrosshairFollow : MonoBehaviour
{
    #region Camera_And_Aiming_Variables
    // Values and references used by this part of the script.
    #endregion
    #region UI_And_Game_State_Variables
    // Values and references used by this part of the script.
    #endregion
    #region General_Variables
    // Values and references used by this part of the script.
    [SerializeField] private RectTransform crosshairRect;
    [SerializeField] private Canvas canvas;
    private Graphic graphic;

    #endregion
    #region Unity_Lifecycle_Methods
    // Methods Unity calls automatically during the object's lifetime.
    private void Awake()
    {
        if (crosshairRect == null) crosshairRect = GetComponent<RectTransform>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        graphic = GetComponent<Graphic>();
    }

    private void LateUpdate()
    {
        if (crosshairRect == null || canvas == null) return;
        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, center, uiCamera, out Vector2 local))
            crosshairRect.anchoredPosition = local;
        if (graphic != null) graphic.enabled = true;
    }
    #endregion
}

