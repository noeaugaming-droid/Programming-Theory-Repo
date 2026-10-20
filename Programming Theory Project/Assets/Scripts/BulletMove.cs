using UnityEngine;

public class BulletMove : MonoBehaviour
{
    [SerializeField] float speed = 5f;
    Rigidbody bulletRb;
    void Start()
    {
        bulletRb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {

        bulletRb.linearVelocity = transform.forward * speed;
    }

    void OnCollisionEnter(Collision other)
    {
        if (other.gameObject)
        {
            Destroy(gameObject);
        }
    }
}
