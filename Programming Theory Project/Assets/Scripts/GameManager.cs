
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private GameObject hud;

    [Header("Player Components")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CameraLook cameraLook;
    [SerializeField] private BulletShooter bulletShooter;

    private static bool gameStarted;
    private bool isGameOver;

    public static bool IsCursorLocked =>
        Cursor.lockState == CursorLockMode.Locked;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGameStarted()
    {
        gameStarted = false;
    }

    private void Awake()
    {
        SetGameState(gameStarted);
    }

    private void Update()
    {
        if (!gameStarted || isGameOver)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            UnlockCursor();
        }
        else if (Input.GetMouseButtonDown(0) &&
                 !IsCursorLocked)
        {
            LockCursor();
        }
    }

    public void StartGame()
    {
        gameStarted = true;
        isGameOver = false;

        SetGameState(true);
    }

    private void SetGameState(bool playing)
    {
        if (titlePanel != null)
            titlePanel.SetActive(!playing);

        if (hud != null)
            hud.SetActive(playing);

        Time.timeScale = playing ? 1f : 0f;

        if (playerController != null)
            playerController.enabled = playing;

        if (cameraLook != null)
            cameraLook.enabled = playing;

        if (bulletShooter != null)
            bulletShooter.enabled = playing;

        if (playing)
            LockCursor();
        else
            UnlockCursor();
    }

    public static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void GameOver()
    {
        isGameOver = true;
        UnlockCursor();

        if (playerController != null)
            playerController.enabled = false;

        if (cameraLook != null)
            cameraLook.enabled = false;

        if (bulletShooter != null)
            bulletShooter.enabled = false;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
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
