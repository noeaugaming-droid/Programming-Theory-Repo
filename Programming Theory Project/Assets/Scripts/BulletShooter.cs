
using System.Collections;
using UnityEngine;

public class BulletShooter : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private CameraLook cameraLook;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float range = 100f;
    [SerializeField] private float shootDelay = 0.25f;
    [SerializeField] private float damage = 20f;

    [Header("Bullet Movement")]
    [SerializeField] private float bulletSpeed = 30f;

    [Header("Collision")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Debug")]
    [SerializeField] private bool showDebugRays = true;

    private bool canShoot = true;

    private void Awake()
    {
        if (aimCamera == null)
            aimCamera = Camera.main;

        if (cameraLook == null)
            cameraLook = FindAnyObjectByType<CameraLook>();
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (Input.GetMouseButtonDown(0) && canShoot)
        {
            if (bulletSpawnPoint == null ||
                bulletPrefab == null)
            {
                Debug.LogWarning(
                    "BulletShooter: Missing bullet references.",
                    this
                );
                return;
            }

            canShoot = false;

            Fire();
            StartCoroutine(ShootDelay());
        }
    }

    private void Fire()
    {
        Vector3 aimPoint;

        if (cameraLook != null)
        {
            aimPoint = cameraLook.AimPoint;
        }
        else if (aimCamera != null)
        {
            Ray ray = aimCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f)
            );

            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                range,
                hitLayers,
                QueryTriggerInteraction.Ignore))
            {
                aimPoint = hit.point;
            }
            else
            {
                aimPoint = ray.GetPoint(range);
            }
        }
        else
        {
            Debug.LogWarning(
                "BulletShooter: No aiming reference.",
                this
            );
            return;
        }

        Vector3 spawnPosition =
            bulletSpawnPoint.position;

        Vector3 direction =
            aimPoint - spawnPosition;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction =
                bulletSpawnPoint.forward;
        }

        direction.Normalize();

        if (showDebugRays)
        {
            Debug.DrawLine(
                spawnPosition,
                aimPoint,
                Color.green,
                2f
            );

            Debug.DrawRay(
                spawnPosition,
                direction * range,
                Color.red,
                2f
            );
        }

        GameObject bullet = Instantiate(
            bulletPrefab,
            spawnPosition,
            bulletSpawnPoint.rotation
        );

        StartCoroutine(
            MoveBullet(bullet, direction)
        );
    }

    private IEnumerator MoveBullet(
        GameObject bullet,
        Vector3 direction)
    {
        float distanceTraveled = 0f;

        while (bullet != null &&
               distanceTraveled < range)
        {
            float moveDistance =
                Mathf.Min(
                    bulletSpeed * Time.deltaTime,
                    range - distanceTraveled
                );

            Vector3 currentPosition =
                bullet.transform.position;

            RaycastHit[] hits = Physics.RaycastAll(
                currentPosition,
                direction,
                moveDistance,
                hitLayers,
                QueryTriggerInteraction.Ignore
            );

            RaycastHit nearestHit = default;
            float nearestDistance = moveDistance;
            bool foundHit = false;

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                if (hit.collider.transform.IsChildOf(
                    transform.root
                ))
                    continue;

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    nearestHit = hit;
                    foundHit = true;
                }
            }

            if (foundHit)
            {
                bullet.transform.position =
                    nearestHit.point;

                Enemy enemy =
                    nearestHit.collider
                    .GetComponentInParent<Enemy>();

                if (enemy != null)
                {
                    Spider spider = enemy as Spider;

                    if (spider != null)
                        spider.Knockback(direction);

                    Beetle beetle = enemy as Beetle;

                    if (beetle != null)
                        beetle.Knockback(direction);

                    enemy.TakeDamage(damage);
                }

                break;
            }

            bullet.transform.position +=
                direction * moveDistance;

            distanceTraveled += moveDistance;

            yield return null;
        }

        if (bullet != null)
            Destroy(bullet);
    }

    private IEnumerator ShootDelay()
    {
        yield return new WaitForSeconds(shootDelay);
        canShoot = true;
    }
}
