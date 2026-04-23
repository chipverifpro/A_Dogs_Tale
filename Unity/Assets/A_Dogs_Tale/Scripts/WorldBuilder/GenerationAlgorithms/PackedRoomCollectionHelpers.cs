using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // ROOM READY
    // Never used, algorithm instead removes duplicates later.
    // Looks for an existing Room that is a duplicate of the one being added.
    bool tryAddRoom(List<Room> rooms, Room room)
    {
        foreach (Room r in rooms)
        {
            // TODO: check contents instead ?????
            if (r == room) return false;
        }
        rooms.Add(room);
        return true;
    }

    // ROOM READY
    // Never used, algorithm instead removes duplicates later.
    // Looks for an existing Cell that is a duplicate of the one being added.
    bool tryAddCell(List<Cell> cells, Cell cell)
    {
        foreach (Cell c in cells)
        {
            if (c == cell) return false;        // exact match ?  check contents
        }
        cells.Add(cell);
        return true;
    }

    // Unused
    bool tryAddPackRoom(List<Room> rooms, Room room)
    {
        foreach (Room r in rooms)
        {
            // TODO: check contents instead ?????
            if (r == room) return false;
        }
        rooms.Add(room);
        return true;
    }

    // Unused
    bool tryAddPackCell(List<Cell> cells, Cell cell)
    {
        foreach (Cell c in cells)
        {
            if (c == cell) return false;
        }
        cells.Add(cell);
        return true;
    }

    // ROOM READY
    // Searches a list of cells and removes duplicates matching X,Y,AND Z (usually list is of a single room)
    // See the function RemoveDuplicateCellsFromAllRooms() for the global search.
    public static int RemoveDuplicateCells(List<Cell> cells)
    {
        if (cells == null || cells.Count == 0) return 0;

        int originalCount = cells.Count;

        // Seen coordinates
        var seen = new HashSet<(int, int, int)>();
        // New list preserving order
        var unique = new List<Cell>(cells.Count);

        foreach (var c in cells)
        {
            var key = (c.x, c.y, c.z);
            if (!seen.Contains(key))
            {
                seen.Add(key);
                unique.Add(c);   // preserve first occurrence order
            }
        }

        // Replace original contents
        cells.Clear();
        cells.AddRange(unique);

        int num_removed = originalCount - cells.Count;
        if (num_removed > 0) Debug.Log($"RemoveDuplicateCells removed {num_removed}");
        return num_removed; // number removed
    }

    // ROOMS READY
    // Searches all Cells in all Rooms and removes duplicates matching X,Y,AND Z.
    public static int RemoveDuplicateCellsFromAllRooms(List<Room> rooms)
    {
        int originalCount = 0;
        int afterCount = 0;

        // Seen coordinates
        var seen = new HashSet<(int, int, int)>();  // Seen hash set
        // New list preserving order
        var unique = new List<Cell>(1024);          // Only the unique Cells

        foreach (Room room in rooms)
        {
            List<Cell> cells = room.cells;

            if (cells == null || cells.Count == 0) return 0;

            originalCount += cells.Count;

            foreach (var c in cells)
            {
                var key = (c.x, c.y, c.z);
                if (!seen.Contains(key))
                {
                    seen.Add(key);
                    unique.Add(c);   // preserve first occurrence order
                }
            }

            // Replace original contents
            cells.Clear();
            cells.AddRange(unique);
            afterCount += cells.Count;
        }
        int num_removed = originalCount - afterCount;
        if (num_removed > 0) Debug.Log($"RemoveDuplicateCellsFromAllRooms removed {num_removed}");
        return num_removed; // number removed
    }
}
