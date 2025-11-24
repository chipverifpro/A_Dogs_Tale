using UnityEngine;
using System.Collections.Generic;

public class RandomSceneryScatter : MonoBehaviour
{
    [Header("Scattering Settings")]
    public List<GameObject> sceneryPrefabs;     // prefabs that must include or can auto-add WorldObject + modules
    public int count = 100;
    public float randomRotation = 360f;
    public Vector2 randomScaleRange = new Vector2(0.9f, 1.1f);
    public float yOffset = 10f;                  // extra adjustment above ground
    public bool addScentEmitters = false;

    private ObjectDirectory dir;

    private void Awake()
    {
        dir = ObjectDirectory.Instance;
    }

    public void ScatterScenery(List<Cell> validCells)
    {
        if (sceneryPrefabs == null || sceneryPrefabs.Count == 0)
        {
            Debug.LogWarning("RandomSceneryScatter: No scenery prefabs assigned.");
            return;
        }

        if (dir == null || dir.gen == null)
        {
            Debug.LogError("RandomSceneryScatter: ObjectDirectory or DungeonGenerator missing.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var cell = validCells[Random.Range(0, validCells.Count)];
            Vector3 position = cell.pos3d_world; 
            position.y += yOffset;

            GameObject prefab = sceneryPrefabs[Random.Range(0, sceneryPrefabs.Count)];
            GameObject instance = Instantiate(prefab, position, Quaternion.identity);

            InitializeWorldObject(instance, cell);
        }
    }

    /// <summary>
    /// Ensure the spawned object has WorldObject + key modules,
    /// then initialize LocationModule & registry info.
    /// </summary>
    private void InitializeWorldObject(GameObject instance, Cell cell)
    {
        // --- 1. Ensure a WorldObject exists ---
        WorldObject wo = instance.GetComponent<WorldObject>();
        if (wo == null)
        {
            wo = instance.AddComponent<WorldObject>();
        }

        // --- 2. Ensure optional modules or allow auto-add ---
        // LocationModule (recommended for all scenery)
        LocationModule loc = instance.GetComponent<LocationModule>();
        if (loc == null)
            loc = instance.AddComponent<LocationModule>();

        // VisualModule auto-add if prefab doesn't have it
        VisualModule visual = instance.GetComponent<VisualModule>();
        if (visual == null)
            visual = instance.AddComponent<VisualModule>();

        // Optional: add ScentEmitter if desired
        if (addScentEmitters)
        {
            ScentEmitter scent = instance.GetComponent<ScentEmitter>();
            if (scent == null)
                scent = instance.AddComponent<ScentEmitter>();
        }

        // --- 3. Initialize LocationModule based on the cell ---
        loc.cell = cell;
        loc.pos3d_f = cell.pos3d_f;          // loc pos3d_f is in grid space
        loc.yawDeg = Random.Range(0f, 360f);

        // Random rotation applied to transform
        instance.transform.rotation = Quaternion.Euler(0f, loc.yawDeg, 0f);

        // Random uniform scale
        float scale = Random.Range(randomScaleRange.x, randomScaleRange.y);
        instance.transform.localScale = new Vector3(scale, scale, scale);

        // --- 4. Register with WorldObjectRegistry ---
        wo.RegisterIfNeeded();

        // (The WorldObject.Awake() has already populated Location, Motion, Visual modules)

        // Debug convenience:
        // Debug.Log($"Placed WorldObject '{wo.displayName}' at {loc.pos3d} cell.");
    }
}
/*
public class RandomSceneryScatterer : MonoBehaviour
{
    [Tooltip("Library of tree/rock prefabs to scatter.")]
    public SceneryLibrary sceneryLibrary;

    [Tooltip("Parent transform for spawned scenery (optional).")]
    public Transform sceneryParent;

    [Tooltip("How many props to try placing per level.")]
    public int numPropsToPlace = 100;

    [Tooltip("Minimum spacing between props (world units).")]
    public float minSpacing = 2f;

    // Call this from your dungeon generator after the level is built.
    public void Scatter(List<Cell> candidateCells, int numPropsToPlace)
    {
        if (sceneryLibrary == null || sceneryLibrary.prefabs == null || sceneryLibrary.prefabs.Count == 0)
        {
            Debug.LogWarning("RandomSceneryScatterer: No prefabs in sceneryLibrary.");
            return;
        }

        if (candidateCells == null || candidateCells.Count == 0)
        {
            Debug.LogWarning("RandomSceneryScatterer: No candidate cells passed in.");
            return;
        }

        List<Vector3> usedPositions = new();

        for (int i = 0; i < numPropsToPlace; i++)
        {
            // Pick a random cell to place on (you can filter to floor cells, outdoors, etc.)
            Cell cell = candidateCells[Random.Range(0, candidateCells.Count)];
            Vector3 pos = cell.pos3d_world;  // or whatever your floor center is

            // Quick spacing check so things don't overlap horribly
            bool tooClose = false;
            foreach (var used in usedPositions)
            {
                if (Vector3.SqrMagnitude(pos - used) < minSpacing * minSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            usedPositions.Add(pos);

            // Random prefab
            GameObject prefab = sceneryLibrary.prefabs[Random.Range(0, sceneryLibrary.prefabs.Count)];
            if (prefab == null) continue;

            // Random rotation and optional scale jitter
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Transform parent = sceneryParent != null ? sceneryParent : transform;
            GameObject instance = Instantiate(prefab, pos, rot, parent);

            // Optional: random slight scale variation for organic feel
            float scaleJitter = Random.Range(0.9f, 1.1f);
            instance.transform.localScale *= scaleJitter;

        }
    }
}
*/