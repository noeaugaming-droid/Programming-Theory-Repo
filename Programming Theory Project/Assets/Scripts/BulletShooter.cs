
using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class BulletShooter : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] private Camera aimCamera;
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
    }

    private void LateUpdate()
    {
        if (Input.GetMouseButtonDown(0) && canShoot)
        {
            if (aimCamera == null ||
                bulletSpawnPoint == null ||
                bulletPrefab == null)
            {
                Debug.LogWarning(
                    "BulletShooter: Missing a reference!"
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
        // Raycast from the final camera view.
        Ray cameraRay = aimCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        Vector3 aimPoint;

        if (Physics.Raycast(
            cameraRay,
            out RaycastHit cameraHit,
            range,
            hitLayers,
            QueryTriggerInteraction.Ignore))
        {
            aimPoint = cameraHit.point;

            Debug.Log(
                "Camera ray hit: " +
                cameraHit.collider.name +
                " at " + aimPoint
            );
        }
        else
        {
            aimPoint = cameraRay.GetPoint(range);

            Debug.Log(
                "Camera ray hit nothing. " +
                "Aiming at: " + aimPoint
            );
        }

        // Bullet always spawns from the gun.
        Vector3 spawnPosition =
            bulletSpawnPoint.position;

        // Aim from the gun toward the camera target.
        Vector3 direction =
            aimPoint - spawnPosition;

        if (direction.sqrMagnitude < 0.0001f)
            direction = cameraRay.direction;
        else
            direction.Normalize();

        if (showDebugRays)
        {
            Debug.DrawLine(
                cameraRay.origin,
                aimPoint,
                Color.green,
                3f
            );

            Debug.DrawRay(
                spawnPosition,
                direction * range,
                Color.red,
                3f
            );
        }

        Debug.Log(
            "Bullet spawn: " + spawnPosition +
            " | Direction: " + direction +
            " | Aim point: " + aimPoint
        );

        Quaternion bulletRotation =
            bulletSpawnPoint.rotation;

        GameObject bullet = Instantiate(
            bulletPrefab,
            spawnPosition,
            bulletRotation
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
                bulletSpeed * Time.deltaTime;

            moveDistance = Mathf.Min(
                moveDistance,
                range - distanceTraveled
            );

            Vector3 currentPosition =
                bullet.transform.position;

            if (Physics.Raycast(
                currentPosition,
                direction,
                out RaycastHit hit,
                moveDistance,
                hitLayers,
                QueryTriggerInteraction.Ignore))
            {
                bullet.transform.position = hit.point;

                Debug.Log(
                    "Bullet hit: " + hit.collider.name
                );

                Enemy enemy =
                    hit.collider.GetComponentInParent<Enemy>();

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
