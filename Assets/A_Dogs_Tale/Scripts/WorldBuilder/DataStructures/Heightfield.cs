// Heightfield.cs
// Drop-in columnar height map with overlap support and minRoomHeight merging.
// Unity-safe, no unsafe code, no external deps.

using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.CompilerServices;

public partial class DungeonGenerator : MonoBehaviour
{
    public static object Instance { get; internal set; }

    // Public "cell" shape used by the builder. Adapt to your own types below.
    public struct RoomCell
    {
        public int x, y, z;   // integer layer or discretized height
        public int roomId;    // source room id
        public int cellId;    // source cell id within room // ADDED, maybe useful later?
        public RoomCell(int x, int y, int z, int roomId, int cellId)
        { this.x = x; this.y = y; this.z = z; this.roomId = roomId; this.cellId = cellId; }
    }

    public struct NeighborMatch
    {
        public int z;             // matched z (or clamped inside segment)
        public int roomId;        // representative room id in that column/segment
        public int segmentIndex;  // -1 if not a segment kind
        public bool isSegment;    // true when match came from a merged vertical segment
        public DirFlags walls;    // directions of detected walls
    }

    internal enum ColumnKind : byte { Empty = 0, Single = 1, SmallVec = 2, SegmentVec = 3 }

    /// <summary>
    /// Heightfield optimized for the case where most (x,y) have one height,
    /// but a few have small stacks (e.g., spiral stairs). Uses tiny inline arrays.
    /// </summary>
    public sealed class Heightfield
    {
        // Tune these if you have more complex overlaps
        public const int SMALL_CAP = 4;     // max discrete heights stored inline
        public const int SEG_CAP = 4;     // max vertical segments per column
        public const int SEG_ROOM_CAP = 4;  // room id fan-in tracked per segment

        [Serializable]
        private sealed class Column
        {
            public ColumnKind kind;

            // Single
            public int zSingle;
            public int roomIdSingle;

            // SmallVec (sorted by z)
            public int smallCount;
            public int[] zInline;      // length SMALL_CAP
            public int[] roomInline;   // length SMALL_CAP

            // SegmentVec (sorted by zLo)
            public int segCount;
            public int[] zLo;          // length SEG_CAP
            public int[] zHi;          // length SEG_CAP
            // For each segment, track up to SEG_ROOM_CAP contributing roomIds (de-duplicated):
            public int[,] segRoomIds;      // [SEG_CAP, SEG_ROOM_CAP]
            public byte[] segRoomCounts;   // length SEG_CAP

            public Column()
            {
                kind = ColumnKind.Empty;
                zInline = new int[SMALL_CAP];
                roomInline = new int[SMALL_CAP];
                zLo = new int[SEG_CAP];
                zHi = new int[SEG_CAP];
                segRoomIds = new int[SEG_CAP, SEG_ROOM_CAP];
                segRoomCounts = new byte[SEG_CAP];
            }
        }

        private readonly int width, height;
        private readonly Column[,] cols;

        public int Width => width;
        public int Height => height;

        public Heightfield(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Invalid size");
            this.width = width; this.height = height;
            cols = new Column[width, height];
            for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                    cols[x, y] = new Column();
        }

