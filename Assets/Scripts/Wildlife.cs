using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

public class Wildlife : MonoBehaviour
{
    public float speed = 2f;
    public int damage = 10;

    // Modified: Make maxHealth public and separate from current health
    public int maxHealth = 2;
    private int currentHealth;

    // New: Points awarded when this wildlife is killed
    public int pointValue = 5;

    // Target settings
    public float targetSearchRadius = 20f;
    public float retargetInterval = 10f;
    public LayerMask targetLayers; // Layer mask for targets (Tower, Turrets, and Player)

    public AudioClip animalSound; // Sound to play while the animal is alive
    public float pitchMin = 0.8f; // Minimum pitch shift
    public float pitchMax = 1.2f; // Maximum pitch shift

    // NavMesh properties
    private NavMeshAgent navAgent;
    public float stoppingDistance = 1.5f; // How close the animal gets before stopping

    // NEW: Knockback settings
    [Header("Knockback Settings")]
    public float knockbackResistance = 1.0f; // Higher values = less knockback (1.0 = normal)
    public float knockbackDuration = 0.3f;   // How long the knockback lasts
    public float recoverySpeed = 2.0f;       // How quickly the wildlife recovers after knockback
    private bool isKnockedBack = false;
    private float knockbackEndTime;
    private Vector3 knockbackDirection;
    private float knockbackForce;

    private Transform currentTarget;
    private TowerHealth towerHealth;
    private TurretHealth turretHealth;
    private PlayerHealth playerHealth; // New reference for player's health
    private Rigidbody rb;
    private AudioSource audioSource;
    private float nextRetargetTime;
    // Changed from private to public so WildlifePositioning can access it
    public bool isAttackingTarget = false;

    // Public property to access current health
    public int Health
    {
        get { return currentHealth; }
    }

    void Start()
    {
        // Initialize current health to max health
        currentHealth = maxHealth;

        // Set up NavMeshAgent
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            // Add NavMeshAgent if not already present
            navAgent = gameObject.AddComponent<NavMeshAgent>();
            navAgent.speed = speed;
            navAgent.stoppingDistance = stoppingDistance;
            navAgent.acceleration = 8f;
            navAgent.angularSpeed = 120f;
            // Set appropriate radius based on the animal size
            navAgent.radius = GetComponent<Collider>()?.bounds.extents.x ?? 0.5f;
            navAgent.height = GetComponent<Collider>()?.bounds.size.y ?? 1.0f;
            Debug.Log($"{gameObject.name}: Added NavMeshAgent component");
        }
        else
        {
            // Update NavMeshAgent with current speed if it already exists
            navAgent.speed = speed;
            navAgent.stoppingDistance = stoppingDistance;
            Debug.Log($"{gameObject.name}: Using existing NavMeshAgent component");
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            // Add Rigidbody for knockback physics
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; // Let NavMeshAgent control movement
            rb.useGravity = false; // NavMeshAgent handles gravity
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            Debug.Log($"{gameObject.name}: Added Rigidbody for knockback physics");
        }
        else
        {
            // Configure existing Rigidbody
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        SetupAudio();

        // Set up initial target
        FindNewTarget();

        // Set up retargeting timer
        nextRetargetTime = Time.time + retargetInterval;

        // Add WildlifePositioning component if not already present
        if (GetComponent<WildlifePositioning>() == null)
        {
            gameObject.AddComponent<WildlifePositioning>();
            Debug.Log($"{gameObject.name}: Added WildlifePositioning component");
        }
    }

