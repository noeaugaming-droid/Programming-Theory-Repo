using System.Collections;
using UnityEngine;

public class BulletShooter : MonoBehaviour
{
    float bulletSpeed;
    bool shootButtonPressed = false;
    public GameObject pivotPos;
    bool canShoot = false;
    [SerializeField] GameObject bulletPrefab;
    void Start()
    {
        canShoot = true;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            shootButtonPressed = true;
        }
    }

    void FixedUpdate()
    {
        if (shootButtonPressed && canShoot)
        {
            Vector3 bulletSpawnPos = pivotPos.transform.position;
            Instantiate(bulletPrefab, bulletSpawnPos, pivotPos.transform.rotation);
            shootButtonPressed = false;
            canShoot = false;
            StartCoroutine(ShootDelay());
        }
    }

    IEnumerator ShootDelay()
    {
       
        yield return new WaitForSeconds(.25f);
        canShoot = true;
       
    }
}
