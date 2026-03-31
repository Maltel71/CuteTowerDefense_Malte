using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurretUpgradeManager : MonoBehaviour
{
    [Header("Upgrade Settings")]
    public GameObject[] turretPrefabs = new GameObject[5]; // Array of upgraded turret prefabs (levels 2-6)
    public float upgradeRange = 3f;            // Distance the player needs to be to upgrade
    public int[] upgradeCosts = new int[5] { 20, 40, 80, 150, 300 }; // Costs for each level upgrade
    public int maxTurretLevel = 6;            // Maximum level a turret can reach

    [Header("UI Settings")]
    public GameObject upgradePanelPrefab;      // The UI panel prefab
    public Vector3 panelOffset = new Vector3(0, 0.5f, 0); // Small offset from the panel position transform
    public string panelPositionObjectName = "UpgradePanelPosition"; // Name of the transform to position panel
    public float checkInterval = 0.1f;         // How often to check for nearby turrets
    public Canvas targetCanvas;                // The canvas to spawn UI elements on

    [Header("Debug")]
    public bool debugMode = true;

    private GameObject activePanel;           // Currently displayed panel
    private TurretController currentTurret;   // Current turret being upgraded
    private Transform currentPanelPositionTransform; // Current transform for panel positioning
    private PlayerController player;          // Reference to the player
    private TurretManager turretManager;      // Reference to TurretManager
    private float nextCheckTime;
    private Camera mainCamera;

    // Reference to the previous turret so we can detect changes
    private TurretController previousTurret;

    // Keep track of all turrets in the scene
    private List<TurretController> activeTurrets = new List<TurretController>();

    void Start()
    {
        // Find player reference
        player = FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("Player not found! TurretUpgradeManager requires a PlayerController in the scene.");
        }

        // Find TurretManager reference
        turretManager = FindObjectOfType<TurretManager>();
        if (turretManager == null)
        {
            Debug.LogWarning("TurretManager not found! Ghost turret check will be limited.");
        }

        // Get main camera reference
        mainCamera = Camera.main;

        // Find or create the UpgradePanel canvas
        SetupCanvas();

        // Initial scan for turrets
        RefreshTurretsList();

        // Ensure we have valid upgrade costs
        ValidateUpgradeCosts();

        // Log the state of the turret prefabs array
        LogTurretPrefabsInfo();
    }

    void LogTurretPrefabsInfo()
    {
        if (debugMode)
        {
            Debug.Log($"Turret Prefabs Array Length: {turretPrefabs.Length}");
            for (int i = 0; i < turretPrefabs.Length; i++)
            {
                string prefabName = turretPrefabs[i] != null ? turretPrefabs[i].name : "null";
                Debug.Log($"  Turret Prefab [{i}]: {prefabName} (Expected Level: {i + 2})");
            }
        }
    }

    void ValidateUpgradeCosts()
    {
        // Make sure we have a cost defined for each possible upgrade
        if (upgradeCosts.Length < maxTurretLevel - 1)
        {
            Debug.LogWarning("Not enough upgrade costs defined. Filling with default values.");
            int[] newCosts = new int[maxTurretLevel - 1];

            // Copy existing costs
            for (int i = 0; i < upgradeCosts.Length; i++)
            {
                newCosts[i] = upgradeCosts[i];
            }

            // Fill remaining with increasing costs
            for (int i = upgradeCosts.Length; i < maxTurretLevel - 1; i++)
            {
                // Each upgrade costs twice as much as the previous one if not defined
                newCosts[i] = (i > 0) ? newCosts[i - 1] * 2 : 20;
            }

            upgradeCosts = newCosts;
        }
    }

    // Refresh the list of all turrets in the scene
    void RefreshTurretsList()
    {
        activeTurrets.Clear();
        TurretController[] turrets = FindObjectsOfType<TurretController>();

        foreach (TurretController turret in turrets)
        {
            if (turret != null && turret.gameObject.activeInHierarchy && IsRealTurret(turret.gameObject))
            {
                activeTurrets.Add(turret);

                if (debugMode)
                {
                    Debug.Log($"Added turret to tracking: {turret.name} (Level: {turret.turretLevel}, ID: {turret.gameObject.GetInstanceID()})");
                }
            }
        }

        if (debugMode)
        {
            Debug.Log($"Tracking {activeTurrets.Count} turrets in the scene");
        }
    }

    // Check if this is a real turret and not a ghost/preview
    bool IsRealTurret(GameObject turretObj)
    {
        // Check if the TurretManager is currently placing a turret
        if (turretManager != null && turretManager.IsPlacingTurret())
        {
            // Get the current ghost turret
            GameObject ghostTurret = turretManager.GetCurrentGhostTurret();

            // If this is the ghost turret, return false
            if (ghostTurret != null && turretObj == ghostTurret)
            {
                if (debugMode) Debug.Log($"Identified ghost turret: {turretObj.name}, skipping");
                return false;
            }
        }

        // Check for common ghost-related names
        if (turretObj.name.Contains("Ghost") || turretObj.name.Contains("Preview") ||
            turretObj.name.Contains("Placement"))
        {
            if (debugMode) Debug.Log($"Skipping turret with ghost-like name: {turretObj.name}");
            return false;
        }

        // Check if it has translucent materials (common for ghosts)
        Renderer[] renderers = turretObj.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            // Skip null checks
            if (renderer == null || renderer.material == null) continue;

            // Check if any material has significant transparency
            if (renderer.material.color.a < 0.9f)
            {
                if (debugMode) Debug.Log($"Skipping turret with translucent materials: {turretObj.name}");
                return false;
            }
        }

        return true;
    }

    void SetupCanvas()
    {
        // If no canvas assigned, try to find one with the tag
        if (targetCanvas == null)
        {
            // Try to find the specific canvas first
            GameObject canvasObj = GameObject.Find("CanvasTurretUpgradePanel");
            if (canvasObj != null)
            {
                targetCanvas = canvasObj.GetComponent<Canvas>();
                if (debugMode) Debug.Log("Found CanvasTurretUpgradePanel");
            }

            // If not found, try to find by tag
            if (targetCanvas == null)
            {
                GameObject taggedCanvas = GameObject.FindWithTag("TurretUpgradeUI");
                if (taggedCanvas != null)
                {
                    targetCanvas = taggedCanvas.GetComponent<Canvas>();
                    if (debugMode) Debug.Log("Found canvas with TurretUpgradeUI tag");
                }
            }

            // If still not found, create a new canvas
            if (targetCanvas == null)
            {
                if (debugMode) Debug.Log("Creating new CanvasTurretUpgradePanel");
                GameObject newCanvas = new GameObject("CanvasTurretUpgradePanel");
                targetCanvas = newCanvas.AddComponent<Canvas>();
                targetCanvas.renderMode = RenderMode.WorldSpace;

                // Add a CanvasScaler for proper sizing
                CanvasScaler scaler = newCanvas.AddComponent<CanvasScaler>();
                scaler.dynamicPixelsPerUnit = 100;

                // Add a GraphicRaycaster for button interactions
                newCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                // Set the tag
                newCanvas.tag = "TurretUpgradeUI";
            }
        }
    }

    void Update()
    {
        // Periodically check if any new turrets have been added
        if (Time.frameCount % 60 == 0) // Every ~1 second (60 frames)
        {
            RefreshTurretsList();
        }

        // Only check periodically for performance
        if (Time.time >= nextCheckTime)
        {
            CheckForNearbyTurrets();
            nextCheckTime = Time.time + checkInterval;
        }

        // Update panel position to face camera if it exists
        if (activePanel != null && activePanel.activeSelf && currentTurret != null)
        {
            UpdatePanelPosition();
        }

        // If TurretManager is placing a turret, ensure no upgrade panel is visible
        if (turretManager != null && turretManager.IsPlacingTurret() && activePanel != null)
        {
            DestroyPanel();
            currentTurret = null;
        }
    }

    void CheckForNearbyTurrets()
    {
        if (player == null) return;

        // Don't check for turrets if the player is currently placing a turret
        if (turretManager != null && turretManager.IsPlacingTurret())
        {
            if (activePanel != null)
            {
                DestroyPanel();
                currentTurret = null;
            }
            return;
        }

        // Check if current turret is still valid
        if (currentTurret != null && (!currentTurret.gameObject.activeInHierarchy || !activeTurrets.Contains(currentTurret) || !IsRealTurret(currentTurret.gameObject)))
        {
            if (debugMode) Debug.Log("Current turret no longer exists or is a ghost, resetting references");
            currentTurret = null;
            currentPanelPositionTransform = null;
            DestroyPanel();
        }

        TurretController closestTurret = null;
        float closestDistance = upgradeRange;

        // Find the closest turret within range
        foreach (TurretController turret in activeTurrets)
        {
            // Skip inactive turrets or ghost turrets
            if (turret == null || !turret.gameObject.activeInHierarchy || !IsRealTurret(turret.gameObject))
                continue;

            float distance = Vector3.Distance(player.transform.position, turret.transform.position);

            // Check if this turret is closer than current closest and within range
            if (distance < closestDistance)
            {
                closestTurret = turret;
                closestDistance = distance;
            }
        }

        // Handle turret changes
        if (closestTurret != null)
        {
            // If we have a new turret in range
            if (currentTurret != closestTurret)
            {
                if (debugMode) Debug.Log($"Found new turret in range: {closestTurret.name} at distance {closestDistance:F2}");

                // Clean up any existing panel
                DestroyPanel();

                // Set new turret references
                currentTurret = closestTurret;
                currentPanelPositionTransform = FindPanelPositionTransform(closestTurret);

                // Create fresh panel for the new turret
                ShowUpgradePanel();
            }
        }
        else
        {
            // No turret in range, clean up
            DestroyPanel();
            currentTurret = null;
            currentPanelPositionTransform = null;
        }

        // Update our previous turret reference
        previousTurret = currentTurret;
    }

    void DestroyPanel()
    {
        if (activePanel != null)
        {
            // Get panel component
            TurretUpgradePanel panelComponent = activePanel.GetComponent<TurretUpgradePanel>();

            if (panelComponent != null)
            {
                // Call the Hide method which includes animation
                panelComponent.Hide();

                // Destroy after a short delay to allow animation to complete
                Destroy(activePanel, 0.3f);
            }
            else
            {
                // Fall back to immediate destruction if no component
                Destroy(activePanel);
            }

            activePanel = null;
        }
    }

    Transform FindPanelPositionTransform(TurretController turret)
    {
        if (turret == null || !turret.gameObject.activeInHierarchy)
            return null;

        // Try to find a direct child with the specified name
        Transform positionTransform = turret.transform.Find(panelPositionObjectName);

        if (positionTransform != null)
        {
            if (debugMode) Debug.Log($"Found panel position transform '{panelPositionObjectName}' on turret {turret.name}");
            return positionTransform;
        }

        // Search deeper in children if not found at the top level
        Transform[] allChildren = turret.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allChildren)
        {
            if (child.name == panelPositionObjectName)
            {
                if (debugMode) Debug.Log($"Found panel position transform '{panelPositionObjectName}' as deeper child of turret {turret.name}");
                return child;
            }
        }

        // If still not found, just use the turret's transform
        if (debugMode) Debug.LogWarning($"Could not find panel position transform '{panelPositionObjectName}' on turret {turret.name}. Using turret transform instead.");
        return turret.transform;
    }

    void ShowUpgradePanel()
    {
        if (currentTurret == null) return;

        // Create a new panel
        if (upgradePanelPrefab != null && targetCanvas != null)
        {
            activePanel = Instantiate(upgradePanelPrefab, targetCanvas.transform);

            // Position panel immediately
            UpdatePanelPosition();

            // Get references to UI elements
            Button upgradeButton = activePanel.GetComponentInChildren<Button>();
            TextMeshProUGUI levelText = null;
            TextMeshProUGUI costText = null;

            // Find texts by name or type
            TextMeshProUGUI[] texts = activePanel.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (TextMeshProUGUI text in texts)
            {
                if (text.name.Contains("Cost"))
                {
                    costText = text;
                }
                else if (text.name.Contains("Level"))
                {
                    levelText = text;
                }
            }

            // Set up button click handler
            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveAllListeners();
                upgradeButton.onClick.AddListener(UpgradeCurrentTurret);

                // Update button state based on points and level
                int turretLevel = GetTurretLevel(currentTurret);
                int upgradeCost = GetUpgradeCost(turretLevel);
                PointsManager pointsManager = PointsManager.Instance;
                bool canAfford = (pointsManager != null) && (pointsManager.GetCurrentPoints() >= upgradeCost);
                bool canUpgrade = (turretLevel < maxTurretLevel);

                // Update button interactability
                upgradeButton.interactable = canAfford && canUpgrade;

                // Update button text
                TextMeshProUGUI buttonText = upgradeButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = canUpgrade ? "UPGRADE" : "MAX LEVEL";
                }

                // Update button colors
                ColorBlock colors = upgradeButton.colors;
                colors.normalColor = (canAfford && canUpgrade) ?
                                     new Color(1f, 1f, 1f, 1f) :
                                     new Color(0.7f, 0.7f, 0.7f, 1f);
                upgradeButton.colors = colors;
            }

            // Update text content
            if (levelText != null)
            {
                levelText.text = $"Level {GetTurretLevel(currentTurret)}";
            }

            if (costText != null && GetTurretLevel(currentTurret) < maxTurretLevel)
            {
                costText.text = $"Cost: {GetUpgradeCost(GetTurretLevel(currentTurret))}";
            }
            else if (costText != null)
            {
                costText.text = "MAX LEVEL";
            }

            // The animation now starts automatically in the panel's Start() method
            // No need to manually trigger it here

            if (debugMode) Debug.Log($"Created new upgrade panel for turret: {currentTurret.name}");
        }
        else
        {
            Debug.LogError("Missing upgradePanelPrefab or targetCanvas!");
        }
    }

    void UpdatePanelPosition()
    {
        if (activePanel == null || currentTurret == null || mainCamera == null) return;

        // Check if the current turret still exists
        if (!currentTurret.gameObject.activeInHierarchy)
        {
            DestroyPanel();
            currentTurret = null;
            currentPanelPositionTransform = null;
            return;
        }

        // Get the position to place the panel
        Vector3 basePosition;

        // Use the dedicated transform if available, otherwise use turret position
        if (currentPanelPositionTransform != null && currentPanelPositionTransform.gameObject.activeInHierarchy)
        {
            basePosition = currentPanelPositionTransform.position;
            if (debugMode) Debug.DrawRay(basePosition, Vector3.up * 0.5f, Color.green);
        }
        else
        {
            basePosition = currentTurret.transform.position;
        }

        // Apply small offset to fine-tune positioning
        Vector3 worldPosition = basePosition + panelOffset;

        // For a World Space canvas
        if (targetCanvas.renderMode == RenderMode.WorldSpace)
        {
            // Set the panel's position in world space
            activePanel.transform.position = worldPosition;

            // Make the panel face the camera
            activePanel.transform.forward = mainCamera.transform.forward;

            if (debugMode)
            {
                Debug.DrawLine(basePosition, worldPosition, Color.yellow, 0.1f);
            }
        }
        // For Screen Space canvases
        else
        {
            // Convert world position to screen position
            Vector2 screenPoint = mainCamera.WorldToScreenPoint(worldPosition);

            RectTransform panelRectTransform = activePanel.GetComponent<RectTransform>();
            if (panelRectTransform != null)
            {
                // Convert screen position to canvas position
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    targetCanvas.GetComponent<RectTransform>(),
                    screenPoint,
                    targetCanvas.renderMode == RenderMode.ScreenSpaceCamera ? mainCamera : null,
                    out Vector2 localPoint))
                {
                    panelRectTransform.anchoredPosition = localPoint;
                }
                else
                {
                    Debug.LogWarning("Failed to convert screen point to local point in rectangle");
                }
            }
        }
    }

    void UpgradeCurrentTurret()
    {
        if (currentTurret == null || !currentTurret.gameObject.activeInHierarchy)
        {
            DestroyPanel();
            return;
        }

        // Get the current level of the turret
        int turretLevel = GetTurretLevel(currentTurret);

        // Check if already at max level
        if (turretLevel >= maxTurretLevel)
        {
            if (debugMode) Debug.LogWarning("Turret is already at max level!");
            return;
        }

        // Get the upgrade cost for the current level
        int upgradeCost = GetUpgradeCost(turretLevel);

        // Check if player has enough points
        PointsManager pointsManager = PointsManager.Instance;
        if (pointsManager == null || pointsManager.GetCurrentPoints() < upgradeCost)
        {
            if (debugMode) Debug.LogWarning($"Not enough points to upgrade turret! Need {upgradeCost}");
            return;
        }

        // Spend points
        if (pointsManager.SpendPoints(upgradeCost))
        {
            // Replace the turret with upgraded version
            UpgradeTurretPrefab(currentTurret, turretLevel);

            // Clean up panel and references
            DestroyPanel();
            currentTurret = null;
            currentPanelPositionTransform = null;
            previousTurret = null;

            // Force refresh of turret list
            RefreshTurretsList();

            if (debugMode) Debug.Log($"Turret upgraded successfully to level {turretLevel + 1}!");
        }
        else
        {
            if (debugMode) Debug.LogError("Failed to spend points for turret upgrade!");
        }
    }

    void UpgradeTurretPrefab(TurretController oldTurret, int currentLevel)
    {
        if (oldTurret == null) return;

        // Calculate the next level
        int nextLevel = currentLevel + 1;

        if (debugMode)
        {
            Debug.Log($"Upgrading turret: {oldTurret.name} from level {currentLevel} to level {nextLevel}");
        }

        // Get the appropriate upgraded prefab
        GameObject nextLevelPrefab = GetTurretPrefabForLevel(nextLevel);

        if (nextLevelPrefab == null)
        {
            Debug.LogError($"No turret prefab found for level {nextLevel}! Cannot upgrade.");
            return;
        }

        // Save important information from the old turret
        Vector3 position = oldTurret.transform.position;
        Quaternion rotation = oldTurret.transform.rotation;

        // Log info before destruction for debugging
        if (debugMode)
        {
            Debug.Log($"Using prefab {nextLevelPrefab.name} for upgrade to level {nextLevel}");
            Debug.Log($"Old turret position: {position}, rotation: {rotation}");
        }

        // Instantiate the new, upgraded turret
        GameObject newTurretObj = Instantiate(nextLevelPrefab, position, rotation);

        // Make sure it has the Turret tag
        newTurretObj.tag = "Turret";

        // Get the TurretController of the new turret
        TurretController newTurret = newTurretObj.GetComponent<TurretController>();

        if (newTurret != null)
        {
            // Set the new level explicitly
            newTurret.turretLevel = nextLevel;

            if (debugMode) Debug.Log($"New turret level set to {newTurret.turretLevel}");

            // If the old turret had a health system, copy remaining health percentage
            TurretHealth oldHealth = oldTurret.GetComponent<TurretHealth>();
            TurretHealth newHealth = newTurret.GetComponent<TurretHealth>();

            if (oldHealth != null && newHealth != null)
            {
                float healthPercentage = (float)oldHealth.GetCurrentHealth() / oldHealth.maxHealth;
                int newHealthValue = Mathf.RoundToInt(healthPercentage * newHealth.maxHealth);

                // Set the new turret's health
                SetTurretHealth(newHealth, newHealthValue);

                if (debugMode) Debug.Log($"Transferred health at {healthPercentage:P0} from old turret to new turret");
            }
        }
        else
        {
            Debug.LogError($"New turret prefab does not have a TurretController component! Something is wrong with prefab {nextLevelPrefab.name}");
        }

        // Destroy the old turret
        Destroy(oldTurret.gameObject);

        if (debugMode) Debug.Log($"Replaced turret with upgraded version at {position}");
    }

    // Get the appropriate turret prefab for a specific level
    GameObject GetTurretPrefabForLevel(int level)
    {
        // Level is 1-based, but array is 0-based
        // For a level 2 turret, we want index 0 in the array
        int prefabIndex = level - 2;

        if (debugMode)
        {
            Debug.Log($"Looking for turret prefab for level {level} at index {prefabIndex}");
        }

        // Check if the index is valid and we have a prefab for it
        if (prefabIndex >= 0 && prefabIndex < turretPrefabs.Length)
        {
            GameObject prefab = turretPrefabs[prefabIndex];

            if (prefab != null)
            {
                if (debugMode) Debug.Log($"Found prefab: {prefab.name} for level {level}");
                return prefab;
            }
            else
            {
                Debug.LogError($"Turret prefab at index {prefabIndex} is null! Check your prefab assignments in the inspector.");
            }
        }
        else
        {
            Debug.LogError($"Invalid prefab index {prefabIndex} for level {level}! Check your turretPrefabs array configuration.");
        }

        return null;
    }

    // Get the upgrade cost for a specific level
    int GetUpgradeCost(int currentLevel)
    {
        // Current level is 1-based, but array is 0-based
        // We're getting the cost to upgrade FROM this level, so we need index (currentLevel - 1)
        int costIndex = currentLevel - 1;

        if (costIndex >= 0 && costIndex < upgradeCosts.Length)
        {
            return upgradeCosts[costIndex];
        }

        // Default cost if not found (shouldn't happen with proper validation)
        Debug.LogWarning($"No defined upgrade cost for level {currentLevel}! Using default.");
        return 50 * currentLevel;
    }

    int GetTurretLevel(TurretController turret)
    {
        if (turret == null) return 0;

        // Use the turretLevel field
        return turret.turretLevel;
    }

    void SetTurretHealth(TurretHealth health, int value)
    {
        // Try to use the SetHealth method if it exists
        System.Reflection.MethodInfo setHealthMethod = typeof(TurretHealth).GetMethod("SetHealth");
        if (setHealthMethod != null)
        {
            setHealthMethod.Invoke(health, new object[] { value });
            if (debugMode) Debug.Log($"Set new turret health to {value} using SetHealth method");
            return;
        }

        // Otherwise, use reflection as a fallback
        System.Reflection.FieldInfo healthField = typeof(TurretHealth).GetField("currentHealth",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        if (healthField != null)
        {
            healthField.SetValue(health, value);
            if (debugMode) Debug.Log($"Set new turret health to {value} using reflection");
        }
        else
        {
            if (debugMode) Debug.LogWarning("Could not set turret health via reflection");
        }
    }

    void OnDrawGizmos()
    {
        // Draw panel position if we have a current turret
        if (currentTurret != null && currentTurret.gameObject.activeInHierarchy)
        {
            Vector3 basePosition;

            // Find position transform if needed
            if (currentPanelPositionTransform == null)
            {
                currentPanelPositionTransform = FindPanelPositionTransform(currentTurret);
            }

            if (currentPanelPositionTransform != null && currentPanelPositionTransform.gameObject.activeInHierarchy)
            {
                basePosition = currentPanelPositionTransform.position;
                // Draw the position transform
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(basePosition, 0.15f);
            }
            else
            {
                basePosition = currentTurret.transform.position;
            }

            // Draw the final position with offset
            Vector3 panelPos = basePosition + panelOffset;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(panelPos, 0.25f);
            Gizmos.DrawLine(basePosition, panelPos);
        }
    }

    void OnDrawGizmosSelected()
    {
        // Visualize the upgrade range in the editor
        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(player.transform.position, upgradeRange);
        }
    }
}