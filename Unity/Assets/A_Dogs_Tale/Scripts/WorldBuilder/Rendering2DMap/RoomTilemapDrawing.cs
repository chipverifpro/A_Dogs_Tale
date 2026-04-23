using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator
{
    // Rooms list -> 2D tilemap
    public void DrawMapByRooms(List<Room> rooms, bool clearscreen = true)
    {
        if (!cfg.showBuildProcess) return;
        if (clearscreen)
        {
            tilemap.ClearAllTiles();
            tilemap_doors.ClearAllTiles();
            tilemap_walls.ClearAllTiles();
        }
        if (map == null) map = new byte[cfg.mapWidth, cfg.mapHeight];
        if (mapHeights == null) mapHeights = new int[cfg.mapWidth, cfg.mapHeight];

        foreach (var room in rooms)
        {
            foreach (var cell in room.cells)
            {
                Vector3Int pos3 = new(cell.x, cell.y, 0);

                map[cell.x, cell.y] = FLOOR;
                mapHeights[cell.x, cell.y] = cell.height;

                DrawRoomFloorTile(pos3, room.colorFloor);
                DrawRoomWallTiles(cell);
                DrawRoomDoorTiles(cell);
            }
        }
    }
}
