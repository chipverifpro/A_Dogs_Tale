using System.Collections;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{
    [SerializeField] private ElementStore elementStore;

    [Header("Floor Appearance")]
    [Tooltip("0 = solid color, 1 = black & white check")]
    public float checkerFloorStrength = 0.25f; 

    [Header("Surface Optimization")]
    [Tooltip("Post-process flat floor/ceiling tiles into larger rectangular quads before manufacturing GameObjects.")]
    [SerializeField] private bool mergeFlatSurfaceTiles = true;

    [Tooltip("Minimum rectangle area in tiles before a flat floor/ceiling region is replaced by one larger tile.")]
    [SerializeField] private int minMergedSurfaceArea = 2;

    [Tooltip("Post-process uninterrupted normal wall segments into longer wall runs before manufacturing GameObjects.")]
    [SerializeField] private bool mergeContinuousWalls = true;

    [Tooltip("Minimum number of adjacent wall segments required before replacing them with one longer wall.")]
    [SerializeField] private int minMergedWallLength = 2;

    [Header("Ceiling Appearance")]

    [Tooltip("Tiny extra z-offset in grid units if you want ceilings slightly above nominal height.")]
    public float ceilingZOffset = 20f;

    [Tooltip("Inset orthogonal wall segments slightly toward the room interior to avoid z-fighting with adjacent-room walls.")]
    [SerializeField] private float wallInsetIntoRoom = 0.01f;

    // 3D Build routine from rooms list.  Places prefabs in correct places.
    //   Includes floors, walls, ramps, cliffs
    //   Eventually expand to include doors, etc.
    public IEnumerator Build3DFromRooms(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Build3DFromRooms"); local_tm = true; }
        try
        {
            if (root == null) root = new GameObject("Terrain3D").transform; // TODO: get existing game object?

            //Destroy3D(); // Clear old objects -- old version
            elementStore.ClearInstances(); // New version using ManufactureGO
            ResetWallpaperTextureCache();

            for (int room_number = 0; room_number < rooms.Count; room_number++)
            {
                //Debug.Log($"Build3DFromOneRoom START room_number = {room_number}");
                yield return StartCoroutine(Build3DFromOneRoom(room_number, tm: null));
                //Debug.Log($"Build3DFromOneRoom DONE room_number = {room_number}");
                //if (tm.IfYield()) yield return null;
            }
            OptimizeFlatSurfaceTiles();
            dir.manufactureGO.BuildAll();
        }
        finally { if (local_tm) tm.End(); }
    }

    public IEnumerator Build3DFromOneRoom(int room_number, TimeTask tm = null)
    {
        if (elementStore == null)
        {
            Debug.LogError("Build3DFromOneRoom: ElementStore is not assigned.");
            yield break;
        }

        bool local_tm = false;
        //if (tm == null) { tm = TimeManager.Instance.BeginTask("Build3DFromOneRoom"); local_tm = true; }

        try
        {
            Vector3 world = new();
            Vector3 cellSize = grid.cellSize;
            Texture2D roomWallpaper = applyWallpaperOnWallTiles
                ? GetWallpaperTextureForRoom(room_number, useMirror: false)
                : null;
            Texture2D roomWallpaperMirror = applyWallpaperOnWallTiles
                ? GetWallpaperTextureForRoom(room_number, useMirror: true)
                : null;

            int num_cells = rooms[room_number].cells.Count;

            for (int cell_number = 0; cell_number < num_cells; cell_number++)
            {
//                if ((cell_number % 500) == 0)
//                    if (tm.IfYield()) yield return null;

                Cell cell = rooms[room_number].cells[cell_number];
                Vector2Int pos = cell.pos;
                int x = pos.x;
                int z = pos.y;
                Color colorFloor = cell.colorFloor;
                if (colorFloor == colorDefault) // if cell has no color, use room's color
                    colorFloor = rooms[room_number].colorFloor;

                // Base world position of this tile center
                world = grid.CellToWorld(new Vector3Int(x, z, 0));

                bool useTriangleFloor = TryAddDiagonalCornerWalls(
                    room_number,
                    cell,
                    world,
                    cellSize,
                    roomWallpaper,
                    out int triangleFloorDirection,
                    out bool suppressNorth,
                    out bool suppressEast,
                    out bool suppressSouth,
                    out bool suppressWest);

                AddPerimeterWallsAndRamps(
                    room_number,
                    cell,
                    world,
                    cellSize,
                    roomWallpaper,
                    roomWallpaperMirror,
                    suppressNorth,
                    suppressEast,
                    suppressSouth,
                    suppressWest);

                AddSurfaceTiles(room_number, cell, world, colorFloor, useTriangleFloor, triangleFloorDirection);
            } // end cell loop
        }
        finally
        {
            if (local_tm) tm.End();
        }
    }

} // End class HeightMap3DBuilder
