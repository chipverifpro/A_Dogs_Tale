using System;
using System.Collections;
using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

public class ItemPlacer : MonoBehaviour
{
    [Header("Item Prefabs")]
    [Tooltip("Prefabs that have a PlacementModule and can be placed independently from furniture.")]
    public List<GameObject> itemPrefabs = new();

    [Header("Auto-Loaded Resource Prefabs")]
    [Tooltip("If enabled, all prefabs with PlacementModule under these Resources folders are merged into Item Prefabs before placement. Resources.LoadAll includes subfolders.")]
    public bool autoLoadResourcePrefabs = true;

    [Tooltip("Resources folders to scan for independently placeable item prefabs. Paths are relative to any Resources folder.")]
    public List<string> resourcePrefabFolders = new() { "Prefabs/Items" };

    [Header("Per-Room Counts")]
    [Tooltip("Minimum number of items per room.")]
    public int minPerRoom = 0;

    [Tooltip("Maximum number of items per room.")]
    public int maxPerRoom = 2;

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
    }

    private IEnumerator Start()
    {
        if (dir == null || dir.gen == null)
            yield break;

        if (!dir.gen.buildComplete)
            yield return new WaitUntil(() => dir.gen.buildComplete);

        PlaceAllItems();
    }

    public void PlaceAllItems()
    {
        if (dir == null || dir.gen == null)
        {
            Debug.LogError("ItemPlacer: missing ObjectDirectory or DungeonGenerator.", this);
            return;
        }

        if (dir.gen.rooms == null || dir.gen.rooms.Count == 0)
        {
            Debug.LogWarning("ItemPlacer: No rooms available in generator.", this);
            return;
        }

        placedPrefabKeys.Clear();
        MergeResourcePrefabs();

        if (itemPrefabs != null && itemPrefabs.Count > 0)
        {
            foreach (Room room in dir.gen.rooms)
            {
                if (!IsUsablePlacementRoom(room))
                    continue;

                PlaceItemsInRoom(room);
            }
        }
        else
        {
            Debug.Log("ItemPlacer: No random item prefabs assigned; only required items will be placed.", this);
        }

        PlaceRequiredItems();
    }

    private void MergeResourcePrefabs()
    {
        if (!autoLoadResourcePrefabs)
            return;

        if (itemPrefabs == null)
            itemPrefabs = new();

        int addedCount = GeneratedObjectPlacementUtility.MergePlaceableResourcePrefabs(
            itemPrefabs,
            resourcePrefabFolders);

        if (addedCount > 0)
            Debug.Log($"ItemPlacer: auto-loaded {addedCount} placeable prefab(s) from Resources.", this);
    }

    private void PlaceItemsInRoom(Room room)
    {
        List<GameObject> compatible = new();

        foreach (GameObject prefab in itemPrefabs)
        {
            if (prefab == null)
                continue;

            PlacementModule placement = prefab.GetComponentInChildren<PlacementModule>();
            if (placement == null)
                continue;

            if (ItemPlacementOverrides.AllowsRoom(prefab, placement, room.placementTypes))
                compatible.Add(prefab);
        }

        if (compatible.Count == 0)
            return;

        int countToPlace = UnityEngine.Random.Range(minPerRoom, maxPerRoom + 1);
        for (int i = 0; i < countToPlace; i++)
        {
            GameObject prefab = compatible[UnityEngine.Random.Range(0, compatible.Count)];
            PlacementModule placement = prefab.GetComponentInChildren<PlacementModule>();
            if (placement == null)
                continue;

            TryPlaceOne(room, prefab, placement);
        }
    }

    private void PlaceRequiredItems()
    {
        foreach (KeyValuePair<string, ItemPlacementOverrides.ItemPlaceRule> entry in ItemPlacementOverrides.MustPlaceRules)
        {
            string prefabKey = entry.Key;
            if (string.IsNullOrWhiteSpace(prefabKey) || HasPlacedPrefab(prefabKey))
                continue;

            GameObject prefab = FindPlacementPrefabByKey(prefabKey);
            if (prefab == null)
            {
                Debug.LogWarning($"ItemPlacer: must-place item '{prefabKey}' could not be found in itemPrefabs or item Resources.", this);
                continue;
            }

            PlacementModule placement = prefab.GetComponentInChildren<PlacementModule>();
            if (placement == null)
            {
                Debug.LogWarning($"ItemPlacer: must-place item '{prefabKey}' has no PlacementModule.", prefab);
                continue;
            }

            if (TryPlaceRequiredItemInAllowedRoom(prefab, placement))
                continue;

            if (TryPlaceRequiredItemInAnyRoom(prefab, placement))
                continue;

            Debug.LogWarning($"ItemPlacer: failed to place required item '{prefabKey}'.", this);
        }
    }

    private bool TryPlaceRequiredItemInAllowedRoom(GameObject prefab, PlacementModule placement)
    {
        foreach (Room room in dir.gen.rooms)
        {
            if (!IsUsablePlacementRoom(room))
                continue;

            if (!ItemPlacementOverrides.AllowsRoom(prefab, placement, room.placementTypes))
                continue;

            if (TryPlaceOne(room, prefab, placement))
                return true;
        }

        return false;
    }

    private bool TryPlaceRequiredItemInAnyRoom(GameObject prefab, PlacementModule placement)
    {
        foreach (Room room in dir.gen.rooms)
        {
            if (!IsUsablePlacementRoom(room))
                continue;

            if (TryPlaceOne(room, prefab, placement))
                return true;
        }

        return false;
    }

    private bool TryPlaceOne(Room room, GameObject prefab, PlacementModule placement)
    {
        if (!GeneratedObjectPlacementUtility.TryPlaceOne(room, prefab, placement, baseYOffset, maxAttemptsPerItem, out GameObject instance))
            return false;

        ItemPlacementOverrides.ApplyToInstance(instance, prefab.name);
        RecordPlacedPrefab(prefab);
        return true;
    }

    private GameObject FindPlacementPrefabByKey(string prefabKey)
    {
        GameObject prefab = FindPrefabByKey(itemPrefabs, prefabKey);
        if (prefab != null)
            return prefab;

        return Resources.Load<GameObject>($"Prefabs/Items/{prefabKey}")
            ?? Resources.Load<GameObject>($"Prefabs/Items/HomeInterior/{prefabKey}")
            ?? Resources.Load<GameObject>($"Prefabs/Items/ChatGPT_Items/{prefabKey}")
            ?? Resources.Load<GameObject>($"Prefabs/ChatGPT_Prefabs/ChatGPT_Items/{prefabKey}")
            ?? FindResourcePrefabByKey("Prefabs/Items", prefabKey)
            ?? FindResourcePrefabByKey("Prefabs/Items/HomeInterior", prefabKey)
            ?? FindResourcePrefabByKey("Prefabs/Items/ChatGPT_Items", prefabKey)
            ?? FindResourcePrefabByKey("Prefabs/ChatGPT_Prefabs/ChatGPT_Items", prefabKey);
    }

    private static GameObject FindPrefabByKey(List<GameObject> prefabs, string prefabKey)
    {
        if (prefabs == null)
            return null;

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null &&
                string.Equals(ItemPlacementOverrides.GetPrefabKey(prefab), prefabKey, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    private static GameObject FindResourcePrefabByKey(string resourcesPath, string prefabKey)
    {
        GameObject[] prefabs = Resources.LoadAll<GameObject>(resourcesPath);
        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null &&
                string.Equals(ItemPlacementOverrides.GetPrefabKey(prefab), prefabKey, StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    private bool HasPlacedPrefab(string prefabKey)
    {
        if (placedPrefabKeys.Contains(prefabKey))
            return true;

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (registry == null)
            return false;

        foreach (WorldObject worldObject in registry.GetAllObjects())
        {
            if (worldObject == null)
                continue;

            if (string.Equals(ItemPlacementOverrides.GetPrefabKey(worldObject.gameObject), prefabKey, StringComparison.OrdinalIgnoreCase))
                return true;

            SavePrefabId savePrefabId = worldObject.GetComponent<SavePrefabId>();
            if (savePrefabId == null)
                continue;

            if (string.Equals(ItemPlacementOverrides.GetPrefabKey(savePrefabId.PrefabId), prefabKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ItemPlacementOverrides.GetPrefabKey(savePrefabId.ResourcesPath), prefabKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ItemPlacementOverrides.GetPrefabKey(savePrefabId.AssetPath), prefabKey, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void RecordPlacedPrefab(GameObject prefab)
    {
        string prefabKey = ItemPlacementOverrides.GetPrefabKey(prefab);
        if (!string.IsNullOrWhiteSpace(prefabKey))
            placedPrefabKeys.Add(prefabKey);
    }

    private static bool IsUsablePlacementRoom(Room room)
    {
        return room != null && room.cells != null && room.cells.Count > 0;
    }
}
