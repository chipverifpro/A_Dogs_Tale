using UnityEngine;

public partial class DungeonGenerator
{
    private void DrawRoomWallTiles(Cell cell)
    {
        Vector3Int centerPos = new Vector3Int(cell.x * 3 + 1, cell.y * 3 + 1, 0);

        for (int i = 0; i < 4; i++)
        {
            if (!TryGetDirectionalTile(cell.walls, i, out Vector3Int d, out int rotate))
                continue;

            SetTilemapWithTransforms(
                tilemap: tilemap_walls,
                pos: centerPos + d,
                offset: (Vector3)d,
                rotation: new Vector3(90f, rotate, 0f),
                scale: new Vector3(3f, 0.1f, 0f),
                tile: wallTile,
                color: Color.black);
        }
    }

    private void DrawRoomDoorTiles(Cell cell)
    {
        Vector3Int centerPos = new Vector3Int(cell.x * 3 + 1, cell.y * 3 + 1, 0);

        for (int i = 0; i < 4; i++)
        {
            if (!TryGetDirectionalTile(cell.doors, i, out Vector3Int d, out int rotate))
                continue;

            SetTilemapWithTransforms(
                tilemap: tilemap_doors,
                pos: centerPos + d,
                offset: Vector3.zero,
                rotation: new Vector3(90f, rotate, 0f),
                scale: new Vector3(1f, 1f, 0.01f),
                tile: floorTile,
                color: Color.red);
        }
    }

    private bool TryGetDirectionalTile(DirFlags flags, int index, out Vector3Int direction, out int rotationY)
    {
        direction = default;
        rotationY = 0;

        switch (index)
        {
            case 0:
                if (!flags.HasFlag(DirFlags.N)) return false;
                direction = Vector3Int.up;
                rotationY = 0;
                return true;
            case 1:
                if (!flags.HasFlag(DirFlags.S)) return false;
                direction = Vector3Int.down;
                rotationY = 0;
                return true;
            case 2:
                if (!flags.HasFlag(DirFlags.W)) return false;
                direction = Vector3Int.left;
                rotationY = 90;
                return true;
            case 3:
                if (!flags.HasFlag(DirFlags.E)) return false;
                direction = Vector3Int.right;
                rotationY = 90;
                return true;
            default:
                return false;
        }
    }
}
