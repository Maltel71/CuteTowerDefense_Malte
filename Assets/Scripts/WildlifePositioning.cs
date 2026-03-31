using UnityEngine;
using System.Collections.Generic;
using UnityEngine.AI;

public class WildlifePositioning : MonoBehaviour
{
    [Header("Tower Attack Positioning")]
    [Tooltip("How far away wildlife should try to spread around the tower")]
    public float spreadRadius = 15f;
    [Tooltip("Minimum distance between wildlife when attacking the tower")]
    public float minSpaceBetweenWildlife = 2.5f;
    [Tooltip("How often to update attack positions (seconds)")]
    public float positionUpdateInterval = 3f;
    [Tooltip("How close to the tower wildlife can get")]
    public float minTowerDistance = 2f;
    [Tooltip("Layer mask for finding obstacles when positioning")]
    public LayerMask obstacleLayerMask;

    // References
    private Transform towerTransform;
    private NavMeshAgent navAgent;
    private Wildlife wildlifeScript;

    // State tracking
    private float nextPositionUpdateTime;
    private Vector3 assignedAttackPosition;
    private bool hasAttackPosition = false;

    void Start()
    {
        // Find reference to the tower
        GameObject tower = GameObject.FindWithTag("Tower");
        if (tower != null)
        {
            towerTransform = tower.transform;
        }
        else
        {
            Debug.LogError("WildlifePositioning: Tower not found!");
        }

        // Get NavMeshAgent from the wildlife
        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent == null)
        {
            Debug.LogError("WildlifePositioning: NavMeshAgent not found!");
        }

        // Get Wildlife script reference
        wildlifeScript = GetComponent<Wildlife>();
        if (wildlifeScript == null)
        {
            Debug.LogError("WildlifePositioning: Wildlife script not found!");
        }

        // Set default layer mask if not set
        if (obstacleLayerMask.value == 0)
        {
            obstacleLayerMask = LayerMask.GetMask("Default");
        }

