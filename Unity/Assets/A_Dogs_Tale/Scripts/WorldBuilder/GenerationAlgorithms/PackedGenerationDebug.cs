using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // used only for debug
    void ClearMapBorders(List<Room> rooms)  // in case the routines here spill out into border like they aren't supposed to, clear them out.
    {                                       // NOTE: Problem seems to be in wall add routine at last x and last y.
        int r_num, c_num;
        int removed = 0;
        for (r_num = rooms.Count - 1; r_num >= 0; r_num--)
        {
            for (c_num = rooms[r_num].cells.Count - 1; c_num >= 0; c_num--) // must count cells backwards because we are deleting as we go.
            {
                if (!In(rooms[r_num].cells[c_num].x, rooms[r_num].cells[c_num].y))
                    rooms[r_num].cells.RemoveAt(c_num);
                removed++;
            }
        }
        Debug.Log($"ClearMapBorders cleared {removed} cells.");  // BUG: WOW big numbers even when the preceeding function did nothing.  What's wrong?
    }

    bool CheckRoomsToGridConsistancy()
    {
        return PackedGenerationValidator.CheckRoomsToGridConsistency(rooms, cellGrid, cfg.mapWidth, cfg.mapHeight, cfg.borderKeepout);
    }
}
