using UnityEngine;
using UnityEngine.UI;

public class TowerHealthBar : MonoBehaviour
{
    public GameObject healthBarPrefab;   // Assign the HealthBarPrefabTower
    public Vector3 offset = new Vector3(0, 5f, 0);  // Height above the tower
    public Canvas targetCanvas;          // Assign this in the Inspector to ensure you use the right canvas

    private GameObject healthBarInstance;
    private Image healthBarBackground;
    private Image healthBarFill;
    private TowerHealth towerHealth;
    private int maxHealth;
    private Camera mainCamera;

    void Start()
    {
        towerHealth = GetComponent<TowerHealth>();
        mainCamera = Camera.main;

        if (towerHealth != null)
        {
            maxHealth = towerHealth.maxHealth;
            CreateHealthBar();
        }
    }

    void CreateHealthBar()
    {
        // Use the assigned canvas or find one by tag as fallback
        if (targetCanvas == null)
        {
            // Try to find canvas with HealthUI tag
            Canvas[] allCanvases = FindObjectsOfType<Canvas>();
            foreach (Canvas c in allCanvases)
            {
                if (c.CompareTag("HealthUI"))
                {
                    targetCanvas = c;
                    break;
                }
            }

            // If still null, try any canvas
            if (targetCanvas == null)
            {
                targetCanvas = FindObjectOfType<Canvas>();
                Debug.LogWarning("No Canvas with 'HealthUI' tag found. Using first available canvas.");
            }

            if (targetCanvas == null)
            {
                Debug.LogError("No Canvas found in the scene. Please add a Canvas with 'HealthUI' tag.");
                return;
            }
        }
        else if (!targetCanvas.CompareTag("HealthUI"))
        {
            Debug.LogWarning("The assigned target canvas doesn't have the 'HealthUI' tag!");
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

                // Optional: Change color to distinguish from wildlife health bars
                healthBarFill.color = new Color(0.2f, 0.6f, 1f); // Blue-ish color for tower
            }
        }
        else
        {
            Debug.LogError("Health bar prefab is missing Background or Fill components");
        }
    }

    void Update()
    {
        if (towerHealth == null || healthBarInstance == null)
            return;

        // Update health bar position to follow the tower
        Vector3 worldPosition = transform.position + offset;

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
            float healthPercent = (float)towerHealth.GetCurrentHealth() / maxHealth;
            healthPercent = Mathf.Clamp01(healthPercent);
            healthBarFill.fillAmount = healthPercent;
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