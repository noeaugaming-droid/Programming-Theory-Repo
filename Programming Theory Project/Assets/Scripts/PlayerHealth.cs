using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float health = 100f;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject hud;

    public bool isDead { get; private set; }

    void Start()
    {
        gameOverPanel.SetActive(false);
        UpdateHealthText();
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;

        health -= amount;
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

    private void UpdateHealthText()
    {
        if (healthText != null) healthText.text = "Health: " + health;
    }
}
