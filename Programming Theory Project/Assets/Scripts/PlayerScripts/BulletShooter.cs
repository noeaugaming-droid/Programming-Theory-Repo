using System.Collections;
using UnityEngine;

public class BulletShooter : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float range = 100f;
    [SerializeField] private float shootDelay = 0.25f;

    [Header("Bullet Movement")]
    [SerializeField] private float bulletSpeed = 30f;

    [Header("Collision")]
    [SerializeField] private LayerMask hitLayers = ~0;

    private bool canShoot = true;

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && canShoot)
        {
            canShoot = false;
            StartCoroutine(Shoot());
            StartCoroutine(ShootDelay());
        }
    }

    IEnumerator Shoot()
    {
        Vector3 direction = bulletSpawnPoint.forward;
        GameObject bullet = Instantiate(bulletPrefab, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        float distanceTraveled = 0f;

        while (distanceTraveled < range)
        {
            float moveDistance = bulletSpeed * Time.deltaTime;
            if (distanceTraveled + moveDistance > range)
            {
                moveDistance = range - distanceTraveled;
            }

            if (Physics.Raycast(bullet.transform.position, direction, out RaycastHit hit,
                moveDistance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                bullet.transform.position = hit.point;
                Enemy enemy = hit.collider.GetComponentInParent<Enemy>();

                if (enemy != null)
                {
                    Spider spider = enemy as Spider;
                    if (spider != null) spider.Knockback(direction);

                    Beetle beetle = enemy as Beetle;
                    if (beetle != null) beetle.Knockback(direction);

                    enemy.TakeDamage(20f);
                }

                Debug.Log("Bullet hit: " + hit.collider.gameObject.name);
                break;
            }

            bullet.transform.position += direction * moveDistance;
            distanceTraveled += moveDistance;
            yield return null;
        }

        Destroy(bullet);
    }

    IEnumerator ShootDelay()
    {
        yield return new WaitForSeconds(shootDelay);
        canShoot = true;
    }
}
