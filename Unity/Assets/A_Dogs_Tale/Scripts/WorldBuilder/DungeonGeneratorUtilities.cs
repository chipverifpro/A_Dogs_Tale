using System;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // NEW
    /// <summary>
    /// Merge rooms that overlap (share at least one tile).
    /// If considerAdjacency is true, rooms that touch by edge/corner are merged too.
    /// </summary>
    /// <param name="rooms">Input rooms (each has List<Vector2Int> tiles)</param>
    /// <param name="considerAdjacency">If true, merge when tiles are neighbors (4- or 8-connected)</param>
    /// <param name="eightWay">If adjacency is considered, choose 4-way or 8-way</param>
    public static List<Room> MergeOverlappingRooms(List<Room> rooms, bool considerAdjacency = false, bool eightWay = true)
    {
        return RoomMergeUtility.MergeOverlappingRooms(rooms, considerAdjacency, eightWay);
    }

    public static bool Check(object o, string name, UnityEngine.Object ctx = null)
    {
        if (o == null)
        {
            Debug.LogError($"[Agent] Null reference: {name}", ctx);
            return false;
        }
        return true;
    }

    public String ListOfIntToString(List<int> ilist, bool do_sort = true)
    {
        String result = "List: ";
        if (do_sort) ilist.Sort();
        foreach (int i in ilist)
        {
            result = result + i + ",";
        }
        return result;
    }

    public Color getColor(Color? color = null, bool highlight = true, string rgba = "")
    {
        Color colorrgba = new(); //temp
        Color return_color = Color.white;

        if (color != null)
            return_color = (Color)color;
        else if ((!string.IsNullOrEmpty(rgba)) && (ColorUtility.TryParseHtmlString(rgba, out colorrgba)))
            return_color = colorrgba;
        else if (highlight)
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f);   // Bright Random
        else // highlight == false
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.1f, 0.4f); // Dark Random

        return return_color;
    }

    // convert cell float position to cell int position.
    public Vector2Int CellAtPosition(Vector2 position)
    {
        int x = Mathf.FloorToInt(position.x);
        int y = Mathf.FloorToInt(position.y);
        return new Vector2Int(x, y);
    }
}
