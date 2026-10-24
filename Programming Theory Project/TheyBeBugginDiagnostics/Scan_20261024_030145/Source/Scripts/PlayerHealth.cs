
using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    #region Combat_And_Health_Variables
    // Values and references used by this part of the script.
    [Header("Health")]
    [SerializeField] private float health = 100f;
    [SerializeField] private TMP_Text healthText;

    #endregion
    #region UI_And_Game_State_Variables
    // Values and references used by this part of the script.
    [Header("UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject hud;

    #endregion
    #region Player_Variables
    // Values and references used by this part of the script.
    [Header("Game Manager")]
    [SerializeField] private GameManager gameManager;

    public bool isDead { get; private set; }

    #endregion
    #region Unity_Lifecycle_Methods
    // Methods Unity calls automatically during the object's lifetime.
    private void Start()
    {
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        UpdateHealthText();
    }

    #endregion
    #region Combat_And_Health_Methods
    // Attacks, damage, and health-related behavior.
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

    #endregion
    #region Unity_Lifecycle_Methods
    // Methods Unity calls automatically during the object's lifetime.
    private void UpdateHealthText()
    {
        if (healthText != null)
            healthText.text = "Health: " + health;
    }
    #endregion
}
