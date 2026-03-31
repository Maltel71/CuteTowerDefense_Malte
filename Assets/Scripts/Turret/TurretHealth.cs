using UnityEngine;

public class TurretHealth : MonoBehaviour
{
    public int maxHealth = 50;
    [SerializeField] private int currentHealth;
    public AudioClip[] damageSounds;
    public ParticleSystem damageParticles;
    public Transform particleSpawnPoint;
    public float pitchMin = 0.9f;
    public float pitchMax = 1.1f;

    [Header("Upgrade Settings")]
    [Tooltip("How much health increases per level (multiplier)")]
    public float healthPerLevelMultiplier = 1.5f;

    private AudioSource audioSource;
    private TurretController turretController; // Reference to the controller

    void Start()
    {
        // Get reference to TurretController
        turretController = GetComponent<TurretController>();

        // Adjust max health based on turret level if applicable
        if (turretController != null && turretController.turretLevel > 1)
        {
            // Calculate level-based health bonus
            int level = turretController.turretLevel;
            float healthMultiplier = 1.0f;

            // Apply a multiplier for each level beyond 1
            for (int i = 1; i < level; i++)
            {
                healthMultiplier *= healthPerLevelMultiplier;
            }

            // Apply the multiplier to max health
            maxHealth = Mathf.RoundToInt(maxHealth * healthMultiplier);
            Debug.Log($"{gameObject.name}: Adjusted max health for level {level} to {maxHealth}");
        }

        // Initialize health
        currentHealth = maxHealth;

        // Set up audio source if not already present
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.playOnAwake = false;
        }

        // Ensure this object has the correct tag
        if (!gameObject.CompareTag("Turret"))
        {
            gameObject.tag = "Turret";
            Debug.Log($"{gameObject.name}: Set tag to Turret for wildlife targeting");
        }

        // Log startup info
        Debug.Log($"{gameObject.name} TurretHealth initialized: {currentHealth}/{maxHealth}");

        // Check if particle spawn point is assigned
        if (particleSpawnPoint == null)
        {
            // Try to find a point named DamageParticlePoint in children
            Transform damagePoint = transform.Find("DamageParticlePoint");
            if (damagePoint != null)
            {
                particleSpawnPoint = damagePoint;
                Debug.Log($"{gameObject.name}: Found DamageParticlePoint in children");
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: No particle spawn point assigned or found");
            }
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0); // Ensure health doesn't go below 0

        Debug.Log($"{gameObject.name} took {damage} damage. Health now: {currentHealth}/{maxHealth}");

        PlayRandomDamageSound();
        SpawnDamageParticles();

        if (currentHealth <= 0)
        {
            DestroyTurret();
        }
    }

    // Set health directly (for use when upgrading turrets)
    public void SetHealth(int newHealth)
    {
        currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);
        Debug.Log($"{gameObject.name}: Health set directly to {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            DestroyTurret();
        }
    }

    // Set health as a percentage of max health
    public void SetHealthPercentage(float percentage)
    {
        int newHealth = Mathf.RoundToInt(maxHealth * Mathf.Clamp01(percentage));
        SetHealth(newHealth);
    }

    void SpawnDamageParticles()
    {
        if (damageParticles != null)
        {
            // Use particle spawn point if available, otherwise use transform position
            Vector3 spawnPosition = particleSpawnPoint != null ?
                                    particleSpawnPoint.position :
                                    transform.position;

            Quaternion spawnRotation = particleSpawnPoint != null ?
                                      particleSpawnPoint.rotation :
                                      Quaternion.identity;

            ParticleSystem particles = Instantiate(damageParticles, spawnPosition, spawnRotation);

            // Ensure the particle system is playing
            if (!particles.isPlaying)
            {
                particles.Play();
            }

            // Get duration of the particle system for proper cleanup
            float duration = particles.main.duration;
            if (particles.main.loop)
            {
                duration = 2.0f; // Default duration for looping particles
            }

            // Add additional time for any lingering particles
            float totalLifetime = duration + particles.main.startLifetime.constantMax;

            Debug.Log($"{gameObject.name}: Damage particles playing at {spawnPosition}, duration: {totalLifetime}s");

            // Destroy after the particle effect completes
            Destroy(particles.gameObject, totalLifetime);
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No damage particles assigned");
        }
    }

    void PlayRandomDamageSound()
    {
        if (damageSounds != null && damageSounds.Length > 0 && audioSource != null)
        {
            int randomIndex = Random.Range(0, damageSounds.Length);
            audioSource.pitch = Random.Range(pitchMin, pitchMax); // Random pitch shift within the specified range
            audioSource.PlayOneShot(damageSounds[randomIndex]);
            Debug.Log($"{gameObject.name}: Playing damage sound {randomIndex}");
        }
        else
        {
            Debug.LogWarning($"{gameObject.name}: No damage sounds assigned or audio source missing");
        }
    }

    void DestroyTurret()
    {
        // Notify nearby wildlife to find new targets
        NotifyWildlifeOfDestruction();

        // Play a final damage effect before destruction
        SpawnDamageParticles();

        Debug.Log($"{gameObject.name}: Turret destroyed!");

        // Destroy the turret after a slight delay to allow final effects to play
        Destroy(gameObject, 0.2f);
    }

    void NotifyWildlifeOfDestruction()
    {
        // Find all wildlife within a larger radius to ensure we catch all attackers
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, 30f);
        int notifiedCount = 0;

        foreach (Collider collider in nearbyColliders)
        {
            Wildlife wildlife = collider.GetComponent<Wildlife>();
            if (wildlife != null)
            {
                // Force wildlife to clear its current target and find a new one
                wildlife.FindNewTarget();
                notifiedCount++;

                // Log for debugging
                Debug.Log($"{gameObject.name}: Notified wildlife {wildlife.gameObject.name} to find new target");
            }
        }

        Debug.Log($"{gameObject.name}: Notified {notifiedCount} wildlife creatures about destruction");
    }

    public int GetCurrentHealth()
    {
        return currentHealth;
    }

    // Get the health as a percentage (useful for upgrading)
    public float GetHealthPercentage()
    {
        return (float)currentHealth / maxHealth;
    }
}