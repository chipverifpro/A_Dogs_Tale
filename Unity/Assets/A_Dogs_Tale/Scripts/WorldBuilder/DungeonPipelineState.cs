using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    private void ResetGenerationState()
    {
        room_rects = new List<RectInt>(); // Clear the list of room rectangles
        map = new byte[cfg.mapWidth, cfg.mapHeight];
        mapHeights = new int[cfg.mapWidth, cfg.mapHeight];
        Destroy3D();
        hf = null; // clear heightfield
    }

    private void InitializeDungeonTiles()
    {
        tilemap.ClearAllTiles();
        tilemap_walls.ClearAllTiles();
        tilemap_doors.ClearAllTiles();
        rooms.Clear();
        map = new byte[cfg.mapWidth, cfg.mapHeight];
        FillVoidToWalls(map);
    }

    private bool HasGeneratedRooms()
    {
        return cfg.useCellularAutomata || cfg.useScatterRooms || cfg.usePackedRooms;
    }

    private bool NeedsRoomCorridors()
    {
        return cfg.useCellularAutomata || cfg.useScatterRooms;
    }
}
