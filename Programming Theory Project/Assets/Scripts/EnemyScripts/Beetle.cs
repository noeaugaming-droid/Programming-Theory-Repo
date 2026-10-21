using UnityEngine;

public class Beetle : Enemy
{
    Transform player;
    Rigidbody beetleRb;
    [SerializeField] float attackRange = 3f;
    PlayerHealth playerHealth;
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        playerHealth = player.GetComponent<PlayerHealth>();
        beetleRb = GetComponent<Rigidbody>();
    }
    void FixedUpdate()
    {
        float distance = Vector3.Distance(transform.position, player.position);

        if (distance > attackRange)
        {
            Vector3 direction = player.position - transform.position;
            direction.y = 0;

            beetleRb.MovePosition(transform.position + direction.normalized * speed * Time.fixedDeltaTime);
        }
    }


    public override void Attack()
    {
        playerHealth.TakeDamage(damage);
        Debug.Log("Beetle attacks!");
    }
}