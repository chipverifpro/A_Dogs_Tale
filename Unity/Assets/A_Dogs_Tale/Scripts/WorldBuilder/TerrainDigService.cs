using System.Collections.Generic;
using UnityEngine;

public static class TerrainDigService
{
    private const string FloorArchetypeId = "Floor";
    private const string HoleArchetypeId = "PF_Floor_Hole";
    private const string MoundArchetypeId = "PF_Floor_Mound";
    private const float FloorVisualYOffset = -0.5f;

    private static readonly Dictionary<BuriedObjectKey, WorldObject> buriedObjectsByCell = new();

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

        ElementLayer floorLayer = GetOrCreateFloorLayer(dir.elementStore);
        if (floorLayer == null || floorLayer.instances == null)
        {
            Debug.LogWarning("[TerrainDigService] Could not create floor layer.");
            return false;
        }
        bool floorLayerUsesFloorKind = floorLayer.kind == ElementLayerKind.Floor;

        ElementLayer triangleFloorLayer = dir.elementStore.GetLayer(ElementLayerKind.TriangleFloor);

        if (!TryFindFloorInstance(floorLayer, cell, out int index, out ElementInstanceData floorInst, out int width, out int height))
        {
            RemoveFloorSurfacesCoveringCell(triangleFloorLayer, ElementLayerKind.TriangleFloor, cell, dir);
            AddDigTile(floorLayer, cell, HoleArchetypeId, dir);
            dir.manufactureGO.BuildAll();
            return true;
        }

        string nextArchetype = string.Equals(floorInst.archetypeId, HoleArchetypeId)
            ? MoundArchetypeId
            : HoleArchetypeId;

        bool needsFullRebuild = true;
        if (width == 1 && height == 1 && floorInst.cellCoord == cellCoord)
        {
            floorInst.archetypeId = nextArchetype;
            floorInst.dirtyFlags = ElementUpdateFlags.All | ElementUpdateFlags.Color;
            floorLayer.instances[index] = floorInst;
            if (floorLayerUsesFloorKind)
            {
                dir.manufactureGO.RebuildInstance(ElementLayerKind.Floor, index);
                dir.manufactureGO.SetManufacturedInstanceActive(
                    ElementLayerKind.Floor,
                    IsPlainFloorInstanceCoveringCell,
                    cell,
                    false);
                needsFullRebuild = false;
            }
            RemoveFloorSurfacesCoveringCell(floorLayer, ElementLayerKind.Floor, cell, dir, preserveIndex: index);
        }
        else
        {
            if (floorLayerUsesFloorKind)
                dir.manufactureGO.SetManufacturedInstanceActive(ElementLayerKind.Floor, index, false);
            SplitMergedFloorAroundCell(floorLayer, index, floorInst, width, height, cell, nextArchetype, dir);
        }

        if (RemoveFloorSurfacesCoveringCell(triangleFloorLayer, ElementLayerKind.TriangleFloor, cell, dir))
            needsFullRebuild = true;

        if (string.Equals(nextArchetype, MoundArchetypeId))
            TryBuryGroundObject(actor, cell);
        else if (string.Equals(nextArchetype, HoleArchetypeId))
            TryRevealBuriedObject(cell);

