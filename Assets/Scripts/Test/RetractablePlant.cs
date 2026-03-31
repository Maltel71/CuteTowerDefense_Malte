using UnityEngine;
using System.Collections;

public class RetractablePlant : MonoBehaviour
{
    [Header("Retraction Settings")]
    [Tooltip("How much the object shrinks when triggered (0.5 = half size)")]
    [Range(0.01f, 0.99f)]
    public float shrinkScale = 0.3f;

    [Tooltip("How fast the object shrinks")]
    public float shrinkSpeed = 3f;

    [Tooltip("How fast the object grows back")]
    public float expandSpeed = 1.5f;

    [Tooltip("How long to stay shrunken before growing back after entities leave")]
    public float stayRetractedTime = 1.5f;

    [Header("Detection Settings")]
    [Tooltip("Radius in which to detect players and wildlife")]
    public float detectionRadius = 3f;

    [Tooltip("How often to check for nearby entities (seconds)")]
    public float checkInterval = 0.1f;

    [Tooltip("Visualization color for detection radius")]
    public Color detectionColor = new Color(0.5f, 1f, 0.5f, 0.3f);

    [Header("Tags to React To")]
    public bool reactToPlayer = true;
    public bool reactToWildlife = true;

    [Header("Sound Effects")]
    public AudioClip retractSound;
    public AudioClip expandSound;
    [Range(0f, 1f)]
    public float soundVolume = 0.5f;
    [Range(0.5f, 1.5f)]
    public float pitchVariation = 0.1f;

    [Header("Advanced Settings")]
    [Tooltip("The point the object shrinks towards (if null, uses object pivot)")]
    public Transform pivotPoint;
    [Tooltip("Only shrink specific parts of the plant")]
    public Transform[] shrinkingParts;
    [Tooltip("Use this layermask for entity detection")]
    public LayerMask detectionLayers = -1; // Default to everything

    // Private variables
    private Vector3[] originalScales;
    private Vector3[] targetScales;
    private Transform[] partsToShrink;
    private Vector3 originalPosition; // Store the original position
    private bool isRetracted = false;
    private bool isAnimating = false;
    private float nextCheckTime = 0f;
    private AudioSource audioSource;
    private Coroutine expandRoutine;
    private int entitiesNearby = 0;
    private bool wasEntityDetected = false;

    private void Start()
    {
        // Store original position
        originalPosition = transform.position;

        // Set up audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && (retractSound != null || expandSound != null))
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.playOnAwake = false;
        }

        // Determine which parts to shrink
        if (shrinkingParts != null && shrinkingParts.Length > 0)
        {
            partsToShrink = shrinkingParts;
        }
        else
        {
            // Use the main object if no specific parts defined
            partsToShrink = new Transform[] { transform };
        }

        // Store original scales
        originalScales = new Vector3[partsToShrink.Length];
        targetScales = new Vector3[partsToShrink.Length];

        for (int i = 0; i < partsToShrink.Length; i++)
        {
            originalScales[i] = partsToShrink[i].localScale;
            targetScales[i] = originalScales[i] * shrinkScale;
        }

