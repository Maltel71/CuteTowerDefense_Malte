using System.Collections;
using UnityEngine;
using TMPro;

public class WaveCounter : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI currentWaveText;  // Reference to text showing current wave
    public TextMeshProUGUI newWaveText;      // Reference to text that fades in/out

    [Header("Animation Settings")]
    public float fadeInDuration = 0.5f;       // Duration of fade in animation
    public float displayDuration = 1.5f;      // How long to show the text
    public float fadeOutDuration = 0.5f;      // Duration of fade out animation

    [Header("Text Settings")]
    public string currentWavePrefix = "Wave: ";
    public string newWavePrefix = "Wave ";
    public string newWaveSuffix = " Starting!";

    private SpawnManager spawnManager;        // Reference to the SpawnManager
    private int lastWaveNumber = 0;           // Track the last wave number
    private Coroutine animationCoroutine;     // Reference to running animation

    private void Start()
    {
        // Find the SpawnManager
        spawnManager = FindObjectOfType<SpawnManager>();
        if (spawnManager == null)
        {
            Debug.LogError("SpawnManager not found! WaveCounter will not function.");
        }

        // Set initial transparency for the "New Wave" text (invisible)
        if (newWaveText != null)
        {
            Color textColor = newWaveText.color;
            textColor.a = 0f;
            newWaveText.color = textColor;
            newWaveText.gameObject.SetActive(true);
        }

        // Update the current wave text with initial value
        UpdateCurrentWaveText(0);
    }

    private void Update()
    {
        if (spawnManager == null) return;

        // Check if the wave number has changed
        int currentWave = spawnManager.currentWave;

        if (currentWave != lastWaveNumber)
        {
            // Wave has changed
            OnWaveChanged(currentWave);
            lastWaveNumber = currentWave;
        }

        // Always update the current wave text (in case it's modified elsewhere)
        UpdateCurrentWaveText(currentWave);
    }

    private void OnWaveChanged(int newWaveNumber)
    {
        // Update current wave text
        UpdateCurrentWaveText(newWaveNumber);

        // Show the "New Wave" text animation
        if (newWaveText != null)
        {
            // Stop any existing animation
            if (animationCoroutine != null)
            {
                StopCoroutine(animationCoroutine);
            }

            // Start a new animation
            animationCoroutine = StartCoroutine(AnimateNewWaveText(newWaveNumber));
        }
    }

    private void UpdateCurrentWaveText(int waveNumber)
    {
        if (currentWaveText != null)
        {
            currentWaveText.text = currentWavePrefix + waveNumber;
        }
    }

    private IEnumerator AnimateNewWaveText(int waveNumber)
    {
        // Set the text
        newWaveText.text = newWavePrefix + waveNumber + newWaveSuffix;

        // Get the initial color
        Color textColor = newWaveText.color;

        // Fade in
        float timer = 0f;
        while (timer < fadeInDuration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / fadeInDuration;
            textColor.a = Mathf.Lerp(0f, 1f, normalizedTime);
            newWaveText.color = textColor;
            yield return null;
        }

        // Ensure fully visible
        textColor.a = 1f;
        newWaveText.color = textColor;

        // Display duration
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        timer = 0f;
        while (timer < fadeOutDuration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / fadeOutDuration;
            textColor.a = Mathf.Lerp(1f, 0f, normalizedTime);
            newWaveText.color = textColor;
            yield return null;
        }

        // Ensure fully invisible
        textColor.a = 0f;
        newWaveText.color = textColor;
    }
}