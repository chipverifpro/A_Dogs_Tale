using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

public static class GeneratedObjectPlacementUtility
{
    public static bool TryPlaceOne(
        Room room,
        GameObject prefab,
        PlacementModule placementTemplate,
        float baseYOffset,
        int maxAttempts,
        out GameObject instance)
    {
        instance = null;

        if (room == null || room.cells == null || room.cells.Count == 0 ||
            prefab == null || placementTemplate == null)
        {
            return false;
        }

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (!PickCellForPlacement(room, placementTemplate, out Cell cell, out DirFlags wallDir))
                continue;

            Vector3 worldPos = placementTemplate.ComputeBaseWorldPosition(cell, baseYOffset);
            Quaternion rot = placementTemplate.ChooseRotation(wallDir);

            instance = Object.Instantiate(prefab, worldPos, rot);
            InitializeWorldObject(instance, cell, baseYOffset);

            PlacementModule instPlacement = instance.GetComponentInChildren<PlacementModule>();
            if (instPlacement != null)
                instPlacement.ApplyPlacement(cell, wallDir, baseYOffset);

            return true;
        }

        return false;
    }

    public static void InitializeWorldObject(GameObject instance, Cell cell, float baseYOffset)
    {
        if (instance == null || cell == null)
            return;

        WorldObject worldObject = instance.GetComponent<WorldObject>();
        if (worldObject == null)
            worldObject = instance.AddComponent<WorldObject>();

        LocationModule location = instance.GetComponent<LocationModule>();
        if (location == null)
            location = instance.AddComponent<LocationModule>();

        VisionPerceptionModule visual = instance.GetComponent<VisionPerceptionModule>();
        if (visual == null)
            visual = instance.AddComponent<VisionPerceptionModule>();

        instance.transform.position = cell.pos3d_world + new Vector3(0f, baseYOffset, 0f);
        worldObject.RegisterIfNeeded();
    }

    public static bool PickCellForPlacement(
        Room room,
        PlacementModule placement,
        out Cell chosenCell,
        out DirFlags chosenWallDir)
    {
        chosenCell = null;
        chosenWallDir = DirFlags.None;

        List<Cell> cells = room != null ? room.cells : null;
        if (cells == null || cells.Count == 0 || placement == null)
            return false;

        const int maxCellTries = 50;

        for (int attempt = 0; attempt < maxCellTries; attempt++)
        {
            Cell cell = cells[Random.Range(0, cells.Count)];
            if (cell == null)
                continue;

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

    public static DirFlags PickOneDirFlag(DirFlags flags)
    {
        List<DirFlags> candidates = new(4);
        foreach (DirFlags d in DirFlagsEx.AllCardinals)
        {
            if ((flags & d) != 0)
                candidates.Add(d);
        }

        if (candidates.Count == 0)
            return DirFlags.None;

        return candidates[Random.Range(0, candidates.Count)];
    }
}
