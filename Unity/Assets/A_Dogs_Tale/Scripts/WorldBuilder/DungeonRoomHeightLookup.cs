using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // UNCHANGED
    public int GetHeightOfLocationFromOneRoom(Room room, Vector2Int pos)
    {
        //Debug.Log($"room.tiles = {room.tiles.Count}; room.heights = {room.heights.Count}");
        int height = room.GetHeightInRoom(pos);
        if (height != 999) return height; // found it

        //Debug.Log("location not found in room");
        return 999;
    }

    // UNCHANGED
    public int GetHeightOfLocationFromAllRooms(List<Room> rooms, Vector2Int pos)
    {
        int height;
        foreach (var room in rooms)
        {
            height = room.GetHeightInRoom(pos);
            if (height != 999) return height; // found it
        }
        //Debug.Log("location not found in rooms");
        return 999;
    }

    // Neighborhood searches...

    // UNCHANGED
    public int GetHeightInNeighborhood(int room_number, Vector2Int pos)
    {
        int ht = rooms[room_number].GetHeightInRoom(pos);
        if (ht != 999) return ht;
        List<int> myneighbors = rooms[room_number].neighbors;
        for (int i = 0; i < myneighbors.Count; i++)
        {
            ht = rooms[myneighbors[i]].GetHeightInRoom(pos);
            if (ht != 999) return ht;
        }
        return ht;
    }

    // UNCHANGED. UNUSED
    public bool IsTileInNeighborhood(int room_number, List<int> room_neighbors, Vector2Int pos)
    {
        //Debug.Log($"IsTileInNeighborhood: room_neighbors.Count={room_neighbors.Count} pos = {pos.x},{pos.y}");
        bool isit = rooms[room_number].IsTileInRoom(pos);
        if (isit) return isit;
        //List<int> myneighbors = rooms[room_number].neighbors;
        for (int i = 0; i < room_neighbors.Count; i++)
        {
            isit = rooms[room_neighbors[i]].IsTileInRoom(pos);
            if (isit) return isit;
        }
        //Debug.Log($"isit = {isit}, in room {room_number}");
        return isit;
    }
}
