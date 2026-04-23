using UnityEngine;

public partial class DungeonGenerator
{
    // If your ramp mesh "forward" is +Z, map directions to rotations:
    static readonly Vector2Int[] Dir4 = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };

    static Quaternion RotFromDir(Vector2Int d)
    {
        if (d == new Vector2Int(0, 1)) return Quaternion.Euler(0, 0, 0);   // face +Z
        if (d == new Vector2Int(1, 0)) return Quaternion.Euler(0, 90, 0);
        if (d == new Vector2Int(0, -1)) return Quaternion.Euler(0, 180, 0);
        return Quaternion.Euler(0, 270, 0); // (-1,0)
    }

    // 45 degree yaw helpers
    static readonly Quaternion Yaw45 = Quaternion.Euler(0, -45, 0);
    static readonly Quaternion Yaw135 = Quaternion.Euler(0, -135, 0);
    static readonly Quaternion Yaw225 = Quaternion.Euler(0, -225, 0);
    static readonly Quaternion Yaw315 = Quaternion.Euler(0, -315, 0);

    // Original design had diagonals set back from the center of the tile.
    // These functions calculated that.  I replaced the calculation
    // with one that puts the diagonal straight through the middle,
    // but left the other code commented in case I'd like to try that again.
    static Vector3 CornerOffset(bool east, bool north, Vector3 cell)
    {
        // Don't offset, leaving wall diagonally across the center of the tile.
        float offsetX = (east ? +1f : -1f) * (cell.x * 0f);
        float offsetZ = (north ? +1f : -1f) * (cell.y * 0f); // grid.y maps to world Z

        // Offset from tile center toward a corner (1/4 cell each axis)
        //float offsetX = (east  ? +1f : -1f) * (cell.x * 0.25f);
        //float offsetZ = (north ? +1f : -1f) * (cell.y * 0.25f); // grid.y maps to world Z
        return new Vector3(offsetX, 0f, offsetZ);
    }

    static float DiagonalInsideLength(Vector3 cell)
    {
        // Length of strip across the center of the tile (corner to corner):
        float halfWidthX = cell.x * 1f;
        float halfWidthZ = cell.y * 1f;
        // Length of a strip across the tile on a 45 degree diagonal (midpoint to midpoint):
        //float halfWidthX = cell.x * 0.5f;
        //float halfWidthZ = cell.y * 0.5f;
        return Mathf.Sqrt(halfWidthX * halfWidthX + halfWidthZ * halfWidthZ);
    }
}
