using UnityEngine;

public partial class DungeonGenerator
{
    void AddPerimeterWallsAndRamps(
        int roomNumber,
        Cell cell,
        Vector3 world,
        Vector3 cellSize,
        Texture2D roomWallpaper,
        Texture2D roomWallpaperMirror,
        bool suppressNorth,
        bool suppressEast,
        bool suppressSouth,
        bool suppressWest)
    {
        int x = cell.x;
        int z = cell.y;
        int ySteps = cell.height;
        DirFlags cellWalls = cell.walls;
        DirFlags cellDoors = cell.doors;

        for (int directionIndex = 0; directionIndex < 4; directionIndex++)
        {
            Vector2Int direction = Dir4[directionIndex];
            int neighborX = x + direction.x;
            int neighborZ = z + direction.y;
            bool neighborIsWall = false;
            bool neighborIsDoor = false;

            if (neighborX < 0 || neighborZ < 0 || neighborX >= cfg.mapWidth || neighborZ >= cfg.mapHeight)
            {
                neighborIsWall = true;     // off map
                neighborIsDoor = false;
            }

            if (direction.x == 0 && direction.y == 1) neighborIsWall = cellWalls.HasFlag(DirFlags.N);
            if (direction.x == 1 && direction.y == 0) neighborIsWall = cellWalls.HasFlag(DirFlags.E);
            if (direction.x == 0 && direction.y == -1) neighborIsWall = cellWalls.HasFlag(DirFlags.S);
            if (direction.x == -1 && direction.y == 0) neighborIsWall = cellWalls.HasFlag(DirFlags.W);

            if (direction.x == 0 && direction.y == 1) neighborIsDoor = cellDoors.HasFlag(DirFlags.N);
            if (direction.x == 1 && direction.y == 0) neighborIsDoor = cellDoors.HasFlag(DirFlags.E);
            if (direction.x == 0 && direction.y == -1) neighborIsDoor = cellDoors.HasFlag(DirFlags.S);
            if (direction.x == -1 && direction.y == 0) neighborIsDoor = cellDoors.HasFlag(DirFlags.W);

            bool suppressDirection =
                (direction.x == 0 && direction.y == 1 && suppressNorth) ||
                (direction.x == 1 && direction.y == 0 && suppressEast) ||
                (direction.x == 0 && direction.y == -1 && suppressSouth) ||
                (direction.x == -1 && direction.y == 0 && suppressWest);

            if ((neighborIsWall || neighborIsDoor) && cliffPrefab != null && !suppressDirection)
            {
                AddPerimeterWall(roomNumber, x, z, ySteps, neighborX, neighborZ, direction, cellSize, roomWallpaper, roomWallpaperMirror, neighborIsDoor);
            }

            int neighborHeightSteps = GetHeightInNeighborhood(roomNumber, new Vector2Int(neighborX, neighborZ));
            int heightDifference = neighborHeightSteps - ySteps;
            if (heightDifference == 0)
                continue;

            if ((Mathf.Abs(heightDifference) >= cfg.minimumRamp) &&
                (Mathf.Abs(heightDifference) <= cfg.maximumRamp) &&
                rampPrefab != null)
            {
                bool goesUp = heightDifference > 0;
                if (goesUp) continue; // keep your existing "only one side makes the ramp" rule

                Vector3 neighborWorld = grid.CellToWorld(new Vector3Int(neighborX, neighborZ, 0));
                int upperHeight = goesUp ? neighborHeightSteps : ySteps;
                Quaternion rampRotation = RotFromDir(direction * (goesUp ? 1 : -1)); // face uphill
                Vector3 rampPosition = neighborWorld + new Vector3(0, upperHeight * cfg.unitHeight, 0);
                Vector3 rampScale = new Vector3(cellSize.x, Mathf.Abs(heightDifference) * cfg.unitHeight * 1.2f, cellSize.y);

                elementStore.AddRamp(
                    archetypeId: "Ramp",
                    roomIndex: roomNumber,
                    cellCoord: new Vector2Int(x, z),
                    heightSteps: ySteps,
                    worldPos: rampPosition,
                    rotation: rampRotation,
                    scale: rampScale,
                    color: Color.white,
                    heightDelta: heightDifference
                );
            }
        }
    }

    void AddPerimeterWall(
        int roomNumber,
        int x,
        int z,
        int floorSteps,
        int neighborX,
        int neighborZ,
        Vector2Int direction,
        Vector3 cellSize,
        Texture2D roomWallpaper,
        Texture2D roomWallpaperMirror,
        bool neighborIsDoor)
    {
        Vector3 world = grid.CellToWorld(new Vector3Int(x, z, 0));
        Vector3 neighborWorld = grid.CellToWorld(new Vector3Int(neighborX, neighborZ, 0));
        Vector3 wallMidpoint = 0.5f * (world + neighborWorld);

        float wallHeight = Mathf.Max(1, cfg.perimeterWallSteps) * cfg.unitHeight;
        float baseY = floorSteps * cfg.unitHeight;

        Vector3 wallPosition = wallMidpoint + new Vector3(0, baseY + (0.5f * wallHeight), 0);
        wallPosition += new Vector3(-direction.x, 0f, -direction.y) * wallInsetIntoRoom;
        Quaternion wallRotation = RotFromDir(new Vector2Int(neighborX - x, neighborZ - z));
        Vector3 wallScale = new Vector3(cellSize.x, wallHeight, cellSize.y * 0.1f);

        bool mirrorWallpaper = !neighborIsDoor && (((x + z) % 2) == 0);
        Texture2D wallWallpaper = mirrorWallpaper ? roomWallpaperMirror : roomWallpaper;

        int customFlags = neighborIsDoor ? 1 : 0; // 1 = door segment
        Color wallColor = neighborIsDoor ? Color.red : WallColorForRoom(roomNumber, wallWallpaper);

        elementStore.AddWall(
            archetypeId: neighborIsDoor ? "Door" : "Wall",
            isDiagonal: false,
            roomIndex: roomNumber,
            cellCoord: new Vector2Int(x, z),
            heightSteps: floorSteps,
            worldPos: wallPosition,
            rotation: wallRotation,
            scale: wallScale,
            color: wallColor,
            textureOverride: neighborIsDoor ? null : wallWallpaper,
            customFlags: customFlags
        );
    }

    Color WallColorForRoom(int roomNumber, Texture2D wallWallpaper)
    {
        if (wallWallpaper != null)
            return Color.white;

        if (rooms == null || roomNumber < 0 || roomNumber >= rooms.Count || rooms[roomNumber] == null)
            return Color.white;

        return rooms[roomNumber].colorWalls;
    }
}
