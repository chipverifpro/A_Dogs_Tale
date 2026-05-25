using UnityEngine;

public partial class DungeonGenerator
{
    public sealed partial class Heightfield
    {
        /// <summary>
        /// Query the neighbor column (x,y) at a target z. Returns true if a neighbor is present
        /// within [z - threshold, z + threshold]. Populates match with representative info.
        /// </summary>
        public bool TryQueryAt(int x, int y, int z, int threshold, out NeighborMatch match)
        {
            match = default;
            if (!InBounds(x, y, width, height)) return false;
            var c = cols[x, y];

            switch (c.kind)
            {
                case ColumnKind.Empty:
                    return false;

                case ColumnKind.Single:
                    if (Mathf.Abs(c.zSingle - z) <= threshold)
                    {
                        match.z = c.zSingle;
                        match.roomId = c.roomIdSingle;
                        match.cellId = c.cellIdSingle;
                        match.segmentIndex = -1;
                        match.isSegment = false;
                        return true;
                    }
                    return false;

                case ColumnKind.SmallVec:
                {
                    int n = c.smallCount;
                    if (n == 0) return false;
                    int idx = LowerBound(c.zInline, n, z);
                    int bestIdx = -1;
                    int bestDelta = int.MaxValue;
                    for (int t = -1; t <= 1; ++t)
                    {
                        int i = idx + t;
                        if (i < 0 || i >= n) continue;
                        int delta = Mathf.Abs(c.zInline[i] - z);
                        if (delta < bestDelta)
                        {
                            bestDelta = delta;
                            bestIdx = i;
                        }
                    }
                    if (bestIdx >= 0 && bestDelta <= threshold)
                    {
                        match.z = c.zInline[bestIdx];
                        match.roomId = c.roomInline[bestIdx];
                        match.cellId = c.cellInline[bestIdx];
                        match.segmentIndex = -1;
                        match.isSegment = false;
                        return true;
                    }
                    return false;
                }

                case ColumnKind.SegmentVec:
                {
                    int n = c.segCount;
                    if (n == 0) return false;
                    int i = LowerBound(c.zLo, n, z);
                    if (i < n && z >= c.zLo[i] - threshold && z <= c.zHi[i] + threshold)
                    {
                        match.z = Mathf.Clamp(z, c.zLo[i], c.zHi[i]);
                        match.roomId = (c.segRoomCounts[i] > 0) ? c.segRoomIds[i, 0] : -1;
                        match.cellId = (c.segCellCounts[i] > 0) ? c.segCellIds[i, 0] : -1;
                        match.segmentIndex = i;
                        match.isSegment = true;
                        return true;
                    }
                    if (i > 0 && z >= c.zLo[i - 1] - threshold && z <= c.zHi[i - 1] + threshold)
                    {
                        int j = i - 1;
                        match.z = Mathf.Clamp(z, c.zLo[j], c.zHi[j]);
                        match.roomId = (c.segRoomCounts[j] > 0) ? c.segRoomIds[j, 0] : -1;
                        match.cellId = (c.segCellCounts[j] > 0) ? c.segCellIds[j, 0] : -1;
                        match.segmentIndex = j;
                        match.isSegment = true;
                        return true;
                    }
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Convenience: 4-neighborhood check around (x,y) at height z. Returns true if any neighbor column
        /// has a floor within threshold (e.g., cfg.minRoomHeight).
        /// </summary>
        public bool HasAdjacentWithinThreshold_experiment(int x, int y, int z, int threshold, out NeighborMatch match)
        {
            bool hasAdjacent = false;
            DirFlags wallDirs = DirFlags.None;
            match = default;
            if (TryQueryAt(x - 1, y, z, threshold, out match)) wallDirs |= DirFlags.W;
            if (TryQueryAt(x + 1, y, z, threshold, out match)) wallDirs |= DirFlags.E;
            if (TryQueryAt(x, y - 1, z, threshold, out match)) wallDirs |= DirFlags.S;
            if (TryQueryAt(x, y + 1, z, threshold, out match)) wallDirs |= DirFlags.N;
            hasAdjacent = wallDirs == DirFlags.None;
            return hasAdjacent;
        }

        public bool HasAdjacentWithinThreshold(int x, int y, int z, int threshold, out NeighborMatch match)
        {
            if (TryQueryAt(x - 1, y, z, threshold, out match)) return true;
            if (TryQueryAt(x + 1, y, z, threshold, out match)) return true;
            if (TryQueryAt(x, y - 1, z, threshold, out match)) return true;
            if (TryQueryAt(x, y + 1, z, threshold, out match)) return true;
            match = default;
            return false;
        }
    }
}
