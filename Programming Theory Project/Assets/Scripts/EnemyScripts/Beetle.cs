using UnityEngine;

public class Beetle : Enemy
{
    Transform player;
    Rigidbody beetleRb;
    [SerializeField] float attackRange = 3f;
    [SerializeField] float attackCooldown = 2f;
    [SerializeField] float chargeForce = 15f;
    [SerializeField] float knockbackForce = 10f;
    bool isKnockedBack = false;
    float knockbackTimer = 0f;
    float attackTimer = 0f;
    PlayerHealth playerHealth;
    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        playerHealth = player.GetComponent<PlayerHealth>();
        beetleRb = GetComponent<Rigidbody>();
    }
    void FixedUpdate()
    {
        if (playerHealth.isDead)
        {
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);
        attackTimer -= Time.fixedDeltaTime;

        if (isKnockedBack)
        {
            knockbackTimer -= Time.fixedDeltaTime;

            if (knockbackTimer <= 0)
            {
                isKnockedBack = false;
            }
        }
        else if (distance > attackRange)
        {
            Vector3 direction = player.position - transform.position;
            direction.y = 0;

            transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(90, 0, 0);

            beetleRb.MovePosition(transform.position + direction.normalized * speed * Time.fixedDeltaTime);
        }
        else if (attackTimer <= 0)
        {
            Enemy enemy = GetComponent<Enemy>();
            enemy.Attack();

            attackTimer = attackCooldown;
        }
    }


    public override void Attack()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0;

        beetleRb.AddForce(direction.normalized * chargeForce, ForceMode.Impulse);

        playerHealth.TakeDamage(damage);
        Debug.Log("Beetle attacks!");
    }
    public void Knockback(Vector3 direction)
    {
        beetleRb.AddForce(direction * knockbackForce, ForceMode.Impulse);

        isKnockedBack = true;
        knockbackTimer = 0.3f;
    }
}