        // Schedule first position update
        nextPositionUpdateTime = Time.time + Random.Range(0f, 1f);
    }

    void Update()
    {
        if (towerTransform == null || navAgent == null || wildlifeScript == null)
            return;

        // Check if this wildlife is targeting the tower
        bool isTargetingTower = wildlifeScript.IsTargetingTower();
        if (!isTargetingTower)
        {
            hasAttackPosition = false;
            return;
        }

        // Only update positions periodically to avoid excessive pathfinding
        if (Time.time >= nextPositionUpdateTime)
        {
            UpdateAttackPosition();
            nextPositionUpdateTime = Time.time + positionUpdateInterval;
        }

        // If we have an attack position, use it
        if (hasAttackPosition)
        {
            // Only update the destination if we're not too close yet
            float distanceToAssignedPos = Vector3.Distance(transform.position, assignedAttackPosition);
            if (distanceToAssignedPos > navAgent.stoppingDistance + 0.5f)
            {
                if (navAgent.isOnNavMesh && navAgent.enabled)
                {
                    navAgent.SetDestination(assignedAttackPosition);
                }
            }
        }
    }

    void UpdateAttackPosition()
    {
        if (towerTransform == null)
            return;

        // Skip position updates for wildlife that is already attacking
        if (wildlifeScript != null && wildlifeScript.isAttackingTarget)
        {
            // Don't reposition wildlife that's already in an attack
            Debug.Log($"{gameObject.name} skipping position update because wildlife is currently attacking");
            return;
        }

        // Try to find a good position around the tower
        Vector3 newPosition = FindOptimalAttackPosition();

        // If we found a valid position, use it
        if (newPosition != Vector3.zero)
        {
            assignedAttackPosition = newPosition;
            hasAttackPosition = true;

            // Set the destination immediately
            if (navAgent.isOnNavMesh && navAgent.enabled)
            {
                navAgent.SetDestination(assignedAttackPosition);
                Debug.Log($"{gameObject.name} moving to tower attack position: {assignedAttackPosition}");
            }
        }
    }

    Vector3 FindOptimalAttackPosition()
    {
        // Get current wildlife attacking the tower
        List<Transform> attackers = GetCurrentTowerAttackers();

        // Calculate the optimal angle for this wildlife based on the other attackers
        float optimalAngle = CalculateOptimalAttackAngle(attackers);

        // Try to find a valid position at the optimal angle
        Vector3 optimalPosition = Vector3.zero;
        float currentRadius = minTowerDistance;
        int maxAttempts = 10;

        while (currentRadius <= spreadRadius && maxAttempts > 0)
        {
            // Calculate position at current radius and optimal angle
            Vector3 directionToTower = new Vector3(Mathf.Cos(optimalAngle), 0f, Mathf.Sin(optimalAngle));
            Vector3 candidatePosition = towerTransform.position + directionToTower * currentRadius;

            // Check if the position is valid (on NavMesh and not blocked)
            if (IsValidAttackPosition(candidatePosition))
            {
                optimalPosition = candidatePosition;
                break;
            }

            // Try a slightly wider radius next time
            currentRadius += 1f;
            maxAttempts--;
        }

        return optimalPosition;
    }

    List<Transform> GetCurrentTowerAttackers()
    {
        List<Transform> attackers = new List<Transform>();

        // Find all wildlife in range that might be attacking the tower
        Collider[] wildlifeNearby = Physics.OverlapSphere(towerTransform.position, spreadRadius * 1.5f);

        foreach (Collider col in wildlifeNearby)
        {
            // Skip itself
            if (col.transform == transform)
                continue;

            // Check if it's wildlife and attacking the tower
            Wildlife otherWildlife = col.GetComponent<Wildlife>();
            if (otherWildlife != null && otherWildlife.IsTargetingTower())
            {
                attackers.Add(col.transform);
            }
        }

        return attackers;
    }

    float CalculateOptimalAttackAngle(List<Transform> otherAttackers)
    {
        // If no other attackers, pick a random angle
        if (otherAttackers.Count == 0)
        {
            return Random.Range(0f, Mathf.PI * 2f);
        }

        // Calculate all the current angles of attackers relative to tower
        List<float> takenAngles = new List<float>();
        foreach (Transform attacker in otherAttackers)
        {
            Vector3 directionFromTower = attacker.position - towerTransform.position;
            directionFromTower.y = 0; // Keep it in the horizontal plane

            if (directionFromTower.magnitude > 0.1f)
            {
                float angle = Mathf.Atan2(directionFromTower.z, directionFromTower.x);
                takenAngles.Add(angle);
            }
        }

        // Sort the angles
        takenAngles.Sort();

        // Find the largest gap between two adjacent angles
        float largestGap = 0f;
        float gapStartAngle = 0f;

        // Add the first angle again at the end to check the gap that wraps around
        takenAngles.Add(takenAngles[0] + Mathf.PI * 2f);

        for (int i = 0; i < takenAngles.Count - 1; i++)
        {
            float gap = takenAngles[i + 1] - takenAngles[i];
            if (gap > largestGap)
            {
                largestGap = gap;
                gapStartAngle = takenAngles[i];
            }
        }

        // Position this wildlife in the middle of the largest gap
        float optimalAngle = gapStartAngle + largestGap / 2f;

        // Keep angle within [0, 2π] range
        while (optimalAngle >= Mathf.PI * 2f)
            optimalAngle -= Mathf.PI * 2f;
        while (optimalAngle < 0f)
            optimalAngle += Mathf.PI * 2f;

        return optimalAngle;
    }

    bool IsValidAttackPosition(Vector3 position)
    {
        // Check if position is on NavMesh
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(position, out hit, 1f, NavMesh.AllAreas))
        {
            return false;
        }

        // Use the actual NavMesh position
        Vector3 navMeshPosition = hit.position;

        // Check if there's a clear line of sight to the tower
        if (Physics.Linecast(navMeshPosition + Vector3.up * 0.5f,
                             towerTransform.position + Vector3.up * 0.5f,
                             obstacleLayerMask))
        {
            return false;
        }

        // Check if the position is too close to other wildlife
        Collider[] nearbyWildlife = Physics.OverlapSphere(navMeshPosition, minSpaceBetweenWildlife);
        foreach (Collider col in nearbyWildlife)
        {
            // Skip itself
            if (col.transform == transform)
                continue;

            // If another wildlife is too close, this position is invalid
            if (col.GetComponent<Wildlife>() != null)
            {
                return false;
            }
        }

        return true;
    }

    void OnDrawGizmosSelected()
    {
        if (towerTransform != null && hasAttackPosition)
        {
            // Draw line to assigned position
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, assignedAttackPosition);

            // Draw sphere at assigned position
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(assignedAttackPosition, 0.3f);
        }
    }
}