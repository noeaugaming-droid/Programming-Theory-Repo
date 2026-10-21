using UnityEngine;

public class Spider : Enemy
{
    PlayerHealth playerHealth;
    [SerializeField] Transform player;
    Rigidbody spiderRb;

    [SerializeField] float attackRange = 3f;
    [SerializeField] float attackDelay = 2f;

    [Header("Knockback")]
    [SerializeField] float knockbackDuration = 0.3f;
    [SerializeField] float knockbackForce = 10f;
    bool isKnockedBack = false;
    float knockbackTimer = 0f;

   
    float attackTimer = 0f;
    void Start()
    {
        spiderRb = GetComponent<Rigidbody>();
        player = GameObject.FindGameObjectWithTag("Player").transform;
        playerHealth = player.GetComponent<PlayerHealth>();

    }
    void Update()
    {
        CheckAttack();

    }

    void FixedUpdate()
    {
        if (playerHealth.isDead)
        {
            return;
        }
        if (isKnockedBack)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            if (knockbackTimer <= 0)
            {
                isKnockedBack = false;
            }
        }
        else
        {
            float distance = Vector3.Distance(transform.position, player.position);
            if (distance > attackRange)
            {
                Move();
            }
        }
    }
    void Move()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0;

        direction.Normalize();

        Vector3 targetPosition = new Vector3(
            player.position.x,
            transform.position.y,
            player.position.z
            );
        transform.LookAt(targetPosition);
        spiderRb.MovePosition(spiderRb.position + direction * speed * Time.fixedDeltaTime);


    }
    void CheckAttack()
    {
        attackTimer += Time.deltaTime;
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= attackRange && attackTimer >= attackDelay && !playerHealth.isDead)
        {
            Attack();
            attackTimer = 0f;
        }

    }
    public void Knockback(Vector3 direction)
    {
        spiderRb.AddForce(direction * knockbackForce, ForceMode.Impulse);
        isKnockedBack = true;
        knockbackTimer = knockbackDuration;
    }
    public override void Attack()
    {
        playerHealth.TakeDamage(damage);
        Debug.Log("Spider Attacks");
    }


}