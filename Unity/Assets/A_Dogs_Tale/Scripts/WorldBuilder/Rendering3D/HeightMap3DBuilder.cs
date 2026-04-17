using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{
    [SerializeField] private ElementStore elementStore;
    [SerializeField] private string wallpaperResourcesPath = "Sprites/Wallpaper";
    [SerializeField] private string wallpaperResourcesPath_mirror = "Sprites/Wallpaper_Mirror";

    private readonly Dictionary<int, Texture2D> roomWallpaperByRoomIndex = new();
    private readonly Dictionary<int, Texture2D> roomWallpaperMirrorByRoomIndex = new();
    private readonly Dictionary<int, string> roomWallpaperKeyByRoomIndex = new();
    private Texture2D[] cachedWallpaperTextures;
    private Texture2D[] cachedWallpaperTexturesMirror;
    private Dictionary<string, Texture2D> cachedWallpaperTexturesByKey;
    private Dictionary<string, Texture2D> cachedWallpaperTexturesMirrorByKey;
    private string[] cachedWallpaperSelectionKeys;

    [Header("Floor Appearance")]
    [Tooltip("0 = solid color, 1 = black & white check")]
    public float checkerFloorStrength = 0.25f; 

    [Header("Ceiling Appearance")]

    [Tooltip("Tiny extra z-offset in grid units if you want ceilings slightly above nominal height.")]
    public float ceilingZOffset = 20f;

    [Header("Wall Appearance")]
    [Tooltip("Enable applying wallpaper textures to generated wall tiles.")]
    [SerializeField] private bool applyWallpaperOnWallTiles = true;

    [Tooltip("Inset orthogonal wall segments slightly toward the room interior to avoid z-fighting with adjacent-room walls.")]
    [SerializeField] private float wallInsetIntoRoom = 0.01f;

    // If your ramp mesh "forward" is +Z, map directions to rotations:
    static readonly Vector2Int[] Dir4 = { new(0, 1), new(1, 0), new(0, -1), new(-1, 0) };

    static Quaternion RotFromDir(Vector2Int d)
    {
        if (d == new Vector2Int(0, 1)) return Quaternion.Euler(0, 0, 0);   // face +Z
        if (d == new Vector2Int(1, 0)) return Quaternion.Euler(0, 90, 0);
        if (d == new Vector2Int(0, -1)) return Quaternion.Euler(0, 180, 0);
        return Quaternion.Euler(0, 270, 0); // (-1,0)
    }

    // 45° yaw helpers
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

        // Offset from tile center toward a corner (¼ cell each axis)
        //float offsetX = (east  ? +1f : -1f) * (cell.x * 0.25f);
        //float offsetZ = (north ? +1f : -1f) * (cell.y * 0.25f); // grid.y maps to world Z
        return new Vector3(offsetX, 0f, offsetZ);
    }

    static float DiagonalInsideLength(Vector3 cell)
    {
        // Lenght of strip across the center of the tile (corner to corner):
        float halfWidthX = cell.x * 1f;
        float halfWidthZ = cell.y * 1f;
        // Length of a strip across the tile on a 45° diagonal (midpoint to midpoint):
        //float halfWidthX = cell.x * 0.5f;
        //float halfWidthZ = cell.y * 0.5f;
        return Mathf.Sqrt(halfWidthX * halfWidthX + halfWidthZ * halfWidthZ);
    }

    // if root exists, destroy all 3D objects under it.
    // AKA: clear 3D tiles.
    public void Destroy3D()
    {
        if (root == null) return;
        for (int childIndex = root.childCount - 1; childIndex >= 0; childIndex--)
            Destroy(root.GetChild(childIndex).gameObject);
    }

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
            roomWallpaperByRoomIndex.Clear();
            roomWallpaperMirrorByRoomIndex.Clear();
            roomWallpaperKeyByRoomIndex.Clear();
            cachedWallpaperTextures = null;
            cachedWallpaperTexturesMirror = null;
            cachedWallpaperTexturesByKey = null;
            cachedWallpaperTexturesMirrorByKey = null;
            cachedWallpaperSelectionKeys = null;

            for (int room_number = 0; room_number < rooms.Count; room_number++)
            {
                //Debug.Log($"Build3DFromOneRoom START room_number = {room_number}");
                yield return StartCoroutine(Build3DFromOneRoom(room_number, tm: null));
                //Debug.Log($"Build3DFromOneRoom DONE room_number = {room_number}");
                //if (tm.IfYield()) yield return null;
            }
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
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Build3DFromOneRoom"); local_tm = true; }

        try
        {
            Vector3 wallMidpoint = new();
            Vector3 world = new();
            Vector3 neighborWorld = new();
            Vector3 cellSize = grid.cellSize;
            bool useTriangleFloor = false;
            int triangleFloorDirection = 0;
            DirFlags cellWalls = DirFlags.None;
            DirFlags cellDoors = DirFlags.None;
            Color colorScent = getColor(Color.purple);
            Texture2D roomWallpaper = applyWallpaperOnWallTiles
                ? GetWallpaperTextureForRoom(room_number, useMirror: false)
                : null;
            Texture2D roomWallpaperMirror = applyWallpaperOnWallTiles
                ? GetWallpaperTextureForRoom(room_number, useMirror: true)
                : null;

            string room_name = rooms[room_number].name;
            int num_cells = rooms[room_number].cells.Count;

            for (int cell_number = 0; cell_number < num_cells; cell_number++)
            {
                if ((cell_number % 500) == 0)
                    if (tm.IfYield()) yield return null;

                Vector2Int pos = rooms[room_number].cells[cell_number].pos;
                int x = pos.x;
                int z = pos.y;
                int ySteps = rooms[room_number].cells[cell_number].height;
                bool isFloor = true;
                useTriangleFloor = false;
                Color colorFloor = rooms[room_number].cells[cell_number].colorFloor;
                if (colorFloor == colorDefault) // if cell has no color, use room's color
                    colorFloor = rooms[room_number].colorFloor;

                // Base world position of this tile center
                world = grid.CellToWorld(new Vector3Int(x, z, 0));

                cellDoors = rooms[room_number].cells[cell_number].doors;
                bool northDoor = cellDoors.HasFlag(DirFlags.N);
                bool eastDoor = cellDoors.HasFlag(DirFlags.E);
                bool southDoor = cellDoors.HasFlag(DirFlags.S);
                bool westDoor = cellDoors.HasFlag(DirFlags.W);

                int num_doors = (northDoor ? 1 : 0) + (southDoor ? 1 : 0) + (eastDoor ? 1 : 0) + (westDoor ? 1 : 0);

                // -------- diagonal corner smoothing (before orthogonal perimeter faces) --------
                bool suppressNorth = false;
                bool suppressEast = false;
                bool suppressSouth = false;
                bool suppressWest = false;

                if (num_doors == 0 && cfg.useDiagonalCorners && isFloor && diagonalWallPrefab != null)
                {
                    cellWalls = rooms[room_number].cells[cell_number].walls;
                    bool northWall = cellWalls.HasFlag(DirFlags.N);
                    bool eastWall = cellWalls.HasFlag(DirFlags.E);
                    bool southWall = cellWalls.HasFlag(DirFlags.S);
                    bool westWall = cellWalls.HasFlag(DirFlags.W);

                    int num_walls = (northWall ? 1 : 0) + (southWall ? 1 : 0) + (eastWall ? 1 : 0) + (westWall ? 1 : 0);

                    if (num_walls == 2)  // must have exactly two walls to use diagonal wall
                    {
                        float floorY = ySteps * cfg.unitHeight;
                        float wallHeight = Mathf.Max(1, cfg.perimeterWallSteps) * cfg.unitHeight;
                        float diagonalLength = DiagonalInsideLength(cellSize);
                        Vector3 wallVerticalOffset = new Vector3(0f, floorY + wallHeight * 0.5f, 0f);

                        // NE corner (N & E)
                        if (northWall && eastWall)
                        {
                            Vector3 wallPosition = world + CornerOffset(east: true, north: true, cellSize) + wallVerticalOffset;
                            Quaternion wallRotation = Yaw45;
                            Vector3 wallScale = new Vector3(cellSize.x * 0.1f, wallHeight, diagonalLength);

                            elementStore.AddWall(
                                archetypeId: "DiagonalWall",
                                isDiagonal: true,
                                roomIndex: room_number,
                                cellCoord: new Vector2Int(x, z),
                                heightSteps: ySteps,
                                worldPos: wallPosition,
                                rotation: wallRotation,
                                scale: wallScale,
                                color: Color.white,
                                textureOverride: roomWallpaper,
                                customFlags: 0
                            );

                            useTriangleFloor = true;
                            triangleFloorDirection = 0;
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressNorth = true; suppressEast = true; }
                        }
                        // NW corner (N & W)
                        if (northWall && westWall)
                        {
                            Vector3 wallPosition = world + CornerOffset(east: false, north: true, cellSize) + wallVerticalOffset;
                            Quaternion wallRotation = Yaw315;
                            Vector3 wallScale = new Vector3(cellSize.x * 0.1f, wallHeight, diagonalLength);

                            elementStore.AddWall(
                                archetypeId: "DiagonalWall",
                                isDiagonal: true,
                                roomIndex: room_number,
                                cellCoord: new Vector2Int(x, z),
                                heightSteps: ySteps,
                                worldPos: wallPosition,
                                rotation: wallRotation,
                                scale: wallScale,
                                color: Color.white,
                                textureOverride: roomWallpaper,
                                customFlags: 0
                            );

                            useTriangleFloor = true;
                            triangleFloorDirection = 3;
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressNorth = true; suppressWest = true; }
                        }
                        // SE corner (S & E)
                        if (southWall && eastWall)
                        {
                            Vector3 wallPosition = world + CornerOffset(east: true, north: false, cellSize) + wallVerticalOffset;
                            Quaternion wallRotation = Yaw135;
                            Vector3 wallScale = new Vector3(cellSize.x * 0.1f, wallHeight, diagonalLength);

                            elementStore.AddWall(
                                archetypeId: "DiagonalWall",
                                isDiagonal: true,
                                roomIndex: room_number,
                                cellCoord: new Vector2Int(x, z),
                                heightSteps: ySteps,
                                worldPos: wallPosition,
                                rotation: wallRotation,
                                scale: wallScale,
                                color: Color.white,
                                textureOverride: roomWallpaper,
                                customFlags: 0
                            );

                            useTriangleFloor = true;
                            triangleFloorDirection = 1;
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressSouth = true; suppressEast = true; }
                        }
                        // SW corner (S & W)
                        if (southWall && westWall)
                        {
                            Vector3 wallPosition = world + CornerOffset(east: false, north: false, cellSize) + wallVerticalOffset;
                            Quaternion wallRotation = Yaw225;
                            Vector3 wallScale = new Vector3(cellSize.x * 0.1f, wallHeight, diagonalLength);

                            elementStore.AddWall(
                                archetypeId: "DiagonalWall",
                                isDiagonal: true,
                                roomIndex: room_number,
                                cellCoord: new Vector2Int(x, z),
                                heightSteps: ySteps,
                                worldPos: wallPosition,
                                rotation: wallRotation,
                                scale: wallScale,
                                color: Color.white,
                                textureOverride: roomWallpaper,
                                customFlags: 0
                            );

                            useTriangleFloor = true;
                            triangleFloorDirection = 2;
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressSouth = true; suppressWest = true; }
                        }
                    }
                }
                // -------- end diagonal corner smoothing, start straight walls/cliffs --------

                for (int directionIndex = 0; directionIndex < 4; directionIndex++)
                {
                    Vector2Int direction = Dir4[directionIndex];
                    int neighborX = x + direction.x;
                    int neighborZ = z + direction.y;
                    bool neighborIsWall = false;
                    bool neighborIsDoor = false;

                    cellWalls = rooms[room_number].cells[cell_number].walls;
                    cellDoors = rooms[room_number].cells[cell_number].doors;

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

                    // If current is FLOOR and neighbor is WALL or DOOR => perimeter face
                    if (isFloor && (neighborIsWall || neighborIsDoor) && cliffPrefab != null)
                    {
                        if ((direction.x == 0 && direction.y == 1 && suppressNorth) ||
                            (direction.x == 1 && direction.y == 0 && suppressEast) ||
                            (direction.x == 0 && direction.y == -1 && suppressSouth) ||
                            (direction.x == -1 && direction.y == 0 && suppressWest))
                        {
                            // skip orthogonal; diagonal was already placed
                        }
                        else
                        {
                            neighborWorld = grid.CellToWorld(new Vector3Int(neighborX, neighborZ, 0));
                            wallMidpoint = 0.5f * (world + neighborWorld);

                            int floorSteps = rooms[room_number].cells[cell_number].height;
                            float wallHeight = Mathf.Max(1, cfg.perimeterWallSteps) * cfg.unitHeight;
                            float baseY = floorSteps * cfg.unitHeight;

                            Vector3 wallPosition = wallMidpoint + new Vector3(0, baseY + (0.5f * wallHeight), 0);
                            wallPosition += new Vector3(-direction.x, 0f, -direction.y) * wallInsetIntoRoom;
                            Quaternion wallRotation = RotFromDir(new Vector2Int(neighborX - x, neighborZ - z));
                            Vector3 wallScale = new Vector3(cellSize.x, wallHeight, cellSize.y * 0.1f);

                            bool mirrorWallpaper = !neighborIsDoor && (((x + z) % 2) == 0);
                            Texture2D wallWallpaper = mirrorWallpaper ? roomWallpaperMirror : roomWallpaper;

                            // For now, store doors as walls with a flag + color.
                            // Later you can route neighborIsDoor into elementStore.AddDoor instead.
                            int customFlags = neighborIsDoor ? 1 : 0; // 1 = door segment
                            Color wallColor = neighborIsDoor ? Color.red : Color.white;

                            elementStore.AddWall(
                                archetypeId: neighborIsDoor ? "Door" : "Wall",
                                isDiagonal: false,
                                roomIndex: room_number,
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
                    }

                    // Height transitions (ramps / cliffs) between this cell and neighbor
                    int neighborHeightSteps = GetHeightInNeighborhood(room_number, new Vector2Int(neighborX, neighborZ));
                    int heightDifference = neighborHeightSteps - ySteps;
                    if (heightDifference == 0) continue;

                    neighborWorld = grid.CellToWorld(new Vector3Int(neighborX, neighborZ, 0));
                    wallMidpoint = 0.5f * (world + neighborWorld);

                    if ((Mathf.Abs(heightDifference) >= cfg.minimumRamp) &&
                        (Mathf.Abs(heightDifference) <= cfg.maximumRamp) &&
                        rampPrefab != null)
                    {
                        bool goesUp = heightDifference > 0;
                        if (goesUp) continue; // keep your existing "only one side makes the ramp" rule

                        int upperHeight = goesUp ? neighborHeightSteps : ySteps;
                        Quaternion rampRotation = RotFromDir(direction * (goesUp ? 1 : -1)); // face uphill
                        Vector3 rampPosition = neighborWorld + new Vector3(0, upperHeight * cfg.unitHeight, 0);
                        Vector3 rampScale = new Vector3(cellSize.x, Mathf.Abs(heightDifference) * cfg.unitHeight * 1.2f, cellSize.y);

                        elementStore.AddRamp(
                            archetypeId: "Ramp",
                            roomIndex: room_number,
                            cellCoord: new Vector2Int(x, z),
                            heightSteps: ySteps,
                            worldPos: rampPosition,
                            rotation: rampRotation,
                            scale: rampScale,
                            color: Color.white,
                            heightDelta: heightDifference
                        );
                    }
                } // end 4-direction loop

                // -------- Floor tiles (square or triangle) --------
                if (isFloor && floorPrefab != null && triangleFloorPrefab != null)
                {
                    Quaternion tilt = rooms[room_number].cells[cell_number].tiltFloor;
                    Vector3 position = world + new Vector3(0f, ySteps * cfg.unitHeight, 0f);

                    float rollRadians = tilt.eulerAngles.z * Mathf.Deg2Rad;
                    float pitchRadians = tilt.eulerAngles.x * Mathf.Deg2Rad;
                    float cosRoll = Mathf.Cos(rollRadians);
                    float cosPitch = Mathf.Cos(pitchRadians);
                    float scaleX = (Mathf.Abs(cosRoll) > 1e-4f) ? 1f / cosRoll : 1f;
                    float scaleZ = (Mathf.Abs(cosPitch) > 1e-4f) ? 1f / cosPitch : 1f;

                    Vector3 finalScale = new Vector3(scaleX, 1f, scaleZ);
                    
                    // checkerboard floor:
                    Color checkerboardColor = (Mathf.Floor(world.x + world.z) % 2 == 0) ? Color.white : Color.black;
                    colorFloor = (colorFloor * (1 - checkerFloorStrength)) + (checkerboardColor * checkerFloorStrength);

                    if (useTriangleFloor)
                    {
                        Quaternion triangleFloorRotation = Quaternion.Euler(-90f, triangleFloorDirection * 90f, 90f);
                        // Approximate final rotation: tilt then triangle orientation
                        Quaternion finalRotation = tilt * triangleFloorRotation;
                        Vector3 triangleScale = finalScale * 50f; // keep your existing fudge factor for now

                        elementStore.AddFloorTile(
                            archetypeId: "TriangleFloor",
                            isTriangle: true,
                            roomIndex: room_number,
                            cellCoord: new Vector2Int(x, z),
                            heightSteps: ySteps,
                            worldPos: position,
                            rotation: finalRotation,
                            scale: triangleScale,
                            color: colorFloor
                        );
                    }
                    else
                    {
                        Quaternion finalRotation = tilt;

                        elementStore.AddFloorTile(
                            archetypeId: "Floor",
                            isTriangle: false,
                            roomIndex: room_number,
                            cellCoord: new Vector2Int(x, z),
                            heightSteps: ySteps,
                            worldPos: position,
                            rotation: finalRotation,
                            scale: finalScale,
                            color: colorFloor
                        );
                    }
                }
                // -------- Ceiling tiles --------
                if (rooms[room_number].ceilingHeight > 0f)
                {
                    float ceilingY = rooms[room_number].ceilingHeight + ceilingZOffset;
                    Vector3 ceilingPosition = world + new Vector3(0f, ceilingY, 0f);

                    elementStore.AddCeilingTile(
                        archetypeId: "Ceiling",
                        roomIndex: room_number,
                        cellCoord: new Vector2Int(x, z),
                        worldPos: ceilingPosition,
                        rotation: Quaternion.Euler(90f, 0f, 0f),
                        scale: new Vector3(1f, 1f, 1f),
                        color: rooms[room_number].colorCeiling
                    );
                }
            } // end cell loop
        }
        finally
        {
            if (local_tm) tm.End();
        }
    }

    private Texture2D GetWallpaperTextureForRoom(int roomIndex, bool useMirror = false)
    {
        Dictionary<int, Texture2D> wallpaperCacheByRoom = useMirror
            ? roomWallpaperMirrorByRoomIndex
            : roomWallpaperByRoomIndex;

        if (wallpaperCacheByRoom.TryGetValue(roomIndex, out Texture2D cachedWallpaper))
            return cachedWallpaper;

        string[] wallpaperKeys = GetAvailableWallpaperKeys();
        if (wallpaperKeys == null || wallpaperKeys.Length == 0)
            return null;

        if (!roomWallpaperKeyByRoomIndex.TryGetValue(roomIndex, out string wallpaperKey))
        {
            int wallpaperIndex = GetDeterministicWallpaperIndex(roomIndex, wallpaperKeys.Length);
            wallpaperKey = wallpaperKeys[wallpaperIndex];
            roomWallpaperKeyByRoomIndex[roomIndex] = wallpaperKey;
        }

        Texture2D selectedWallpaper = ResolveWallpaperTextureForKey(wallpaperKey, useMirror);
        wallpaperCacheByRoom[roomIndex] = selectedWallpaper;
        return selectedWallpaper;
    }

    private string[] GetAvailableWallpaperKeys()
    {
        EnsureWallpaperTextureCache();
        return cachedWallpaperSelectionKeys;
    }

    private Texture2D[] GetAvailableWallpaperTextures(bool useMirror = false)
    {
        EnsureWallpaperTextureCache();
        return useMirror ? cachedWallpaperTexturesMirror : cachedWallpaperTextures;
    }

    private void EnsureWallpaperTextureCache()
    {
        if (cachedWallpaperSelectionKeys != null)
            return;

        cachedWallpaperTexturesByKey = LoadWallpaperTexturesByKey(wallpaperResourcesPath);
        cachedWallpaperTexturesMirrorByKey = LoadWallpaperTexturesByKey(wallpaperResourcesPath_mirror);

        List<string> sharedKeys = new();
        foreach (KeyValuePair<string, Texture2D> entry in cachedWallpaperTexturesByKey)
        {
            if (cachedWallpaperTexturesMirrorByKey.ContainsKey(entry.Key))
                sharedKeys.Add(entry.Key);
        }

        sharedKeys.Sort(StringComparer.OrdinalIgnoreCase);

        List<string> selectionKeys = sharedKeys;
        if (selectionKeys.Count == 0)
        {
            selectionKeys = new List<string>(cachedWallpaperTexturesByKey.Keys);
            if (selectionKeys.Count == 0)
                selectionKeys.AddRange(cachedWallpaperTexturesMirrorByKey.Keys);

            selectionKeys.Sort(StringComparer.OrdinalIgnoreCase);

            if (cachedWallpaperTexturesByKey.Count > 0 && cachedWallpaperTexturesMirrorByKey.Count > 0)
                Debug.LogWarning("Wallpaper textures were found in both normal and mirror folders, but no filenames matched. Falling back to non-paired wallpaper selection.");
        }
        else if (sharedKeys.Count != cachedWallpaperTexturesByKey.Count || sharedKeys.Count != cachedWallpaperTexturesMirrorByKey.Count)
        {
            Debug.LogWarning($"Wallpaper pairing found {sharedKeys.Count} filename matches between Resources/{wallpaperResourcesPath} and Resources/{wallpaperResourcesPath_mirror}. Unmatched files will be skipped so mirrored wallpapers stay paired correctly.");
        }

        cachedWallpaperSelectionKeys = selectionKeys.ToArray();
        cachedWallpaperTextures = BuildWallpaperTextureArray(cachedWallpaperSelectionKeys, useMirror: false);
        cachedWallpaperTexturesMirror = BuildWallpaperTextureArray(cachedWallpaperSelectionKeys, useMirror: true);

        if (cachedWallpaperSelectionKeys.Length == 0)
            Debug.LogWarning($"No wallpaper textures found at Resources/{wallpaperResourcesPath} or Resources/{wallpaperResourcesPath_mirror}.");
    }

    private Dictionary<string, Texture2D> LoadWallpaperTexturesByKey(string resourcesPath)
    {
        Dictionary<string, Texture2D> wallpapersByKey = new(StringComparer.OrdinalIgnoreCase);

        Sprite[] wallpaperSprites = Resources.LoadAll<Sprite>(resourcesPath);
        for (int spriteIndex = 0; spriteIndex < wallpaperSprites.Length; spriteIndex++)
        {
            Sprite sprite = wallpaperSprites[spriteIndex];
            Texture2D texture = sprite != null ? sprite.texture : null;
            string wallpaperKey = GetWallpaperTextureKey(sprite != null ? sprite.name : null, texture);
            if (texture == null || string.IsNullOrWhiteSpace(wallpaperKey) || wallpapersByKey.ContainsKey(wallpaperKey))
                continue;

            wallpapersByKey[wallpaperKey] = texture;
        }

        if (wallpapersByKey.Count == 0)
        {
            Texture2D[] loadedTextures = Resources.LoadAll<Texture2D>(resourcesPath);
            for (int textureIndex = 0; textureIndex < loadedTextures.Length; textureIndex++)
            {
                Texture2D texture = loadedTextures[textureIndex];
                string wallpaperKey = GetWallpaperTextureKey(texture != null ? texture.name : null, texture);
                if (texture == null || string.IsNullOrWhiteSpace(wallpaperKey) || wallpapersByKey.ContainsKey(wallpaperKey))
                    continue;

                wallpapersByKey[wallpaperKey] = texture;
            }
        }

        return wallpapersByKey;
    }

    private Texture2D[] BuildWallpaperTextureArray(string[] wallpaperKeys, bool useMirror)
    {
        List<Texture2D> wallpapers = new();
        for (int keyIndex = 0; keyIndex < wallpaperKeys.Length; keyIndex++)
        {
            Texture2D texture = ResolveWallpaperTextureForKey(wallpaperKeys[keyIndex], useMirror);
            if (texture != null)
                wallpapers.Add(texture);
        }

        return wallpapers.ToArray();
    }

    private Texture2D ResolveWallpaperTextureForKey(string wallpaperKey, bool useMirror)
    {
        if (string.IsNullOrWhiteSpace(wallpaperKey))
            return null;

        Dictionary<string, Texture2D> preferredTextures = useMirror
            ? cachedWallpaperTexturesMirrorByKey
            : cachedWallpaperTexturesByKey;
        Dictionary<string, Texture2D> fallbackTextures = useMirror
            ? cachedWallpaperTexturesByKey
            : cachedWallpaperTexturesMirrorByKey;

        if (preferredTextures != null && preferredTextures.TryGetValue(wallpaperKey, out Texture2D texture))
            return texture;

        if (fallbackTextures != null && fallbackTextures.TryGetValue(wallpaperKey, out texture))
            return texture;

        return null;
    }

    private static string GetWallpaperTextureKey(string assetName, Texture2D texture)
    {
        string wallpaperKey = !string.IsNullOrWhiteSpace(assetName)
            ? assetName.Trim()
            : (texture != null ? texture.name.Trim() : string.Empty);

        return wallpaperKey;
    }

    private int GetDeterministicWallpaperIndex(int roomIndex, int wallpaperCount)
    {
        unchecked
        {
            int seed = cfg != null ? cfg.seed : 0;
            int hash = seed;
            hash = (hash * 397) ^ roomIndex;
            hash ^= unchecked((int)0x9e3779b9u);
            if (hash < 0)
                hash = ~hash;

            return wallpaperCount > 0 ? hash % wallpaperCount : 0;
        }
    }

    public void BuildCeilings()
    {
        var rooms = dir.gen.rooms;
        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogWarning("CeilingBuilder: No rooms found to build ceilings.");
            return;
        }

        int addedCount = 0;

        foreach (var room in rooms)
        {
            if (room == null || room.cells == null || room.cells.Count == 0)
                continue;

            // Rule: no ceiling if height <= 0, or if room is outdoor.
            // If you have IsOutdoorRoom, use it here.
            if (room.ceilingHeight <= 0f)
                continue;

            // If you added an Outdoor flag:
            // if ((room.placementTypes & PlacementRoomTypeFlags.Outdoor) != 0)
            //     continue;

            foreach (var cell in room.cells)
            {
                if (cell == null) continue;

                float zHeight = room.ceilingHeight + ceilingZOffset;

                int idx = dir.elementStore.AddCeiling(cell, zHeight, room.colorCeiling);
                if (idx >= 0) addedCount++;
            }
        }

        // Now ask ManufactureGO to actually build the GameObjects for this layer
        dir.manufactureGO.BuildNewInstancesForLayer(ElementLayerKind.Ceiling);
        dir.manufactureGO.ApplyPendingUpdates();

        Debug.Log($"CeilingBuilder: Created {addedCount} ceiling instances via ElementStore.");
    }

} // End class HeightMap3DBuilder
