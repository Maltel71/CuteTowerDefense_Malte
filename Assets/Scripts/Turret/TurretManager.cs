using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class TurretManager : MonoBehaviour
{
    [Header("Turret Settings")]
    public GameObject turretPrefab;
    public GameObject turretGhostPrefab;
    public int turretCost = 10;

    [Header("UI References")]
    public Canvas canvasTurretButtons;
    public Button turretButton;
    public TextMeshProUGUI buttonText;

    [Header("Input Settings")]
    public KeyCode cancelKey = KeyCode.Escape; // Key to cancel placement
    public KeyCode placementKey = KeyCode.T;   // Optional hotkey to start placement

    [Header("Debug Settings")]
    public bool debugMode = true;

    // Add a tag to identify ghost turrets
    [HideInInspector]
    public string ghostTurretTag = "TurretGhost";

    private GameObject currentTurretGhost;
    private bool placingTurret = false;
    private bool validPlacement = false;
    private PlayerController playerController;

    // Use this to track when we've consumed mouse input for turret placement
    private bool placedTurretThisFrame = false;

    void Start()
    {
        Debug.Log("TurretManager starting...");

        // Find player controller
        playerController = FindObjectOfType<PlayerController>();
        if (playerController == null && debugMode)
        {
            Debug.LogWarning("Could not find PlayerController");
        }

        SetupButtonReferences();

        // Add click listener to the button
        if (turretButton != null)
        {
            turretButton.onClick.RemoveAllListeners();
            turretButton.onClick.AddListener(StartPlacingTurret);
            turretButton.gameObject.SetActive(true);
            Debug.Log("Click listener added to button and ensured visibility");
        }

        // Subscribe to points changed event
        PointsManager.OnPointsChanged += OnPointsChanged;

        UpdateButtonState();
    }

    void OnDestroy()
    {
        // Unsubscribe from points changed event
        PointsManager.OnPointsChanged -= OnPointsChanged;
    }

    void OnPointsChanged(int newPoints)
    {
        if (debugMode) Debug.Log($"TurretManager notified of point change. New points: {newPoints}");
        UpdateButtonState();
    }

    void SetupButtonReferences()
    {
        // If button is not assigned, try to find it
        if (turretButton == null)
        {
            Debug.Log("Looking for turret button...");
            turretButton = GameObject.Find("TurretButton")?.GetComponent<Button>();

            if (turretButton == null)
            {
                Debug.LogError("Turret button not found! Please assign it in the inspector.");
            }
            else
            {
                Debug.Log("Turret button found");
            }
        }

        // If button text is not assigned, try to find it
        if (buttonText == null && turretButton != null)
        {
            buttonText = turretButton.GetComponentInChildren<TextMeshProUGUI>();

            if (buttonText == null)
            {
                Debug.LogError("Button text not found! Please ensure the button has a TextMeshProUGUI child component.");
            }
            else
            {
                Debug.Log("Button text component found");
            }
        }
    }

    void LateUpdate()
    {
        // Reset this flag each frame after player has processed inputs
        placedTurretThisFrame = false;
    }

    void Update()
    {
        // Hotkey to start placement (alternative to button)
        if (Input.GetKeyDown(placementKey) && !placingTurret)
        {
            PointsManager pointManager = PointsManager.Instance;
            if (pointManager != null && pointManager.GetCurrentPoints() >= turretCost)
            {
                StartPlacingTurret();
            }
        }

        if (placingTurret)
        {
            UpdateGhostPosition();

            // Place turret on left mouse button click - BUT only if not over UI
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            {
                if (debugMode) Debug.Log("LEFT MOUSE CLICKED - Attempting to place turret");
                PlaceTurret();

                // Set this flag so PlayerController knows not to shoot
                placedTurretThisFrame = true;
            }

            // Cancel placement on right mouse button click or escape key
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(cancelKey))
            {
                if (debugMode) Debug.Log("Placement canceled by user input");
                CancelPlacement();
            }
        }
    }

    // Method to check if currently placing a turret
    public bool IsPlacingTurret()
    {
        return placingTurret;
    }

    // Method to get the current ghost turret for other systems to check
    public GameObject GetCurrentGhostTurret()
    {
        return currentTurretGhost;
    }

    // This method can be called by PlayerController to check if mouse input was used for turret placement
    public bool WasMouseInputUsedForTurret()
    {
        return placedTurretThisFrame;
    }

    // Check if the mouse is over a UI element
    private bool IsPointerOverUI()
    {
        return EventSystem.current.IsPointerOverGameObject();
    }

    void UpdateGhostPosition()
    {
        if (currentTurretGhost != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Use a more reliable method to hit the ground
            if (Physics.Raycast(ray, out hit, 100f, LayerMask.GetMask("Ground")))
            {
                // Adjust Y position slightly to prevent Z-fighting with the ground
                Vector3 position = hit.point;
                position.y += 0.01f;
                currentTurretGhost.transform.position = position;

                // Check if placement is valid and update visual feedback
                validPlacement = IsValidPlacement(position);
                SetGhostColor(validPlacement ? Color.green : Color.red);

                if (debugMode) Debug.Log($"Ghost Position: {position}, Valid Placement: {validPlacement}");
            }
            else if (debugMode)
            {
                Debug.Log("Raycast failed to hit Ground layer");
            }
        }
    }

    bool IsValidPlacement(Vector3 position)
    {
        // Check if there's enough clearance for the turret
        Collider[] colliders = Physics.OverlapSphere(position, 1.5f);

        if (debugMode && colliders.Length > 0)
        {
            Debug.Log($"Found {colliders.Length} colliders at placement position");
        }

        foreach (Collider collider in colliders)
        {
            // Skip the ghost turret itself
            if (collider.gameObject == currentTurretGhost || collider.transform.IsChildOf(currentTurretGhost.transform))
                continue;

            // Skip ground layer objects
            if (collider.gameObject.layer == LayerMask.NameToLayer("Ground"))
                continue;

            // Skip trigger colliders - this is the key fix
            if (collider.isTrigger)
                continue;

            // Any other collider means invalid placement
            if (debugMode) Debug.Log($"Invalid placement due to: {collider.gameObject.name}");
            return false;
        }

        return true;
    }

    void SetGhostColor(Color color)
    {
        Renderer[] renderers = currentTurretGhost.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            Material material = renderer.material;
            color.a = 0.5f;
            material.color = color;
        }
    }

    void PlaceTurret()
    {
        if (currentTurretGhost == null)
        {
            if (debugMode) Debug.LogError("Cannot place turret: ghost is null");
            return;
        }

        Vector3 position = currentTurretGhost.transform.position;
        PointsManager pointManager = PointsManager.Instance;

        if (pointManager == null)
        {
            Debug.LogError("PointsManager not found!");
            return;
        }

        if (debugMode) Debug.Log($"Trying to place turret at {position}, Valid: {validPlacement}, Points: {pointManager.GetCurrentPoints()}/{turretCost}");

        // Only place if position is valid and we have enough points
        if (validPlacement)
        {
            // Try to spend points
            if (pointManager.SpendPoints(turretCost))
            {
                GameObject newTurret = Instantiate(turretPrefab, position, Quaternion.identity);

                // Set the tag to "Turret" so wildlife can detect it
                newTurret.tag = "Turret";

                // Find the health bar prefab to pass to the new turret
                GameObject healthBarPrefab = FindHealthBarPrefab();

                // Configure the new turret controller with the health bar prefab
                TurretController turretController = newTurret.GetComponent<TurretController>();
                if (turretController != null && healthBarPrefab != null)
                {
                    turretController.healthBarPrefab = healthBarPrefab;
                    if (debugMode) Debug.Log("Health bar prefab assigned to new turret");
                }

                Debug.Log("Turret placed successfully at " + position);

                // Clean up ghost and reset state
                FinishPlacement();
            }
            else
            {
                Debug.LogWarning("Failed to spend points for turret");
                CancelPlacement();
            }
        }
        else
        {
            Debug.LogWarning("Cannot place turret: invalid position");
        }
    }

    GameObject FindHealthBarPrefab()
    {
        // Try to find prefab from existing turrets
        TurretHealthBar[] existingHealthBars = FindObjectsOfType<TurretHealthBar>();
        foreach (TurretHealthBar healthBar in existingHealthBars)
        {
            if (healthBar.healthBarPrefab != null)
            {
                return healthBar.healthBarPrefab;
            }
        }

        // Try commonly used names
        GameObject[] possiblePrefabs = {
            GameObject.Find("HealthBarPrefab"),
            GameObject.Find("HealthBarPrefabTurret"),
            GameObject.Find("TurretHealthBarPrefab")
        };

        foreach (GameObject prefab in possiblePrefabs)
        {
            if (prefab != null)
            {
                return prefab;
            }
        }

        // Last resort - try to find in active scene by tag
        GameObject taggedPrefab = GameObject.FindWithTag("HealthBarPrefab");
        if (taggedPrefab != null)
        {
            return taggedPrefab;
        }

        if (debugMode) Debug.LogWarning("Could not find health bar prefab!");
        return null;
    }

    void CancelPlacement()
    {
        if (currentTurretGhost != null)
        {
            Destroy(currentTurretGhost);
            currentTurretGhost = null;
            placingTurret = false;

            // Re-enable the button
            UpdateButtonState();

            Debug.Log("Turret placement canceled");
        }
    }

    void FinishPlacement()
    {
        Destroy(currentTurretGhost);
        currentTurretGhost = null;
        placingTurret = false;

        // Update button state after spending points
        UpdateButtonState();
    }

    void UpdateButtonState()
    {
        if (turretButton != null)
        {
            PointsManager pointManager = PointsManager.Instance;

            if (pointManager != null)
            {
                int currentPoints = pointManager.GetCurrentPoints();
                bool canAfford = currentPoints >= turretCost;

                if (debugMode) Debug.Log($"Button state check: Points={currentPoints}, Cost={turretCost}, CanAfford={canAfford}");

                // Ensure the button is interactable based on points
                turretButton.interactable = canAfford && !placingTurret;

                // Make sure the button GameObject is active regardless of points
                if (!turretButton.gameObject.activeSelf)
                {
                    Debug.Log("Button GameObject was inactive - activating");
                    turretButton.gameObject.SetActive(true);
                }

                // Force update the button colors to make the change visible
                ColorBlock colors = turretButton.colors;
                if (canAfford && !placingTurret)
                {
                    // Make the button more vibrant when affordable
                    colors.disabledColor = new Color(0.8f, 0.8f, 0.8f, 1f);
                    colors.normalColor = new Color(1f, 1f, 1f, 1f);
                }
                else
                {
                    // Make the button greyed out when not affordable or during placement
                    colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                }
                turretButton.colors = colors;
            }
            else
            {
                Debug.LogWarning("PointsManager instance not found!");
            }
        }
        else
        {
            Debug.LogWarning("Turret button not found!");
        }
    }

    void StartPlacingTurret()
    {
        PointsManager pointManager = PointsManager.Instance;

        if (pointManager == null)
        {
            Debug.LogError("PointsManager not found!");
            return;
        }

        // Log detailed information about the purchase attempt
        Debug.Log($"StartPlacingTurret called. Current points: {pointManager.GetCurrentPoints()}, Turret cost: {turretCost}, CanAfford: {pointManager.GetCurrentPoints() >= turretCost}");

        if (pointManager.GetCurrentPoints() >= turretCost && turretGhostPrefab != null)
        {
            Debug.Log("Starting turret placement...");
            placingTurret = true;
            currentTurretGhost = Instantiate(turretGhostPrefab);

            // Tag the ghost turret for easy identification
            currentTurretGhost.tag = ghostTurretTag;

            // Also add "Ghost" to the name for additional identification
            currentTurretGhost.name = "TurretGhost_Preview";

            // Make ghost partially transparent
            Renderer[] renderers = currentTurretGhost.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material material = new Material(renderer.material); // Create new instance to avoid shared material
                Color color = material.color;
                color.a = 0.5f;
                material.color = color;
                renderer.material = material;
            }

            // Disable the button while placing
            if (turretButton != null)
            {
                turretButton.interactable = false;
            }
        }
        else
        {
            Debug.LogWarning("Cannot place turret: insufficient points or missing ghost prefab");
        }
    }
}