using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretController : MonoBehaviour
{
    [Header("Turret Components")]
    public Transform turretBase;
    public Transform turretHead;
    public Transform bulletSpawnPoint;

    [Header("Turret Settings")]
    public float range = 10f;
    public float fireRate = 1f;
    public int damage = 2;
    public LayerMask targetLayers; // Layer mask for wildlife detection
    public int turretLevel = 1;    // Tracks the turret's upgrade level (now supports 1-6)

    [Header("Bullet Settings")]
    public GameObject bulletPrefab;
    public float bulletSpeed = 20f;

    [Header("Effects")]
    public ParticleSystem muzzleFlash;
    public Transform muzzleFlashParticlePoint; // Reference for muzzle flash position
    public AudioClip[] shootSounds;
    public float soundVolume = 0.5f;
    public float pitchMin = 0.9f;
    public float pitchMax = 1.1f;

    [Header("Health Settings")]
    public int maxHealth = 50;
    public ParticleSystem damageParticlesPrefab;
    public Transform damageParticlePoint;
    public AudioClip[] damageSoundClips;

    [Header("Health Bar")]
    public GameObject healthBarPrefab;

    [Header("Debug")]
    public bool debugMode = true;

    private float nextFireTime;
    private Transform currentTarget;
    private AudioSource audioSource;
    private TurretHealth healthComponent;

    // Variables for level-based scaling factors
    private float levelDamageMultiplier = 1.0f;
    private float levelFireRateMultiplier = 1.0f;
    private float levelRangeMultiplier = 1.0f;
    private float levelHealthMultiplier = 1.0f;

    // Helper method to convert LayerMask to readable string of layer names
    private string GetLayerMaskAsString(LayerMask layerMask)
    {
        string result = "";
        for (int i = 0; i < 32; i++)
        {
            if ((layerMask & (1 << i)) != 0)
            {
                result += (result.Length > 0 ? ", " : "") + LayerMask.LayerToName(i);
            }
        }
        return result;
    }

    void Start()
    {
        // Set the tag to "Turret" to ensure wildlife can target it
        gameObject.tag = "Turret";

        // Make sure we have a collider with trigger for detecting wildlife attacks
        Collider existingCollider = GetComponent<Collider>();
        if (existingCollider == null)
        {
            // Add a spherical collider if none exists
            SphereCollider newCollider = gameObject.AddComponent<SphereCollider>();
            newCollider.radius = 1.5f;
            newCollider.isTrigger = true;
            Debug.Log($"{gameObject.name}: Added SphereCollider for wildlife targeting");
        }
        else if (!existingCollider.isTrigger)
        {
            // If the main collider is not a trigger, check if we already have a trigger collider
            bool hasTriggerCollider = false;
            Collider[] allColliders = GetComponents<Collider>();
            foreach (Collider col in allColliders)
            {
                if (col.isTrigger)
                {
                    hasTriggerCollider = true;
                    break;
                }
            }

            // If no trigger collider found, add one
            if (!hasTriggerCollider)
            {
                SphereCollider triggerCollider = gameObject.AddComponent<SphereCollider>();
                triggerCollider.radius = 2.0f;
                triggerCollider.isTrigger = true;
                Debug.Log($"{gameObject.name}: Added trigger SphereCollider for wildlife targeting");
            }
        }

        // Add an AudioSource component if not already present
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.playOnAwake = false;
            audioSource.volume = soundVolume;
        }

        // Set up health component
        SetupHealthComponent();

        // Add health bar
        AddHealthBar();

        // Start scanning for targets
        StartCoroutine(ScanForTargetsRoutine());

        if (debugMode)
        {
            Debug.Log($"{gameObject.name}: TurretController started, targeting layers: {GetLayerMaskAsString(targetLayers)}");
        }

        // Apply turret level settings
        ApplyLevelSettings();
    }

    // Apply settings based on turret level
    void ApplyLevelSettings()
    {
        // Calculate multipliers based on turret level (starts at level 1)
        // Each level increases damage, fire rate, range and health

        // Calculate the multiplier for each stat based on level
        levelDamageMultiplier = 1.0f + (turretLevel - 1) * 0.25f;      // +25% damage per level
        levelFireRateMultiplier = 1.0f + (turretLevel - 1) * 0.2f;     // +20% fire rate per level
        levelRangeMultiplier = 1.0f + (turretLevel - 1) * 0.15f;       // +15% range per level
        levelHealthMultiplier = 1.0f + (turretLevel - 1) * 0.5f;       // +50% health per level

        // Apply multipliers to base stats
        int scaledDamage = Mathf.RoundToInt(damage * levelDamageMultiplier);
        float scaledFireRate = fireRate * levelFireRateMultiplier;
        float scaledRange = range * levelRangeMultiplier;

        // Apply scaled values
        damage = scaledDamage;
        fireRate = scaledFireRate;
        range = scaledRange;

        // Apply health multiplier if health component exists
        if (healthComponent != null)
        {
            int scaledMaxHealth = Mathf.RoundToInt(maxHealth * levelHealthMultiplier);
            healthComponent.maxHealth = scaledMaxHealth;

            if (debugMode)
            {
                Debug.Log($"{gameObject.name}: Applied Level {turretLevel} settings - " +
                          $"Damage: {damage} (+{(levelDamageMultiplier - 1) * 100:0}%), " +
                          $"Fire Rate: {fireRate:F2} (+{(levelFireRateMultiplier - 1) * 100:0}%), " +
                          $"Range: {range:F1} (+{(levelRangeMultiplier - 1) * 100:0}%), " +
                          $"Health: {scaledMaxHealth} (+{(levelHealthMultiplier - 1) * 100:0}%)");
            }
        }

        // Visual enhancements based on level
        ApplyVisualUpgrades();
    }

    // Apply visual upgrades based on the turret's level
    void ApplyVisualUpgrades()
    {
        // Find renderers to modify
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        // Different visual enhancements based on level - ONLY SIZE CHANGES, NO COLORS
        switch (turretLevel)
        {
            case 1:
                // Base level - no changes
                break;

            case 2:
                // Level 2 - slightly larger
                transform.localScale = Vector3.one * 1.1f;
                break;

            case 3:
                // Level 3 - larger
                transform.localScale = Vector3.one * 1.15f;
                break;

            case 4:
                // Level 4 - even larger
                transform.localScale = Vector3.one * 1.2f;
                break;

            case 5:
                // Level 5 - larger
                transform.localScale = Vector3.one * 1.25f;
                break;

            case 6:
                // Level 6 - maximum level
                transform.localScale = Vector3.one * 1.3f;
                break;
        }
    }

    // Helper method to tint all renderers
    void TintRenderers(Renderer[] renderers, Color tint)
    {
        foreach (Renderer renderer in renderers)
        {
            // Create unique material instances to avoid affecting other turrets
            Material[] uniqueMaterials = new Material[renderer.materials.Length];

            for (int i = 0; i < renderer.materials.Length; i++)
            {
                Material originalMat = renderer.materials[i];
                uniqueMaterials[i] = new Material(originalMat);

                // Apply tint if the material has a color property
                if (uniqueMaterials[i].HasProperty("_Color"))
                {
                    Color originalColor = uniqueMaterials[i].color;
                    uniqueMaterials[i].color = originalColor * tint;
                }
            }

            // Assign the tinted materials back to the renderer
            renderer.materials = uniqueMaterials;
        }
    }

    void SetupHealthComponent()
    {
        // Add TurretHealth component if not already present
        healthComponent = GetComponent<TurretHealth>();
        if (healthComponent == null)
        {
            healthComponent = gameObject.AddComponent<TurretHealth>();

            // Configure health component
            healthComponent.maxHealth = maxHealth;
            healthComponent.damageParticles = damageParticlesPrefab;
            healthComponent.damageSounds = damageSoundClips;

            // Set particle spawn point to damage particle point if available
            if (damageParticlePoint != null)
            {
                healthComponent.particleSpawnPoint = damageParticlePoint;
                if (debugMode) Debug.Log($"{gameObject.name}: Set damage particle spawn point");
            }
            else if (bulletSpawnPoint != null)
            {
                healthComponent.particleSpawnPoint = bulletSpawnPoint;
                if (debugMode) Debug.Log($"{gameObject.name}: Using bullet spawn point for damage particles");
            }
        }
    }

    void AddHealthBar()
    {
        // Add TurretHealthBar component if not already present
        TurretHealthBar healthBar = GetComponent<TurretHealthBar>();
        if (healthBar == null)
        {
            healthBar = gameObject.AddComponent<TurretHealthBar>();

            // Try to find the health bar prefab
            if (healthBarPrefab != null)
            {
                // Use the directly assigned prefab
                healthBar.healthBarPrefab = healthBarPrefab;
            }
            else
            {
                // Try to find an existing health bar prefab on another turret
                TurretHealthBar[] existingHealthBars = FindObjectsOfType<TurretHealthBar>();
                foreach (TurretHealthBar existingBar in existingHealthBars)
                {
                    if (existingBar != healthBar && existingBar.healthBarPrefab != null)
                    {
                        healthBar.healthBarPrefab = existingBar.healthBarPrefab;
                        if (debugMode)
                            Debug.Log("Found health bar prefab from existing turret");
                        break;
                    }
                }

                // If still not found, try to find by name in scene
                if (healthBar.healthBarPrefab == null)
                {
                    GameObject[] possiblePrefabs = {
                        GameObject.Find("HealthBarPrefab"),
                        GameObject.Find("HealthBarPrefabTurret"),
                        GameObject.Find("TurretHealthBarPrefab")
                    };

                    foreach (GameObject prefab in possiblePrefabs)
                    {
                        if (prefab != null)
                        {
                            healthBar.healthBarPrefab = prefab;
                            if (debugMode)
                                Debug.Log($"Found health bar prefab: {prefab.name}");
                            break;
                        }
                    }
                }
            }

            // Find the HealthUI canvas
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas.CompareTag("HealthUI"))
                {
                    healthBar.targetCanvas = canvas;
                    if (debugMode)
                        Debug.Log("Found HealthUI canvas");
                    break;
                }
            }

            if (healthBar.healthBarPrefab == null)
            {
                Debug.LogError("Could not find health bar prefab! Health bar will not be displayed.");
            }
        }
    }

    IEnumerator ScanForTargetsRoutine()
    {
        while (true)
        {
            // Only scan if we don't have a target
            if (currentTarget == null)
            {
                FindClosestTarget();
            }

            // Wait a bit before scanning again to save performance
            yield return new WaitForSeconds(0.5f);
        }
    }

    void FindClosestTarget()
    {
        // Use OverlapSphere to find all colliders in range that match the target layers
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, range, targetLayers);

        if (debugMode)
            Debug.Log($"{gameObject.name}: Scanning for targets. Found {hitColliders.Length} objects in range on layers: {GetLayerMaskAsString(targetLayers)}");

        float closestDistance = Mathf.Infinity;
        Transform closestTarget = null;

        foreach (Collider collider in hitColliders)
        {
            // Verify the object has a Wildlife component
            Wildlife wildlife = collider.GetComponent<Wildlife>();
            if (wildlife != null)
            {
                float distance = Vector3.Distance(transform.position, collider.transform.position);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = collider.transform;

                    if (debugMode)
                        Debug.Log($"{gameObject.name}: Found potential target: {collider.name} at distance {distance:F1}");
                }
            }
        }

        if (closestTarget != null)
        {
            if (debugMode)
                Debug.Log($"{gameObject.name}: Targeting {closestTarget.name} at distance {closestDistance:F1}");

            currentTarget = closestTarget;
        }
        else if (debugMode)
        {
            Debug.Log($"{gameObject.name}: No valid targets found in range");
        }
    }

    void Update()
    {
        if (currentTarget != null)
        {
            // Check if target is still in range
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

            if (distanceToTarget > range)
            {
                if (debugMode)
                    Debug.Log($"{gameObject.name}: Target went out of range");

                currentTarget = null;
                return;
            }

            // Rotate turret head towards target
            RotateTurretHead();

            // Fire if ready
            if (Time.time >= nextFireTime)
            {
                Fire();
                nextFireTime = Time.time + (1f / fireRate);
            }
        }
    }

    void RotateTurretHead()
    {
        if (turretHead != null && currentTarget != null)
        {
            Vector3 targetPosition = currentTarget.position;
            targetPosition.y = turretHead.position.y; // Keep rotation level horizontally

            Vector3 direction = targetPosition - turretHead.position;
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // Rotation speed can be faster for upgraded turrets
            float rotationSpeed = 5f;
            if (turretLevel > 1)
            {
                rotationSpeed = 7f; // Level 2 turrets rotate faster
            }

            // Smoothly rotate the turret head
            turretHead.rotation = Quaternion.Slerp(turretHead.rotation, targetRotation, Time.deltaTime * rotationSpeed);

            if (debugMode && Vector3.Angle(turretHead.forward, direction) < 5f)
                Debug.Log($"{gameObject.name}: Aimed at target");
        }
    }

    void Fire()
    {
        if (bulletPrefab != null && bulletSpawnPoint != null && currentTarget != null)
        {
            if (debugMode)
                Debug.Log($"{gameObject.name}: Firing at {currentTarget.name}");

            // Instantiate bullet
            GameObject bulletObj = Instantiate(bulletPrefab, bulletSpawnPoint.position, bulletSpawnPoint.rotation);

            // Try TurretBullet first, then regular Bullet
            TurretBullet turretBullet = bulletObj.GetComponent<TurretBullet>();
            if (turretBullet != null)
            {
                turretBullet.damage = damage;
                turretBullet.speed = bulletSpeed;

                // Visual enhancement for upgraded turrets' bullets
                if (turretLevel > 1)
                {
                    // Scale up the bullet slightly for level 2 turrets
                    bulletObj.transform.localScale *= 1.2f;

                    // Assuming your bullet has a renderer, we can change its color
                    Renderer bulletRenderer = bulletObj.GetComponent<Renderer>();
                    if (bulletRenderer != null && bulletRenderer.material != null)
                    {
                        // Give level 2 bullets a slightly different color (e.g., more orange/red)
                        bulletRenderer.material.color = new Color(1f, 0.7f, 0.3f);
                    }
                }
            }
            else
            {
                // If no TurretBullet, try regular Bullet
                Bullet bullet = bulletObj.GetComponent<Bullet>();
                if (bullet != null)
                {
                    bullet.damage = damage;
                    bullet.speed = bulletSpeed;

                    // Apply level-based visual changes here too if needed
                    if (turretLevel > 1)
                    {
                        bulletObj.transform.localScale *= 1.2f;
                    }
                }
                else
                {
                    // If no Bullet component, add velocity directly
                    Rigidbody rb = bulletObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = bulletSpawnPoint.forward * bulletSpeed;
                    }
                }
            }

            // Play effects
            PlayMuzzleFlash();
            PlayShootSound();

            // Destroy bullet after 3 seconds if it hasn't hit anything
            Destroy(bulletObj, 3f);
        }
    }

    void PlayMuzzleFlash()
    {
        if (muzzleFlash != null)
        {
            if (muzzleFlashParticlePoint != null && !muzzleFlash.transform.IsChildOf(muzzleFlashParticlePoint))
            {
                // If muzzle flash isn't already a child of the particle point, instantiate it there
                ParticleSystem newFlash = Instantiate(muzzleFlash, muzzleFlashParticlePoint.position, muzzleFlashParticlePoint.rotation, muzzleFlashParticlePoint);
                newFlash.Play();

                // Destroy after playing
                float duration = newFlash.main.duration + newFlash.main.startLifetime.constantMax;
                Destroy(newFlash.gameObject, duration);

                if (debugMode)
                    Debug.Log($"{gameObject.name}: Instantiated and played muzzle flash at particle point");
            }
            else
            {
                // If muzzleFlash is a direct reference to a component, just play it
                muzzleFlash.Play();

                if (debugMode)
                    Debug.Log($"{gameObject.name}: Played muzzle flash");
            }
        }
        else if (debugMode)
        {
            Debug.LogWarning($"{gameObject.name}: No muzzle flash particle system assigned");
        }
    }

    void PlayShootSound()
    {
        if (audioSource != null && shootSounds != null && shootSounds.Length > 0)
        {
            // Randomly choose a sound from the array
            int soundIndex = Random.Range(0, shootSounds.Length);
            AudioClip selectedSound = shootSounds[soundIndex];

            // Apply random pitch variation
            audioSource.pitch = Random.Range(pitchMin, pitchMax);

            // Play the sound
            audioSource.PlayOneShot(selectedSound);

            if (debugMode)
                Debug.Log($"{gameObject.name}: Playing shoot sound {soundIndex} with pitch {audioSource.pitch:F2}");
        }
        else if (debugMode)
        {
            Debug.LogWarning($"{gameObject.name}: No shoot sounds assigned or audio source missing");
        }
    }

    // Visualize the range in editor
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);

        if (bulletSpawnPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(bulletSpawnPoint.position, bulletSpawnPoint.forward * 2f);
        }

        // Draw muzzle flash point
        if (muzzleFlashParticlePoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(muzzleFlashParticlePoint.position, 0.1f);
            Gizmos.DrawRay(muzzleFlashParticlePoint.position, muzzleFlashParticlePoint.forward * 1f);
        }

        // Draw damage particle point
        if (damageParticlePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(damageParticlePoint.position, 0.1f);
        }
    }
}