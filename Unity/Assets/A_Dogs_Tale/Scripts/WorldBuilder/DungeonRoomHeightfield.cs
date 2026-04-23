using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    public Heightfield hf;
    public bool hf_valid = false; // so we only create it once.
    DirFlags wall_dirs;

    // uses Heightfield.cs:
    public void PrepareHeightfield()
    {
        // count all the cells to allocate enough for the tmp array
        int totalCellCount = 0;
        foreach (var room in rooms) totalCellCount += room.cell_dictionary_room.Count;

        // Prepare cells from your rooms:
        var tmp = new List<RoomCell>(totalCellCount);
        int room_id = 0;
        int cell_id = 0;
        int worldWidth = 1;
        int worldHeight = 1;
        foreach (var room in rooms)
        {
            cell_id = 0;
            foreach (var cell in room.cells)
            { // (x,y,height)
                tmp.Add(new RoomCell(cell.x, cell.y, cell.height, room_id, cell_id));
                if (cell.x > worldWidth) worldWidth = cell.x;
                if (cell.y > worldHeight) worldHeight = cell.y;
                cell_id++;
            }
            room_id++;
        }

        // Build the global array hf
        //hf = Heightfield.BuildFromCells(tmp, worldWidth, worldHeight, cfg.minRoomHeight);
        hf = Heightfield.BuildFromCells(tmp, cfg.mapWidth, cfg.mapHeight, cfg.minRoomHeight);
        hf_valid = true;
    }

    public IEnumerator BuildWallsAroundFloorsInRooms(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("BuildWallsAroundFloorsInRooms"); local_tm = true; }
        try
        {
            // Build the heighfield hf if it doesn't yet exist.
            if (!hf_valid || hf == null || hf.Width == 0 || hf.Height == 0)
            {
                PrepareHeightfield();
                if (tm.IfYield()) yield return null;
            }

            // For all cells, find walls around floors
            // use an appropriate policy for the type of map:
            NeighborPolicy policy = cfg.GenerateWallsBetweenTouchingRoomsEffective()
                ? NeighborPolicy.TreatDifferentRoomAsWall
                : NeighborPolicy.SameLevelOnly;
            int room_number = 0;
            foreach (Room room in rooms)
            {
                int cell_num = 0;
                foreach (var cell in room.cells)
                {
                    var dirs = HeightfieldWalls.GetExposedDirs(
                        hf, cell.x, cell.y, cell.height, cfg.minRoomHeight,
                        currentRoomId: room_number,
                        policy: policy,
                        treatBoundsAsWalls: true
                    );
                    cell.walls = dirs;

                    cell_num++;
                }
                room_number++;
                if (tm.IfYield()) yield return null;
            }
        }
        finally { if (local_tm) tm.End(); }
    }
}
