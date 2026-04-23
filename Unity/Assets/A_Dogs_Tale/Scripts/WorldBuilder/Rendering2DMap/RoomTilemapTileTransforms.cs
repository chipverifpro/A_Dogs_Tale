using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator
{
    public void SetTilemapWithTransforms(Tilemap tilemap, Vector3Int pos, Vector3 offset, Vector3 rotation, Vector3 scale, TileBase tile, Color color)
    {
        Matrix4x4 transformM = Matrix4x4.TRS(
            offset,
            Quaternion.Euler(rotation),
            scale
        );

        tilemap.SetTile(pos, tile);
        tilemap.SetTileFlags(pos, TileFlags.None);
        tilemap.SetColor(pos, color);
        tilemap.SetTransformMatrix(pos, transformM);
    }

    private void DrawRoomFloorTile(Vector3Int pos3, Color roomColor)
    {
        SetTilemapWithTransforms(
            tilemap: tilemap,
            pos: pos3,
            offset: Vector3.zero,
            rotation: new Vector3(90f, 0f, 0f),
            scale: Vector3.one,
            tile: floorTile,
            color: roomColor);
    }
}
