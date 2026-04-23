using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator
{
    // ROOM READY
    // Checks if the room is big enough and stretched enough to split.
    // Determines where to cut the room.
    // Performs the cut: moat cells removed, left side kept in old room, right side moved to a new room.
    // Add new room to master rooms list
    // Update the screen, calculate new bounds and frontiers.
    int SplitOversizedRooms(int moatCells, List<HashSet<(int, int)>> frontiers)
    {
        int cutsMade = 0;
        for (int i = 0; i < rooms.Count; i++)
        {
            var r = rooms[i];
            int area = r.cells.Count;
            if (area <= cfg.grow.splitArea) continue;

            RectInt oldBoundingBox = r.GetBounds();

            RectInt bounds = r.GetBounds();
            int minx = bounds.x;
            int miny = bounds.y;
            int w = Mathf.Max(1, bounds.width);
            int h = Mathf.Max(1, bounds.height);
            float aspect = (float)Mathf.Max(w, h) / Mathf.Max(1, Mathf.Min(w, h));
            if (aspect < cfg.grow.splitAspect) continue;

            int splitPercent = rng.Next(25, 75);
            bool splitVert = (w >= h);
            int cut = splitVert ? (minx + w * splitPercent / 100) : (miny + h * splitPercent / 100);

            Room newRoom = new Room { my_room_number = rooms.Count, cells = new List<Cell>() };
            newRoom.setColorFloor(highlight: true);
            newRoom.my_room_number = rooms.Count;
            newRoom.isCorridor = false;

            var keep = new List<Cell>();
            var newer = new List<Cell>();

            for (int oldCellNum = 0; oldCellNum < r.cells.Count; oldCellNum++)
            {
                bool leftSide;
                bool onCut;

                Cell c = r.cells[oldCellNum];
                if (cfg.UseThinWallsEffective())
                {
                    leftSide = splitVert ? (c.x < cut) : (c.y < cut);
                    onCut = false;
                }
                else
                {
                    leftSide = splitVert ? (c.x < cut) : (c.y < cut);
                    onCut = splitVert ? ((c.x >= cut) && (c.x < cut + moatCells))
                                      : ((c.y >= cut) && (c.y < cut + moatCells));
                }

                if (onCut)
                {
                    c.room_number = -1;

                    if (cfg.showBuildProcess)
                    {
                        Vector3Int pos3 = new Vector3Int(c.x, c.y, 0);
                        tilemap.SetTile(pos3, null);
                    }
                }
                else if (leftSide)
                {
                    keep.Add(c);
                }
                else
                {
                    newer.Add(c);

                    if (cfg.showBuildProcess)
                    {
                        Vector3Int pos3 = new Vector3Int(c.x, c.y, 0);
                        tilemap.SetTile(pos3, floorTile);
                        tilemap.SetTileFlags(pos3, TileFlags.None);
                        tilemap.SetColor(pos3, newRoom.colorFloor);
                    }
                }
            }

            r.cells = keep;
            newRoom.cells = newer;

            foreach (Cell cell in newRoom.cells)
            {
                cell.room_number = newRoom.my_room_number;
                cell.colorFloor = newRoom.colorFloor;
            }

            if (newRoom.cells.Count > 0)
                rooms.Add(newRoom);

            cutsMade++;

            RectInt newBoxA = r.GetBounds();
            RectInt newBoxB = newRoom.GetBounds();

            Debug.Log($"Split room {r.my_room_number} into newroom {newRoom.my_room_number}; splitvert {splitVert}; cutline = {cut} ({splitPercent}%)");
            Debug.Log($"original_box = {oldBoundingBox.x},{oldBoundingBox.y},{oldBoundingBox.xMax},{oldBoundingBox.yMax}");
            Debug.Log($"new_box_a    = {newBoxA.x},{newBoxA.y},{newBoxA.xMax},{newBoxA.yMax}");
            Debug.Log($"new_box_b    = {newBoxB.x},{newBoxB.y},{newBoxB.xMax},{newBoxB.yMax}");
        }

        foreach (Room r in rooms) r.GetBounds();

        while (frontiers.Count < rooms.Count) frontiers.Add(new HashSet<(int, int)>());
        for (int fi = 0; fi < frontiers.Count; fi++)
            RebuildFrontierFor(fi, moatCells, frontiers[fi]);

        return cutsMade;
    }
}
