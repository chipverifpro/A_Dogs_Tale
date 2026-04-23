using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // NEW Apply this function to a room to give subtle ripples to the floor heights.
    public Room AddPerlinToFloorHeights(Room room)
    {
        if (cfg.perlinFloorHeights == 0) return room;

        int perlinFloor;
        float seedX = cfg.GlobalPerlinSeed ? perlinSeedX : UnityEngine.Random.Range(0f, 9999f);
        float seedY = cfg.GlobalPerlinSeed ? perlinSeedY : UnityEngine.Random.Range(0f, 9999f);
        foreach (Cell cell in room.cells)
        {
            perlinFloor = (int)(Mathf.PerlinNoise((cell.x + seedX) * cfg.perlinFloorWavelength,
                                                  (cell.y + seedY) * cfg.perlinFloorWavelength)
                                                  * cfg.perlinFloorHeights);
            cell.height += perlinFloor;
        }
        return room;
    }

    public Room TiltRoom(Room room, Vector2 topDir, float angleDeg, float heightUnitsPerTile = 1f)
    {
        if (room == null || room.cells == null || room.cells.Count == 0) return room;
        if (topDir.sqrMagnitude < 1e-8f) return room;
        if (Mathf.Abs(angleDeg) < 1e-6f) return room;

        Vector2 dir = topDir.normalized;
        RectInt b = room.GetBounds();
        float cx = b.xMin + (b.width - 1) * 0.5f;
        float cy = b.yMin + (b.height - 1) * 0.5f;
        float slopePerTile = Mathf.Tan(angleDeg * Mathf.Deg2Rad) / heightUnitsPerTile;

        Debug.Log($"Tilting room {room.my_room_number} in direction {dir} with max angle {angleDeg}°. Slope per tile = {slopePerTile:F3} height units.");
        foreach (var cell in room.cells)
        {
            float proj = (cell.x - cx) * dir.x + (cell.y - cy) * dir.y;
            float delta = slopePerTile * proj;
            int dInt = Mathf.RoundToInt(delta);
            cell.height += dInt;
        }

        return room;
    }

    // UNUSED NEW
    // MoveRoom will shift a room in x,y,and z(height) directions.
    // If allow_collision = false, room doesn't move when it collides with another room.
    // TODO: check for collision.  Also allow rotation, scaling, growing?
    public bool MoveRoom(int room_number, Vector3Int transpose_vector, bool allow_collision = true)
    {
        List<Cell> newCells = new();
        List<Door> newDoors = new();
        int collisions = 0;

        for (int tileNumber = 0; tileNumber < rooms[room_number].cells.Count; tileNumber++)
        {
            Vector2Int newFloor = rooms[room_number].cells[tileNumber].pos + new Vector2Int(transpose_vector.x, transpose_vector.y);
            int newHeight = rooms[room_number].heights[tileNumber] + transpose_vector.z;
            newCells.Add(new Cell(newFloor.x, newFloor.y, newHeight));
        }
        if (collisions == 0 || allow_collision)
        {
            rooms[room_number].cells = newCells;
            rooms[room_number].doors = newDoors;
            return true;
        }
        else
        {
            return false;
        }
    }
}
