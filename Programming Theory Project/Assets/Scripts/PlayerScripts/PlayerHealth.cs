using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float health = 100f;
    [SerializeField] TMP_Text healthText;

    public bool isDead = false;

    void Start()
    {
        UpdateHealthText();
    }

    public void TakeDamage(float damageAmount)
    {
        if (!isDead)
        {
            health -= damageAmount;
            UpdateHealthText();

            Debug.Log("Player Health: " + health);

            if (health <= 0)
            {
                isDead = true;

                Debug.Log("Player Died!");

                GetComponent<PlayerController>().enabled = false;
                GetComponentInChildren<BulletShooter>().enabled = false;
            }
        }
    }
    void UpdateHealthText()
    {
        healthText.text = "Health: " + health;
    }
}