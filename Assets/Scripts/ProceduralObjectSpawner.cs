using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[ExecuteInEditMode]
public class ProceduralObjectSpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnableObject
    {
        public GameObject prefab;
        public string name;
        public float densityPer100m = 10f;

        // Size settings
        public float minSize = 0.8f;
        public float maxSize = 1.2f;

        // Rotation settings
        [Range(0f, 1f)]
        public float randomRotationX = 0f;
        [Range(0f, 1f)]
        public float randomRotationY = 1f;
        [Range(0f, 1f)]
        public float randomRotationZ = 0f;

        public bool alignToTerrain = true;
    }

    public Terrain targetTerrain;
    public List<SpawnableObject> spawnableObjects = new List<SpawnableObject>();
    public bool avoidOverlap = true;
    public float minDistanceBetweenObjects = 2f;
    public bool showGizmos = true;

    [Tooltip("Use prefab variants instead of clones for better performance")]
    public bool usePrefabInstances = true;

    private List<GameObject> spawnedObjects = new List<GameObject>();
    private Transform objectsParent;

    public void GenerateObjects()
    {
        if (targetTerrain == null)
        {
            Debug.LogError("No terrain assigned!");
            return;
        }

        // Create or find a parent object to hold all spawned objects
        if (objectsParent == null)
        {
            GameObject parent = new GameObject("Generated_Objects");
            objectsParent = parent.transform;
            objectsParent.parent = transform;
        }

        // Clear any previously spawned objects
        ClearObjects();

        TerrainData terrainData = targetTerrain.terrainData;
        Vector3 terrainSize = terrainData.size;
        Vector3 terrainPosition = targetTerrain.transform.position;

        float areaSizeIn100m = (terrainSize.x * terrainSize.z) / 10000f; // Area in 100x100m squares

        // Generate each type of object
        foreach (SpawnableObject spawnObj in spawnableObjects)
        {
            if (spawnObj.prefab == null) continue;

            int totalToSpawn = Mathf.RoundToInt(spawnObj.densityPer100m * areaSizeIn100m);
            List<Vector3> spawnPositions = new List<Vector3>();

            // Create a category parent
            GameObject categoryParent = new GameObject(spawnObj.name + "_Group");
            categoryParent.transform.parent = objectsParent;

            // Try to spawn the required number of objects
            int attempts = 0;
            int maxAttempts = totalToSpawn * 5; // Allow multiple attempts per object

            while (spawnPositions.Count < totalToSpawn && attempts < maxAttempts)
            {
                attempts++;

                // Generate random position on terrain
                float randomX = Random.Range(0, terrainSize.x);
                float randomZ = Random.Range(0, terrainSize.z);

                // Get the height at that position
                float terrainHeight = terrainData.GetHeight(
                    Mathf.RoundToInt(randomX / terrainSize.x * terrainData.heightmapResolution),
                    Mathf.RoundToInt(randomZ / terrainSize.z * terrainData.heightmapResolution)
                );

                Vector3 worldPos = new Vector3(
                    randomX + terrainPosition.x,
                    terrainHeight + terrainPosition.y,
                    randomZ + terrainPosition.z
                );

                // Check if position is too close to existing objects
                bool tooClose = false;
                if (avoidOverlap)
                {
                    foreach (Vector3 existingPos in spawnPositions)
                    {
                        if (Vector3.Distance(worldPos, existingPos) < minDistanceBetweenObjects)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                }

                if (!tooClose)
                {
                    spawnPositions.Add(worldPos);

                    GameObject newObj;

#if UNITY_EDITOR
                    if (usePrefabInstances)
                    {
                        // Create a prefab instance that maintains connection to the prefab
                        newObj = (GameObject)PrefabUtility.InstantiatePrefab(spawnObj.prefab, categoryParent.transform);
                        newObj.transform.position = worldPos;

                        // Apply transformations as prefab overrides
                        float randomScale = Random.Range(spawnObj.minSize, spawnObj.maxSize);
                        newObj.transform.localScale *= randomScale;

                        // Apply random rotation on each axis based on slider values
                        Vector3 randomRotation = new Vector3(
                            Random.Range(0, 360 * spawnObj.randomRotationX),
                            Random.Range(0, 360 * spawnObj.randomRotationY),
                            Random.Range(0, 360 * spawnObj.randomRotationZ)
                        );

                        newObj.transform.Rotate(randomRotation);

                        // Record prefab modifications so they persist
                        PrefabUtility.RecordPrefabInstancePropertyModifications(newObj.transform);
                    }
                    else
                    {
                        // Create a normal instance (clone)
                        newObj = (GameObject)PrefabUtility.InstantiatePrefab(spawnObj.prefab, categoryParent.transform);
                        newObj.transform.position = worldPos;
                    }
#else
                    // Fallback for runtime use
                    newObj = Instantiate(spawnObj.prefab, worldPos, Quaternion.identity, categoryParent.transform);
#endif

                    // Apply random size between min and max if not using prefab instances or at runtime
                    if (!usePrefabInstances || !Application.isEditor)
                    {
                        float randomScale = Random.Range(spawnObj.minSize, spawnObj.maxSize);
                        newObj.transform.localScale *= randomScale;

                        // Apply random rotation on each axis based on slider values
                        Vector3 randomRotation = new Vector3(
                            Random.Range(0, 360 * spawnObj.randomRotationX),
                            Random.Range(0, 360 * spawnObj.randomRotationY),
                            Random.Range(0, 360 * spawnObj.randomRotationZ)
                        );

                        newObj.transform.Rotate(randomRotation);
                    }

                    // Align object to terrain normal if requested
                    if (spawnObj.alignToTerrain)
                    {
                        AlignToTerrain(newObj.transform, terrainData, terrainPosition, terrainSize);

                        // Record this alignment as a prefab modification if using prefab instances
#if UNITY_EDITOR
                        if (usePrefabInstances)
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(newObj.transform);
                        }
#endif
                    }

                    spawnedObjects.Add(newObj);
                }
            }

            if (attempts >= maxAttempts && spawnPositions.Count < totalToSpawn)
            {
                Debug.LogWarning($"Could only spawn {spawnPositions.Count} of {totalToSpawn} requested {spawnObj.name} objects. " +
                    "Try decreasing density or minimum distance between objects.");
            }
        }

        Debug.Log($"Generated {spawnedObjects.Count} objects across the terrain. Using prefab instances: {usePrefabInstances}");
    }

    private void AlignToTerrain(Transform objTransform, TerrainData terrainData, Vector3 terrainPosition, Vector3 terrainSize)
    {
        // Get local position relative to terrain
        Vector3 localPos = objTransform.position - terrainPosition;

        // Get terrain normal
        float normX = localPos.x / terrainSize.x;
        float normZ = localPos.z / terrainSize.z;

        Vector3 normal = terrainData.GetInterpolatedNormal(normX, normZ);

        // Align rotation to normal
        objTransform.up = normal;

        // Add random rotation around the new up vector
        objTransform.Rotate(Vector3.up, Random.Range(0, 360));
    }

    public void ClearObjects()
    {
        // Destroy all previously spawned objects
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                DestroyImmediate(obj);
            }
        }
        spawnedObjects.Clear();

        // Clean up any empty containers
        if (objectsParent != null)
        {
            for (int i = objectsParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(objectsParent.GetChild(i).gameObject);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || targetTerrain == null) return;

        // Visualize the terrain bounds
        Gizmos.color = Color.green;
        TerrainData terrainData = targetTerrain.terrainData;
        Vector3 terrainSize = terrainData.size;
        Vector3 terrainPosition = targetTerrain.transform.position;
        Gizmos.DrawWireCube(terrainPosition + terrainSize / 2, terrainSize);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ProceduralObjectSpawner))]
