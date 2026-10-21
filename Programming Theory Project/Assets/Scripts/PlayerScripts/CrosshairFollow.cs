using UnityEngine;

public class CrosshairFollow : MonoBehaviour
{
    [SerializeField] private Transform bulletSpawner;

    void LateUpdate()
    {
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(bulletSpawner.position);
        transform.position = screenPosition;
    }
}
