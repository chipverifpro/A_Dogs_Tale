using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator
{
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

                            if ((cluster.cells.Count & 0x1FFF) == 0)
                                if (tm.IfYield()) yield return null;
                        }
                        cluster.name = $"Cluster {outRooms.Count + 1} ({cluster.cells.Count} tiles)";
                        cluster.setColorFloor(highlight: true);
                        outRooms.Add(cluster);

                        if (tm.IfYield()) yield return null; // let UI breathe between clusters
                    }
                }
            }
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
            yield return StartCoroutine(RemoveTinyClustersCoroutine(rooms, cfg.MinimumRoomSize, WALL, null, tm: null));
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
            var islands = new List<Room>(128);
            yield return StartCoroutine(FindClustersCoroutine(map, WALL, islands, tm: null));
            yield return StartCoroutine(RemoveTinyClustersCoroutine(islands, cfg.MinimumRockSize, FLOOR, floorTile, tm: null));
            if (tm.IfYield()) yield return null;
        }
        finally { if (local_tm) tm.End(); }
    }
}
