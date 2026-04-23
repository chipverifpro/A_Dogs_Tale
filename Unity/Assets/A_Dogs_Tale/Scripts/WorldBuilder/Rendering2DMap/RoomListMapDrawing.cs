using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator
{
    // NEW Draw: room -> 2D tiles
    public void DrawMapFromRoomsList(List<Room> rooms)
    {
        tilemap.ClearAllTiles();

        foreach (Room room in rooms)
        {
            foreach (Cell cell in room.cells)
            {
                Vector3Int pos = new Vector3Int(cell.x, cell.y, 0);
                tilemap.SetTile(pos, floorTile);
                tilemap.SetTileFlags(pos, TileFlags.None); // Allow color changes
                tilemap.SetColor(pos, cell.colorFloor);
            }
        }
    }
}
