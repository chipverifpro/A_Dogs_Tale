using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{

    // ===================================================
    // Generate Corridors

    // NEW
    // Iterates through all unconnected rooms.  Finds a short corridor between two closest super-rooms and
    // generates that corridor as a new room.  Connects both ends to the rooms, making a bigger super-room.
    public IEnumerator ConnectRoomsByCorridors(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("ConnectRoomsByCorridors"); local_tm = true; }
        try
        {
            List<int> unconnected_rooms = new(); // start with all, and then remove as rooms are merged
            Vector2Int center_i = Vector2Int.zero;
            Vector2Int close_i = Vector2Int.zero;
            Vector2Int close_j = Vector2Int.zero;
            int connection_room_i = -1, connection_room_j = -1;

            //bool rooms_overlap = false;
            int minimum_corridor_length = 0;

            BottomBanner.Show($"Connecting {rooms.Count} rooms by ..");

            // initialize unconnected rooms to include all room indexes
            for (int room_no = 0; room_no < rooms.Count; room_no++)
                unconnected_rooms.Add(room_no);

            while (unconnected_rooms.Count > 1)
            {
                //Debug.Log("unconnected_rooms = " + ListOfIntToString(unconnected_rooms));
                //Debug.Log("Room "+ +" neighbor_rooms = " + ListOfIntToString(unconnected_rooms));
                for (int xx = 0; xx < rooms.Count; xx++)
                {
                    List<int> all_connected_to_xx = get_union_of_connected_room_indexes(xx);
                    //Debug.Log("NEIGHBORS of Room " + xx + "; neighbors  = " + ListOfIntToString(rooms[xx].neighbors));
                    //Debug.Log("NEIGHBORS of Room " + xx + "; all_connected_to_xx  = " + ListOfIntToString(all_connected_to_xx));
                }

                // Find two closest rooms (i and j),
                // and a point in each close to the other (close_i, close_j)
                // (not guaranteed to be THE closest, but good enough for room connections)
                Vector2Int closestPair = FindTwoClosestRooms(unconnected_rooms);
                if (closestPair == Vector2Int.zero)
                {
                    Debug.Log("Problem? No pairs found but unconnected_rooms.Count = " + unconnected_rooms.Count);
                    break; // no pairs found, exit loop
                }
                int i = closestPair.x;
                int j = closestPair.y;
                List<Vector2Int> all_tiles_i = get_union_of_connected_room_cells(i);
                List<Vector2Int> all_tiles_j = get_union_of_connected_room_cells(j);

                minimum_corridor_length = 50;
                // Closest points between rooom i and room j.
                //Debug.Log("Begin GetClosest");
                center_i = GetCenterOfTiles(all_tiles_i);
                close_j = GetClosestPointInTilesList(all_tiles_j, center_i, 0);
                close_i = GetClosestPointInTilesList(all_tiles_i, close_j, minimum_corridor_length);
                //Debug.Log("Done GetClosest");
                connection_room_i = -1;  // initial assumptions to be adjusted in for loop
                connection_room_j = -1;
                //rooms_overlap = false;
                for (int rn = 0; rn < rooms.Count; rn++)
                {
                    foreach (Cell cell in rooms[rn].cells)
                    {
                        if (cell.pos == close_i)
                        {
                            connection_room_i = rn;
                        }
                        if (cell.pos == close_j)
                        {
                            connection_room_j = rn;
                        }
                    }
                }
                if (connection_room_i == -1)
                {
                    Debug.Log("ERROR: No connection_room_i(" + i + ") found");
                    //yield return tm.YieldOrDelay(5f);
                }
                if (connection_room_j == -1)
                {
                    Debug.Log("ERROR: No connection_room_j(" + j + ") found");
                    //yield return tm.YieldOrDelay(5f);
                }
                if (connection_room_i == connection_room_j)
                {
                    Debug.Log($"Rooms overlap");
                    // TODO: what to do?
                    //rooms_overlap = true;
                }
                // find height of each corridor endpoint, limiting search to specific room
                //Debug.Log("Begin GetHeights");
                int height_i = GetHeightOfLocationFromOneRoom(rooms[connection_room_i], close_i);
                int height_j = GetHeightOfLocationFromOneRoom(rooms[connection_room_j], close_j);

                //minimum_corridor_length = (Math.Abs(height_i - height_j));


                // Carve the corridor and create a new room of it
                //Debug.Log($"Begin DrawCorridorsSloped from room {connection_room_i} to {connection_room_j}");
                Room corridorRoom = DrawCorridorSloped(close_i, close_j, height_i, height_j, connection_room_i, connection_room_j);
                //Debug.Log("End DrawCorridorsSloped");
                corridorRoom.isCorridor = true; // Mark as corridor
                corridorRoom.name = $"Corridor {connection_room_i}-{connection_room_j}";
                corridorRoom.setColorFloor(highlight: false); // Set corridor color
                corridorRoom.neighbors = new();

                // connect the two rooms and the new corridor via connected_rooms lists
                corridorRoom.neighbors.Add(connection_room_i);
                corridorRoom.neighbors.Add(connection_room_j);
                int corridor_room_no = rooms.Count;
                corridorRoom.my_room_number = corridor_room_no;
                rooms.Add(corridorRoom); // add new corridor room to the master list
                rooms[connection_room_i].neighbors.Add(corridor_room_no);
                rooms[connection_room_j].neighbors.Add(corridor_room_no);

                // Remove second room (j) from unconnected rooms list
                for (var index = 0; index < unconnected_rooms.Count; index++)
                {
                    //Debug.Log($"index = {index} of {unconnected_rooms.Count}");
                    if (tm.IfYield()) yield return null;
                    if (unconnected_rooms[index] == j)
                    {
                        unconnected_rooms.RemoveAt(index);
                        break; // found it, done removing j from unconnected rooms list
                    }
                }

                //Debug.Log($"Begin draw maps with {rooms.Count} rooms");
                DrawMapFromRoomsList(rooms);
                DrawWalls();

                //Debug.Log($"End draw maps with {rooms.Count} rooms");
                //yield return tm.YieldOrDelay(cfg.stepDelay / 3);
                if (tm.IfYield()) yield return null;
            }
            if (tm.IfYield()) yield return null;
            //Debug.Log("Ending ConnectRoomsByCorridors");
        }
        finally { if (local_tm) tm.End(); }
    }

    // ================= Helper functions ==========================

    // FindTwoClosestRooms does just that.  This way we connect rooms with short corridors first that are unlikely to
    // cross another room.
    // TODO: not very efficient, should use hashes
    public Vector2Int FindTwoClosestRooms(List<int> unconnected_rooms)
    {
        if (unconnected_rooms.Count < 2) return Vector2Int.zero; // not enough rooms

        Vector2Int closestPair = Vector2Int.zero;
        float minDistance = float.MaxValue;

        for (int i = 0; i < rooms.Count; i++)
        {
            if (!unconnected_rooms.Contains(i)) continue;  // i is not a unique room

            List<Vector2Int> room_cells_i = get_union_of_connected_room_cells(i);
            Vector2Int center_i = GetCenterOfTiles(room_cells_i);

            for (int j = i + 1; j < rooms.Count; j++)
            {
                if (!unconnected_rooms.Contains(j)) continue;  // j is not a unique room

                List<Vector2Int> room_cells_j = get_union_of_connected_room_cells(j);
                Vector2Int center_j = GetCenterOfTiles(room_cells_j);

                float distance = Vector2Int.Distance(center_i, center_j);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPair = new Vector2Int(i, j);
                }
            }
        }

        return closestPair;
    }

    // UNCHANGED
    // GetCeterOfTiles is used in finding a short corridor between unconnected rooms
    public Vector2Int GetCenterOfTiles(List<Vector2Int> tiles)
    {
        if (tiles.Count == 0) return new Vector2Int(0, 0);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;

        foreach (var t in tiles)
        {
            if (t.x < minX) minX = t.x;
            if (t.y < minY) minY = t.y;
            if (t.x > maxX) maxX = t.x;
            if (t.y > maxY) maxY = t.y;
        }

        return new Vector2Int(((minX + maxX) / 2), (minY + maxY) / 2);
    }

}
