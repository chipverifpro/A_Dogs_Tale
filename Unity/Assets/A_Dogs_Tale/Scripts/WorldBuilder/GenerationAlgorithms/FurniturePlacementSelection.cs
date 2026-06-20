using System.Collections.Generic;
using UnityEngine;
using DogGame.Modules;

public partial class FurniturePlacer
{
    private void PlaceFurnitureInRoom(Room room)
    {
        var compatible = new List<GameObject>();

        foreach (var prefab in furniturePrefabs)
        {
            if (prefab == null) continue;

            var placement = prefab.GetComponentInChildren<PlacementModule>();
            if (placement == null) continue;

            if (ItemPlacementOverrides.AllowsRoom(prefab, placement, room.placementTypes))
                compatible.Add(prefab);
        }

        if (compatible.Count == 0)
        {
            Debug.Log($"FurniturePlacer: No compatible furniture for room {room.my_room_number} with types {room.placementTypes}.", this);
            return;
        }

        int countToPlace = Random.Range(minPerRoom, maxPerRoom + 1);
        if (countToPlace <= 0) return;

        for (int i = 0; i < countToPlace; i++)
        {
            var prefab = compatible[Random.Range(0, compatible.Count)];
            var placement = prefab.GetComponentInChildren<PlacementModule>();
            if (placement == null) continue;

            TryPlaceOne(room, prefab, placement);
        }
    }

    private bool TryPlaceOne(Room room, GameObject prefab, PlacementModule placementTemplate)
    {
        if (room.cells == null || room.cells.Count == 0)
            return false;

        for (int attempt = 0; attempt < maxAttemptsPerItem; attempt++)
        {
            if (!PickCellForPlacement(room, placementTemplate, out Cell cell, out DirFlags wallDir))
                continue;

            Vector3 worldPos = placementTemplate.ComputeBaseWorldPosition(cell, baseYOffset);
            Quaternion rot = placementTemplate.ChooseRotation(wallDir);

            GameObject instance = Instantiate(prefab, worldPos, rot);
            InitializeWorldObject(instance, cell);

            var instPlacement = instance.GetComponentInChildren<PlacementModule>();
            if (instPlacement != null)
                instPlacement.ApplyPlacement(cell, wallDir, baseYOffset);

            RecordPlacedPrefab(prefab);
            return true;
        }

        return false;
    }

    private void PlaceRequiredFurniture()
    {
        foreach (KeyValuePair<string, ItemPlacementOverrides.ItemPlaceRule> entry in ItemPlacementOverrides.MustPlaceRules)
        {
            string prefabKey = entry.Key;
            if (string.IsNullOrWhiteSpace(prefabKey) || HasPlacedPrefab(prefabKey))
                continue;

            GameObject prefab = FindPlacementPrefabByKey(prefabKey);
            if (prefab == null)
            {
                Debug.LogWarning($"FurniturePlacer: must-place prefab '{prefabKey}' could not be found in furniturePrefabs or Resources.", this);
                continue;
            }

            PlacementModule placement = prefab.GetComponentInChildren<PlacementModule>();
            if (placement == null)
            {
                Debug.LogWarning($"FurniturePlacer: must-place prefab '{prefabKey}' has no PlacementModule.", prefab);
                continue;
            }

            if (TryPlaceRequiredFurnitureInAllowedRoom(prefab, placement))
                continue;

            if (TryPlaceRequiredFurnitureInAnyRoom(prefab, placement))
                continue;

            Debug.LogWarning($"FurniturePlacer: failed to place required prefab '{prefabKey}'.", this);
        }
    }

    private bool TryPlaceRequiredFurnitureInAllowedRoom(GameObject prefab, PlacementModule placement)
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

    private bool TryPlaceRequiredFurnitureInAnyRoom(GameObject prefab, PlacementModule placement)
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