        // If no pivot point is set, create one at the bottom of the object
        if (pivotPoint == null)
        {
            // Create pivot at the bottom center of the object based on collider/renderer
            Collider col = GetComponent<Collider>();
            Renderer rend = GetComponent<Renderer>();

            Vector3 bottomPos;
            if (col != null)
            {
                bottomPos = new Vector3(0, col.bounds.min.y - transform.position.y, 0);
            }
            else if (rend != null)
            {
                bottomPos = new Vector3(0, rend.bounds.min.y - transform.position.y, 0);
            }
            else
            {
                bottomPos = Vector3.zero; // Default to object pivot
            }

            // Create empty pivot object
            GameObject pivotObj = new GameObject(gameObject.name + "_Pivot");
            pivotObj.transform.parent = transform;
            pivotObj.transform.localPosition = bottomPos;
            pivotPoint = pivotObj.transform;

            Debug.Log($"Created pivot point at {bottomPos} for {gameObject.name}");
        }
    }

    private void Update()
    {
        // Only check periodically to save performance
        if (Time.time >= nextCheckTime)
        {
            CheckForEntities();
            nextCheckTime = Time.time + checkInterval;
        }
    }

    private void CheckForEntities()
    {
        // Reset entity count
        entitiesNearby = 0;

        // Get all colliders in radius
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, detectionLayers);

        // Count relevant entities
        foreach (Collider collider in hitColliders)
        {
            // Skip self
            if (collider.transform == transform || collider.transform.IsChildOf(transform))
                continue;

            // Check tags
            if ((reactToPlayer && collider.CompareTag("Player")) ||
                (reactToWildlife && collider.CompareTag("Wildlife")))
            {
                entitiesNearby++;
            }
        }

        // Update plant state based on entity presence
        bool isEntityDetected = entitiesNearby > 0;

        // Only take action if detection state has changed
        if (isEntityDetected != wasEntityDetected)
        {
            wasEntityDetected = isEntityDetected;

            if (isEntityDetected)
            {
                Retract();
            }
            else
            {
                // Start expand routine with delay
                if (expandRoutine != null)
                {
                    StopCoroutine(expandRoutine);
                }
                expandRoutine = StartCoroutine(ExpandAfterDelay(stayRetractedTime));
            }
        }
    }

    private void Retract()
    {
        // Cancel any expand routine
        if (expandRoutine != null)
        {
            StopCoroutine(expandRoutine);
            expandRoutine = null;
        }

        // Only start retracting if not already retracted
        if (!isRetracted)
        {
            StartCoroutine(AnimateScale(true));
        }
    }

    private IEnumerator ExpandAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Double-check that no entities are nearby (in case something entered during the delay)
        CheckForEntities();
        if (entitiesNearby <= 0)
        {
            StartCoroutine(AnimateScale(false));
        }
    }

    private IEnumerator AnimateScale(bool shrinking)
    {
        // Prevent multiple animation coroutines
        if (isAnimating)
        {
            yield break;
        }

        isAnimating = true;

        // Play sound
        PlaySound(shrinking ? retractSound : expandSound);

        // Calculate start and target scales for each part
        Vector3[] startScales = new Vector3[partsToShrink.Length];
        Vector3[] endScales = new Vector3[partsToShrink.Length];

        for (int i = 0; i < partsToShrink.Length; i++)
        {
            startScales[i] = partsToShrink[i].localScale;
            endScales[i] = shrinking ? targetScales[i] : originalScales[i];
        }

        // Get animation speed based on direction
        float speed = shrinking ? shrinkSpeed : expandSpeed;

        // Animation progress from 0 to 1
        float progress = 0f;

        while (progress < 1f)
        {
            // Update progress
            progress += Time.deltaTime * speed;
            float t = Mathf.Clamp01(progress);

            // Apply scale to each part
            for (int i = 0; i < partsToShrink.Length; i++)
            {
                if (partsToShrink[i] != null)
                {
                    // Use a smooth step for a more natural movement
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    partsToShrink[i].localScale = Vector3.Lerp(startScales[i], endScales[i], smoothT);
                }
            }

            yield return null;
        }

        // Restore original position when done expanding
        if (!shrinking && transform.position != originalPosition)
        {
            transform.position = originalPosition;
        }

        isRetracted = shrinking;
        isAnimating = false;
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            // Add some pitch variation for natural sound
            audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
            audioSource.PlayOneShot(clip, soundVolume);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Visualize the detection radius
        Gizmos.color = detectionColor;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Visualize the pivot point
        if (pivotPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(pivotPoint.position, 0.1f);
            Gizmos.DrawLine(transform.position, pivotPoint.position);
        }

        // Visualize original and target scales
        if (partsToShrink != null && Application.isPlaying)
        {
            for (int i = 0; i < partsToShrink.Length; i++)
            {
                if (partsToShrink[i] != null)
                {
                    // Draw box showing target scale
                    Gizmos.color = new Color(1f, 0.5f, 0.5f, 0.3f);
                    Vector3 targetSize = Vector3.Scale(
                        partsToShrink[i].lossyScale / partsToShrink[i].localScale.magnitude,
                        targetScales[i]
                    );
                    Gizmos.DrawWireCube(partsToShrink[i].position, targetSize);
                }
            }
        }
    }
}