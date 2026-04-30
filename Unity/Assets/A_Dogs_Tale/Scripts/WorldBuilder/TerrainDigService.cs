using UnityEngine;

public static class TerrainDigService
{
    private const string FloorArchetypeId = "Floor";
    private const string HoleArchetypeId = "PF_Floor_Hole";
    private const string MoundArchetypeId = "PF_Floor_Mound";
    private const float DigTileYOffset = 0.43f;

    public static bool TryDigAt(WorldObject actor)
    {
        if (actor == null)
            return false;

        Dir dir = actor.dir != null ? actor.dir : Dir.Instance;
        if (dir == null || dir.gen == null || dir.elementStore == null || dir.manufactureGO == null)
        {
            Debug.LogWarning("[TerrainDigService] Missing Dir, DungeonGenerator, ElementStore, or ManufactureGO.");
            return false;
        }

        Vector3 mapPos = actor.pos3d_map;
        Vector2Int cellCoord = new Vector2Int(Mathf.FloorToInt(mapPos.x), Mathf.FloorToInt(mapPos.z));
        if (!dir.gen.In(cellCoord.x, cellCoord.y) || dir.gen.cellGrid[cellCoord.x, cellCoord.y] == null)
        {
            Debug.LogWarning($"[TerrainDigService] No floor cell at {cellCoord}.");
            return false;
        }

        Cell cell = dir.gen.cellGrid[cellCoord.x, cellCoord.y];
        EnsureDigArchetype(dir.elementStore, HoleArchetypeId, "Prefabs/Terrain/PF_Floor_Hole");
        EnsureDigArchetype(dir.elementStore, MoundArchetypeId, "Prefabs/Terrain/PF_Floor_Mound");

        ElementLayer floorLayer = dir.elementStore.GetLayer(ElementLayerKind.Floor);
        if (floorLayer == null || floorLayer.instances == null)
        {
            Debug.LogWarning("[TerrainDigService] No floor layer exists.");
            return false;
        }

        if (!TryFindFloorInstance(floorLayer, cell, out int index, out ElementInstanceData floorInst, out int width, out int height))
        {
            AddDigTile(floorLayer, cell, HoleArchetypeId, dir);
            dir.manufactureGO.BuildAll();
            return true;
        }

        string nextArchetype = string.Equals(floorInst.archetypeId, HoleArchetypeId)
            ? MoundArchetypeId
            : HoleArchetypeId;

        if (width == 1 && height == 1 && floorInst.cellCoord == cellCoord)
        {
            floorInst.archetypeId = nextArchetype;
            floorInst.position = DigTilePosition(dir, cell.pos, cell.height);
            floorInst.dirtyFlags = ElementUpdateFlags.All | ElementUpdateFlags.Color;
            floorLayer.instances[index] = floorInst;
        }
        else
        {
            SplitMergedFloorAroundCell(floorLayer, index, floorInst, width, height, cell, nextArchetype, dir);
        }

        dir.manufactureGO.BuildAll();
        return true;
    }

