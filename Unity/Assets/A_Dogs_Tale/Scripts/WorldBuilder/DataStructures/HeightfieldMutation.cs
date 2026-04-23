using System;
using UnityEngine;

public partial class DungeonGenerator
{
    public sealed partial class Heightfield
    {
        /// <summary>
        /// Insert a single cell (x,y,z,roomId,cellId).
        /// Call this for all cells before FinalizeColumns().
        /// </summary>
        public void Insert(int x, int y, int z, int roomId, int cellId)
        {
            if (!InBounds(x, y, width, height)) return;
            var c = cols[x, y];

            if (c.kind == ColumnKind.Empty)
            {
                c.kind = ColumnKind.Single;
                c.zSingle = z;
                c.roomIdSingle = roomId;
                c.cellIdSingle = cellId;
                return;
            }

            if (c.kind == ColumnKind.Single)
            {
                if (z == c.zSingle) return;

                c.kind = ColumnKind.SmallVec;
                c.smallCount = 2;
                if (z < c.zSingle)
                {
                    c.zInline[0] = z;
                    c.roomInline[0] = roomId;
                    c.cellInline[0] = cellId;
                    c.zInline[1] = c.zSingle;
                    c.roomInline[1] = c.roomIdSingle;
                    c.cellInline[1] = c.cellIdSingle;
                }
                else
                {
                    c.zInline[0] = c.zSingle;
                    c.roomInline[0] = c.roomIdSingle;
                    c.cellInline[0] = c.cellIdSingle;
                    c.zInline[1] = z;
                    c.roomInline[1] = roomId;
                    c.cellInline[1] = cellId;
                }
                return;
            }

            if (c.kind == ColumnKind.SmallVec)
            {
                int n = c.smallCount;
                int idx = Array.BinarySearch(c.zInline, 0, n, z);
                if (idx >= 0) return;

                idx = ~idx;
                if (n < SMALL_CAP)
                {
                    for (int i = n; i > idx; --i)
                    {
                        c.zInline[i] = c.zInline[i - 1];
                        c.roomInline[i] = c.roomInline[i - 1];
                    }
                    c.zInline[idx] = z;
                    c.roomInline[idx] = roomId;
                    c.cellInline[idx] = cellId;
                    c.smallCount = n + 1;
                    return;
                }

                int replace = (Mathf.Abs(z - c.zInline[0]) > Mathf.Abs(z - c.zInline[n - 1])) ? 0 : n - 1;
                c.zInline[replace] = z;
                c.roomInline[replace] = roomId;
                c.cellInline[replace] = cellId;
                return;
            }

            if (c.kind == ColumnKind.SegmentVec)
            {
                int segIdx = LowerBound(c.zLo, c.segCount, z);
                bool matched = false;
                if (segIdx < c.segCount && z >= c.zLo[segIdx] && z <= c.zHi[segIdx])
                {
                    matched = true;
                    AddRoomToSeg(c, segIdx, roomId, cellId);
                }
                else if (segIdx > 0 && z >= c.zLo[segIdx - 1] && z <= c.zHi[segIdx - 1])
                {
                    matched = true;
                    AddRoomToSeg(c, segIdx - 1, roomId, cellId);
                }

                if (!matched)
                {
                    if (c.segCount < SEG_CAP)
                    {
                        for (int i = c.segCount; i > segIdx; --i)
                        {
                            c.zLo[i] = c.zLo[i - 1];
                            c.zHi[i] = c.zHi[i - 1];
                            for (int k = 0; k < SEG_ROOM_CAP; ++k) c.segRoomIds[i, k] = c.segRoomIds[i - 1, k];
                            c.segRoomCounts[i] = c.segRoomCounts[i - 1];
                        }
                        c.zLo[segIdx] = z;
                        c.zHi[segIdx] = z;
                        c.segRoomCounts[segIdx] = 0;
                        AddRoomToSeg(c, segIdx, roomId, cellId);
                        c.segCount++;
                    }
                    else
                    {
                        int attach = Mathf.Clamp(segIdx, 0, c.segCount - 1);
                        AddRoomToSeg(c, attach, roomId, cellId);
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
                    if (c.kind != ColumnKind.SmallVec || c.smallCount <= 1) continue;

                    Array.Sort(c.zInline, c.roomInline, 0, c.smallCount);

                    int segCount = 0;
                    int curLo = c.zInline[0];
                    int curHi = c.zInline[0];
                    int[] tmpRooms = new int[SEG_ROOM_CAP];
                    int tmpCount = 0;
                    AddRoomOnce(tmpRooms, ref tmpCount, c.roomInline[0]);

                    for (int i = 1; i < c.smallCount; ++i)
                    {
                        int z = c.zInline[i];
                        int gap = z - curHi;
                        if (gap <= minRoomHeight)
                        {
                            curHi = z;
                            AddRoomOnce(tmpRooms, ref tmpCount, c.roomInline[i]);
                        }
                        else
                        {
                            if (segCount < SEG_CAP)
                            {
                                c.zLo[segCount] = curLo;
                                c.zHi[segCount] = curHi;
                                c.segRoomCounts[segCount] = (byte)tmpCount;
                                for (int k = 0; k < tmpCount; ++k) c.segRoomIds[segCount, k] = tmpRooms[k];
                                segCount++;
                            }
                            curLo = curHi = z;
                            tmpCount = 0;
                            AddRoomOnce(tmpRooms, ref tmpCount, c.roomInline[i]);
                        }
                    }

                    if (segCount < SEG_CAP)
                    {
                        c.zLo[segCount] = curLo;
                        c.zHi[segCount] = curHi;
                        c.segRoomCounts[segCount] = (byte)tmpCount;
                        for (int k = 0; k < tmpCount; ++k) c.segRoomIds[segCount, k] = tmpRooms[k];
                        segCount++;
                    }

                    if (segCount == 1 && c.zLo[0] == c.zHi[0])
                    {
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
                        c.kind = ColumnKind.Single;
                        c.zSingle = c.zInline[0];
                        c.roomIdSingle = c.roomInline[0];
                        c.segCount = 0;
                    }
                }
        }
    }
}
