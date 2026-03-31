using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

[RequireComponent(typeof(Button))]
public class TurretButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Scale Settings")]
    [Tooltip("How much the button scales up when hovered")]
    public float hoverScaleAmount = 1.1f;
    [Tooltip("How much the button scales down when clicked")]
    public float clickScaleAmount = 0.9f;
    [Tooltip("How fast the button scales up/down")]
    public float scaleSpeed = 10f;
    [Tooltip("Should the button scale up over time when hovered")]
    public bool useScaleOverTime = false;
    [Tooltip("Duration for scale animation")]
    public float scaleDuration = 0.5f;

    [Header("Rotation Settings")]
    [Tooltip("Enable rotation animation on hover")]
    public bool useRotationOnHover = false;
    [Tooltip("Maximum rotation in degrees when hovered")]
    public float maxRotationAngle = 30f;
    [Tooltip("How fast the button rotates")]
    public float rotationSpeed = 5f;
    [Tooltip("Should the button rotate over time when hovered")]
    public bool useRotationOverTime = false;
    [Tooltip("Duration for rotation animation")]
    public float rotationDuration = 0.5f;

    [Header("Position Offset Settings")]
    [Tooltip("Enable position offset animation on hover")]
    public bool usePositionOffset = false;
    [Tooltip("Offset on X axis when hovered")]
    public float xOffset = 0f;
    [Tooltip("Offset on Y axis when hovered")]
    public float yOffset = 10f;
    [Tooltip("Offset on Z axis when hovered")]
    public float zOffset = 0f;
    [Tooltip("How fast the button moves to offset position")]
    public float offsetSpeed = 5f;
    [Tooltip("Should the button offset over time when hovered")]
    public bool useOffsetOverTime = false;
    [Tooltip("Duration for offset animation")]
    public float offsetDuration = 0.5f;

    [Header("Sound Effects")]
    [Tooltip("Sound to play when cursor hovers over button")]
    public AudioClip hoverSound;
    [Tooltip("Sound to play when button is clicked")]
    public AudioClip clickSound;
    [Tooltip("Volume for button sounds")]
    [Range(0f, 1f)]
    public float soundVolume = 0.5f;

    private Vector3 originalScale;
    private Vector3 targetScale;
    private Vector3 originalPosition;
    private Vector3 targetPosition;
    private Quaternion originalRotation;
    private Quaternion targetRotation;
    private Button button;
    private AudioSource audioSource;
    private bool isPointerOver = false;
    private bool isPointerDown = false;
    private Coroutine scaleCoroutine;
    private Coroutine rotationCoroutine;
    private Coroutine offsetCoroutine;
    private RectTransform rectTransform;

    private void Awake()
    {
        // Get references
        button = GetComponent<Button>();
        audioSource = GetComponent<AudioSource>();
        rectTransform = GetComponent<RectTransform>();

        // Create audio source if it doesn't exist
        if (audioSource == null && (hoverSound != null || clickSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // Store original transform values
        originalScale = transform.localScale;
        originalPosition = rectTransform.anchoredPosition3D;
        originalRotation = transform.localRotation;

        // Set initial targets to original values
        targetScale = originalScale;
        targetPosition = originalPosition;
        targetRotation = originalRotation;
    }

    private void Update()
    {
        if (!useScaleOverTime)
        {
            // Smoothly interpolate towards target scale
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
        }

        if (!useRotationOverTime && useRotationOnHover)
        {
            // Smoothly interpolate towards target rotation
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        if (!useOffsetOverTime && usePositionOffset)
        {
            // Smoothly interpolate towards target position
            rectTransform.anchoredPosition3D = Vector3.Lerp(rectTransform.anchoredPosition3D, targetPosition, Time.deltaTime * offsetSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerOver = true;
        UpdateTargetTransform();
        PlaySound(hoverSound);

        // Start over-time animations if enabled
        if (useScaleOverTime)
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleOverTime(targetScale, scaleDuration));
        }

        if (useRotationOverTime && useRotationOnHover)
        {
            if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
            rotationCoroutine = StartCoroutine(RotateOverTime(targetRotation, rotationDuration));
        }

        if (useOffsetOverTime && usePositionOffset)
        {
            if (offsetCoroutine != null) StopCoroutine(offsetCoroutine);
            offsetCoroutine = StartCoroutine(OffsetOverTime(targetPosition, offsetDuration));
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerOver = false;
        UpdateTargetTransform();

        // Start over-time animations to return to original state if enabled
        if (useScaleOverTime)
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleOverTime(targetScale, scaleDuration));
        }

        if (useRotationOverTime && useRotationOnHover)
        {
            if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
            rotationCoroutine = StartCoroutine(RotateOverTime(targetRotation, rotationDuration));
        }

        if (useOffsetOverTime && usePositionOffset)
        {
            if (offsetCoroutine != null) StopCoroutine(offsetCoroutine);
            offsetCoroutine = StartCoroutine(OffsetOverTime(targetPosition, offsetDuration));
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerDown = true;
        UpdateTargetTransform();
        PlaySound(clickSound);

        // Add click animation
        if (scaleCoroutine != null)
        {
            StopCoroutine(scaleCoroutine);
        }
        scaleCoroutine = StartCoroutine(ClickAnimation());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPointerDown = false;
        UpdateTargetTransform();

        // Start over-time animations to return to hover or normal state if enabled
        if (useScaleOverTime)
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleOverTime(targetScale, scaleDuration));
        }

        if (useRotationOverTime && useRotationOnHover)
        {
            if (rotationCoroutine != null) StopCoroutine(rotationCoroutine);
            rotationCoroutine = StartCoroutine(RotateOverTime(targetRotation, rotationDuration));
        }

        if (useOffsetOverTime && usePositionOffset)
        {
            if (offsetCoroutine != null) StopCoroutine(offsetCoroutine);
            offsetCoroutine = StartCoroutine(OffsetOverTime(targetPosition, offsetDuration));
        }
    }

    private void UpdateTargetTransform()
    {
        // Update scale target
        if (isPointerDown)
        {
            targetScale = originalScale * clickScaleAmount;
        }
        else if (isPointerOver)
        {
            targetScale = originalScale * hoverScaleAmount;
        }
        else
        {
            targetScale = originalScale;
        }

        // Update rotation target
        if (useRotationOnHover && isPointerOver && !isPointerDown)
        {
            // Create a rotated quaternion around Z axis
            targetRotation = originalRotation * Quaternion.Euler(0, 0, maxRotationAngle);
        }
        else
        {
            targetRotation = originalRotation;
        }

        // Update position target
        if (usePositionOffset && isPointerOver && !isPointerDown)
        {
            targetPosition = originalPosition + new Vector3(xOffset, yOffset, zOffset);
        }
        else
        {
            targetPosition = originalPosition;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }

    private IEnumerator ClickAnimation()
    {
        // Quick push in
        targetScale = originalScale * clickScaleAmount;
        yield return new WaitForSeconds(0.1f);

        // If still clicked, keep pushed, otherwise pop back out
        if (isPointerDown)
        {
            targetScale = originalScale * clickScaleAmount;
        }
        else if (isPointerOver)
        {
            targetScale = originalScale * hoverScaleAmount;
        }
        else
        {
            targetScale = originalScale;
        }
    }

    private IEnumerator ScaleOverTime(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            // Use smoothstep for more pleasing animation
            float smoothT = t * t * (3f - 2f * t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);

            yield return null;
        }

        transform.localScale = targetScale;
    }

    private IEnumerator RotateOverTime(Quaternion targetRotation, float duration)
    {
        Quaternion startRotation = transform.localRotation;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            // Use smoothstep for more pleasing animation
            float smoothT = t * t * (3f - 2f * t);
            transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);

            yield return null;
        }

        transform.localRotation = targetRotation;
    }

    private IEnumerator OffsetOverTime(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = rectTransform.anchoredPosition3D;
        float time = 0;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);

            // Use smoothstep for more pleasing animation
            float smoothT = t * t * (3f - 2f * t);
            rectTransform.anchoredPosition3D = Vector3.Lerp(startPosition, targetPosition, smoothT);

            yield return null;
        }

        rectTransform.anchoredPosition3D = targetPosition;
    }
}