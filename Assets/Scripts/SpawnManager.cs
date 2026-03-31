using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.AI; // Add this for NavMesh functionality

public class SpawnManager : MonoBehaviour
{
    public GameObject[] wildlifePrefabs; // Array of wildlife prefabs
    public Transform[] spawnPoints; // Array of spawn points
    public float waveDuration = 60f;
    public float restDuration = 20f;
    public int initialSpawnCount = 5;
    public int waveIncrement = 2;
    public int[] wildlifeSpawnChances; // Array of spawn chances for each wildlife type
    public TextMeshProUGUI waveCountText; // TextMeshPro for displaying wave count

    [Header("NavMesh Spawning")]
    public float maxNavMeshSpawnDistance = 5f; // Maximum distance to find valid NavMesh position
    public bool ensureSpawnPointOnNavMesh = true; // Should we ensure spawn points are on NavMesh?
    public bool validateSpawnPointsOnStart = true; // Check spawn points on start

    public int currentWave = 0;
    private TowerHealth towerHealth;

    void Start()
    {
        towerHealth = GameObject.FindGameObjectWithTag("Tower").GetComponent<TowerHealth>();

        // Validate spawn points
        if (validateSpawnPointsOnStart)
        {
            ValidateSpawnPoints();
        }

        StartCoroutine(SpawnWaves());
    }

    void ValidateSpawnPoints()
    {
        // Check if spawn points are on NavMesh
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            if (spawnPoints[i] != null && !IsPointOnNavMesh(spawnPoints[i].position))
            {
                Debug.LogWarning($"Spawn point {i} at {spawnPoints[i].position} is not on NavMesh! Wildlife may not be able to navigate from here.");

                // Try to find a nearby valid position
                if (ensureSpawnPointOnNavMesh)
                {
                    Vector3 validPos;
                    if (TryGetValidNavMeshPoint(spawnPoints[i].position, out validPos))
                    {
                        spawnPoints[i].position = validPos;
                        Debug.Log($"Adjusted spawn point {i} to valid NavMesh position: {validPos}");
                    }
                    else
                    {
                        Debug.LogError($"Could not find valid NavMesh position near spawn point {i}!");
                    }
                }
            }
        }
    }

    bool IsPointOnNavMesh(Vector3 position)
    {
        NavMeshHit hit;
        return NavMesh.SamplePosition(position, out hit, 0.1f, NavMesh.AllAreas);
    }

    bool TryGetValidNavMeshPoint(Vector3 startPosition, out Vector3 validPosition)
    {
        NavMeshHit hit;
        if (NavMesh.SamplePosition(startPosition, out hit, maxNavMeshSpawnDistance, NavMesh.AllAreas))
        {
            validPosition = hit.position;
            return true;
        }

        validPosition = startPosition;
        return false;
    }

    void Update()
    {
        // Keep the wave count text
        waveCountText.text = "Wave: " + currentWave;
    }

    IEnumerator SpawnWaves()
    {
        while (true)
        {
            currentWave++;
            int spawnCount = initialSpawnCount + (waveIncrement * (currentWave - 1));

            Debug.Log($"Wave {currentWave} starting - Spawning {spawnCount} wildlife");

            for (int i = 0; i < spawnCount; i++)
            {
                SpawnWildlife();
                // Add a small delay between spawns to prevent NavMesh agent conflicts
                yield return new WaitForSeconds(0.2f);
            }

            yield return new WaitForSeconds(waveDuration);
            Debug.Log($"Wave {currentWave} ended - Rest period starting");
            yield return new WaitForSeconds(restDuration);
        }
    }

    void SpawnWildlife()
    {
        // Select random spawn point
        int spawnIndex = Random.Range(0, spawnPoints.Length);
        Transform spawnPoint = spawnPoints[spawnIndex];

        if (spawnPoint == null)
        {
            Debug.LogError("Spawn point is null!");
            return;
        }

        Vector3 spawnPosition = spawnPoint.position;

        // Ensure spawn position is on NavMesh
        if (ensureSpawnPointOnNavMesh)
        {
            Vector3 validPos;
            if (!IsPointOnNavMesh(spawnPosition) && TryGetValidNavMeshPoint(spawnPosition, out validPos))
            {
                spawnPosition = validPos;
            }
        }

        // Select wildlife type based on spawn chances
        int wildlifeIndex = GetRandomWildlifeIndex();
        GameObject prefab = wildlifePrefabs[wildlifeIndex];

        if (prefab == null)
        {
            Debug.LogError($"Wildlife prefab at index {wildlifeIndex} is null!");
            return;
        }

        // Instantiate and initialize wildlife
        GameObject wildlife = Instantiate(prefab, spawnPosition, spawnPoint.rotation);

        // Check if we need to add a NavMeshAgent component
        NavMeshAgent agent = wildlife.GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = wildlife.AddComponent<NavMeshAgent>();
            Debug.Log($"Added NavMeshAgent to {prefab.name} during spawn");

            // Set default NavMeshAgent properties if the Wildlife script doesn't do it
            agent.speed = wildlife.GetComponent<Wildlife>()?.speed ?? 2f;
            agent.stoppingDistance = 1.5f;
            agent.acceleration = 8f;
            agent.angularSpeed = 120f;
        }

        // Ensure the wildlife is on the NavMesh
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"Wildlife spawned at {spawnPosition} is not on NavMesh!");

            // Try to warp to a valid position
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPosition, out hit, maxNavMeshSpawnDistance, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                Debug.Log($"Warped wildlife to valid NavMesh position: {hit.position}");
            }
            else
            {
                Debug.LogError($"Could not find valid NavMesh position for wildlife - it may not navigate properly!");
            }
        }

        Debug.Log($"Spawned: {prefab.name} at position {spawnPosition}, NavMesh valid: {agent.isOnNavMesh}");
    }

    int GetRandomWildlifeIndex()
    {
        int total = 0;
        foreach (int chance in wildlifeSpawnChances)
        {
            total += chance;
        }

        int randomPoint = Random.Range(0, total);
        for (int i = 0; i < wildlifeSpawnChances.Length; i++)
        {
            if (randomPoint < wildlifeSpawnChances[i])
            {
                return i;
            }
            randomPoint -= wildlifeSpawnChances[i];
        }
        return 0; // Default to first wildlife type
    }
}