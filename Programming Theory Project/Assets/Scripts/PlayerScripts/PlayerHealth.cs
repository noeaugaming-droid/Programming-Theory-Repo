using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float health = 100f;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject hud;

    public bool isDead = false;

    void Start()
    {
        gameOverPanel.SetActive(false);
        UpdateHealthText();
    }

    public void TakeDamage(float damageAmount)
    {
        if (isDead) return;

        health -= damageAmount;
        if (health < 0f) health = 0f;
        UpdateHealthText();

        if (health <= 0f)
        {
            isDead = true;
            gameOverPanel.SetActive(true);
            hud.SetActive(false);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GetComponent<PlayerController>().enabled = false;
            GetComponentInChildren<BulletShooter>().enabled = false;
            GetComponentInChildren<CameraLook>().enabled = false;
        }
    }

    void UpdateHealthText()
    {
        healthText.text = "Health: " + health;
    }
}
