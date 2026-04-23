using UnityEngine;
using System.Collections.Generic;
using System;

// ========================== Room class =================================
public class Room
{
    // == Properties of the room
    public int my_room_number = -1; // Uniquely identifies this room based on global "rooms" list
    public String name = "";

    // Tile-by-tile list of everything about a cell: floors/walls/doors/etc
    public List<Cell> cells = new();

    // NOTE: The above structure will replace these fields below.
    //public List<Vector2Int> tiles = new();
    //public List<Vector2Int> walls = new();
    public List<int> heights = new(); // Heights for each tile in the room, used for 3D generation

    public List<Door> doors = new();  // Details of every door in this room

    public int Size => cells.Count;     // OLD used tiles, NEW will use cells
    public int Last => cells.Count - 1; // Handy index for editing a newly added cell.
    public Color colorFloor = new(1f, 0.4f, 0.7f, 0.5f); // semi-transparent pink; // Color for the whole room, cell may override this
    public List<int> neighbors = new(); // List of neighboring rooms by index into global "rooms" list
    public bool isCorridor = false;     // Indicate if this room was generated as a corridor
    public bool connectedToCorridor = false; // Corridor defined as room 0.  gets set during build.  Should all be true for a fully connected map.
    [Header("Ceiling / Environment")]
    public float ceilingHeight = 3.5f;   // world units above floor
    public bool isOutdoor = false;       // or infer from placementTypes
    public Color colorCeiling = new(.076f, 0.75f, 0.63f, 1f); // Light Olive Grey
       
    public PlacementRoomTypeFlags placementTypes = PlacementRoomTypeFlags.Generic;
    public int area = 0;
    public RectInt bounds; // minX, minY, sizeX, sizeY

    // NEW style: After migrating to using class Cell instead of separate lists.
    // GetCellInRoom(pos) returns the index into this room's "cells" list.
    // on not finding the cell, function returns -1.
    public Dictionary<Vector2Int, int> cell_dictionary_room = new();


    // == constructors...
    public Room() { }

    // NEW
    public Room(List<Vector2Int> initialTileList, List<int> initialHeightsList)
    {
        cells = new List<Cell>();
        for (int i = 0; i < initialTileList.Count; i++)
        {
            cells.Add(new Cell(initialTileList[i].x, initialTileList[i].y, initialHeightsList[i]));
        }
    }

    public bool IsTileInRoom(Vector2Int pos)
    {
        int cell_num = GetCellInRoom(pos);
        return (cell_num >= 0);
    }

    public int GetCellInRoom(Vector2Int pos)
    {
        if (cell_dictionary_room.Count == 0) // then build cache
        {
            //Debug.Log($"Building cell_dictionary_room.");
            // Build dictionary once and keep it.
            //   Auto-regenerates if "cells" list length changes.
            //   Note that you must manually call ResetCellDictionary()
            //   yourself if you modify the list
            cell_dictionary_room = new(cells.Count);
            int cell_number = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cell_dictionary_room.TryAdd(cells[i].pos, cell_number))
                    cell_number++;
            }
        }
        // Here is the actual function.  Everything above was calculating the cache.
        return cell_dictionary_room.TryGetValue(pos, out var v) ? v : -1;
    }

    // NEW
    public void ResetCellDictionary()
    {
        Debug.Log($"Clearing cell_dictionary_room.");
        cell_dictionary_room = new();   // will force list to be regenerated next time it is used.
    }

    // NEW
    // simple helper lookup function for height.
    // Other fields could be done the same way.
    public int GetHeightInRoom(Vector2Int pos)
    {
        int index = GetCellInRoom(pos);
        //Debug.Log($"GetHeightInRoom: index = {index}, cells.Count = {cells.Count}");
        if (index >= 0) return cells[index].height;
        else return 999; // not found
    }

    public RectInt GetBounds()
    {
        if (cells == null || cells.Count == 0)
            return new RectInt(0, 0, 0, 0);

        int minX = cells[0].x;
        int maxX = cells[0].x;
        int minY = cells[0].y;
        int maxY = cells[0].y;

        foreach (var cell in cells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }
        // cache the value and then return it.
        bounds = new RectInt(minX, minY, (maxX - minX + 1), (maxY - minY + 1));
        return bounds;
    }


    // ==================== Color Helper functions...

    //setColorFloor sets all floors of a room to a color.

    // Set the color for the floor tiles in this room many ways...
    // room.setColorFloor(Color.white);        // White
    // room.setColorFloor(rgb: "#FF0000FF"); // Red
    // room.setColorFloor();                   // Bright Random
    // room.setColorFloor(highlight: false);   // Dark   Random
    // room.setColorFloor(highlight: true);    // Bright Random
    public Color setColorFloor(Color? color = null, bool highlight = true, string rgba = "")
    {
        colorFloor = getColor(color: color, highlight: highlight, rgba: rgba);
        return colorFloor;
    }

    //getColor is a simple helper to generate a Color based on various ways to specify a color
    // (see setColorFloor for examples)
    public Color getColor(Color? color = null, bool highlight = true, string rgba = "")
    {
        Color colorrgba; // temp
        Color return_color = Color.white;

        if (color != null)
            return_color = (Color)color;
        else if ((!string.IsNullOrEmpty(rgba)) && (ColorUtility.TryParseHtmlString(rgba, out colorrgba)))
            colorFloor = colorrgba;
        else if (highlight)
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f);   // Bright Random
        else // highlight == false
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.1f, 0.4f); // Dark Random

        return return_color;
    }

}
