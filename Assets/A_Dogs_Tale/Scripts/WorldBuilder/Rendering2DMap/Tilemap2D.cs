using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

public partial class DungeonGenerator : MonoBehaviour  // Tilemap2D
{

    // The 2D map and Unity's tilemap functions and data are here.....

    public byte[,] map; // each byte represents one of the below constants
    public int[,] mapHeights; // 2D array to store height information for each tile
    //public bool mapStale = true; // Flag to indicate if map needs to be regenerated from rooms
    [HideInInspector] public const byte WALL = 1;
    [HideInInspector] public const byte FLOOR = 2;
    [HideInInspector] public const byte RAMP = 3;
    [HideInInspector] public const byte UNKNOWN = 99;
    // Additional tile types to be defined here


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

    // helper routines for 2D map

    // Generic cluster finder: find connected components whose cells equal `target` (FLOOR or WALL)
    // Uses 4-way adjacency like FindRoomsCoroutine did.
    public IEnumerator FindClustersCoroutine(byte[,] map, byte target, List<Room> outRooms, TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("FindClustersCoroutine"); local_tm = true; }
        try
        {
            outRooms.Clear();
            int width = map.GetLength(0);
            int height = map.GetLength(1);
            bool[,] visited = new bool[width, height];
            int room_height;
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            //($"begin Find Clusters Coroutine");
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!visited[x, y] && map[x, y] == target)
                    {
                        Room cluster = new Room();
                        room_height = (int)Random.Range(0f, (float)cfg.maxElevation);
                        Queue<Vector2Int> q = new Queue<Vector2Int>(16);
                        q.Enqueue(new Vector2Int(x, y));
                        visited[x, y] = true;

                        while (q.Count > 0)
                        {
                            var p = q.Dequeue();
                            cluster.cells.Add(new Cell(p.x, p.y, room_height));
                            //cluster.heights.Add(room_height);

                            foreach (var d in directions)
                            {
                                int nx = p.x + d.x;
                                int ny = p.y + d.y;
                                if (nx >= 0 && ny >= 0 && nx < width && ny < height &&
                                    !visited[nx, ny] && map[nx, ny] == target)
                                {
                                    q.Enqueue(new Vector2Int(nx, ny));
                                    visited[nx, ny] = true;
                                }
                            }

                            // Periodic yield to keep UI responsive on large clusters
                            if ((cluster.cells.Count & 0x1FFF) == 0)
                                if (tm.IfYield()) yield return null;
                        }
                        cluster.name = $"Cluster {outRooms.Count + 1} ({cluster.cells.Count} tiles)";
                        cluster.setColorFloor(highlight: true);
                        outRooms.Add(cluster);

                        if (tm.IfYield()) yield return null; // let UI breathe between clusters
                    }
                }
                // optional progress log
                //Debug.Log($"Cluster finder processed col {x} of {width}");
                //yield return null;
            }
            
            //Debug.Log($"End Find Clusters Coroutine");
        }
        finally { if (local_tm) tm.End(); }
    }


    // Remove clusters smaller than cfg.MinimumRoomSize by repainting them to `replacement` (FLOOR or WALL)
    public IEnumerator RemoveTinyClustersCoroutine(List<Room> clusters, int minimumSize, byte replacement, TileBase replacementTile = null, TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("RemoveTinyClustersCoroutine"); local_tm = true; }
        try
        {
            bool Done = false;
            while (!Done)
            {
                Done = true;
                for (int i = 0; i < clusters.Count; i++)
                {
                    var room = clusters[i];
                    if (room.Size < minimumSize)
                    {
                        foreach (var t in room.cells)
                        {
                            map[t.x, t.y] = replacement; // flip to replacement
                        }
                        clusters.RemoveAt(i);
                        Done = false;
                        if (tm.IfYield()) yield return null; // UI breathe
                        break;
                    }
                }
            }
            DrawMapFromByteArray();
            if (tm.IfYield()) yield return null;        
        }
        finally { if (local_tm) tm.End(); }
    }

    public IEnumerator RemoveTinyRoomsCoroutine(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("RemoveTinyRoomsCoroutine"); local_tm = true; }
        try
        {
            // 1) Find Floor clusters
            // we already found the rooms, just filter them out for tiny ones.
            //rooms = new List<Room>();
            //yield return StartCoroutine(FindClustersCoroutine(map, FLOOR, rooms, tm: null));
            // 2) Remove the tiny ones by turning them into WALL
            yield return StartCoroutine(RemoveTinyClustersCoroutine(rooms, cfg.MinimumRoomSize, WALL, null, tm: null));
            // 3) Redraw (floor/wall visuals updated by DrawMapFromByteArray)
            //DrawMapFromByteArray();
            if (tm.IfYield()) yield return null;
        }
        finally { if (local_tm) tm.End(); }
    }

    public IEnumerator RemoveTinyRocksCoroutine(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("RemoveTinyRocksCoroutine"); local_tm = true; }
        try
        {
            // 1) Find WALL clusters
            var islands = new List<Room>(128);
            yield return StartCoroutine(FindClustersCoroutine(map, WALL, islands, tm: null));
            // 2) Remove the tiny ones by turning them into FLOOR
            yield return StartCoroutine(RemoveTinyClustersCoroutine(islands, cfg.MinimumRockSize, FLOOR, floorTile, tm: null));
            // 3) Redraw (floor/wall visuals updated by DrawMapFromByteArray)
            //DrawMapFromByteArray();
            if (tm.IfYield()) yield return null;
        }
        finally { if (local_tm) tm.End(); }
    }

    // UNUSED
    public void ClearTileAndNeighborWalls(Tilemap tilemap, Vector3Int cellPos)
    {
        // Square filled radius 2
        var squareR2 = NeighborCache.Get(2, NeighborCache.Shape.Square, borderOnly: false, includeDiagonals: true);
        //Debug.Log($"Clearing tile at {cellPos} and its neighbors with square radius 2");

        foreach (var offset in squareR2)
        {
            var neighbor = cellPos + offset;
            if (tilemap.GetTile(neighbor) == wallTile)
                tilemap.SetTile(neighbor, null);
        }

        tilemap.SetTile(cellPos, null); // Clear the main tile
    }



    // Get the closest floor tile location in this room to a given target location
    // TODO: for overlapping rooms where corridor will be zero length, do sommething different
    public Vector2Int GetClosestPointInTilesList(List<Vector2Int> tile_list, Vector2Int target, int minimum_corridor_length)
    {
        int min_distance = int.MaxValue;
        int cur_distance = int.MaxValue;
        Vector2Int closest_point = Vector2Int.zero;

        if (tile_list.Count == 0) return Vector2Int.zero;

        foreach (var t in tile_list)
        {
            cur_distance = (t - target).sqrMagnitude;
            if ((cur_distance < min_distance) && (cur_distance > minimum_corridor_length))
            {
                min_distance = cur_distance;
                closest_point = t;
            }
        }

        return closest_point;
    }
}
