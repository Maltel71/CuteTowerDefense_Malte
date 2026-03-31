using UnityEngine;
using UnityEngine.UI;

public class WildlifeHealthBar : MonoBehaviour
{
    public GameObject healthBarPrefab;   // Assign a UI prefab with Image components
    public Vector3 offset = new Vector3(0, 0.5f, 0);  // Height above the wildlife

    [Tooltip("Set this to match the Wildlife's maxHealth value")]
    public int maxHealthValue = 2;  // Default value that matches Wildlife default

    private GameObject healthBarInstance;
    private Image healthBarBackground;   // Background of the health bar
    private Image healthBarFill;         // Foreground of the health bar with Fill method
    private MonoBehaviour targetHealthComponent;  // Generic reference instead of Wildlife-specific
    private Canvas canvas;               // Reference to the UI canvas
    private Camera mainCamera;           // Reference to the main camera

    // This will hold the reflection method to get health
    private System.Reflection.MethodInfo getHealthMethod;

    void Start()
    {
        mainCamera = Camera.main;

        // First try to get Wildlife component
        targetHealthComponent = GetComponent<MonoBehaviour>();

        if (targetHealthComponent != null)
        {
            // Try to find public "Health" property or "GetCurrentHealth" method
            System.Type type = targetHealthComponent.GetType();
            getHealthMethod = type.GetMethod("GetCurrentHealth");

            if (getHealthMethod == null)
            {
                // Try to find a Health property
                System.Reflection.PropertyInfo healthProperty = type.GetProperty("Health");
                if (healthProperty != null)
                {
                    getHealthMethod = healthProperty.GetGetMethod();
                }
            }

            // Try to get maxHealth field value directly
            System.Reflection.FieldInfo maxHealthField = type.GetField("maxHealth");
            if (maxHealthField != null)
            {
                object value = maxHealthField.GetValue(targetHealthComponent);
                if (value != null)
                {
                    maxHealthValue = (int)value;
                    Debug.Log($"Found maxHealth field: {maxHealthValue}");
                }
            }

            CreateHealthBar();
        }
        else
        {
            Debug.LogError("No valid health component found on this GameObject!");
        }
    }

    void CreateHealthBar()
    {
        // Find the canvas with HealthUI tag in the scene
        Canvas[] allCanvases = FindObjectsOfType<Canvas>();
        foreach (Canvas c in allCanvases)
        {
            if (c.CompareTag("HealthUI"))
            {
                canvas = c;
                break;
            }
        }

        // If no tagged canvas found, try to find any canvas
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>();
            Debug.LogWarning("No Canvas with 'HealthUI' tag found. Using first available canvas.");
        }

        if (canvas == null)
        {
            Debug.LogError("No Canvas found in the scene. Please add a Canvas with 'HealthUI' tag.");
            return;
        }

        // Create health bar instance
        healthBarInstance = Instantiate(healthBarPrefab, canvas.transform);

        // Get references to the background and fill images
        Transform bgTransform = healthBarInstance.transform.Find("Background");
        Transform fillTransform = healthBarInstance.transform.Find("Fill");

        if (bgTransform != null && fillTransform != null)
        {
            healthBarBackground = bgTransform.GetComponent<Image>();
            healthBarFill = fillTransform.GetComponent<Image>();

            // IMPORTANT: Make sure the rendering order is correct
            // Background should be behind Fill
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
                // These should already be set in the prefab, but we'll set them here as a fallback
                healthBarFill.type = Image.Type.Filled;
                healthBarFill.fillMethod = Image.FillMethod.Horizontal;
                healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                healthBarFill.fillAmount = 1.0f; // Start with full health
            }
        }
        else
        {
            Debug.LogError("Health bar prefab is missing Background or Fill components");
        }
    }

    void Update()
    {
        if (targetHealthComponent == null || healthBarInstance == null)
            return;

        // Get world position
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
            // Try to get current health through reflection
            int currentHealth = 0;

            if (getHealthMethod != null)
            {
                object result = getHealthMethod.Invoke(targetHealthComponent, null);
                if (result != null)
                {
                    currentHealth = (int)result;
                }
            }

            float healthPercent = (float)currentHealth / maxHealthValue;
            healthPercent = Mathf.Clamp01(healthPercent);
            healthBarFill.fillAmount = healthPercent;
        }
    }

    void OnDestroy()
    {
        // Clean up the health bar when the wildlife is destroyed
        if (healthBarInstance != null)
        {
            Destroy(healthBarInstance);
        }
    }
}