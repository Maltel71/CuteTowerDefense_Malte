using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

// Unified points management system incorporating both wildlife points and turret points
public class PointsManager : MonoBehaviour
{
    public static PointsManager Instance; // Singleton instance
    public TextMeshProUGUI pointsText; // Reference to the UI text
    public string pointsPrefix = "Points: "; // Text to display before the points value
    public Canvas pointsCanvas; // Reference to the specific canvas for points display
    public int startingPoints = 100; // Starting points for the player

    [Header("Pop Animation Settings")]
    public float scaleMultiplier = 1.3f; // How much to scale up (1.3 = 130% of original size)
    public float scaleDuration = 0.2f; // How long the scaling animation takes
    public float cooldownDuration = 0.5f; // Cooldown between animations

    private int currentPoints = 0; // Current player points
    private Vector3 originalScale; // Original scale of the text
    private bool isAnimating = false; // Flag to track if the animation is in progress
    private bool isOnCooldown = false; // Flag to track if we're on cooldown

    // Event system for point changes (from TurretPointManager)
    public delegate void PointsChangedEvent(int newPoints);
    public static event PointsChangedEvent OnPointsChanged;

    // Reference list for TurretManagers
    private List<TurretManager> turretManagers = new List<TurretManager>();

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Initialize with starting points
        currentPoints = startingPoints;

        // Verify we're using the correct canvas
        if (pointsCanvas == null)
        {
            Debug.LogWarning("No PointsCanvas assigned! Please assign CanvasPoints in the inspector.");
            // Try to find canvas by tag as fallback
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas.CompareTag("PointsUI"))
                {
                    pointsCanvas = canvas;
                    Debug.Log("Found PointsUI canvas by tag.");
                    break;
                }
            }
        }
        else
        {
            // Make sure the canvas has the correct tag
            if (!pointsCanvas.CompareTag("PointsUI"))
            {
                Debug.LogWarning("The assigned Points Canvas doesn't have the 'PointsUI' tag!");
            }
        }

        if (pointsText != null)
        {
            // Ensure the points text is a child of the correct canvas
            if (pointsCanvas != null && pointsText.transform.parent != pointsCanvas.transform)
            {
                Debug.LogWarning("Points text is not a child of the assigned points canvas!");
            }

            // Store the original scale of the text
            originalScale = pointsText.transform.localScale;
        }

        // Find all TurretManagers in the scene (from TurretPointManager)
        TurretManager[] managers = FindObjectsOfType<TurretManager>();
        foreach (TurretManager manager in managers)
        {
            turretManagers.Add(manager);
        }

        UpdatePointsDisplay();
        Debug.Log($"PointsManager initialized with {currentPoints} points");
    }

    // Method to add points to the player's score
    public void AddPoints(int points)
    {
        if (points <= 0) return;

        currentPoints += points;
        UpdatePointsDisplay();

        // Notify all listeners that points have changed (for TurretManager)
        OnPointsChanged?.Invoke(currentPoints);

        // Manually update all TurretManagers
        foreach (TurretManager manager in turretManagers)
        {
            if (manager != null)
            {
                manager.SendMessage("UpdateButtonState", SendMessageOptions.DontRequireReceiver);
            }
        }

        // Only trigger animation if not already animating and not on cooldown
        if (!isAnimating && !isOnCooldown && pointsText != null)
        {
            StartCoroutine(AnimatePointsText());
        }

        Debug.Log($"Points added: +{points}. Total: {currentPoints}");
    }

    // Method to spend points (for turrets and other purchases)
    public bool SpendPoints(int points)
    {
        if (currentPoints >= points)
        {
            currentPoints -= points;
            UpdatePointsDisplay();

            // Notify all listeners that points have changed
            OnPointsChanged?.Invoke(currentPoints);

            // Manually update all TurretManagers
            foreach (TurretManager manager in turretManagers)
            {
                if (manager != null)
                {
                    manager.SendMessage("UpdateButtonState", SendMessageOptions.DontRequireReceiver);
                }
            }

            Debug.Log($"Points spent: -{points}. Remaining: {currentPoints}");
            return true;
        }

        Debug.Log($"Not enough points to spend {points}. Current: {currentPoints}");
        return false;
    }

    // Method to get the current points (useful for other scripts)
    public int GetCurrentPoints()
    {
        return currentPoints;
    }

    // Update the UI with the current points
    private void UpdatePointsDisplay()
    {
        if (pointsText != null)
        {
            pointsText.text = pointsPrefix + currentPoints;
        }
    }

    // Coroutine to animate the points text with a pop effect
    private IEnumerator AnimatePointsText()
    {
        isAnimating = true;

        // Calculate the target scale
        Vector3 targetScale = originalScale * scaleMultiplier;

        // Scale up
        float elapsed = 0f;
        while (elapsed < scaleDuration / 2)
        {
            float t = elapsed / (scaleDuration / 2);
            pointsText.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Scale down
        elapsed = 0f;
        while (elapsed < scaleDuration / 2)
        {
            float t = elapsed / (scaleDuration / 2);
            pointsText.transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure scale is reset to original value
        pointsText.transform.localScale = originalScale;

        isAnimating = false;

        // Start cooldown
        StartCoroutine(AnimationCooldown());
    }

    // Coroutine to handle the animation cooldown
    private IEnumerator AnimationCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldownDuration);
        isOnCooldown = false;
    }
}