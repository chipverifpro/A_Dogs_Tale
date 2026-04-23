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

            if (placement.AllowsRoom(room.placementTypes))
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

            return true;
        }

        return false;
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
