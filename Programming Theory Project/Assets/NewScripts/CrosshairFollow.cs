using UnityEngine;

public class CrosshairFollow : MonoBehaviour
{
    [SerializeField] private Transform bulletSpawner;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (bulletSpawner == null || mainCamera == null) return;
        transform.position = mainCamera.WorldToScreenPoint(bulletSpawner.position);
    }
}
