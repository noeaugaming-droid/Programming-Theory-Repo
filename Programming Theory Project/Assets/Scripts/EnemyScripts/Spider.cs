using UnityEngine;

public class Spider : Enemy
{
    [SerializeField] Transform player;
    Rigidbody spiderRb;
    void Start()
    {
        spiderRb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        Move();
    }
    void Move()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0;

        direction.Normalize();
        spiderRb.MovePosition(spiderRb.position + direction * 5f * Time.fixedDeltaTime);
    }

}