    private static void EnsureDigArchetype(ElementStore store, string id, string resourcesPath)
    {
        if (store.GetArchetype(id) != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(resourcesPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[TerrainDigService] Could not load Resources/{resourcesPath}.");
            return;
        }

        store.archetypes ??= new System.Collections.Generic.List<ElementArchetype>();
        store.archetypes.Add(new ElementArchetype
        {
            id = id,
            displayName = id,
            kind = ElementLayerKind.Floor,
            prefab = prefab,
            defaultScale = Vector3.one,
            defaultColor = Color.white,
            renderFlags = ElementRenderFlags.ReceivesShadows | ElementRenderFlags.NavWalkable
        });
        store.BuildArchetypeLookup();
    }

    private static bool TryFindFloorInstance(
        ElementLayer floorLayer,
        Cell cell,
        out int index,
        out ElementInstanceData instance,
        out int width,
        out int height)
    {
        index = -1;
        instance = null;
        width = 1;
        height = 1;

        for (int i = 0; i < floorLayer.instances.Count; i++)
        {
            ElementInstanceData candidate = floorLayer.instances[i];
            if (candidate == null)
                continue;
            if (candidate.heightSteps != cell.height)
                continue;
            if (!CellIsCovered(candidate, cell.pos, out int candidateWidth, out int candidateHeight))
                continue;

            index = i;
            instance = candidate;
            width = candidateWidth;
            height = candidateHeight;
            return true;
        }

        return false;
    }

    private static bool CellIsCovered(ElementInstanceData instance, Vector2Int cell, out int width, out int height)
    {
        width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(instance.scale.x)));
        height = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(instance.scale.z)));

        Vector2Int min = instance.cellCoord;
        Vector2Int max = new Vector2Int(min.x + width - 1, min.y + height - 1);
        return cell.x >= min.x && cell.x <= max.x && cell.y >= min.y && cell.y <= max.y;
    }

    private static void SplitMergedFloorAroundCell(
        ElementLayer floorLayer,
        int sourceIndex,
        ElementInstanceData source,
        int width,
        int height,
        Cell digCell,
        string digArchetypeId,
        Dir dir)
    {
        floorLayer.instances.RemoveAt(sourceIndex);

        Vector2Int min = source.cellCoord;
        Vector2Int dig = digCell.pos;
        int leftWidth = dig.x - min.x;
        int rightWidth = min.x + width - dig.x - 1;
        int bottomHeight = dig.y - min.y;
        int topHeight = min.y + height - dig.y - 1;

        AddFloorRectIfAny(floorLayer, source, min, width, bottomHeight, dir);
        AddFloorRectIfAny(floorLayer, source, new Vector2Int(min.x, dig.y + 1), width, topHeight, dir);
        AddFloorRectIfAny(floorLayer, source, new Vector2Int(min.x, dig.y), leftWidth, 1, dir);
        AddFloorRectIfAny(floorLayer, source, new Vector2Int(dig.x + 1, dig.y), rightWidth, 1, dir);

        AddDigTile(floorLayer, digCell, digArchetypeId, dir, source);
    }

    private static void AddFloorRectIfAny(
        ElementLayer floorLayer,
        ElementInstanceData source,
        Vector2Int min,
        int width,
        int height,
        Dir dir)
    {
        if (width <= 0 || height <= 0)
            return;

        Vector3 minPosition = TilePosition(dir, min, source.heightSteps);
        Vector3 maxPosition = TilePosition(dir, new Vector2Int(min.x + width - 1, min.y + height - 1), source.heightSteps);
        Vector3 unitScale = new Vector3(
            source.scale.x / Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(source.scale.x))),
            source.scale.y,
            source.scale.z / Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(source.scale.z))));

        floorLayer.instances.Add(new ElementInstanceData(
            archetypeId: FloorArchetypeId,
            layerKind: ElementLayerKind.Floor,
            roomIndex: source.roomIndex,
            cellCoord: min,
            heightSteps: source.heightSteps,
            position: (minPosition + maxPosition) * 0.5f,
            rotation: source.rotation,
            scale: new Vector3(unitScale.x * width, source.scale.y, unitScale.z * height),
            color: source.color,
            textureOverride: source.textureOverride,
            customFlags: source.customFlags,
            customValue: source.customValue));
    }

    private static void AddDigTile(
        ElementLayer floorLayer,
        Cell cell,
        string archetypeId,
        Dir dir,
        ElementInstanceData source = null)
    {
        floorLayer.instances.Add(new ElementInstanceData(
            archetypeId: archetypeId,
            layerKind: ElementLayerKind.Floor,
            roomIndex: source != null ? source.roomIndex : cell.room_number,
            cellCoord: cell.pos,
            heightSteps: cell.height,
            position: DigTilePosition(dir, cell.pos, cell.height),
            rotation: source != null ? source.rotation : cell.tiltFloor,
            scale: Vector3.one,
            color: source != null ? source.color : cell.colorFloor,
            textureOverride: source?.textureOverride,
            customFlags: 0,
            customValue: 0f));
    }

    private static Vector3 TilePosition(Dir dir, Vector2Int coord, int heightSteps)
    {
        Vector3 world = dir.gen.grid != null
            ? dir.gen.grid.CellToWorld(new Vector3Int(coord.x, coord.y, 0))
            : new Vector3(coord.x, 0f, coord.y);

        float unitHeight = dir.cfg != null ? dir.cfg.unitHeight : 1f;
        return world + new Vector3(0f, heightSteps * unitHeight, 0f);
    }

    private static Vector3 DigTilePosition(Dir dir, Vector2Int coord, int heightSteps)
    {
        return TilePosition(dir, coord, heightSteps) + new Vector3(0f, DigTileYOffset, 0f);
    }
}
