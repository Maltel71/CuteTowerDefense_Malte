using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    public Button retryButton;
    public Button quitButton;
    public TextMeshProUGUI gameOverText;

    [Header("Animation Settings")]
    public float pulsateSpeed = 1.5f;
    public float pulsateAmount = 0.1f;

    private TowerHealth towerHealth;
    private Vector3 originalScale;

    void Awake()
    {
        // Make sure this canvas starts disabled
        gameObject.SetActive(false);
    }

    void Start()
    {
        // Find TowerHealth component for button connections
        GameObject tower = GameObject.FindGameObjectWithTag("Tower");
        if (tower != null)
        {
            towerHealth = tower.GetComponent<TowerHealth>();
        }
        else
        {
            Debug.LogError("Tower not found with 'Tower' tag!");
        }

        SetupButtons();

        // Store original scale for animation
        if (gameOverText != null)
        {
            originalScale = gameOverText.transform.localScale;
        }
    }

    void SetupButtons()
    {
        // Set up retry button
        if (retryButton != null)
        {
            // First clear any existing listeners
            retryButton.onClick.RemoveAllListeners();

            if (towerHealth != null)
            {
                // Use TowerHealth method for restarting
                retryButton.onClick.AddListener(towerHealth.RestartGame);
            }
            else
            {
                // Fallback if TowerHealth not found
                retryButton.onClick.AddListener(RestartGame);
            }

            Debug.Log("Retry button set up");
        }
        else
        {
            // Try to find the retry button by name
            retryButton = transform.Find("RetryButton")?.GetComponent<Button>();
            if (retryButton != null)
            {
                SetupButtons(); // Recursively call to set up the found button
            }
            else
            {
                Debug.LogError("Retry button not found!");
            }
        }

        // Set up quit button
        if (quitButton != null)
        {
            // First clear any existing listeners
            quitButton.onClick.RemoveAllListeners();

            if (towerHealth != null)
            {
                // Use TowerHealth method for quitting
                quitButton.onClick.AddListener(towerHealth.QuitGame);
            }
            else
            {
                // Fallback if TowerHealth not found
                quitButton.onClick.AddListener(QuitGame);
            }

            Debug.Log("Quit button set up");
        }
        else
        {
            // Try to find the quit button by name
            quitButton = transform.Find("QuitButton")?.GetComponent<Button>();
            if (quitButton != null)
            {
                SetupButtons(); // Recursively call to set up the found button
            }
            else
            {
                Debug.LogError("Quit button not found!");
            }
        }
    }

    void Update()
    {
        // Always use unscaledTime to ensure animations work even when game is frozen
        if (gameOverText != null)
        {
            float pulse = 1.0f + Mathf.Sin(Time.unscaledTime * pulsateSpeed) * pulsateAmount;
            gameOverText.transform.localScale = originalScale * pulse;
        }
    }

    // Fallback method if TowerHealth is not found
    public void RestartGame()
    {
        // Reset time scale first
        Time.timeScale = 1.0f;

        // Get current scene and reload it
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    // Fallback method if TowerHealth is not found
    public void QuitGame()
    {
        // Reset time scale first
        Time.timeScale = 1.0f;

        // In editor, this just stops play mode
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif

        Debug.Log("Quitting game");
    }

    // This method gets called when the canvas is enabled
    void OnEnable()
    {
        Debug.Log("Game Over UI enabled");

        // Force cursor to be visible in case it was hidden during gameplay
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}