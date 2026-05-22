using UnityEngine;

public partial class DungeonGenerator
{
    bool TryAddDiagonalCornerWalls(
        int roomNumber,
        Cell cell,
        Vector3 world,
        Vector3 cellSize,
        Texture2D roomWallpaper,
        Texture2D roomWallpaperMirror,
        out int triangleFloorDirection,
        out bool suppressNorth,
        out bool suppressEast,
        out bool suppressSouth,
        out bool suppressWest)
    {
        triangleFloorDirection = 0;
        suppressNorth = false;
        suppressEast = false;
        suppressSouth = false;
        suppressWest = false;

        if (!cfg.useDiagonalCorners || diagonalWallPrefab == null)
            return false;

        DirFlags cellDoors = cell.doors;
        bool northDoor = cellDoors.HasFlag(DirFlags.N);
        bool eastDoor = cellDoors.HasFlag(DirFlags.E);
        bool southDoor = cellDoors.HasFlag(DirFlags.S);
        bool westDoor = cellDoors.HasFlag(DirFlags.W);
        int numDoors = (northDoor ? 1 : 0) + (southDoor ? 1 : 0) + (eastDoor ? 1 : 0) + (westDoor ? 1 : 0);
        if (numDoors != 0)
            return false;

        DirFlags cellWalls = cell.walls;
        bool northWall = cellWalls.HasFlag(DirFlags.N);
        bool eastWall = cellWalls.HasFlag(DirFlags.E);
        bool southWall = cellWalls.HasFlag(DirFlags.S);
        bool westWall = cellWalls.HasFlag(DirFlags.W);

        int numWalls = (northWall ? 1 : 0) + (southWall ? 1 : 0) + (eastWall ? 1 : 0) + (westWall ? 1 : 0);
        if (numWalls != 2)
            return false;

        int x = cell.x;
        int z = cell.y;
        int ySteps = cell.height;
        float floorY = ySteps * cfg.unitHeight;
        float wallHeight = Mathf.Max(1, cfg.perimeterWallSteps) * cfg.unitHeight;
        float diagonalLength = DiagonalInsideLength(cellSize);
        Vector3 wallVerticalOffset = new Vector3(0f, floorY + wallHeight * 0.5f, 0f);

        if (northWall && eastWall)
        {
            AddDiagonalWall(roomNumber, x, z, ySteps, world + CornerOffset(east: true, north: true, cellSize) + wallVerticalOffset, Yaw45, cellSize, wallHeight, diagonalLength, roomWallpaper, roomWallpaperMirror);
            triangleFloorDirection = 0;
            if (cfg.skipOrthogonalWhenDiagonal) { suppressNorth = true; suppressEast = true; }
            return true;
        }

        if (northWall && westWall)
        {
            AddDiagonalWall(roomNumber, x, z, ySteps, world + CornerOffset(east: false, north: true, cellSize) + wallVerticalOffset, Yaw315, cellSize, wallHeight, diagonalLength, roomWallpaper, roomWallpaperMirror);
            triangleFloorDirection = 3;
            if (cfg.skipOrthogonalWhenDiagonal) { suppressNorth = true; suppressWest = true; }
            return true;
        }

        if (southWall && eastWall)
        {
            AddDiagonalWall(roomNumber, x, z, ySteps, world + CornerOffset(east: true, north: false, cellSize) + wallVerticalOffset, Yaw135, cellSize, wallHeight, diagonalLength, roomWallpaper, roomWallpaperMirror);
            triangleFloorDirection = 1;
            if (cfg.skipOrthogonalWhenDiagonal) { suppressSouth = true; suppressEast = true; }
            return true;
        }

        if (southWall && westWall)
        {
            AddDiagonalWall(roomNumber, x, z, ySteps, world + CornerOffset(east: false, north: false, cellSize) + wallVerticalOffset, Yaw225, cellSize, wallHeight, diagonalLength, roomWallpaper, roomWallpaperMirror);
            triangleFloorDirection = 2;
            if (cfg.skipOrthogonalWhenDiagonal) { suppressSouth = true; suppressWest = true; }
            return true;
        }

        return false;
    }

    void AddDiagonalWall(
        int roomNumber,
        int x,
        int z,
        int ySteps,
        Vector3 wallPosition,
        Quaternion wallRotation,
        Vector3 cellSize,
        float wallHeight,
        float diagonalLength,
        Texture2D roomWallpaper,
        Texture2D roomWallpaperMirror)
    {
        Vector3 wallScale = new Vector3(cellSize.x * 0.1f, wallHeight, diagonalLength);
        bool mirrorWallpaper = (x % 2) == 0;
        Texture2D diagonalWallpaper = mirrorWallpaper && roomWallpaperMirror != null ? roomWallpaperMirror : roomWallpaper;

        elementStore.AddWall(
            archetypeId: "DiagonalWall",
            isDiagonal: true,
            roomIndex: roomNumber,
            cellCoord: new Vector2Int(x, z),
            heightSteps: ySteps,
            worldPos: wallPosition,
            rotation: wallRotation,
            scale: wallScale,
            color: WallColorForRoom(roomNumber, diagonalWallpaper),
            textureOverride: diagonalWallpaper,
            customFlags: 0
        );
    }
}
