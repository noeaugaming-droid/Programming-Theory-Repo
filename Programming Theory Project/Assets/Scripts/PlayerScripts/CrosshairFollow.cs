
using UnityEngine;

public class CrosshairFollow : MonoBehaviour
{
    [SerializeField] Transform bulletSpawner;

    void LateUpdate()
    {
        // Convert bullet spawner position to screen position
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(bulletSpawner.position);

        // Move crosshair to that screen position
        transform.position = screenPosition;
    }
}
