using UnityEngine;
using System.Collections.Generic;

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