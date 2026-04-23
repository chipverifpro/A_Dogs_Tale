using UnityEngine;

public static class DoorPlacement
{
    /// Convert a grid cell to world. If your grid origin/scale differs, adjust here.
    public static Vector3 GridToWorld(Vector2Int cell, float cellSize = 1f, float floorY = 0f)
        => new Vector3(cell.x * cellSize, floorY, cell.y * cellSize);

    /// Compute a world pose (position/rotation) to place the door prefab.
    /// For edge: midpoint between aEntry and bEntry.
    /// For tile: center of wallStart.
    public static void GetWorldPose(Door door, out Vector3 pos, out Quaternion rot, float cellSize = 1f, float floorY = 0f)
    {
        var anc = door.anchor;
        Vector3 forward = DirToForward(anc.normal);

        if (anc.type == DoorAnchorType.Edge)
        {
            var a = GridToWorld(anc.aEntry, cellSize, floorY);
            var b = GridToWorld(anc.bEntry, cellSize, floorY);
            pos = (a + b) * 0.5f;
        }
        else
        {
            var c = GridToWorld(anc.wallStart, cellSize, floorY);
            pos = c;
        }

        rot = Quaternion.LookRotation(forward, Vector3.up);
    }

    public static Vector3 GetWorldSize(Door door, float cellSize = 1f, float defaultThickness = 0.15f, float height = 2.2f)
    {
        var anc = door.anchor;
        float width = Mathf.Max(1, anc.spanTiles) * cellSize;
        float depth = anc.IsTileAnchored ? Mathf.Max(1, anc.throughDepthTiles) * cellSize : defaultThickness;
        return new Vector3(width, height, depth);
    }

    private static Vector3 DirToForward(Direction4 d) => d switch
    {
        Direction4.North => new Vector3(0, 0, 1),
        Direction4.South => new Vector3(0, 0, -1),
        Direction4.East => new Vector3(1, 0, 0),
        Direction4.West => new Vector3(-1, 0, 0),
        _ => Vector3.forward
    };
}