    private static bool IsUsablePlacementRoom(Room room)
    {
        return room != null && room.cells != null && room.cells.Count > 0;
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

            if (string.Equals(ItemPlacementOverrides.GetPrefabKey(worldObject.gameObject), prefabKey, System.StringComparison.OrdinalIgnoreCase))
                return true;

            SavePrefabId savePrefabId = worldObject.GetComponent<SavePrefabId>();
            if (savePrefabId == null)
                continue;

            if (string.Equals(ItemPlacementOverrides.GetPrefabKey(savePrefabId.PrefabId), prefabKey, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ItemPlacementOverrides.GetPrefabKey(savePrefabId.ResourcesPath), prefabKey, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ItemPlacementOverrides.GetPrefabKey(savePrefabId.AssetPath), prefabKey, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private GameObject FindPlacementPrefabByKey(string prefabKey)
    {
        GameObject prefab = FindFurniturePrefabByKey(prefabKey);
        if (prefab != null)
            return prefab;

        prefab = Resources.Load<GameObject>($"Prefabs/Items/{prefabKey}");
        if (prefab != null)
            return prefab;

        prefab = Resources.Load<GameObject>($"Prefabs/Items/HomeInterior/{prefabKey}");
        if (prefab != null)
            return prefab;

        prefab = Resources.Load<GameObject>($"Prefabs/Scenery/{prefabKey}");
        if (prefab != null)
            return prefab;

        prefab = Resources.Load<GameObject>($"Prefabs/ChatGPT_Prefabs/ChatGPT_Items/{prefabKey}");
        if (prefab != null)
            return prefab;

        return FindResourcePrefabByKey("Prefabs/Items", prefabKey)
            ?? FindResourcePrefabByKey("Prefabs/Scenery", prefabKey)
            ?? FindResourcePrefabByKey("Prefabs/ChatGPT_Prefabs/ChatGPT_Items", prefabKey);
    }

    private GameObject FindFurniturePrefabByKey(string prefabKey)
    {
        if (furniturePrefabs == null)
            return null;

        for (int i = 0; i < furniturePrefabs.Count; i++)
        {
            GameObject prefab = furniturePrefabs[i];
            if (prefab != null &&
                string.Equals(ItemPlacementOverrides.GetPrefabKey(prefab), prefabKey, System.StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    private static GameObject FindResourcePrefabByKey(string resourcesPath, string prefabKey)
    {
        GameObject[] prefabs = Resources.LoadAll<GameObject>(resourcesPath);
        for (int i = 0; i < prefabs.Length; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab != null &&
                string.Equals(ItemPlacementOverrides.GetPrefabKey(prefab), prefabKey, System.StringComparison.OrdinalIgnoreCase))
            {
                return prefab;
            }
        }

        return null;
    }

    private void RecordPlacedPrefab(GameObject prefab)
    {
        string prefabKey = ItemPlacementOverrides.GetPrefabKey(prefab);
        if (!string.IsNullOrWhiteSpace(prefabKey))
            placedPrefabKeys.Add(prefabKey);
    }

    /// <summary>
    /// Simple heuristic-based cell selection depending on edgeHint.
    /// This version ignores sizeInCells and clearance; those can be layered on later.
    /// </summary>
    private bool PickCellForPlacement(Room room, PlacementModule placement, out Cell chosenCell, out DirFlags chosenWallDir)
    {
        chosenCell = null;
        chosenWallDir = DirFlags.None;

        var cells = room.cells;
        if (cells == null || cells.Count == 0)
            return false;

        const int maxCellTries = 50;

        for (int attempt = 0; attempt < maxCellTries; attempt++)
        {
            Cell cell = cells[Random.Range(0, cells.Count)];
            if (cell == null) continue;

            DirFlags wallDir = DirFlags.None;

            switch (placement.edgeHint)
            {
                case EdgeHint.Free:
                    break;

                case EdgeHint.NearWall:
                case EdgeHint.AgainstWall:
                    if (cell.walls == DirFlags.None)
                        continue;

                    wallDir = PickOneDirFlag(cell.walls);
                    if (wallDir == DirFlags.None)
                        continue;
                    break;

                case EdgeHint.InCorner:
                    if (DirFlagsEx.Count(cell.walls) < 2)
                        continue;

                    wallDir = cell.walls;
                    break;

                case EdgeHint.CenterOfRoom:
                    if (cell.walls != DirFlags.None)
                        continue;
                    break;
            }

            if (placement.mustTouchWall &&
                placement.edgeHint != EdgeHint.Free &&
                wallDir == DirFlags.None)
            {
                continue;
            }

            chosenCell = cell;
            chosenWallDir = wallDir;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Pick a single direction bit from a DirFlags bitfield (N,E,S,W).
    /// </summary>
    private DirFlags PickOneDirFlag(DirFlags flags)
    {
        var candidates = new List<DirFlags>(4);
        foreach (var d in DirFlagsEx.AllCardinals)
        {
            if ((flags & d) != 0)
                candidates.Add(d);
        }

        if (candidates.Count == 0)
            return DirFlags.None;

        return candidates[Random.Range(0, candidates.Count)];
    }
}
