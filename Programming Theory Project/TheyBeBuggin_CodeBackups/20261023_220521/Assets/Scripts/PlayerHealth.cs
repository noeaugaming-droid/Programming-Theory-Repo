
using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float health = 100f;
    [SerializeField] private TMP_Text healthText;

    [Header("UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject hud;

    [Header("Game Manager")]
    [SerializeField] private GameManager gameManager;

    public bool isDead { get; private set; }

    private void Start()
    {
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        UpdateHealthText();
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f)
            return;

        health -= amount;

        if (health < 0f)
            health = 0f;

        UpdateHealthText();

        if (health <= 0f)
        {
            isDead = true;

            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);

            if (hud != null)
                hud.SetActive(false);

            if (gameManager != null)
            {
                gameManager.GameOver();
            }
            else
            {
                GameManager.UnlockCursor();

                PlayerController controller =
                    GetComponent<PlayerController>();

                if (controller != null)
                    controller.enabled = false;

                BulletShooter shooter =
                    GetComponentInChildren<BulletShooter>();

                if (shooter != null)
                    shooter.enabled = false;
            }
        }
    }

    private void UpdateHealthText()
    {
        if (healthText != null)
            healthText.text = "Health: " + health;
    }
}
