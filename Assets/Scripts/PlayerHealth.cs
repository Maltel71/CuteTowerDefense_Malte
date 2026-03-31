using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;
    [SerializeField] private int currentHealth;

    [Header("UI References")]
    public GameObject healthBarPrefab;
    public Vector3 healthBarOffset = new Vector3(0, 2.0f, 0);
    public Canvas targetCanvas;

    [Header("Effects")]
    public AudioClip[] damageSounds;
    public ParticleSystem damageParticles;
    public float pitchMin = 0.9f;
    public float pitchMax = 1.1f;

    [Header("Knockback Settings")]
    public float knockbackForce = 10f;
    public float knockbackDuration = 0.25f;
    private bool isKnockedBack = false;

    [Header("Game Over")]
    public GameObject gameOverCanvas;
    public AudioClip deathSound;
    public float gameOverDelay = 1.5f;

    [Header("Debug")]
    public bool debugMode = true;  // Set to true to enable detailed logging
    public bool forcePlayerDeath = false; // Set to true in Inspector to test death

    private AudioSource audioSource;
    private GameObject healthBarInstance;
    private Image healthBarBackground;
    private Image healthBarFill;
    private Camera mainCamera;
    private bool isGameOver = false;
    private CharacterController characterController;
    private Rigidbody rb;
    private PlayerController playerController;
    private Vector3 knockbackDirection;
    private float knockbackEndTime;

    void Start()
    {
        // Ensure the player has the correct tag
        if (!gameObject.CompareTag("Player"))
        {
            gameObject.tag = "Player";
            DebugLog("Set tag to Player for wildlife targeting");
        }

        // Initialize health
        currentHealth = maxHealth;
        DebugLog($"Player health initialized: {currentHealth}/{maxHealth}");

        mainCamera = Camera.main;
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.playOnAwake = false;
            DebugLog("Added AudioSource component to player");
        }

        // Find and test game over canvas
        FindGameOverCanvas();
        if (gameOverCanvas != null)
        {
            DebugLog($"Game over canvas found: {gameOverCanvas.name}, currently {(gameOverCanvas.activeSelf ? "active" : "inactive")}");
        }

        CreateHealthBar();
    }

    void Update()
    {
        UpdateHealthBarPosition();

        // Apply knockback if active
        if (isKnockedBack && Time.time < knockbackEndTime)
        {
            ApplyKnockback();
        }
        else if (isKnockedBack && Time.time >= knockbackEndTime)
        {
            // End knockback
            isKnockedBack = false;
            if (playerController != null)
            {
                playerController.enabled = true;
                DebugLog("Knockback ended, re-enabling player controller");
            }
        }

        // Debug feature to force player death with F12 key
        if (Input.GetKeyDown(KeyCode.F12) || forcePlayerDeath)
        {
            forcePlayerDeath = false;  // Reset flag
            DebugLog("DEBUG: Forcing player death");
            currentHealth = 0;
            Die();
        }
    }

    void ApplyKnockback()
    {
        Vector3 movement = knockbackDirection * knockbackForce * Time.deltaTime;

        // Apply knockback based on movement component
        if (characterController != null && characterController.enabled)
        {
            characterController.Move(movement);
            DebugLog($"Applied knockback to CharacterController: {movement}");
        }
        else if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(movement, ForceMode.VelocityChange);
            DebugLog($"Applied knockback to Rigidbody: {movement}");
        }
        else
        {
            // Fallback to direct transform movement if neither component is available
            transform.position += movement;
            DebugLog($"Applied knockback to Transform: {movement}");
        }
    }

    void DebugLog(string message)
    {
        if (debugMode)
        {
            Debug.Log($"[PlayerHealth] {message}");
        }
    }

    void FindGameOverCanvas()
    {
        // Log the initial state
        if (gameOverCanvas != null)
        {
            DebugLog($"Game over canvas already assigned: {gameOverCanvas.name}");
            return;
        }

        // Method 1: Try to get it from TowerHealth
        TowerHealth towerHealth = FindObjectOfType<TowerHealth>();
        if (towerHealth != null)
        {
            DebugLog($"Found TowerHealth component on: {towerHealth.gameObject.name}");

            if (towerHealth.gameOverCanvas != null)
            {
                gameOverCanvas = towerHealth.gameOverCanvas;
                DebugLog($"Found game over canvas from TowerHealth: {gameOverCanvas.name}");
                return;
            }
            else
            {
                DebugLog("TowerHealth exists but its gameOverCanvas is null");
            }
        }
        else
        {
            DebugLog("No TowerHealth component found in scene");
        }

        // Method 2: Try by name
        gameOverCanvas = GameObject.Find("CanvasGameOver");
        if (gameOverCanvas != null)
        {
            DebugLog($"Found game over canvas by name 'CanvasGameOver'");
            return;
        }

        // Method 3: Try alternative names
        string[] possibleCanvasNames = {
            "GameOverCanvas",
            "GameOver",
            "UIGameOver",
            "GameOverUI"
        };

        foreach (string name in possibleCanvasNames)
        {
            GameObject foundCanvas = GameObject.Find(name);
            if (foundCanvas != null)
            {
                gameOverCanvas = foundCanvas;
                DebugLog($"Found game over canvas with alternate name '{name}'");
                return;
            }
        }

        // Method 4: Try by tag
        GameObject taggedCanvas = GameObject.FindWithTag("GameOverUI");
        if (taggedCanvas != null)
        {
            gameOverCanvas = taggedCanvas;
            DebugLog($"Found game over canvas by 'GameOverUI' tag");
            return;
        }

        // Method 5: Last resort - find any canvas with "GameOver" in its name
        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true); // Include inactive canvases
        DebugLog($"Searching through {allCanvases.Length} canvases in scene");

        foreach (Canvas canvas in allCanvases)
        {
            DebugLog($"Found canvas: {canvas.name}, active: {canvas.gameObject.activeSelf}");

            if (canvas.name.Contains("GameOver") || canvas.name.Contains("gameOver"))
            {
                gameOverCanvas = canvas.gameObject;
                DebugLog($"Found likely game over canvas: {canvas.name}");
                return;
            }
        }

        // Method 6: Last resort - log all game objects in scene to help identify the canvas
        GameObject[] allObjects = FindObjectsOfType<GameObject>(true); // Include inactive objects
        DebugLog($"Failed to find game over canvas! Listing all GameObjects with 'Canvas' in name:");

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Canvas"))
            {
                DebugLog($"- {obj.name} (active: {obj.activeSelf})");
            }
        }

        Debug.LogError("Could not find game over canvas! Player death will not show game over screen.");
    }

    void CreateHealthBar()
    {
        // Find canvas
        if (targetCanvas == null)
        {
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            foreach (Canvas c in allCanvases)
            {
                if (c.CompareTag("HealthUI"))
                {
                    targetCanvas = c;
                    DebugLog($"Found health UI canvas: {c.name}");
                    break;
                }
            }

            if (targetCanvas == null)
            {
                targetCanvas = FindObjectOfType<Canvas>();
                DebugLog("No Canvas with 'HealthUI' tag found. Using first available canvas: " +
                    (targetCanvas != null ? targetCanvas.name : "None found!"));
            }
        }
        else
        {
            DebugLog($"Using assigned target canvas: {targetCanvas.name}");
        }

        if (targetCanvas == null)
        {
            Debug.LogError("No Canvas found. Health bar will not be displayed.");
            return;
        }

        // Find health bar prefab if not assigned
        if (healthBarPrefab == null)
        {
            DebugLog("No health bar prefab assigned, trying to find one");

            // Try to find from turrets first
            TurretHealthBar[] turretHealthBars = FindObjectsOfType<TurretHealthBar>();
            foreach (TurretHealthBar bar in turretHealthBars)
            {
                if (bar.healthBarPrefab != null)
                {
                    healthBarPrefab = bar.healthBarPrefab;
                    DebugLog($"Found health bar prefab from turret: {bar.gameObject.name}");
                    break;
                }
            }

            if (healthBarPrefab == null)
            {
                // Try common names
                GameObject[] possiblePrefabs = {
                    GameObject.Find("HealthBarPrefab"),
                    GameObject.Find("HealthBarPrefabPlayer"),
                    GameObject.Find("PlayerHealthBarPrefab")
                };

                foreach (GameObject prefab in possiblePrefabs)
                {
                    if (prefab != null)
                    {
                        healthBarPrefab = prefab;
                        DebugLog($"Found health bar prefab: {prefab.name}");
                        break;
                    }
                }
            }
        }
        else
        {
            DebugLog($"Using assigned health bar prefab: {healthBarPrefab.name}");
        }

        if (healthBarPrefab == null)
        {
            Debug.LogError("Health bar prefab not found. Health bar will not be displayed.");
            return;
        }

        // Create health bar
        healthBarInstance = Instantiate(healthBarPrefab, targetCanvas.transform);
        DebugLog($"Created health bar instance from prefab");

        // Get component references
        Transform bgTransform = healthBarInstance.transform.Find("Background");
        Transform fillTransform = healthBarInstance.transform.Find("Fill");

        if (bgTransform != null && fillTransform != null)
        {
            healthBarBackground = bgTransform.GetComponent<Image>();
            healthBarFill = fillTransform.GetComponent<Image>();
            DebugLog("Found Background and Fill components in health bar");

            // Set rendering order
            bgTransform.SetSiblingIndex(0);
            fillTransform.SetSiblingIndex(1);

            // Ensure the background has full alpha
            if (healthBarBackground != null)
            {
                Color bgColor = healthBarBackground.color;
                healthBarBackground.color = new Color(bgColor.r, bgColor.g, bgColor.b, 1f);
            }

            // Configure fill image
            if (healthBarFill != null)
            {
                healthBarFill.type = Image.Type.Filled;
                healthBarFill.fillMethod = Image.FillMethod.Horizontal;
                healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                healthBarFill.fillAmount = 1.0f;

                // Use a unique color for player health
                healthBarFill.color = new Color(0.1f, 0.8f, 0.2f); // Bright green for player
                DebugLog("Health bar fill configured");
            }
        }
        else
        {
            if (bgTransform == null) DebugLog("ERROR: Background component not found in health bar prefab");
            if (fillTransform == null) DebugLog("ERROR: Fill component not found in health bar prefab");
            Debug.LogError("Health bar prefab is missing Background or Fill components");
        }
    }

    void UpdateHealthBarPosition()
    {
        if (healthBarInstance == null) return;

        // Get world position
        Vector3 worldPosition = transform.position + healthBarOffset;

        // Position the health bar at the world position
        healthBarInstance.transform.position = worldPosition;

        // Make health bar face the camera
        if (mainCamera != null)
        {
            healthBarInstance.transform.forward = mainCamera.transform.forward;
        }

        // Update fill amount
        if (healthBarFill != null)
        {
            float healthPercent = (float)currentHealth / maxHealth;
            healthPercent = Mathf.Clamp01(healthPercent);
            healthBarFill.fillAmount = healthPercent;
        }
    }

    public void TakeDamage(int damage, Vector3 sourcePosition = default)
    {
        // Prevent taking damage if already game over
        if (isGameOver) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0); // Ensure health doesn't go below 0

        DebugLog($"Player took {damage} damage. Health now: {currentHealth}/{maxHealth}");

        // Calculate knockback direction - away from the source
        if (sourcePosition != default)
        {
            // Direction from damage source to player (normalized)
            knockbackDirection = (transform.position - sourcePosition).normalized;
            // Make sure we're not pushing downward
            knockbackDirection.y = 0;
            if (knockbackDirection.magnitude < 0.1f)
            {
                // Fallback if positions are too close
                knockbackDirection = transform.forward * -1;
            }
        }
        else
        {
            // Default knockback direction if no source position
            knockbackDirection = transform.forward * -1;
        }

        // Start knockback
        StartKnockback();

        PlayDamageEffects();

        if (currentHealth <= 0 && !isGameOver)
        {
            DebugLog("Player health reached 0 or below, calling Die()");
            Die();
        }
    }

    private void StartKnockback()
    {
        // Set knockback duration
        knockbackEndTime = Time.time + knockbackDuration;
        isKnockedBack = true;

        // Disable player controller during knockback to prevent conflicting movement
        if (playerController != null)
        {
            playerController.enabled = false;
            DebugLog("Temporarily disabling player controller for knockback");
        }
    }

    void PlayDamageEffects()
    {
        // Play random damage sound
        if (damageSounds != null && damageSounds.Length > 0 && audioSource != null)
        {
            int soundIndex = Random.Range(0, damageSounds.Length);
            audioSource.pitch = Random.Range(pitchMin, pitchMax);
            audioSource.PlayOneShot(damageSounds[soundIndex]);
            DebugLog($"Played damage sound {soundIndex}");
        }
        else if (debugMode)
        {
            if (damageSounds == null || damageSounds.Length == 0)
                DebugLog("No damage sounds assigned");
            if (audioSource == null)
                DebugLog("No audio source available");
        }

        // Spawn damage particles
        if (damageParticles != null)
        {
            ParticleSystem particles = Instantiate(damageParticles, transform.position + Vector3.up, Quaternion.identity);
            if (!particles.isPlaying)
            {
                particles.Play();
            }

            float duration = particles.main.duration;
            if (particles.main.loop)
            {
                duration = 2.0f; // Default duration for looping particles
            }

            // Add the max start lifetime
            float totalLifetime = duration + particles.main.startLifetime.constantMax;
            Destroy(particles.gameObject, totalLifetime);
            DebugLog("Spawned damage particles");
        }
        else
        {
            DebugLog("No damage particles assigned");
        }
    }

    void Die()
    {
        isGameOver = true;
        Debug.Log("Player died! Game Over");

        // Play death sound
        if (deathSound != null && audioSource != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(deathSound);
            DebugLog("Played death sound");
        }
        else
        {
            DebugLog("No death sound assigned or audio source missing");
        }

        // Notify nearby wildlife to stop attacking
        NotifyWildlifeOfDeath();

        // DIRECT TEST - try to show game over screen immediately AND after delay
        DebugLog("Testing direct game over screen activation first");
        DirectShowGameOverScreen();

        // Show game over screen after delay
        DebugLog($"Invoking ShowGameOverScreen with delay of {gameOverDelay} seconds");
        Invoke("ShowGameOverScreen", gameOverDelay);
    }

    // Direct test method to try activating the game over screen immediately
    void DirectShowGameOverScreen()
    {
        DebugLog("DirectShowGameOverScreen called");

        // Attempt to find the canvas again
        if (gameOverCanvas == null)
        {
            DebugLog("Game over canvas is null, trying to find it again");
            FindGameOverCanvas();
        }

        // Show game over screen instantly for testing
        if (gameOverCanvas != null)
        {
            DebugLog($"Trying to activate game over canvas: {gameOverCanvas.name}");
            gameOverCanvas.SetActive(true);

            // Check if it's actually activated
            if (gameOverCanvas.activeSelf)
            {
                DebugLog("Successfully activated game over canvas directly");
            }
            else
            {
                DebugLog("Failed to activate game over canvas directly!");
            }

            // Also try to directly reference it by name if our reference isn't working
            GameObject directCanvasRef = GameObject.Find("CanvasGameOver");
            if (directCanvasRef != null && directCanvasRef != gameOverCanvas)
            {
                DebugLog($"Found different CanvasGameOver object directly: {directCanvasRef.name}");
                directCanvasRef.SetActive(true);
                DebugLog($"Directly activated alternate canvas, now active: {directCanvasRef.activeSelf}");
            }
        }
        else
        {
            DebugLog("Game over canvas still null after FindGameOverCanvas");
        }
    }

    void NotifyWildlifeOfDeath()
    {
        // Find all wildlife within a radius
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, 30f);
        int wildlifeCount = 0;

        foreach (Collider collider in nearbyColliders)
        {
            Wildlife wildlife = collider.GetComponent<Wildlife>();
            if (wildlife != null)
            {
                wildlife.FindNewTarget();
                wildlifeCount++;
            }
        }

        DebugLog($"Notified {wildlifeCount} wildlife of player death");
    }

    void ShowGameOverScreen()
    {
        DebugLog("ShowGameOverScreen called - showing game over UI");

        // Freeze the game
        Time.timeScale = 0f;
        DebugLog($"Time scale set to {Time.timeScale}");

        // Double-check if we need to find the canvas again
        if (gameOverCanvas == null)
        {
            DebugLog("Game over canvas is still null, trying one last search");
            FindGameOverCanvas();
        }

        // Show game over screen
        if (gameOverCanvas != null)
        {
            DebugLog($"Activating game over canvas: {gameOverCanvas.name}, current state: {gameOverCanvas.activeSelf}");
            gameOverCanvas.SetActive(true);

            // Check if it was successfully activated
            if (gameOverCanvas.activeSelf)
            {
                DebugLog("Successfully activated game over canvas");
            }
            else
            {
                DebugLog("Failed to activate game over canvas!");
            }

            // Ensure canvas component is enabled
            Canvas canvas = gameOverCanvas.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.enabled = true;
                DebugLog("Ensured Canvas component is enabled");

                // Set high sorting order
                if (canvas.sortingOrder < 10)
                {
                    canvas.sortingOrder = 10;
                    DebugLog($"Set canvas sorting order to {canvas.sortingOrder}");
                }
            }
            else
            {
                DebugLog("WARNING: Game over canvas has no Canvas component!");
            }

            // Try to find and enable any child objects
            for (int i = 0; i < gameOverCanvas.transform.childCount; i++)
            {
                Transform child = gameOverCanvas.transform.GetChild(i);
                child.gameObject.SetActive(true);
                DebugLog($"Activated child object: {child.name}");
            }
        }
        else
        {
            Debug.LogError("Failed to show game over screen - couldn't find canvas");
        }
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    void OnDestroy()
    {
        if (healthBarInstance != null)
        {
            Destroy(healthBarInstance);
        }
    }
}