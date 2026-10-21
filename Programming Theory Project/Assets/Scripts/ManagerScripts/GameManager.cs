using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject hud;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CameraLook cameraLook;
    [SerializeField] private BulletShooter bulletShooter;

    private static bool gameStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGameStarted()
    {
        gameStarted = false;
    }

    void Awake()
    {
        titlePanel.SetActive(!gameStarted);
        hud.SetActive(gameStarted);
        Time.timeScale = gameStarted ? 1f : 0f;

        playerController.enabled = gameStarted;
        cameraLook.enabled = gameStarted;
        bulletShooter.enabled = gameStarted;

        Cursor.lockState = gameStarted ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !gameStarted;
    }

    public void StartGame()
    {
        gameStarted = true;
        titlePanel.SetActive(false);
        hud.SetActive(true);
        Time.timeScale = 1f;

        playerController.enabled = true;
        cameraLook.enabled = true;
        bulletShooter.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