    void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.clip = animalSound;
        audioSource.loop = true;
        audioSource.spatialBlend = 1.0f; // Set to 3D sound
        audioSource.Play();
    }

    void Update()
    {
        // Handle knockback first
        if (isKnockedBack)
        {
            if (Time.time < knockbackEndTime)
            {
                ApplyKnockback();
            }
            else
            {
                RecoverFromKnockback();
            }
            return; // Skip normal behavior while knocked back
        }

        // Check if our target is still valid
        if (currentTarget != null)
        {
            // Check if target was destroyed
            if (!currentTarget.gameObject.activeInHierarchy)
            {
                Debug.Log($"{gameObject.name}: Target is no longer active, finding new target");
                currentTarget = null;
                FindNewTarget();
                return;
            }

            // Check if the health component is still valid
            if (turretHealth != null && turretHealth.GetCurrentHealth() <= 0)
            {
                Debug.Log($"{gameObject.name}: Turret target has zero health, finding new target");
                currentTarget = null;
                FindNewTarget();
                return;
            }
        }

        // Normal behavior - only execute if not knocked back
        // Periodic retargeting - CHANGED TO NOT INTERRUPT ACTIVE ATTACKS
        if (Time.time >= nextRetargetTime)
        {
            // Only retarget if not currently attacking
            if (!isAttackingTarget)
            {
                Debug.Log($"{gameObject.name}: Time to retarget, current isAttackingTarget={isAttackingTarget}");
                FindNewTarget();
            }
            nextRetargetTime = Time.time + retargetInterval;
        }

        // If no target, find a new one immediately
        if (currentTarget == null)
        {
            FindNewTarget();
        }
        else
        {
            UpdateMovement();
            LookAtTarget();
            AdjustPitchBasedOnDistance();
        }
    }

    void ApplyKnockback()
    {
        // Calculate the knockback movement for this frame
        Vector3 movement = knockbackDirection * knockbackForce * Time.deltaTime;

        // Make sure NavMeshAgent is disabled during knockback
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.enabled = false;
        }

        // Apply knockback through physics or direct movement
        if (rb != null)
        {
            // Use direct position change instead of forces for more predictable results
            transform.position += movement;
        }
        else
        {
            // Fallback if no rigidbody
            transform.position += movement;
        }

        // Gradually reduce the knockback force
        knockbackForce = Mathf.Lerp(knockbackForce, 0, Time.deltaTime * 3f);
    }

    void RecoverFromKnockback()
    {
        isKnockedBack = false;

        // Re-enable NavMeshAgent
        if (navAgent != null && !navAgent.enabled)
        {
            navAgent.enabled = true;

            // Warp to current position to prevent NavMeshAgent teleportation issues
            if (navAgent.isOnNavMesh)
            {
                navAgent.Warp(transform.position);
                navAgent.SetDestination(currentTarget != null ? currentTarget.position : transform.position);
                Debug.Log($"{gameObject.name}: Recovered from knockback, resuming navigation");
            }
            else
            {
                // Try to find a nearby navmesh position
                NavMeshHit hit;
                if (NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas))
                {
                    transform.position = hit.position;
                    navAgent.Warp(hit.position);
                    Debug.Log($"{gameObject.name}: Repositioned to valid NavMesh point after knockback");
                }
                else
                {
                    Debug.LogWarning($"{gameObject.name}: Failed to find valid NavMesh position after knockback");
                }
            }
        }

        // Reset attack state and force re-evaluation of target
        isAttackingTarget = false;
        CancelInvoke("AttackTarget");
        nextRetargetTime = Time.time; // Force immediate retargeting
    }

    // Find the nearest valid target (tower, turret, or player)
    public void FindNewTarget()
    {
        // Reset attack state
        isAttackingTarget = false;
        CancelInvoke("AttackTarget");

        // Reset target references
        currentTarget = null;
        towerHealth = null;
        turretHealth = null;
        playerHealth = null;

        // Find all potential targets within search radius
        Collider[] colliders = Physics.OverlapSphere(transform.position, targetSearchRadius, targetLayers);

        // Create a list to store valid targets
        List<Transform> validTargets = new List<Transform>();
        List<float> targetDistances = new List<float>();

        // First, process each collider
        foreach (Collider collider in colliders)
        {
            // Skip destroyed objects or inactive objects
            if (collider == null || !collider.gameObject.activeInHierarchy)
                continue;

            // Check for turrets
            if (collider.CompareTag("Turret"))
            {
                TurretHealth potentialTurret = collider.GetComponent<TurretHealth>();
                if (potentialTurret != null)
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    validTargets.Add(collider.transform);
                    targetDistances.Add(distance);
                    Debug.Log($"{gameObject.name} found turret {collider.name} at distance {distance:F2}");
                }
            }
            // Check for tower
            else if (collider.CompareTag("Tower"))
            {
                TowerHealth potentialTower = collider.GetComponent<TowerHealth>();
                if (potentialTower != null)
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    validTargets.Add(collider.transform);
                    targetDistances.Add(distance);
                    Debug.Log($"{gameObject.name} found tower {collider.name} at distance {distance:F2}");
                }
            }
            // Check for player
            else if (collider.CompareTag("Player"))
            {
                PlayerHealth potentialPlayer = collider.GetComponent<PlayerHealth>();
                if (potentialPlayer != null)
                {
                    float distance = Vector3.Distance(transform.position, collider.transform.position);
                    validTargets.Add(collider.transform);
                    targetDistances.Add(distance);
                    Debug.Log($"{gameObject.name} found player at distance {distance:F2}");
                }
            }
        }

        // If no targets found via colliders within search radius, try checking for nearest targets
        if (validTargets.Count == 0)
        {
            // First check for nearby turrets and player within search radius
            GameObject[] turrets = GameObject.FindGameObjectsWithTag("Turret");
            foreach (GameObject turret in turrets)
            {
                // Skip destroyed objects (this is crucial)
                if (turret == null || !turret.activeInHierarchy)
                    continue;

                float distance = Vector3.Distance(transform.position, turret.transform.position);
                if (distance <= targetSearchRadius)
                {
                    validTargets.Add(turret.transform);
                    targetDistances.Add(distance);
                    Debug.Log($"{gameObject.name} found turret (by tag) {turret.name} at distance {distance:F2}");
                }
            }

            // Check for player within search radius
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance <= targetSearchRadius)
                {
                    validTargets.Add(player.transform);
                    targetDistances.Add(distance);
                    Debug.Log($"{gameObject.name} found player (by tag) at distance {distance:F2}");
                }
            }

            // If still no targets in range, default to the tower regardless of distance
            if (validTargets.Count == 0)
            {
                GameObject tower = GameObject.FindGameObjectWithTag("Tower");
                if (tower != null)
                {
                    float distance = Vector3.Distance(transform.position, tower.transform.position);
                    validTargets.Add(tower.transform);
                    targetDistances.Add(distance);
                    Debug.Log($"{gameObject.name} defaulting to tower target at distance {distance:F2}");
                }
            }
        }

        // Find the closest target
        if (validTargets.Count > 0)
        {
            int closestIndex = 0;
            float closestDistance = targetDistances[0];

            for (int i = 1; i < targetDistances.Count; i++)
            {
                if (targetDistances[i] < closestDistance)
                {
                    closestDistance = targetDistances[i];
                    closestIndex = i;
                }
            }

            // Set the target
            currentTarget = validTargets[closestIndex];

            // Set the appropriate health component
            if (currentTarget.CompareTag("Tower"))
            {
                towerHealth = currentTarget.GetComponent<TowerHealth>();
                Debug.Log($"{gameObject.name} targeting tower: {currentTarget.name}");
            }
            else if (currentTarget.CompareTag("Turret"))
            {
                turretHealth = currentTarget.GetComponent<TurretHealth>();
                Debug.Log($"{gameObject.name} targeting turret: {currentTarget.name}");
            }
            // Handle player targeting
            else if (currentTarget.CompareTag("Player"))
            {
                playerHealth = currentTarget.GetComponent<PlayerHealth>();
                Debug.Log($"{gameObject.name} targeting player!");
            }

            // Set NavMesh destination to target
            SetDestinationToTarget();
        }
        else
        {
            Debug.Log($"{gameObject.name} couldn't find any targets");
        }
    }

    void SetDestinationToTarget()
    {
        if (navAgent != null && currentTarget != null && navAgent.isOnNavMesh && navAgent.enabled)
        {
            // Set destination to the target's position
            navAgent.SetDestination(currentTarget.position);
            Debug.Log($"{gameObject.name} setting NavMesh destination to {currentTarget.name}");
        }
        else if (currentTarget != null)
        {
            Debug.LogWarning($"{gameObject.name} has a target but is not on NavMesh or NavMeshAgent is disabled!");
        }
    }

    void UpdateMovement()
    {
        if (currentTarget != null)
        {
            // Check if we have a WildlifePositioning component and are targeting the tower
            WildlifePositioning positioning = GetComponent<WildlifePositioning>();
            bool useTowerPositioning = positioning != null && IsTargetingTower();

            // Only directly update NavMesh destination if not using tower positioning
            if (!useTowerPositioning && navAgent != null && navAgent.isOnNavMesh && navAgent.enabled)
            {
                navAgent.SetDestination(currentTarget.position);
            }

            // Check if we're close enough to attack
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

            // If we're very close but not triggering the collider, start attacking
            if (distanceToTarget <= stoppingDistance && !isAttackingTarget)
            {
                Debug.Log($"{gameObject.name} starting attack at distance {distanceToTarget:F2}");
                isAttackingTarget = true;
                InvokeRepeating("AttackTarget", 0f, 1f);

                // Stop NavMeshAgent movement while attacking
                if (navAgent != null)
                {
                    navAgent.isStopped = true;
                    navAgent.updatePosition = false;  // Disable position updates while attacking
                }
            }
            // If we moved too far away, stop attacking
            else if (distanceToTarget > stoppingDistance + 0.5f && isAttackingTarget)
            {
                Debug.Log($"{gameObject.name} stopping attack - moved away from target");
                isAttackingTarget = false;
                CancelInvoke("AttackTarget");

                // Resume NavMeshAgent movement after stopping attack
                if (navAgent != null)
                {
                    navAgent.isStopped = false;
                    navAgent.updatePosition = true;  // Re-enable position updates
                }
            }
        }
    }

    void LookAtTarget()
    {
        if (currentTarget != null && !isAttackingTarget)
        {
            // Only manually rotate if we're not attacking yet (NavMeshAgent handles rotation while moving)
            Vector3 direction = (currentTarget.position - transform.position).normalized;
            Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * navAgent.angularSpeed * 0.01f);
        }
    }

    void AdjustPitchBasedOnDistance()
    {
        if (currentTarget != null)
        {
            float distance = Vector3.Distance(transform.position, currentTarget.position);
            float normalizedDistance = Mathf.InverseLerp(0, 50, distance); // Assuming 50 is the max distance
            audioSource.pitch = Mathf.Lerp(pitchMax, pitchMin, normalizedDistance);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Skip processing triggers if being knocked back
        if (isKnockedBack) return;

        // Check if we've reached our current target
        if (towerHealth != null && other.CompareTag("Tower"))
        {
            // Tower target reached
            isAttackingTarget = true;
            InvokeRepeating("AttackTarget", 0f, 1f);
            Debug.Log($"{gameObject.name} attacking tower");

            // Stop NavMeshAgent movement while attacking
            if (navAgent != null)
            {
                navAgent.isStopped = true;
                navAgent.updatePosition = false;  // Disable position updates while attacking
            }
        }
        else if (other.CompareTag("Turret"))
        {
            // Check if this is the turret we're targeting
            TurretHealth hitTurretHealth = other.GetComponent<TurretHealth>();
            if (hitTurretHealth != null)
            {
                // Update our turret reference if we don't have one
                if (turretHealth == null)
                {
                    turretHealth = hitTurretHealth;
                    currentTarget = other.transform;
                }

                isAttackingTarget = true;
                InvokeRepeating("AttackTarget", 0f, 1f);
                Debug.Log($"{gameObject.name} attacking turret: {other.name}");

                // Stop NavMeshAgent movement while attacking
                if (navAgent != null)
                {
                    navAgent.isStopped = true;
                    navAgent.updatePosition = false;  // Disable position updates while attacking
                }
            }
        }
        // Handle player collisions
        else if (other.CompareTag("Player"))
        {
            // Check if this is the player we're targeting
            PlayerHealth hitPlayerHealth = other.GetComponent<PlayerHealth>();
            if (hitPlayerHealth != null)
            {
                // Update our player reference if we don't have one
                if (playerHealth == null)
                {
                    playerHealth = hitPlayerHealth;
                    currentTarget = other.transform;
                }

                isAttackingTarget = true;
                InvokeRepeating("AttackTarget", 0f, 1f);
                Debug.Log($"{gameObject.name} attacking player!");

                // Stop NavMeshAgent movement while attacking
                if (navAgent != null)
                {
                    navAgent.isStopped = true;
                    navAgent.updatePosition = false;  // Disable position updates while attacking
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Skip processing triggers if being knocked back
        if (isKnockedBack) return;

        // Check if we've moved away from our current target
        if (towerHealth != null && other.CompareTag("Tower"))
        {
            isAttackingTarget = false;
            CancelInvoke("AttackTarget");
            Debug.Log($"{gameObject.name} stopped attacking tower");

            // Resume NavMeshAgent movement after stopping attack
            if (navAgent != null)
            {
                navAgent.isStopped = false;
                navAgent.updatePosition = true;  // Re-enable position updates
            }
        }
        else if (other.CompareTag("Turret"))
        {
            TurretHealth exitTurretHealth = other.GetComponent<TurretHealth>();
            if (exitTurretHealth != null && (turretHealth == exitTurretHealth || turretHealth == null))
            {
                isAttackingTarget = false;
                CancelInvoke("AttackTarget");
                Debug.Log($"{gameObject.name} stopped attacking turret: {other.name}");

                // Resume NavMeshAgent movement after stopping attack
                if (navAgent != null)
                {
                    navAgent.isStopped = false;
                    navAgent.updatePosition = true;  // Re-enable position updates
                }
            }
        }
        // Handle player exit
        else if (other.CompareTag("Player"))
        {
            PlayerHealth exitPlayerHealth = other.GetComponent<PlayerHealth>();
            if (exitPlayerHealth != null && (playerHealth == exitPlayerHealth || playerHealth == null))
            {
                isAttackingTarget = false;
                CancelInvoke("AttackTarget");
                Debug.Log($"{gameObject.name} stopped attacking player");

                // Resume NavMeshAgent movement after stopping attack
                if (navAgent != null)
                {
                    navAgent.isStopped = false;
                    navAgent.updatePosition = true;  // Re-enable position updates
                }
            }
        }
    }

    // Helper method to check if a target is valid
    bool IsTargetValid()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            return false;

        if (turretHealth != null && turretHealth.GetCurrentHealth() <= 0)
            return false;

        if (towerHealth != null && towerHealth.GetCurrentHealth() <= 0)
            return false;

        if (playerHealth != null && playerHealth.GetCurrentHealth() <= 0)
            return false;

        return true;
    }

    void AttackTarget()
    {
        // First verify target is still valid
        if (!IsTargetValid())
        {
            Debug.Log($"{gameObject.name}: Target became invalid during attack, stopping attack");
            isAttackingTarget = false;
            CancelInvoke("AttackTarget");
            FindNewTarget();
            return;
        }

        if (towerHealth != null)
        {
            towerHealth.TakeDamage(damage);
            Debug.Log($"{gameObject.name} dealt {damage} damage to tower");
        }
        else if (turretHealth != null)
        {
            turretHealth.TakeDamage(damage);
            Debug.Log($"{gameObject.name} dealt {damage} damage to turret");
        }
        // Handle player damage with position information
        else if (playerHealth != null)
        {
            // Pass the attacker's position to calculate knockback direction
            playerHealth.TakeDamage(damage, transform.position);
            Debug.Log($"{gameObject.name} dealt {damage} damage to player");
        }
    }

    public void TakeDamage(int damage, Vector3 damageSourcePos = default)
    {
        // Apply the damage
        currentHealth -= damage;

        // Apply knockback if we have a damage source
        if (damageSourcePos != default && !isKnockedBack)
        {
            ApplyDamageKnockback(damageSourcePos, damage);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void ApplyDamageKnockback(Vector3 sourcePosition, float damageAmount)
    {
        // Calculate direction from source to wildlife
        Vector3 direction = (transform.position - sourcePosition).normalized;

        // Ensure Y component is minimal to prevent flying
        direction.y = 0.1f;

        // Normalize direction again after modifying y
        if (direction.magnitude < 0.1f)
        {
            // Fallback if source is too close
            direction = transform.forward;
        }
        else
        {
            direction.Normalize();
        }

        // Calculate force based on damage and resistance
        // Base knockback force is damage * 2, modified by resistance
        float force = (damageAmount * 2f) / knockbackResistance;

        // Cap force to reasonable limits
        force = Mathf.Clamp(force, 1f, 15f);

        // Set knockback parameters
        knockbackDirection = direction;
        knockbackForce = force;
        knockbackEndTime = Time.time + knockbackDuration;
        isKnockedBack = true;

        // Stop NavMeshAgent during knockback
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true;
        }

        // Cancel any ongoing attack
        if (isAttackingTarget)
        {
            isAttackingTarget = false;
            CancelInvoke("AttackTarget");
        }

        Debug.Log($"{gameObject.name} knocked back with force {force} in direction {direction}");
    }

    void Die()
    {
        // Award points before destroying
        if (PointsManager.Instance != null)
        {
            PointsManager.Instance.AddPoints(pointValue);
        }
        else
        {
            Debug.LogWarning("PointsManager instance not found. Points not awarded.");
        }

        // Disable NavMeshAgent to prevent errors
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        Destroy(gameObject);
    }

    // This method determines if this wildlife is targeting the tower
    public bool IsTargetingTower()
    {
        // Return true if the current target is the tower
        return currentTarget != null &&
               currentTarget.CompareTag("Tower") &&
               towerHealth != null;
    }

    // Visual debugging
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, targetSearchRadius);

        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }

        // Show stopping distance
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);
    }
}