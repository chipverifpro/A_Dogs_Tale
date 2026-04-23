using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    private readonly struct MergedSurfaceRect
    {
        public readonly int roomIndex;
        public readonly Vector2Int min;
        public readonly int width;
        public readonly int height;

        public MergedSurfaceRect(int roomIndex, Vector2Int min, int width, int height)
        {
            this.roomIndex = roomIndex;
            this.min = min;
            this.width = width;
            this.height = height;
        }
    }

    private void OptimizeFlatSurfaceTiles()
    {
        if (elementStore == null)
            return;

        if (mergeFlatSurfaceTiles)
        {
            int minArea = Mathf.Max(2, minMergedSurfaceArea);
            ElementLayer floorLayer = elementStore.GetLayer(ElementLayerKind.Floor);
            if (floorLayer != null && floorLayer.instances != null && floorLayer.instances.Count > 0)
            {
                List<MergedSurfaceRect> mergedFloorRects = MergeFlatSurfaceLayer(floorLayer, Quaternion.identity, minArea, requireRectFromFloors: null);

                ElementLayer ceilingLayer = elementStore.GetLayer(ElementLayerKind.Ceiling);
                if (ceilingLayer != null && ceilingLayer.instances != null && ceilingLayer.instances.Count > 0 && mergedFloorRects.Count > 0)
                    MergeFlatSurfaceLayer(ceilingLayer, Quaternion.Euler(90f, 0f, 0f), minArea, mergedFloorRects);
            }
        }

        if (mergeContinuousWalls)
            MergeContinuousWallRuns(Mathf.Max(2, minMergedWallLength));
    }

    private List<MergedSurfaceRect> MergeFlatSurfaceLayer(
        ElementLayer layer,
        Quaternion expectedRotation,
        int minArea,
        List<MergedSurfaceRect> requireRectFromFloors)
    {
        List<MergedSurfaceRect> mergedRects = new();
        if (layer == null || layer.instances == null || layer.instances.Count == 0)
            return mergedRects;

        List<ElementInstanceData> preserved = new();
        Dictionary<int, Dictionary<Vector2Int, ElementInstanceData>> candidatesByRoom = new();

        for (int i = 0; i < layer.instances.Count; i++)
        {
            ElementInstanceData inst = layer.instances[i];
            if (!CanMergeFlatSurfaceTile(inst, expectedRotation))
            {
                if (inst != null)
                    preserved.Add(inst);
                continue;
            }

            if (!candidatesByRoom.TryGetValue(inst.roomIndex, out Dictionary<Vector2Int, ElementInstanceData> byCoord))
            {
                byCoord = new Dictionary<Vector2Int, ElementInstanceData>();
                candidatesByRoom[inst.roomIndex] = byCoord;
            }

            byCoord[inst.cellCoord] = inst;
        }

        if (requireRectFromFloors == null)
        {
            foreach (KeyValuePair<int, Dictionary<Vector2Int, ElementInstanceData>> entry in candidatesByRoom)
            {
                MergeRoomSurfaceCandidates(entry.Key, entry.Value, preserved, mergedRects, minArea);
            }
        }
        else
        {
            Dictionary<int, List<MergedSurfaceRect>> rectsByRoom = new();
            for (int i = 0; i < requireRectFromFloors.Count; i++)
            {
                MergedSurfaceRect rect = requireRectFromFloors[i];
                if (!rectsByRoom.TryGetValue(rect.roomIndex, out List<MergedSurfaceRect> roomRects))
                {
                    roomRects = new List<MergedSurfaceRect>();
                    rectsByRoom[rect.roomIndex] = roomRects;
                }
                roomRects.Add(rect);
            }

            foreach (KeyValuePair<int, Dictionary<Vector2Int, ElementInstanceData>> entry in candidatesByRoom)
            {
                MergeRoomCeilingCandidates(entry.Key, entry.Value, preserved, mergedRects, rectsByRoom);
            }
        }

        layer.instances = preserved;
        return mergedRects;
    }

    private void MergeRoomSurfaceCandidates(
        int roomIndex,
        Dictionary<Vector2Int, ElementInstanceData> candidates,
        List<ElementInstanceData> output,
        List<MergedSurfaceRect> mergedRects,
        int minArea)
    {
        List<Vector2Int> coords = new(candidates.Keys);
        coords.Sort((a, b) =>
        {
            int yCompare = a.y.CompareTo(b.y);
            return yCompare != 0 ? yCompare : a.x.CompareTo(b.x);
        });

        HashSet<Vector2Int> consumed = new();

        for (int i = 0; i < coords.Count; i++)
        {
            Vector2Int start = coords[i];
            if (consumed.Contains(start))
                continue;

            ElementInstanceData seed = candidates[start];
            int width = ComputeMergeWidth(start, seed, candidates, consumed);
            int height = ComputeMergeHeight(start, width, seed, candidates, consumed);
            int area = width * height;

            if (area >= minArea)
            {
                MarkRectConsumed(start, width, height, consumed);
                output.Add(CreateMergedSurfaceInstance(seed, start, width, height, candidates));
                mergedRects.Add(new MergedSurfaceRect(roomIndex, start, width, height));
            }
            else
            {
                consumed.Add(start);
                output.Add(seed);
            }
        }
    }

    private void MergeRoomCeilingCandidates(
        int roomIndex,
        Dictionary<Vector2Int, ElementInstanceData> candidates,
        List<ElementInstanceData> output,
        List<MergedSurfaceRect> mergedRects,
        Dictionary<int, List<MergedSurfaceRect>> rectsByRoom)
    {
        HashSet<Vector2Int> consumed = new();

        if (rectsByRoom.TryGetValue(roomIndex, out List<MergedSurfaceRect> roomRects))
        {
            for (int i = 0; i < roomRects.Count; i++)
            {
                MergedSurfaceRect rect = roomRects[i];
                if (!TryCreateMergedCeiling(rect, candidates, consumed, out ElementInstanceData merged))
                    continue;

                output.Add(merged);
                mergedRects.Add(rect);
            }
        }

        foreach (KeyValuePair<Vector2Int, ElementInstanceData> entry in candidates)
        {
            if (!consumed.Contains(entry.Key))
                output.Add(entry.Value);
        }
    }

    private bool TryCreateMergedCeiling(
        MergedSurfaceRect rect,
        Dictionary<Vector2Int, ElementInstanceData> candidates,
        HashSet<Vector2Int> consumed,
        out ElementInstanceData merged)
    {
        merged = null;
        if (!candidates.TryGetValue(rect.min, out ElementInstanceData seed))
            return false;

        for (int dz = 0; dz < rect.height; dz++)
        {
            for (int dx = 0; dx < rect.width; dx++)
            {
                Vector2Int coord = new Vector2Int(rect.min.x + dx, rect.min.y + dz);
                if (!candidates.TryGetValue(coord, out ElementInstanceData candidate))
                    return false;
                if (!AreSurfaceMergeCompatible(seed, candidate))
                    return false;
            }
        }

        MarkRectConsumed(rect.min, rect.width, rect.height, consumed);
        merged = CreateMergedSurfaceInstance(seed, rect.min, rect.width, rect.height, candidates);
        return true;
    }

    private int ComputeMergeWidth(
        Vector2Int start,
        ElementInstanceData seed,
        Dictionary<Vector2Int, ElementInstanceData> candidates,
        HashSet<Vector2Int> consumed)
    {
        int width = 1;
        while (true)
        {
            Vector2Int next = new Vector2Int(start.x + width, start.y);
            if (consumed.Contains(next))
                break;
            if (!candidates.TryGetValue(next, out ElementInstanceData candidate))
                break;
            if (!AreSurfaceMergeCompatible(seed, candidate))
                break;
            width++;
        }
        return width;
    }

    private int ComputeMergeHeight(
        Vector2Int start,
        int width,
        ElementInstanceData seed,
        Dictionary<Vector2Int, ElementInstanceData> candidates,
        HashSet<Vector2Int> consumed)
    {
        int height = 1;
        while (true)
        {
            int nextY = start.y + height;
            for (int dx = 0; dx < width; dx++)
            {
                Vector2Int coord = new Vector2Int(start.x + dx, nextY);
                if (consumed.Contains(coord))
                    return height;
                if (!candidates.TryGetValue(coord, out ElementInstanceData candidate))
                    return height;
                if (!AreSurfaceMergeCompatible(seed, candidate))
                    return height;
            }
            height++;
        }
    }

    private void MarkRectConsumed(Vector2Int start, int width, int height, HashSet<Vector2Int> consumed)
    {
        for (int dz = 0; dz < height; dz++)
            for (int dx = 0; dx < width; dx++)
                consumed.Add(new Vector2Int(start.x + dx, start.y + dz));
    }

    private ElementInstanceData CreateMergedSurfaceInstance(
        ElementInstanceData seed,
        Vector2Int min,
        int width,
        int height,
        Dictionary<Vector2Int, ElementInstanceData> candidates)
    {
        Vector2Int max = new Vector2Int(min.x + width - 1, min.y + height - 1);
        ElementInstanceData a = candidates[min];
        ElementInstanceData b = candidates[max];

        Vector3 center = (a.position + b.position) * 0.5f;
        Vector3 mergedScale = GetMergedSurfaceScale(seed, width, height);

        return new ElementInstanceData(
            archetypeId: seed.archetypeId,
            layerKind: seed.layerKind,
            roomIndex: seed.roomIndex,
            cellCoord: min,
            heightSteps: seed.heightSteps,
            position: center,
            rotation: seed.rotation,
            scale: mergedScale,
            color: seed.color,
            textureOverride: seed.textureOverride,
            customFlags: seed.customFlags,
            customValue: seed.customValue
        );
    }

    private bool CanMergeFlatSurfaceTile(ElementInstanceData inst, Quaternion expectedRotation)
    {
        if (inst == null)
            return false;
        if (inst.scale.x <= 0f || inst.scale.z <= 0f)
            return false;
        if (!QuaternionApproximately(inst.rotation, expectedRotation))
            return false;
        if (!Mathf.Approximately(inst.scale.x, 1f) || !Mathf.Approximately(inst.scale.z, 1f))
            return false;
        return true;
    }

    private bool AreSurfaceMergeCompatible(ElementInstanceData a, ElementInstanceData b)
    {
        if (a == null || b == null)
            return false;
        if (a.layerKind != b.layerKind)
            return false;
        if (a.roomIndex != b.roomIndex)
            return false;
        if (a.heightSteps != b.heightSteps)
            return false;
        if (!string.Equals(a.archetypeId, b.archetypeId))
            return false;
        if (!Color32Equals((Color32)a.color, (Color32)b.color))
            return false;
        if (a.textureOverride != b.textureOverride)
            return false;
        if (a.customFlags != b.customFlags)
            return false;
        if (!Mathf.Approximately(a.customValue, b.customValue))
            return false;
        if (!QuaternionApproximately(a.rotation, b.rotation))
            return false;
        if (!Mathf.Approximately(a.scale.x, b.scale.x) ||
            !Mathf.Approximately(a.scale.y, b.scale.y) ||
            !Mathf.Approximately(a.scale.z, b.scale.z))
            return false;
        return true;
    }

    private bool QuaternionApproximately(Quaternion a, Quaternion b)
    {
        return Mathf.Abs(Quaternion.Dot(a, b)) > 0.9999f;
    }

    private void MergeContinuousWallRuns(int minLength)
    {
        ElementLayer wallLayer = elementStore.GetLayer(ElementLayerKind.Wall);
        if (wallLayer == null || wallLayer.instances == null || wallLayer.instances.Count == 0)
            return;

        List<ElementInstanceData> preserved = new();
        Dictionary<WallRunKey, Dictionary<Vector2Int, ElementInstanceData>> candidatesByRun = new();

        for (int i = 0; i < wallLayer.instances.Count; i++)
        {
            ElementInstanceData inst = wallLayer.instances[i];
            if (!CanMergeWallSegment(inst, out WallOrientation orientation))
            {
                if (inst != null)
                    preserved.Add(inst);
                continue;
            }

            WallRunKey key = BuildWallRunKey(inst, orientation);
            if (!candidatesByRun.TryGetValue(key, out Dictionary<Vector2Int, ElementInstanceData> run))
            {
                run = new Dictionary<Vector2Int, ElementInstanceData>();
                candidatesByRun[key] = run;
            }

            run[inst.cellCoord] = inst;
        }

        foreach (KeyValuePair<WallRunKey, Dictionary<Vector2Int, ElementInstanceData>> entry in candidatesByRun)
            MergeWallRunCandidates(entry.Key, entry.Value, preserved, minLength);

        wallLayer.instances = preserved;
    }

    private void MergeWallRunCandidates(
        WallRunKey key,
        Dictionary<Vector2Int, ElementInstanceData> candidates,
        List<ElementInstanceData> output,
        int minLength)
    {
        List<Vector2Int> coords = new(candidates.Keys);
        coords.Sort((a, b) => key.orientation == WallOrientation.AlongX
            ? a.x.CompareTo(b.x)
            : a.y.CompareTo(b.y));

        int index = 0;
        while (index < coords.Count)
        {
            Vector2Int start = coords[index];
            ElementInstanceData seed = candidates[start];

            int runLength = 1;
            while (index + runLength < coords.Count)
            {
                Vector2Int prev = coords[index + runLength - 1];
                Vector2Int next = coords[index + runLength];
                if (!AreWallNeighborsInRun(prev, next, key.orientation))
                    break;

                ElementInstanceData candidate = candidates[next];
                if (!AreSurfaceMergeCompatible(seed, candidate))
                    break;

                runLength++;
            }

            if (runLength >= minLength)
            {
                Vector2Int end = coords[index + runLength - 1];
                output.Add(CreateMergedWallInstance(seed, start, end, runLength, candidates));
            }
            else
            {
                for (int i = 0; i < runLength; i++)
                    output.Add(candidates[coords[index + i]]);
            }

            index += runLength;
        }
    }

    private ElementInstanceData CreateMergedWallInstance(
        ElementInstanceData seed,
        Vector2Int start,
        Vector2Int end,
        int runLength,
        Dictionary<Vector2Int, ElementInstanceData> candidates)
    {
        ElementInstanceData a = candidates[start];
        ElementInstanceData b = candidates[end];
        Vector3 center = (a.position + b.position) * 0.5f;
        Vector3 mergedScale = new Vector3(seed.scale.x * runLength, seed.scale.y, seed.scale.z);

        return new ElementInstanceData(
            archetypeId: seed.archetypeId,
            layerKind: seed.layerKind,
            roomIndex: seed.roomIndex,
            cellCoord: start,
            heightSteps: seed.heightSteps,
            position: center,
            rotation: seed.rotation,
            scale: mergedScale,
            color: seed.color,
            textureOverride: seed.textureOverride,
            customFlags: seed.customFlags,
            customValue: seed.customValue
        );
    }

    private bool CanMergeWallSegment(ElementInstanceData inst, out WallOrientation orientation)
    {
        orientation = default;
        if (inst == null)
            return false;
        if (inst.layerKind != ElementLayerKind.Wall)
            return false;
        if (inst.customFlags != 0)
            return false;
        if (inst.scale.x <= 0f || inst.scale.y <= 0f || inst.scale.z <= 0f)
            return false;

        if (QuaternionApproximately(inst.rotation, Quaternion.Euler(0f, 0f, 0f)) ||
            QuaternionApproximately(inst.rotation, Quaternion.Euler(0f, 180f, 0f)))
        {
            orientation = WallOrientation.AlongX;
            return true;
        }

        if (QuaternionApproximately(inst.rotation, Quaternion.Euler(0f, 90f, 0f)) ||
            QuaternionApproximately(inst.rotation, Quaternion.Euler(0f, 270f, 0f)))
        {
            orientation = WallOrientation.AlongY;
            return true;
        }

        return false;
    }

    private WallRunKey BuildWallRunKey(ElementInstanceData inst, WallOrientation orientation)
    {
        int fixedAxis = orientation == WallOrientation.AlongX ? inst.cellCoord.y : inst.cellCoord.x;
        int yawBucket = RotationYawBucket(inst.rotation);
        return new WallRunKey(inst.roomIndex, orientation, fixedAxis, inst.heightSteps, yawBucket, inst.archetypeId, inst.color, inst.textureOverride, inst.customFlags, inst.customValue, inst.scale.y, inst.scale.z);
    }

    private bool AreWallNeighborsInRun(Vector2Int a, Vector2Int b, WallOrientation orientation)
    {
        if (orientation == WallOrientation.AlongX)
            return b.y == a.y && b.x == a.x + 1;

        return b.x == a.x && b.y == a.y + 1;
    }

    private int RotationYawBucket(Quaternion rotation)
    {
        return Mathf.RoundToInt(rotation.eulerAngles.y) % 360;
    }

    private Vector3 GetMergedSurfaceScale(ElementInstanceData seed, int width, int height)
    {
        if (seed.layerKind == ElementLayerKind.Ceiling)
        {
            // Ceiling tiles are authored with a 90-degree X rotation, so their
            // horizontal plane maps to local X/Y rather than X/Z.
            return new Vector3(seed.scale.x * width, seed.scale.y * height, seed.scale.z);
        }

        return new Vector3(seed.scale.x * width, seed.scale.y, seed.scale.z * height);
    }

    private bool Color32Equals(Color32 a, Color32 b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }

    private enum WallOrientation
    {
        AlongX,
        AlongY
    }

    private readonly struct WallRunKey
    {
        public readonly int roomIndex;
        public readonly WallOrientation orientation;
        public readonly int fixedAxis;
        public readonly int heightSteps;
        public readonly int yawBucket;
        public readonly string archetypeId;
        public readonly Color32 color;
        public readonly Texture textureOverride;
        public readonly int customFlags;
        public readonly float customValue;
        public readonly float scaleY;
        public readonly float scaleZ;

        public WallRunKey(
            int roomIndex,
            WallOrientation orientation,
            int fixedAxis,
            int heightSteps,
            int yawBucket,
            string archetypeId,
            Color color,
            Texture textureOverride,
            int customFlags,
            float customValue,
            float scaleY,
            float scaleZ)
        {
            this.roomIndex = roomIndex;
            this.orientation = orientation;
            this.fixedAxis = fixedAxis;
            this.heightSteps = heightSteps;
            this.yawBucket = yawBucket;
            this.archetypeId = archetypeId;
            this.color = color;
            this.textureOverride = textureOverride;
            this.customFlags = customFlags;
            this.customValue = customValue;
            this.scaleY = scaleY;
            this.scaleZ = scaleZ;
        }
    }
}
