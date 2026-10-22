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
        SetGameState(gameStarted);
    }

    public void StartGame()
    {
        gameStarted = true;
        SetGameState(true);
    }

    private void SetGameState(bool playing)
    {
        titlePanel.SetActive(!playing);
        hud.SetActive(playing);
        Time.timeScale = playing ? 1f : 0f;
        playerController.enabled = playing;
        cameraLook.enabled = playing;
        bulletShooter.enabled = playing;
        Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !playing;
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
