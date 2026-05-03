using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator
{
    // DrawWalls() will add solid wall tiles around all rooms on the tilemap.
    // It is intended for non-thinWalls maps
    public void DrawWalls()  // from tilemap, adds walls to the existing 2D tilemap
    {
        return; // DEBUG: This isn't working quite right, remove for now...
/*        BoundsInt bounds = tilemap.cellBounds;
        //BottomBanner.LogBuildProgress("Drawing walls...");
        for (int x = bounds.xMin - 1; x <= bounds.xMax + 1; x++)
        {
            for (int y = bounds.yMin - 1; y <= bounds.yMax + 1; y++)
            {
                Vector3Int pos = new(x, y, 0);
                if (tilemap.GetTile(pos) == floorTile)
                    continue;                       // Skip floor tiles
                if (HasFloorNeighbor(pos))
                    tilemap.SetTile(pos, wallTile); // add wall tile
                else
                    tilemap.SetTile(pos, null);     // Remove wall if no floor neighbor
            }
        }
*/
    }

    // UNUSED
    // Check if a tile at position pos has a neighboring floor tile within the specified radius
    bool HasFloorNeighbor(Vector3Int pos, int radius = 1)
    {
        if (tilemap == null || floorTile == null) return false; // Safety check

        // Check all neighbors within the specified radius
        NeighborCache.Shape shape = cfg.neighborShape;
        bool includeDiagonals = cfg.includeDiagonals;
        var neighbors = NeighborCache.Get(radius, shape, borderOnly: true, includeDiagonals);

        foreach (var offset in neighbors)
        {
            Vector3Int neighborPos = pos + offset;
            if (tilemap.GetTile(neighborPos) == floorTile)
            {
                return true; // Found a floor tile neighbor
            }
        }
        return false; // No floor tile neighbors found
    }

    // UNUSED
    void GenerateWallLists_OLD() // replaced by BuildWallsAroundFloorsInRooms in Rooms.cs
    {
        //List<Vector2Int> wall_list_room;
        //HashSet<Vector2Int> new_wall_hash;

        // TODO: replace with directions in Room.cs
        Vector2Int[] directions = {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };

        for (int room_number = 0; room_number < rooms.Count; room_number++)
        {
            //wall_list_room = new();
            //new_wall_hash = new();
            foreach (Cell cell in rooms[room_number].cells)
            {
                Vector2Int pos = cell.pos;
                foreach (Vector2Int dir in directions)
                {
                    // look in dir, if not in room then consider it a wall and
                    ///  set appropriate wall direction flag.
                    if (rooms[room_number].GetCellInRoom(pos + dir) == -1) // non-existance check.
                    {
                        DirFlags dir_flag = DirFlagsEx.FromVector2Int(dir);
                        cell.walls |= dir_flag;

                        // Debug to map display:
                        Vector3Int pos3d = new Vector3Int(cell.x, cell.y, 0);
                        tilemap.SetTile(pos3d, wallTile);
                        tilemap.SetTileFlags(pos3d, TileFlags.None);
                        tilemap.SetColor(pos3d, Color.red);
                        //Debug.Log($"Wall found in direction {dir.x},{dir.y}; flags + {dir_flag} = {cell.walls}");
                    }
                }
                //Debug.Log($"walls = {cell.walls}");
            }
        }
    }

    // uses map[,]
    public void FillVoidToWalls(byte[,] map)
    {
        for (var y = 0; y < cfg.mapHeight; y++)
            for (var x = 0; x < cfg.mapWidth; x++)
            {
                if (map[x, y] == 0) map[x, y] = WALL;
            }
    }
}
