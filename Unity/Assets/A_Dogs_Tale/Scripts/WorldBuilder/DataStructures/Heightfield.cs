// Heightfield.cs
// Drop-in columnar height map with overlap support and minRoomHeight merging.
// Unity-safe, no unsafe code, no external deps.

using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{
    public static object Instance { get; internal set; }

    /// <summary>
    /// Heightfield optimized for the case where most (x,y) have one height,
    /// but a few have small stacks (e.g., spiral stairs). Uses tiny inline arrays.
    /// </summary>
    public sealed partial class Heightfield
    {
        public const int SMALL_CAP = 4;
        public const int SEG_CAP = 4;
        public const int SEG_ROOM_CAP = 4;

        [Serializable]
        private sealed class Column
        {
            public ColumnKind kind;

            public int zSingle;
            public int roomIdSingle;
            public int cellIdSingle;

            public int smallCount;
            public int[] zInline;
            public int[] roomInline;
            public int[] cellInline;

            public int segCount;
            public int[] zLo;
            public int[] zHi;
            public int[,] segRoomIds;
            public int[,] segCellIds;
            public byte[] segRoomCounts;
            public byte[] segCellCounts;

            public Column()
            {
                kind = ColumnKind.Empty;
                zInline = new int[SMALL_CAP];
                roomInline = new int[SMALL_CAP];
                cellInline = new int[SMALL_CAP];
                zLo = new int[SEG_CAP];
                zHi = new int[SEG_CAP];
                segRoomIds = new int[SEG_CAP, SEG_ROOM_CAP];
                segCellIds = new int[SEG_CAP, SEG_ROOM_CAP];
                segRoomCounts = new byte[SEG_CAP];
                segCellCounts = new byte[SEG_CAP];
            }
        }

        private readonly int width, height;
        private readonly Column[,] cols;

        public int Width => width;
        public int Height => height;

        public Heightfield(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("Invalid size");
            this.width = width;
            this.height = height;
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

        private static int LowerBound(int[] arr, int count, int value)
        {
            int lo = 0;
            int hi = count;
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

        private static void AddRoomToSeg(Column c, int segIdx, int roomId, int cellId)
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
