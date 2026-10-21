using UnityEngine;

public class Spider : Enemy
{
    [SerializeField] Transform player;
    Rigidbody spiderRb;

    [SerializeField] float attackRange = 3f;
    [SerializeField] float attackDelay = 2f;
    float attackTimer = 0f;
    void Start()
    {
        spiderRb = GetComponent<Rigidbody>();
        
    }
    void Update()
    {
        CheckAttack();

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

        Vector3 targetPosition = new Vector3(
            player.position.x,
            transform.position.y,
            player.position.z
            );
        transform.LookAt(targetPosition);
        spiderRb.MovePosition(spiderRb.position + direction * 5f * Time.fixedDeltaTime);

      
    }
    void CheckAttack()
    {
        attackTimer += Time.deltaTime;
        float distance = Vector3.Distance(transform.position, player.position);
        if(distance <= attackRange && attackTimer >= attackDelay)
        {
            Attack();
            attackTimer = 0f;
        }

    }
    public override void Attack()
    {
        Debug.Log("Spider Attacks");
    }


}