        if (needsFullRebuild)
            dir.manufactureGO.BuildAll();
        return true;
    }

    private static ElementLayer GetOrCreateFloorLayer(ElementStore store)
    {
        if (store == null)
            return null;

        ElementLayer layer = store.GetLayer(ElementLayerKind.Floor);
        if (layer != null)
        {
            layer.instances ??= new List<ElementInstanceData>();
            return layer;
        }

        if (store.layers != null)
        {
            for (int i = 0; i < store.layers.Count; i++)
            {
                ElementLayer candidateLayer = store.layers[i];
                if (candidateLayer == null || candidateLayer.instances == null)
                    continue;

                for (int j = 0; j < candidateLayer.instances.Count; j++)
                {
                    ElementInstanceData instance = candidateLayer.instances[j];
                    if (instance == null)
                        continue;

                    if (instance.layerKind == ElementLayerKind.Floor)
                        return candidateLayer;
                }
            }
        }

        store.layers ??= new List<ElementLayer>();
        layer = new ElementLayer
        {
            name = "FloorTile",
            kind = ElementLayerKind.Floor,
            instances = new List<ElementInstanceData>()
        };
        store.layers.Add(layer);
        return layer;
    }

    private static bool TryBuryGroundObject(WorldObject actor, Cell cell)
    {
        BuriedObjectKey key = BuriedObjectKey.FromCell(cell);
        if (buriedObjectsByCell.TryGetValue(key, out WorldObject existing) && existing != null)
            return false;

        if (!TryFindGroundObjectToBury(actor, cell, out WorldObject target))
            return false;

        buriedObjectsByCell[key] = target;
        target.gameObject.SetActive(false);
        BottomBanner.Show($"{target.DisplayName} was buried.");
        return true;
    }

    private static bool TryRevealBuriedObject(Cell cell)
    {
        BuriedObjectKey key = BuriedObjectKey.FromCell(cell);
        if (!buriedObjectsByCell.TryGetValue(key, out WorldObject target))
            return false;

        buriedObjectsByCell.Remove(key);
        if (target == null)
            return false;

        target.gameObject.SetActive(true);
        BottomBanner.Show($"{target.DisplayName} was found.");
        return true;
    }

    private static bool TryFindGroundObjectToBury(WorldObject actor, Cell cell, out WorldObject target)
    {
        target = null;

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (registry == null)
            return false;

        float nearestDistanceSqr = float.PositiveInfinity;
        Vector3 cellCenter = cell.center3d_f;

        foreach (WorldObject candidate in registry.GetAllObjects())
        {
            if (!CanBuryCandidate(actor, candidate, cell))
                continue;

            Vector3 delta = candidate.pos3d_map - cellCenter;
            delta.y = 0f;
            float distanceSqr = delta.sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr)
                continue;

            nearestDistanceSqr = distanceSqr;
            target = candidate;
        }

        return target != null;
    }

    private static bool CanBuryCandidate(WorldObject actor, WorldObject candidate, Cell cell)
    {
        if (candidate == null || candidate == actor)
            return false;

        if (candidate.Kind != WorldObjectKind.Item && candidate.Kind != WorldObjectKind.Container)
            return false;

        if (!candidate.gameObject.activeInHierarchy)
            return false;

        if (candidate.transform.parent != null &&
            candidate.transform.parent.GetComponentInParent<WorldObject>() != null)
        {
            return false;
        }

        Vector3 candidatePos = candidate.pos3d_map;
        int x = Mathf.FloorToInt(candidatePos.x);
        int y = Mathf.FloorToInt(candidatePos.z);
        return x == cell.x && y == cell.y;
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
            if (candidate.layerKind != ElementLayerKind.Floor)
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

    private static bool IsPlainFloorInstanceCoveringCell(ElementInstanceData instance, Cell cell)
    {
        return instance != null &&
               cell != null &&
               string.Equals(instance.archetypeId, FloorArchetypeId) &&
               CellIsCovered(instance, cell.pos, out _, out _);
    }

    private static bool RemoveFloorSurfacesCoveringCell(
        ElementLayer layer,
        ElementLayerKind kind,
        Cell cell,
        Dir dir,
        int preserveIndex = -1)
    {
        if (layer == null || layer.instances == null || cell == null || dir == null || dir.manufactureGO == null)
            return false;

        bool removedAny = false;
        for (int i = 0; i < layer.instances.Count; i++)
        {
            if (i == preserveIndex)
                continue;

            ElementInstanceData instance = layer.instances[i];
            if (!IsFloorSurfaceInstanceCoveringCell(instance, cell))
                continue;

            layer.instances[i] = null;
            dir.manufactureGO.SetManufacturedInstanceActive(kind, i, false);
            removedAny = true;
        }

        return removedAny;
    }

    private static bool IsFloorSurfaceInstanceCoveringCell(ElementInstanceData instance, Cell cell)
    {
        return instance != null &&
               cell != null &&
               (string.Equals(instance.archetypeId, FloorArchetypeId) ||
                string.Equals(instance.archetypeId, "TriangleFloor")) &&
               CellIsCovered(instance, cell.pos, out _, out _);
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
        minPosition.y = source.position.y;
        maxPosition.y = source.position.y;
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
            position: source != null ? DigTilePositionFromSource(source, cell.pos) : DigTilePosition(dir, cell.pos, cell.height),
            rotation: source != null ? source.rotation : cell.tiltFloor,
            scale: source != null ? SingleTileScaleFromSource(source) : Vector3.one,
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
        return TilePosition(dir, coord, heightSteps) + new Vector3(0f, FloorVisualYOffset, 0f);
    }

    private static Vector3 DigTilePositionFromSource(ElementInstanceData source, Vector2Int coord)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(source.scale.x)));
        int height = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(source.scale.z)));
        Vector2Int min = source.cellCoord;
        float normalizedX = width > 1 ? (coord.x - min.x) / (float)(width - 1) : 0.5f;
        float normalizedZ = height > 1 ? (coord.y - min.y) / (float)(height - 1) : 0.5f;

        Vector3 minPosition = source.position - new Vector3((width - 1) * 0.5f, 0f, (height - 1) * 0.5f);
        Vector3 maxPosition = source.position + new Vector3((width - 1) * 0.5f, 0f, (height - 1) * 0.5f);
        return new Vector3(
            Mathf.Lerp(minPosition.x, maxPosition.x, normalizedX),
            source.position.y,
            Mathf.Lerp(minPosition.z, maxPosition.z, normalizedZ));
    }

    private static Vector3 SingleTileScaleFromSource(ElementInstanceData source)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(source.scale.x)));
        int height = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(source.scale.z)));
        return new Vector3(source.scale.x / width, source.scale.y, source.scale.z / height);
    }

    private readonly struct BuriedObjectKey
    {
        private readonly int x;
        private readonly int y;
        private readonly int height;

        private BuriedObjectKey(int x, int y, int height)
        {
            this.x = x;
            this.y = y;
            this.height = height;
        }

        public static BuriedObjectKey FromCell(Cell cell)
        {
            return new BuriedObjectKey(cell.x, cell.y, cell.height);
        }
    }
}
