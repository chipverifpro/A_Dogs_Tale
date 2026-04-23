using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator
{
    // map -> tilemap
    public void DrawMapFromByteArray()
    {
        tilemap.ClearAllTiles();

        for (int x = 0; x < cfg.mapWidth; x++)
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (map[x, y] == FLOOR)
                {
                    tilemap.SetTile(pos, floorTile);
                    tilemap.SetTileFlags(pos, TileFlags.None);
                    tilemap.SetColor(pos, Color.white);
                }
                else // WALL
                {
                    if (HasFloorNeighbor(pos))
                    {
                        tilemap.SetTile(pos, wallTile);
                        tilemap.SetTileFlags(pos, TileFlags.None);
                        tilemap.SetColor(pos, Color.white);
                    }
                    else
                    {
                        tilemap.SetTile(pos, null); // optional: don't draw deep interior walls
                    }
                }
            }
    }

    // from map
    bool HasFloorNeighbor(Vector3Int pos)
    {
        for (int x = -cfg.wallThickness; x <= cfg.wallThickness; x++)
            for (int y = -cfg.wallThickness; y <= cfg.wallThickness; y++)
            {
                Vector3Int dir = new Vector3Int(x, y, 0);
                if (dir.x == 0 && dir.y == 0) continue; // Skip self
                if (pos.x + dir.x < 0 || pos.y + dir.y < 0 ||
                            pos.x + dir.x >= cfg.mapWidth || pos.y + dir.y >= cfg.mapHeight)
                    continue; // Out of bounds
                if (map[pos.x + dir.x, pos.y + dir.y] == FLOOR)
                    return true;
            }
        return false;
    }
}
