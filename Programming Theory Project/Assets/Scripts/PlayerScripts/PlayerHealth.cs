using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] float health = 100f;
    [SerializeField] TMP_Text healthText;
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] GameObject hud;

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
                gameOverPanel.SetActive(true);
                hud.SetActive(false);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

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