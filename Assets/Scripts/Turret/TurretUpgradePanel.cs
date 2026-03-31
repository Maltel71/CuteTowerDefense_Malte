using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class TurretUpgradePanel : MonoBehaviour
{
    [Header("Panel References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI levelText;
    public TextMeshProUGUI costText;
    public Button upgradeButton;
    public TextMeshProUGUI buttonText;

    [Header("Animation Settings")]
    public float appearDuration = 0.5f;

    [Header("Floating Animation Settings")]
    public float bounceHeight = 0.2f;
    public float bounceSpeed = 1.5f;
    public bool enableBounceAnimation = true;

    private RectTransform rectTransform;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // Get components
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Store the original scale
        originalScale = transform.localScale;
    }

    private void Start()
    {
        // Store the original position
        originalPosition = transform.position;

        // Find references if not assigned
        if (upgradeButton == null)
            upgradeButton = GetComponentInChildren<Button>();

        if (buttonText == null && upgradeButton != null)
            buttonText = upgradeButton.GetComponentInChildren<TextMeshProUGUI>();

        // Find text components by name
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>();
        foreach (var text in texts)
        {
            if (text.name.Contains("Title") && titleText == null)
                titleText = text;
            else if (text.name.Contains("Level") && levelText == null)
                levelText = text;
            else if (text.name.Contains("Cost") && costText == null)
                costText = text;
        }

        // Start appear animation automatically
        StartCoroutine(AppearAnimation());
    }

    private void Update()
    {
        // Apply gentle floating animation
        if (enableBounceAnimation && gameObject.activeSelf)
        {
            float bounceOffset = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
            transform.position = originalPosition + new Vector3(0, bounceOffset, 0);
        }
    }

    private IEnumerator AppearAnimation()
    {
        Debug.Log("Starting panel appear animation");

        // Set initial state
        transform.localScale = originalScale * 0.1f; // Start very small
        canvasGroup.alpha = 0f;

        // Animation timer
        float elapsed = 0f;

        while (elapsed < appearDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / appearDuration;

            // Simple smoothstep for clean animation without bounce
            float smoothT = t * t * (3f - 2f * t); // Smoothstep formula

            // Apply scale and alpha
            transform.localScale = Vector3.Lerp(originalScale * 0.1f, originalScale, smoothT);
            canvasGroup.alpha = Mathf.Clamp01(t * 2); // Fade in faster than scale

            yield return null;
        }

        // Ensure final state
        transform.localScale = originalScale;
        canvasGroup.alpha = 1f;

        Debug.Log("Panel appear animation complete");
    }

    public void Show()
    {
        gameObject.SetActive(true);
        StartCoroutine(AppearAnimation());
    }

    public void Hide()
    {
        StartCoroutine(HideWithAnimation());
    }

    private IEnumerator HideWithAnimation()
    {
        Debug.Log("Starting panel hide animation");

        // Quick fade out animation
        float duration = 0.05f;
        float elapsed = 0f;

        // Get current values
        Vector3 startScale = transform.localScale;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;

            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            transform.localScale = Vector3.Lerp(startScale, originalScale * 0.5f, t);

            yield return null;
        }

        // Ensure final state
        canvasGroup.alpha = 0f;
        Debug.Log("Panel hide animation complete");

        gameObject.SetActive(false);
    }

    public void SetPanelContent(int level, int cost, bool canUpgrade, bool canAfford)
    {
        // Update level text
        if (levelText != null)
            levelText.text = $"Level {level}";

        // Update cost text
        if (costText != null)
            costText.text = $"Cost: {cost}";

        // Update title if needed
        if (titleText != null)
            titleText.text = "Turret Upgrade";

        // Update button state
        if (upgradeButton != null)
        {
            upgradeButton.interactable = canUpgrade && canAfford;

            // Update button text
            if (buttonText != null)
            {
                if (!canUpgrade)
                    buttonText.text = "MAX LEVEL";
                else if (!canAfford)
                    buttonText.text = "NOT ENOUGH POINTS";
                else
                    buttonText.text = "UPGRADE";
            }
        }
    }

    public void SetPosition(Vector3 worldPosition)
    {
        originalPosition = worldPosition;
        transform.position = worldPosition;
    }
}