
using System.Collections;
using UnityEngine;

public class BulletShooter : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] Transform bulletSpawnPoint;
    [SerializeField] GameObject bulletPrefab;
    [SerializeField] float range = 100f;
    [SerializeField] float shootDelay = 0.25f;

    [Header("Bullet Movement")]
    [SerializeField] float bulletSpeed = 30f;

    [Header("Collision")]
    [SerializeField] LayerMask hitLayers = ~0;

    bool canShoot = true;

    void Update()
    {
        // Shoot when left mouse button is pressed
        if (Input.GetMouseButtonDown(0) && canShoot)
        {
            canShoot = false;

            StartCoroutine(Shoot());
            StartCoroutine(ShootDelay());
        }
    }

    IEnumerator Shoot()
    {
        // Get bullet starting position and direction
        Vector3 spawnPosition = bulletSpawnPoint.position;
        Vector3 direction = bulletSpawnPoint.forward;

        // Create visible bullet
        GameObject bullet = Instantiate(
            bulletPrefab,
            spawnPosition,
            bulletSpawnPoint.rotation
        );

        float distanceTraveled = 0f;

        // Keep moving until bullet hits something or reaches max range
        while (distanceTraveled < range)
        {
            float moveDistance = bulletSpeed * Time.deltaTime;

            // Prevent bullet from exceeding its maximum range
            if (distanceTraveled + moveDistance > range)
            {
                moveDistance = range - distanceTraveled;
            }

            // Check for collisions in front of bullet
            RaycastHit hit;

            if (Physics.Raycast(
                bullet.transform.position,
                direction,
                out hit,
                moveDistance,
                hitLayers,
                QueryTriggerInteraction.Ignore))
            {
                // Move bullet to the impact point
                bullet.transform.position = hit.point;

                Debug.Log("Bullet hit: " + hit.collider.gameObject.name);
                Enemy enemy = hit.collider.GetComponentInParent<Enemy>();

                if (enemy != null)
                {
                    Spider spider = enemy as Spider;

                    if (spider != null)
                    {
                        spider.Knockback(direction);
                    }

                    Beetle beetle = enemy as Beetle;

                    if (beetle != null)
                    {
                        beetle.Knockback(direction);
                    }

                    enemy.TakeDamage(20f);
                }
                Debug.DrawLine(bullet.transform.position, hit.point, Color.red, 2f);

                break;
            }

            // Move bullet forward
            bullet.transform.position += direction * moveDistance;

            distanceTraveled += moveDistance;

            yield return null;
        }
        

        // Remove bullet when finished
        Destroy(bullet);
    }

    IEnumerator ShootDelay()
    {
        // Wait before allowing another shot
        yield return new WaitForSeconds(shootDelay);

        canShoot = true;
    }
}
