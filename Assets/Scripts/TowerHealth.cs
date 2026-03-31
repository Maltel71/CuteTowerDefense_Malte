using UnityEngine;
using UnityEngine.SceneManagement;

public class TowerHealth : MonoBehaviour
{
    public int maxHealth = 1000;
    [SerializeField] private int currentHealth; // Now visible in inspector with SerializeField
    public AudioClip[] damageSounds; // Array of sounds to play when taking damage
    public ParticleSystem damageParticles; // Particle system to play when taking damage
    public Transform particleSpawnPoint; // Spawn point for the particle system
    public float pitchMin = 0.9f; // Minimum pitch shift
    public float pitchMax = 1.1f; // Maximum pitch shift

    [Header("Game Over Settings")]
    public GameObject gameOverCanvas; // Reference to the game over canvas
    public AudioClip gameOverSound; // Sound to play when game over
    public float gameOverDelay = 1.5f; // Delay before showing game over screen

    private AudioSource audioSource;
    private bool isGameOver = false;

    void Start()
    {
        currentHealth = maxHealth;
        audioSource = GetComponent<AudioSource>();

        // Make sure the game over canvas is disabled at start
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(false);
        }
        else
        {
            // Try to find the game over canvas by name
            gameOverCanvas = GameObject.Find("CanvasGameOver");
            if (gameOverCanvas != null)
            {
                gameOverCanvas.SetActive(false);
                Debug.Log("Found game over canvas by name");
            }
            else
            {
                Debug.LogWarning("Game over canvas not found. Please assign it in the inspector.");
            }
        }
    }

    public void TakeDamage(int damage)
    {
        // Prevent taking damage if already game over
        if (isGameOver)
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0); // Ensure health doesn't go below 0

        PlayRandomDamageSound();

        if (damageParticles != null && particleSpawnPoint != null)
        {
            ParticleSystem particles = Instantiate(damageParticles, particleSpawnPoint.position, particleSpawnPoint.rotation);
            Destroy(particles.gameObject, 1.5f); // Destroy the particle system after 1.5 seconds
        }

        if (currentHealth <= 0 && !isGameOver)
        {
            DestroyTower();
        }
    }

    void PlayRandomDamageSound()
    {
        if (damageSounds.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, damageSounds.Length);
            audioSource.clip = damageSounds[randomIndex];
            audioSource.pitch = Random.Range(pitchMin, pitchMax); // Random pitch shift within the specified range
            audioSource.Play();
        }
    }

    void PlayGameOverSound()
    {
        if (gameOverSound != null && audioSource != null)
        {
            audioSource.clip = gameOverSound;
            audioSource.pitch = 1.0f;
            audioSource.Play();
        }
    }

    void DestroyTower()
    {
        isGameOver = true;
        Debug.Log("Tower destroyed! Game Over");

        // Notify nearby wildlife to stop attacking
        NotifyWildlifeOfDestruction();

        // Play a special game over sound if assigned
        PlayGameOverSound();

        // Show game over screen after a delay
        Invoke("ShowGameOverScreen", gameOverDelay);
    }

    void ShowGameOverScreen()
    {
        // Completely freeze the game for a freeze-frame effect
        Time.timeScale = 0f;

        // Enable the game over canvas
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
            Debug.Log("Game over screen displayed with freeze-frame effect");

            // Make sure the canvas is actually enabled
            Canvas canvas = gameOverCanvas.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
            }

            // Make sure it's in the foreground
            if (canvas != null && canvas.sortingOrder < 10)
            {
                canvas.sortingOrder = 10;
            }
        }
        else
        {
            Debug.LogError("Game over canvas not assigned or found!");
        }
    }

    void NotifyWildlifeOfDestruction()
    {
        // Find all wildlife within a reasonable radius (e.g., 30 units)
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, 30f);

        foreach (Collider collider in nearbyColliders)
        {
            Wildlife wildlife = collider.GetComponent<Wildlife>();
            if (wildlife != null)
            {
                // Call the method to find a new target or disable AI
                wildlife.FindNewTarget();
            }
        }
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    // Public method that can be called by UI buttons
    public void RestartGame()
    {
        // Reset time scale
        Time.timeScale = 1f;

        // Get current scene and reload it
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    // Public method that can be called by UI buttons
    public void QuitGame()
    {
        // Reset time scale first
        Time.timeScale = 1f;

        // In editor, this just stops play mode
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif

        Debug.Log("Quitting game");
    }
}