        public void Clear()
        {
            for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                {
                    var c = cols[x, y];
                    c.kind = ColumnKind.Empty;
                    c.smallCount = 0;
                    c.segCount = 0;
                    c.zSingle = 0;
                    c.roomIdSingle = -1;
                    Array.Clear(c.segRoomCounts, 0, c.segRoomCounts.Length);
                }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool InBounds(int x, int y, int w, int h)
            => (uint)x < (uint)w && (uint)y < (uint)h && x >= 0 && y >= 0;

        /// <summary>
        /// Insert a single cell (x,y,z,roomId).
        /// Call this for all cells before FinalizeColumns().
        /// </summary>
        public void Insert(int x, int y, int z, int roomId)
        {
            if (!InBounds(x, y, width, height)) return;
            var c = cols[x, y];

            if (c.kind == ColumnKind.Empty)
            {
                c.kind = ColumnKind.Single;
                c.zSingle = z;
                c.roomIdSingle = roomId;
                return;
            }

            if (c.kind == ColumnKind.Single)
            {
                if (z == c.zSingle)
                {
                    // Same height; if room differs we still consider them the same discrete layer.
                    // Choose a stable room id (first wins).
                    return;
                }
                // Upgrade to SmallVec with two entries
                c.kind = ColumnKind.SmallVec;
                c.smallCount = 2;
                if (z < c.zSingle)
                {
                    c.zInline[0] = z; c.roomInline[0] = roomId;
                    c.zInline[1] = c.zSingle; c.roomInline[1] = c.roomIdSingle;
                }
                else
                {
                    c.zInline[0] = c.zSingle; c.roomInline[0] = c.roomIdSingle;
                    c.zInline[1] = z; c.roomInline[1] = roomId;
                }
                return;
            }

            if (c.kind == ColumnKind.SmallVec)
            {
                // Insert into sorted zInline (unique by z)
                int n = c.smallCount;
                // If z already present, ignore (keep first room id)
                int idx = Array.BinarySearch(c.zInline, 0, n, z);
                if (idx >= 0) return;

                idx = ~idx;
                if (n < SMALL_CAP)
                {
                    // shift right from idx
                    for (int i = n; i > idx; --i)
                    {
                        c.zInline[i] = c.zInline[i - 1];
                        c.roomInline[i] = c.roomInline[i - 1];
                    }
                    c.zInline[idx] = z;
                    c.roomInline[idx] = roomId;
                    c.smallCount = n + 1;
                    return;
                }

                // Out of inline capacity: fall back to segment building path by forcing finalize to SegmentVec
                // We temporarily append "at the end" (not sorted) and let FinalizeColumns fix it.
                // Reuse last slot to mark overflow; we'll re-sort later.
                // For correctness right now, replace the farthest z to keep n constant.
                // (Corner case; will be fixed in FinalizeColumns.)
                int replace = (Mathf.Abs(z - c.zInline[0]) > Mathf.Abs(z - c.zInline[n - 1])) ? 0 : n - 1;
                c.zInline[replace] = z;
                c.roomInline[replace] = roomId;
                // Keep smallCount unchanged; Finalize will normalize/segment.
                return;
            }

            // SegmentVec: insert by assigning to a segment during Finalize; for now, queue as discrete by hijacking zInline/roomInline if space allows
            if (c.kind == ColumnKind.SegmentVec)
            {
                // Try to place into an existing segment immediately (fast path if already finalized)
                int segIdx = LowerBound(c.zLo, c.segCount, z);
                bool matched = false;
                if (segIdx < c.segCount && z >= c.zLo[segIdx] && z <= c.zHi[segIdx]) { matched = true; AddRoomToSeg(c, segIdx, roomId); }
                else if (segIdx > 0 && z >= c.zLo[segIdx - 1] && z <= c.zHi[segIdx - 1]) { matched = true; AddRoomToSeg(c, segIdx - 1, roomId); }

                if (!matched)
                {
                    // No segment covers this exact z. If there's capacity, create a tiny 1-height segment; otherwise, clamp to nearest.
                    if (c.segCount < SEG_CAP)
                    {
                        // Insert new segment at segIdx
                        for (int i = c.segCount; i > segIdx; --i)
                        {
                            c.zLo[i] = c.zLo[i - 1]; c.zHi[i] = c.zHi[i - 1];
                            // shift room ids
                            for (int k = 0; k < SEG_ROOM_CAP; ++k) c.segRoomIds[i, k] = c.segRoomIds[i - 1, k];
                            c.segRoomCounts[i] = c.segRoomCounts[i - 1];
                        }
                        c.zLo[segIdx] = z; c.zHi[segIdx] = z;
                        c.segRoomCounts[segIdx] = 0;
                        AddRoomToSeg(c, segIdx, roomId);
                        c.segCount++;
                    }
                    else
                    {
                        // Exhausted capacity; attach to closest segment by expanding it by 0 (no effect) and record room
                        int attach = Mathf.Clamp(segIdx, 0, c.segCount - 1);
                        AddRoomToSeg(c, attach, roomId);
                    }
                }
            }
        }

        /// <summary>
        /// Must be called after all Insert() and before queries. Merges discrete heights into segments when gaps <= minRoomHeight.
        /// </summary>
        public void FinalizeColumns(int minRoomHeight)
        {
            for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                {
                    var c = cols[x, y];
                    if (c.kind == ColumnKind.SmallVec && c.smallCount > 1)
                    {
                        // Ensure sorted (may already be)
                        Array.Sort(c.zInline, c.roomInline, 0, c.smallCount);

                        // Build segments by merging gaps <= minRoomHeight
                        int segCount = 0;
                        int curLo = c.zInline[0];
                        int curHi = c.zInline[0];
                        // temp room set for the current segment
                        int[] tmpRooms = new int[SEG_ROOM_CAP];
                        int tmpCount = 0; AddRoomOnce(tmpRooms, ref tmpCount, c.roomInline[0]);

                        for (int i = 1; i < c.smallCount; ++i)
                        {
                            int z = c.zInline[i];
                            int gap = z - curHi;
                            if (gap <= minRoomHeight)
                            {
                                // Merge into current segment
                                curHi = z;
                                AddRoomOnce(tmpRooms, ref tmpCount, c.roomInline[i]);
                            }
                            else
                            {
                                // Flush segment
                                if (segCount < SEG_CAP)
                                {
                                    c.zLo[segCount] = curLo; c.zHi[segCount] = curHi;
                                    c.segRoomCounts[segCount] = (byte)tmpCount;
                                    for (int k = 0; k < tmpCount; ++k) c.segRoomIds[segCount, k] = tmpRooms[k];
                                    segCount++;
                                }
                                // Start new
                                curLo = curHi = z;
                                tmpCount = 0; AddRoomOnce(tmpRooms, ref tmpCount, c.roomInline[i]);
                            }
                        }
                        // Flush last
                        if (segCount < SEG_CAP)
                        {
                            c.zLo[segCount] = curLo; c.zHi[segCount] = curHi;
                            c.segRoomCounts[segCount] = (byte)tmpCount;
                            for (int k = 0; k < tmpCount; ++k) c.segRoomIds[segCount, k] = tmpRooms[k];
                            segCount++;
                        }

                        if (segCount == 1 && (c.zLo[0] == c.zHi[0]))
                        {
                            // Degenerates back to Single
                            c.kind = ColumnKind.Single;
                            c.zSingle = c.zLo[0];
                            c.roomIdSingle = (c.segRoomCounts[0] > 0) ? c.segRoomIds[0, 0] : c.roomInline[0];
                            c.segCount = 0;
                        }
                        else if (segCount >= 1)
                        {
                            c.kind = ColumnKind.SegmentVec;
                            c.segCount = segCount;
                        }
                        else
                        {
                            // Fallback to single (shouldn't happen)
                            c.kind = ColumnKind.Single;
                            c.zSingle = c.zInline[0];
                            c.roomIdSingle = c.roomInline[0];
                            c.segCount = 0;
                        }
                    }
                    // Single/Empty/SegmentVec stay as is
                }
        }

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
                    {
                        if (Mathf.Abs(c.zSingle - z) <= threshold)
                        {
                            match.z = c.zSingle;
                            match.roomId = c.roomIdSingle;
                            match.segmentIndex = -1;
                            match.isSegment = false;
                            return true;
                        }
                        return false;
                    }

                case ColumnKind.SmallVec:
                    {
                        int n = c.smallCount;
                        if (n == 0) return false;
                        int idx = LowerBound(c.zInline, n, z);
                        int bestIdx = -1; int bestDelta = int.MaxValue;
                        // check idx and neighbors
                        for (int t = -1; t <= 1; ++t)
                        {
                            int i = idx + t;
                            if (i < 0 || i >= n) continue;
                            int delta = Mathf.Abs(c.zInline[i] - z);
                            if (delta < bestDelta) { bestDelta = delta; bestIdx = i; }
                        }
                        if (bestIdx >= 0 && bestDelta <= threshold)
                        {
                            match.z = c.zInline[bestIdx];
                            match.roomId = c.roomInline[bestIdx];
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
                        int i = LowerBound(c.zLo, n, z); // first zLo > z
                                                         // Check i and i-1 for overlap within threshold
                        if (i < n && z >= c.zLo[i] - threshold && z <= c.zHi[i] + threshold)
                        {
                            match.z = Mathf.Clamp(z, c.zLo[i], c.zHi[i]);
                            match.roomId = (c.segRoomCounts[i] > 0) ? c.segRoomIds[i, 0] : -1;
                            match.segmentIndex = i;
                            match.isSegment = true;
                            return true;
                        }
                        if (i > 0 && z >= c.zLo[i - 1] - threshold && z <= c.zHi[i - 1] + threshold)
                        {
                            int j = i - 1;
                            match.z = Mathf.Clamp(z, c.zLo[j], c.zHi[j]);
                            match.roomId = (c.segRoomCounts[j] > 0) ? c.segRoomIds[j, 0] : -1;
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
            bool has_adjacent = false;
            DirFlags wall_dirs = DirFlags.None;
            match = default;
            // 4-neighbors: (x±1,y), (x,y±1)
            if (TryQueryAt(x - 1, y, z, threshold, out match)) wall_dirs |= DirFlags.W;
            if (TryQueryAt(x + 1, y, z, threshold, out match)) wall_dirs |= DirFlags.E;
            if (TryQueryAt(x, y - 1, z, threshold, out match)) wall_dirs |= DirFlags.S;
            if (TryQueryAt(x, y + 1, z, threshold, out match)) wall_dirs |= DirFlags.N;
            has_adjacent = wall_dirs == DirFlags.None;
            return has_adjacent;
        }

        public bool HasAdjacentWithinThreshold(int x, int y, int z, int threshold, out NeighborMatch match)
        {
            // 4-neighbors: (x±1,y), (x,y±1)
            if (TryQueryAt(x - 1, y, z, threshold, out match)) return true;
            if (TryQueryAt(x + 1, y, z, threshold, out match)) return true;
            if (TryQueryAt(x, y - 1, z, threshold, out match)) return true;
            if (TryQueryAt(x, y + 1, z, threshold, out match)) return true;
            match = default;
            return false;
        }

        // ---------- Builders ----------

        /// <summary>
        /// Build a heightfield directly from a list/array of cells. You can also call Insert() yourself and then FinalizeColumns().
        /// </summary>
        public static Heightfield BuildFromCells(IEnumerable<RoomCell> cells, int width, int height, int minRoomHeight)
        {
            var hf = new Heightfield(width, height);
            foreach (var c in cells)
            {
                //if (!In(c.x, c.y)) continue; // next line is equivalent.
                if (c.x < 0 || c.y < 0 || c.x >= hf.Width || c.y >= hf.Height) continue;
                hf.Insert(c.x, c.y, c.z, c.roomId);
            }
            hf.FinalizeColumns(minRoomHeight);
            return hf;
        }

        // ---------- Utilities ----------

        private static int LowerBound(int[] arr, int count, int value)
        {
            int lo = 0, hi = count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (arr[mid] <= value) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        private static void AddRoomOnce(int[] dst, ref int count, int roomId)
        {
            for (int i = 0; i < count; ++i) if (dst[i] == roomId) return;
            if (count < dst.Length) dst[count++] = roomId;
        }

        private static void AddRoomToSeg(Column c, int segIdx, int roomId)
        {
            int cnt = c.segRoomCounts[segIdx];
            for (int i = 0; i < cnt; ++i) if (c.segRoomIds[segIdx, i] == roomId) return;
            if (cnt < SEG_ROOM_CAP)
            {
                c.segRoomIds[segIdx, cnt] = roomId;
                c.segRoomCounts[segIdx] = (byte)(cnt + 1);
            }
        }
    }

    // Put alongside your Heightfield code (same namespace if you like).

    [Flags]
    /*   public enum DirFlags
        {
            None  = 0,
            North = 1 << 0, // y+1
            South = 1 << 1, // y-1
            West  = 1 << 2, // x-1
            East  = 1 << 3, // x+1
        }
    */
    // Configure how to treat neighbors
    public enum NeighborPolicy
    {
        // Only absence of neighbor creates a wall
        SameLevelOnly,

        // Absence OR a different room at same level creates a wall
        TreatDifferentRoomAsWall,

        // Absence OR a different "segment" (separated by > threshold in same column)
        // acts like a wall/railing; useful for mezzanines.
        TreatDifferentSegmentAsWall,
    }

    // ====================================================

    public static class HeightfieldWalls
    {
        /// <summary>
        /// Returns which sides of (x,y,z) are exposed (i.e., need walls/railings), as DirFlags.
        /// Coordinates are grid-space; z is the discretized height used by Heightfield.
        /// 
        /// Conventions:
        ///   North = (x,   y+1)
        ///   South = (x,   y-1)
        ///   West  = (x-1, y  )
        ///   East  = (x+1, y  )
        /// </summary>
        /// <param name="hf">Built/Finalized Heightfield</param>
        /// <param name="x">Cell X</param>
        /// <param name="y">Cell Y</param>
        /// <param name="z">Cell height (int units consistent with hf)</param>
        /// <param name="threshold">Typically cfg.minRoomHeightInt</param>
        /// <param name="currentRoomId">
        ///   Room id of the floor at (x,y,z). If unknown, pass -1 to disable inter-room policies.
        /// </param>
        /// <param name="policy">How to treat neighbors (see NeighborPolicy)</param>
        /// <param name="treatBoundsAsWalls">
        ///   If true, map edges (out of bounds) are considered walls on that side.
        /// </param>
        public static DirFlags GetExposedDirs(
            Heightfield hf,
            int x, int y, int z,
            int threshold,
            int currentRoomId = -1,
            NeighborPolicy policy = NeighborPolicy.TreatDifferentRoomAsWall, //old default: SameLevelOnly,
            bool treatBoundsAsWalls = true)
        {
            DirFlags flags = DirFlags.None;

            // Direction vectors (dx, dy) in N, S, W, E order to match enum bit meanings above.
            var dirs = new (int dx, int dy, DirFlags bit)[] {
                (0, +1, DirFlags.N),
                (0, -1, DirFlags.S),
                (-1, 0, DirFlags.W),
                (+1, 0, DirFlags.E),
            };

            foreach (var d in dirs)
            {
                int nx = x + d.dx;
                int ny = y + d.dy;

                // Out of bounds?
                //int border = cfg.borderKeepout;
                if (nx < 0 || ny < 0 || nx >= hf.Width || ny >= hf.Height)
                {
                    if (treatBoundsAsWalls)
                    {
                        flags |= d.bit;
                        //Debug.Log($"GetExposedDirs: treatBoundsAsWalls x,y={x},{y} nx,ny={nx},{ny}");
                        //continue;
                    }
                }

                // Is there a neighbor floor near z at (nx,ny)?
                if (!hf.TryQueryAt(nx, ny, z, threshold, out var match))
                {
                    // No neighbor within threshold: exposed
                    flags |= d.bit;
                    continue;
                }

                // There IS a neighbor. Depending on policy we may still consider it "wall-worthy".
                switch (policy)
                {
                    case NeighborPolicy.SameLevelOnly:
                        // A neighbor exists within threshold => not exposed
                        break;

                    case NeighborPolicy.TreatDifferentRoomAsWall:
                        if (currentRoomId >= 0 && match.roomId >= 0 && match.roomId != currentRoomId)
                            flags |= d.bit;
                        break;

                    case NeighborPolicy.TreatDifferentSegmentAsWall:
                        // If your world can have multiple segments at the same column height range,
                        // treat crossings between distinct segments as needing a barrier/rail.
                        // Here we use segmentIndex (valid when isSegment==true). If the current
                        // column also has segments and the current z lies in a different one than
                        // neighbor, count it as exposed. We need our own segment index at (x,y,z):
                        int mySeg = SegmentIndexAt(hf, x, y, z, threshold);
                        int nbSeg = match.isSegment ? match.segmentIndex : -1;
                        if (mySeg != -2 && nbSeg != -2 && mySeg != nbSeg)
                            flags |= d.bit;
                        break;
                }
            }

            return flags;
        }

        /// <summary>
        /// Helper: returns the segment index covering (x,y) at z within threshold, or
        /// -1 if not in a segment kind, -2 if no match at all.
        /// </summary>
        private static int SegmentIndexAt(Heightfield hf, int x, int y, int z, int threshold)
        {
            if (!hf.TryQueryAt(x, y, z, threshold, out var m))
                return -2;
            return m.isSegment ? m.segmentIndex : -1;
        }



    }
    // --------------------------
        // Adapters to YOUR data
        // --------------------------
        //
        // 1) If your heights are float, choose a scale (e.g., 100) and round: zInt = Mathf.RoundToInt(zFloat * scale).
        // 2) If your world is big, build per-chunk Heightfield instances (e.g., 64x64) and query only active chunks.
        //
        // Example usage in your pipeline:
        //
        //   // Prepare cells from your rooms:
        //   var tmp = new List<RoomCell>(totalCellCount);
        //   foreach (var room in rooms)
        //       foreach (var cell in room.tiles3D) // (x,y,height)
        //           tmp.Add(new RoomCell(cell.x, cell.y, HeightToInt(cell.height), room.id));
        //
        //   // Build
        //   var hf = Heightfield.BuildFromCells(tmp, worldWidth, worldHeight, cfg.minRoomHeight);
        //
        //   // During wall placement for a floor cell (x,y,zInt):
        //   NeighborMatch m;
        //   bool hasNeighbor = hf.HasAdjacentWithinThreshold(x, y, zInt, cfg.minRoomHeight, out m);
        //   if (!hasNeighbor) {
        //       // exposed side -> place wall
        //   } else {
        //       // Optional: decide based on room ids or m.isSegment (e.g., railing for stairwell)
        //   }
        //
        // Helper for float heights:
        // static int HeightToInt(float z) => Mathf.RoundToInt(z * 100f); // choose a scale that matches your thresholds
    }


/* This structure was created by ChatGPT based on the following prompt:

In finding walls, I need to deal with 3D locations.  Each room has a list
of cells with X,Y,height. Rooms may overlap each other, but be considered
separate if height difference is beyond a threshold (cfg.minRoomHeight).
If below that threshold, they need to be considered adjacent for the
placement of walls.  The current algorithm repeatedly looks up cells in a
hash (created at the beginning) to get height for every comparison which
is accurate but slowing things down.  I am considering creating a 2D map
of each room (or neighborhood or world) with heights at each location.
What should I do when two cells exist at the same X,Y but different enough
heights to be different rooms?  For my current rooms, the room and its
immediately connected adjacent neighbors don't have any overlap, but I
want to add a spiral staircase room that WILL have overlap within a single
room.  I'm considering adding a bit to the 2D heights map to indicate
overlap and force fallback to the current hash method just for those cells,
but is there a simpler better faster efficient way?

Response:
You’re on the right track wanting a 2D structure; you just don’t need to
fall back to the old hash for overlaps. Use a columnar heightfield: one
container per (x,y) that usually stores a single height, but can store a
tiny sorted list (or segments) when multiple heights share that (x,y).
This gives O(1) for the common case and O(log k) (k ≪ 8) for overlaps like
a spiral stair—without touching a global hash each time.
...
and eventually after a bit more clarification, produced this file.
*/