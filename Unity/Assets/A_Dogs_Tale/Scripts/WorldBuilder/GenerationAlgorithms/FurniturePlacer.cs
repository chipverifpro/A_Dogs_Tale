using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public partial class FurniturePlacer : MonoBehaviour
{
    [Header("Furniture Prefabs")]
    [Tooltip("Prefabs that have a PlacementModule + WorldObject setup (or can have modules auto-added).")]
    public List<GameObject> furniturePrefabs = new();

    [Header("Auto-Loaded Resource Prefabs")]
    [Tooltip("If enabled, all prefabs with PlacementModule under these Resources folders are merged into Furniture Prefabs before placement. Resources.LoadAll includes subfolders.")]
    public bool autoLoadResourcePrefabs = true;

    [Tooltip("Resources folders to scan for static scenery prefabs. Paths are relative to any Resources folder.")]
    public List<string> resourcePrefabFolders = new() { "Prefabs/Scenery" };

    [Header("Per-Room Counts")]
    [Tooltip("Minimum number of furniture items per room.")]
    public int minPerRoom = 0;

    [Tooltip("Maximum number of furniture items per room.")]
    public int maxPerRoom = 3;

    [Tooltip("Max placement attempts per item before giving up for that item.")]
    public int maxAttemptsPerItem = 20;

    [Header("Placement Offsets")]
    [Tooltip("Extra Y offset above the cell's world position for placement.")]
    public float baseYOffset = 0f;

    private Dir dir;
    private readonly HashSet<string> placedPrefabKeys = new(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        dir = Dir.Instance;
        if (dir == null)
        {
            Debug.LogError("FurniturePlacer: ObjectDirectory.Instance is null. Cannot place furniture.", this);
        }
    }

    private IEnumerator Start()
    {
        if (dir == null || dir.gen == null)
            yield break;

        // Wait for dungeon build to complete before placing furniture
        if (!dir.gen.buildComplete)
        {
            yield return new WaitUntil(() => dir.gen.buildComplete);
        }

        PlaceAllFurniture();
    }

    /// <summary>
    /// Entry point: place furniture in all rooms according to PlacementModule hints.
    /// </summary>
    public void PlaceAllFurniture()
    {
        if (dir == null || dir.gen == null)
        {
            Debug.LogError("FurniturePlacer: missing ObjectDirectory or DungeonGenerator.", this);
            return;
        }

        MergeResourcePrefabs();

        if (furniturePrefabs == null || furniturePrefabs.Count == 0)
        {
            Debug.LogWarning("FurniturePlacer: No furniture prefabs assigned.");
            return;
        }

        if (dir.gen.rooms == null || dir.gen.rooms.Count == 0)
        {
            Debug.LogWarning("FurniturePlacer: No rooms available in generator.");
            return;
        }

        placedPrefabKeys.Clear();

        foreach (var room in dir.gen.rooms)
        {
            if (room == null || room.cells == null || room.cells.Count == 0)
                continue;

            PlaceFurnitureInRoom(room);
        }

        // Required item placement is owned by ItemPlacer.
    }

    private void MergeResourcePrefabs()
    {
        if (!autoLoadResourcePrefabs)
            return;

        if (furniturePrefabs == null)
            furniturePrefabs = new();

        int addedCount = GeneratedObjectPlacementUtility.MergePlaceableResourcePrefabs(
            furniturePrefabs,
            resourcePrefabFolders);

        if (addedCount > 0)
            Debug.Log($"FurniturePlacer: auto-loaded {addedCount} placeable prefab(s) from Resources.", this);
    }

}
