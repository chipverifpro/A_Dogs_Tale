using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // UNCHANGED
    // create a complete list of all rooms connected, ignoring duplicates
    public List<int> get_union_of_connected_room_indexes(int start_room_number, bool everything = true)
    {
        bool added = true;
        List<int> rooms_to_connect = new();
        rooms_to_connect.Add(start_room_number);
        rooms_to_connect.AddRange(rooms[start_room_number].neighbors);

        // if everything, include all neighboring rooms of neighbors
        // if !everything, only include direct neighbors
        if (!everything) return rooms_to_connect;

        // create a complete list of all rooms connected, ignoring duplicates
        // keep going over the list until no more to add
        while (added == true)
        {
            added = false;

            for (int i = 0; i < rooms_to_connect.Count; i++)
            {
                for (int j = 0; j < rooms[rooms_to_connect[i]].neighbors.Count; j++)
                {
                    if (!rooms_to_connect.Contains(rooms[rooms_to_connect[i]].neighbors[j]))
                    {
                        rooms_to_connect.Add(rooms[rooms_to_connect[i]].neighbors[j]);
                        added = true;
                    }
                }
            }
        }
        return rooms_to_connect;
    }

    // NEW
    public List<Vector2Int> get_union_of_connected_room_cells(int start_room_number, bool everything = true)
    {
        List<Vector2Int> union_of_cells = new();
        // create a complete list of all rooms connected, ignoring duplicates
        List<int> rooms_to_connect = get_union_of_connected_room_indexes(start_room_number, everything);

        // add tiles from all connected rooms to the list (union of cells)
        for (int i = 0; i < rooms_to_connect.Count; i++)
        {
            foreach (Cell cell in rooms[rooms_to_connect[i]].cells)
                union_of_cells.Add(cell.pos);
        }

        //Debug.Log("get_union_of_connected_room_cells(" + start_room_number + ") -> length " + union_of_cells.Count + " END");
        return union_of_cells;
    }
}