public class ProceduralObjectSpawnerEditor : Editor
{
    private bool[] foldouts;

    private void OnEnable()
    {
        ProceduralObjectSpawner spawner = (ProceduralObjectSpawner)target;
        foldouts = new bool[spawner.spawnableObjects.Count];
    }

    public override void OnInspectorGUI()
    {
        ProceduralObjectSpawner spawner = (ProceduralObjectSpawner)target;

        // Ensure our foldout array matches the list size
        if (foldouts == null || foldouts.Length != spawner.spawnableObjects.Count)
        {
            foldouts = new bool[spawner.spawnableObjects.Count];
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetTerrain"));

        // Add the new usePrefabInstances property with helpful explanation
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Optimization Settings", EditorStyles.boldLabel);
        SerializedProperty usePrefabInstancesProp = serializedObject.FindProperty("usePrefabInstances");
        EditorGUILayout.PropertyField(usePrefabInstancesProp);

        if (usePrefabInstancesProp.boolValue)
        {
            EditorGUILayout.HelpBox("Using prefab instances maintains connection to source prefabs. Changes to original prefabs will update all instances. This is more memory efficient.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Using clones creates independent copies. Changes to original prefabs won't affect spawned objects.", MessageType.Info);
        }

        EditorGUILayout.Space(5);

        // Start of spawnable objects list
        EditorGUILayout.LabelField("Spawnable Objects", EditorStyles.boldLabel);

        SerializedProperty spawnableObjectsProperty = serializedObject.FindProperty("spawnableObjects");
        EditorGUI.indentLevel++;

        for (int i = 0; i < spawnableObjectsProperty.arraySize; i++)
        {
            SerializedProperty objectProperty = spawnableObjectsProperty.GetArrayElementAtIndex(i);

            // Custom foldout with remove button
            EditorGUILayout.BeginHorizontal();

            SerializedProperty nameProperty = objectProperty.FindPropertyRelative("name");
            string displayName = string.IsNullOrEmpty(nameProperty.stringValue) ?
                $"Object {i + 1}" : nameProperty.stringValue;

            foldouts[i] = EditorGUILayout.Foldout(foldouts[i], displayName, true);

            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                spawnableObjectsProperty.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                return; // Exit to avoid issues with deleted element
            }

            EditorGUILayout.EndHorizontal();

            if (foldouts[i])
            {
                EditorGUI.indentLevel++;

                // Basic properties
                EditorGUILayout.PropertyField(objectProperty.FindPropertyRelative("prefab"));
                EditorGUILayout.PropertyField(objectProperty.FindPropertyRelative("name"));
                EditorGUILayout.PropertyField(objectProperty.FindPropertyRelative("densityPer100m"));
                EditorGUILayout.PropertyField(objectProperty.FindPropertyRelative("alignToTerrain"));

                // Size settings
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Size Settings", EditorStyles.boldLabel);

                SerializedProperty minSizeProp = objectProperty.FindPropertyRelative("minSize");
                SerializedProperty maxSizeProp = objectProperty.FindPropertyRelative("maxSize");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Size Range");

                // Get the current values
                float minVal = minSizeProp.floatValue;
                float maxVal = maxSizeProp.floatValue;

                // Field for min value
                minVal = EditorGUILayout.FloatField(minVal, GUILayout.Width(50));

                // MinMaxSlider with local variables
                EditorGUILayout.MinMaxSlider(
                    ref minVal,
                    ref maxVal,
                    0.1f, 5f
                );

                // Field for max value
                maxVal = EditorGUILayout.FloatField(maxVal, GUILayout.Width(50));

                // Apply values back to properties
                minSizeProp.floatValue = minVal;
                maxSizeProp.floatValue = maxVal;
                EditorGUILayout.EndHorizontal();

                // Rotation settings
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Rotation Settings (0=None, 1=Full Random)", EditorStyles.boldLabel);

                SerializedProperty rotXProperty = objectProperty.FindPropertyRelative("randomRotationX");
                SerializedProperty rotYProperty = objectProperty.FindPropertyRelative("randomRotationY");
                SerializedProperty rotZProperty = objectProperty.FindPropertyRelative("randomRotationZ");

                EditorGUILayout.Slider(rotXProperty, 0f, 1f, "X Rotation");
                EditorGUILayout.Slider(rotYProperty, 0f, 1f, "Y Rotation");
                EditorGUILayout.Slider(rotZProperty, 0f, 1f, "Z Rotation");

                EditorGUI.indentLevel--;
            }
        }

        EditorGUI.indentLevel--;

        EditorGUILayout.Space(5);
        if (GUILayout.Button("Add New Object Type"))
        {
            spawnableObjectsProperty.arraySize++;
            SerializedProperty newElement = spawnableObjectsProperty.GetArrayElementAtIndex(spawnableObjectsProperty.arraySize - 1);
            newElement.FindPropertyRelative("name").stringValue = "";
            newElement.FindPropertyRelative("densityPer100m").floatValue = 10f;
            newElement.FindPropertyRelative("minSize").floatValue = 0.8f;
            newElement.FindPropertyRelative("maxSize").floatValue = 1.2f;
            newElement.FindPropertyRelative("randomRotationX").floatValue = 0f;
            newElement.FindPropertyRelative("randomRotationY").floatValue = 1f;
            newElement.FindPropertyRelative("randomRotationZ").floatValue = 0f;
            newElement.FindPropertyRelative("alignToTerrain").boolValue = true;

            // Expand the new element
            foldouts = new bool[spawnableObjectsProperty.arraySize];
            foldouts[foldouts.Length - 1] = true;
        }

        // Other settings
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Placement Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("avoidOverlap"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minDistanceBetweenObjects"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showGizmos"));

        // Apply changes
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10);

        // Add buttons
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Generate", GUILayout.Height(30)))
        {
            spawner.GenerateObjects();
            EditorUtility.SetDirty(spawner);
        }

        if (GUILayout.Button("Clear All", GUILayout.Height(30)))
        {
            spawner.ClearObjects();
            EditorUtility.SetDirty(spawner);
        }

        EditorGUILayout.EndHorizontal();
    }
}
#endif