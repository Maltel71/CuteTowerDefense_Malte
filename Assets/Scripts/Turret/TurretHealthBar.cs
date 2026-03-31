using UnityEngine;
using UnityEngine.UI;

public class TurretHealthBar : MonoBehaviour
{
    public GameObject healthBarPrefab;   // Assign the HealthBarPrefab

    [Header("Health Bar Positioning")]
    public Transform healthBarPosition;  // Reference to the "HealthBarPosition" GameObject

    [Header("Debug")]
    public bool debugMode = false;

    // No need to manually assign the canvas anymore
    public Canvas targetCanvas;
    private GameObject healthBarInstance;
    private Image healthBarBackground;
    private Image healthBarFill;
    private TurretHealth turretHealth;
    private int maxHealth;
    private Camera mainCamera;
    private bool hasTriedToCreateHealthBar = false;

    void Start()
    {
        turretHealth = GetComponent<TurretHealth>();
        mainCamera = Camera.main;

        // If healthBarPosition isn't set, try to find it as a child
        if (healthBarPosition == null)
        {
            Transform child = transform.Find("HealthBarPosition");
            if (child != null)
            {
                healthBarPosition = child;
            }
            else
            {
                if (debugMode) Debug.LogWarning("No 'HealthBarPosition' transform assigned or found! Using default position.");
            }
        }

        if (turretHealth != null)
        {
            maxHealth = turretHealth.maxHealth;
            TryCreateHealthBar();
        }
    }

    void Update()
    {
        if (turretHealth == null || healthBarInstance == null)
            return;

        // Get the world position
        Vector3 worldPosition;
        if (healthBarPosition != null)
        {
            worldPosition = healthBarPosition.position;
        }
        else
        {
            // Fallback to using the turret's position with a default offset
            worldPosition = transform.position + new Vector3(0, 1.5f, 0);
        }

        // Simply position the health bar at the world position
        healthBarInstance.transform.position = worldPosition;

        // Make the health bar face the camera
        if (mainCamera != null)
        {
            healthBarInstance.transform.forward = mainCamera.transform.forward;
        }

        // Update health bar fill amount
        if (healthBarFill != null)
        {
            float healthPercent = (float)turretHealth.GetCurrentHealth() / maxHealth;
            healthPercent = Mathf.Clamp01(healthPercent);
            healthBarFill.fillAmount = healthPercent;
        }
    }

    void TryCreateHealthBar()
    {
        hasTriedToCreateHealthBar = true;

        // Find the appropriate canvas
        FindTargetCanvas();

        // Ensure we have the health bar prefab
        if (healthBarPrefab == null)
        {
            FindHealthBarPrefab();
        }

        if (targetCanvas != null && healthBarPrefab != null)
        {
            CreateHealthBar();
        }
        else
        {
            if (debugMode) Debug.LogWarning("Cannot create health bar: missing canvas or prefab");
            // We'll try again next frame
            hasTriedToCreateHealthBar = false;
        }
    }

    void FindTargetCanvas()
    {
        // Automatically find the HealthBarCanvas by tag first (most reliable)
        GameObject taggedCanvasObj = GameObject.FindWithTag("HealthUI");
        if (taggedCanvasObj != null)
        {
            targetCanvas = taggedCanvasObj.GetComponent<Canvas>();
            if (targetCanvas != null && debugMode)
                Debug.Log("Found canvas with HealthUI tag");
            return;
        }

        // Try by name next
        GameObject canvasObj = GameObject.Find("HealthBarCanvas");
        if (canvasObj != null)
        {
            targetCanvas = canvasObj.GetComponent<Canvas>();
            if (targetCanvas != null)
            {
                if (debugMode) Debug.Log("Found HealthBarCanvas by name");
                return;
            }
            else
            {
                if (debugMode) Debug.LogError("HealthBarCanvas found but it has no Canvas component!");
            }
        }

        // Last resort - find any canvas
        targetCanvas = FindObjectOfType<Canvas>();
        if (targetCanvas != null)
        {
            if (debugMode) Debug.LogWarning("Using first available canvas for health bars.");
        }
        else
        {
            if (debugMode) Debug.LogError("No Canvas found! Health bar will not be displayed.");
        }
    }

    void FindHealthBarPrefab()
    {
        // Try commonly used names
        string[] prefabNames = {
            "HealthBarPrefab",
            "HealthBarPrefabTurret",
            "TurretHealthBarPrefab"
        };

        foreach (string name in prefabNames)
        {
            GameObject prefab = GameObject.Find(name);
            if (prefab != null)
            {
                healthBarPrefab = prefab;
                if (debugMode) Debug.Log($"Found health bar prefab: {name}");
                return;
            }
        }

        // Try by tag
        GameObject taggedPrefab = GameObject.FindWithTag("HealthBarPrefab");
        if (taggedPrefab != null)
        {
            healthBarPrefab = taggedPrefab;
            if (debugMode) Debug.Log("Found health bar prefab by tag");
            return;
        }

        // Final approach: try to find from an existing turret
        TurretHealthBar[] existingBars = FindObjectsOfType<TurretHealthBar>();
        foreach (TurretHealthBar bar in existingBars)
        {
            if (bar != this && bar.healthBarPrefab != null)
            {
                healthBarPrefab = bar.healthBarPrefab;
                if (debugMode) Debug.Log("Found health bar prefab from another turret");
                return;
            }
        }

        if (debugMode) Debug.LogWarning("Could not find health bar prefab!");
    }

    void CreateHealthBar()
    {
        if (targetCanvas == null || healthBarPrefab == null)
        {
            if (debugMode) Debug.LogError("Cannot create health bar: Canvas or prefab missing");
            return;
        }

        // Create health bar instance
        healthBarInstance = Instantiate(healthBarPrefab, targetCanvas.transform);

        // Get references to the background and fill images
        Transform bgTransform = healthBarInstance.transform.Find("Background");
        Transform fillTransform = healthBarInstance.transform.Find("Fill");

        if (bgTransform != null && fillTransform != null)
        {
            healthBarBackground = bgTransform.GetComponent<Image>();
            healthBarFill = fillTransform.GetComponent<Image>();

            // Set rendering order
            bgTransform.SetSiblingIndex(0);
            fillTransform.SetSiblingIndex(1);

            // Ensure the background color has full alpha
            if (healthBarBackground != null)
            {
                Color bgColor = healthBarBackground.color;
                healthBarBackground.color = new Color(bgColor.r, bgColor.g, bgColor.b, 1f);
            }

            // Ensure Fill image is using Fill method
            if (healthBarFill != null)
            {
                healthBarFill.type = Image.Type.Filled;
                healthBarFill.fillMethod = Image.FillMethod.Horizontal;
                healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                healthBarFill.fillAmount = 1.0f;

                // Use a different color to distinguish turret health bars
                healthBarFill.color = new Color(1f, 0.6f, 0.2f); // Orange-ish color for turrets
            }

            if (debugMode) Debug.Log("Health bar created successfully");
        }
        else
        {
            if (debugMode) Debug.LogError("Health bar prefab is missing Background or Fill components");
        }
    }

    void OnDestroy()
    {
        if (healthBarInstance != null)
        {
            Destroy(healthBarInstance);
        }
    }
}