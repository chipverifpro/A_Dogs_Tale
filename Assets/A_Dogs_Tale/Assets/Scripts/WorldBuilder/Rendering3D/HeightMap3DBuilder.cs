using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{
    [SerializeField] private ElementStore elementStore;

    //[Header("3D Build Settings")]
    //    public float unitHeight = 0.1f;             // world Y per step
    //    public bool useDiagonalCorners = true;
    //    public bool skipOrthogonalWhenDiagonal = true;
    //    public int perimeterWallSteps = 30; // height of perimeter walls in steps

    // what is this used for?  0 references.
    //public Dictionary<Vector2Int, int> idx;  // Build once at the top of Build3DFromOneRoom

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
        float ox = (east ? +1f : -1f) * (cell.x * 0f);
        float oz = (north ? +1f : -1f) * (cell.y * 0f); // grid.y maps to world Z

        // Offset from tile center toward a corner (¼ cell each axis)
        //float ox = (east  ? +1f : -1f) * (cell.x * 0.25f);
        //float oz = (north ? +1f : -1f) * (cell.y * 0.25f); // grid.y maps to world Z
        return new Vector3(ox, 0f, oz);
    }

    static float DiagonalInsideLength(Vector3 cell)
    {
        // Lenght of strip across the center of the tile (corner to corner):
        float hx = cell.x * 1f;
        float hz = cell.y * 1f;
        // Length of a strip across the tile on a 45° diagonal (midpoint to midpoint):
        //float hx = cell.x * 0.5f;
        //float hz = cell.y * 0.5f;
        return Mathf.Sqrt(hx * hx + hz * hz);
    }

    // if root exists, destroy all 3D objects under it.
    // AKA: clear 3D tiles.
    public void Destroy3D()
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
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
            Vector3 mid = new();
            Vector3 world = new();
            Vector3 nWorld = new();
            Vector3 cell = grid.cellSize;
            bool use_triangle_floor = false;
            int triangle_floor_dir = 0;
            DirFlags mywalls = DirFlags.None;
            DirFlags mydoors = DirFlags.None;
            Color colorScent = getColor(Color.purple);

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
                use_triangle_floor = false;
                Color colorFloor = rooms[room_number].cells[cell_number].colorFloor;
                if (colorFloor == colorDefault) // if cell has no color, use room's color
                    colorFloor = rooms[room_number].colorFloor;

                // Base world position of this tile center
                world = grid.CellToWorld(new Vector3Int(x, z, 0));

                mydoors = rooms[room_number].cells[cell_number].doors;
                bool ND = mydoors.HasFlag(DirFlags.N);
                bool ED = mydoors.HasFlag(DirFlags.E);
                bool SD = mydoors.HasFlag(DirFlags.S);
                bool WD = mydoors.HasFlag(DirFlags.W);

                int num_doors = (ND ? 1 : 0) + (SD ? 1 : 0) + (ED ? 1 : 0) + (WD ? 1 : 0);

                // -------- diagonal corner smoothing (before orthogonal perimeter faces) --------
                bool suppressN = false, suppressE = false, suppressS = false, suppressW = false;

                if (num_doors == 0 && cfg.useDiagonalCorners && isFloor && diagonalWallPrefab != null)
                {
                    mywalls = rooms[room_number].cells[cell_number].walls;
                    bool N = mywalls.HasFlag(DirFlags.N);
                    bool E = mywalls.HasFlag(DirFlags.E);
                    bool S = mywalls.HasFlag(DirFlags.S);
                    bool W = mywalls.HasFlag(DirFlags.W);

                    int num_walls = (N ? 1 : 0) + (S ? 1 : 0) + (E ? 1 : 0) + (W ? 1 : 0);

                    if (num_walls == 2)  // must have exactly two walls to use diagonal wall
                    {
                        float floorY = ySteps * cfg.unitHeight;
                        float wallH = Mathf.Max(1, cfg.perimeterWallSteps) * cfg.unitHeight;
                        float diagLen = DiagonalInsideLength(cell);
                        Vector3 baseY = new Vector3(0f, floorY + wallH * 0.5f, 0f);

                        // NE corner (N & E)
                        if (N && E)
                        {
                            Vector3 posWorld = world + CornerOffset(east: true, north: true, cell) + baseY;
                            Quaternion rot = Yaw45;
                            Vector3 scale = new Vector3(cell.x * 0.1f, wallH, diagLen);

                            elementStore.AddWall(
                                archetypeId: "DiagonalWall",
                                isDiagonal: true,
                                roomIndex: room_number,
                                cellCoord: new Vector2Int(x, z),
                                heightSteps: ySteps,
                                worldPos: posWorld,
                                rotation: rot,
                                scale: scale,
                                color: Color.white,
                                customFlags: 0
                            );

                            use_triangle_floor = true;
                            triangle_floor_dir = 0;
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressN = true; suppressE = true; }
                        }
                        // NW corner (N & W)
                        if (N && W)
                        {
                            Vector3 posWorld = world + CornerOffset(east: false, north: true, cell) + baseY;
                            Quaternion rot = Yaw315;
                            Vector3 scale = new Vector3(cell.x * 0.1f, wallH, diagLen);

                            elementStore.AddWall(
                                archetypeId: "DiagonalWall",
                                isDiagonal: true,
                                roomIndex: room_number,
                                cellCoord: new Vector2Int(x, z),
                                heightSteps: ySteps,
                                worldPos: posWorld,
                                rotation: rot,
                                scale: scale,
                                color: Color.white,
                                customFlags: 0
                            );

                            use_triangle_floor = true;
                            triangle_floor_dir = 3;
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressN = true; suppressW = true; }
                        }
                        // SE corner (S & E)
                        if (S && E)
                        {
                            Vector3 posWorld = world + CornerOffset(east: true, north: false, cell) + baseY;
                            Quaternion rot = Yaw135;
                            Vector3 scale = new Vector3(cell.x * 0.1f, wallH, diagLen);

                            elementStore.AddWall(
                                archetypeId: "DiagonalWall",
                                isDiagonal: true,
                                roomIndex: room_number,
                                cellCoord: new Vector2Int(x, z),
                                heightSteps: ySteps,
                                worldPos: posWorld,
                                rotation: rot,
                                scale: scale,
                                color: Color.white,
                                customFlags: 0
                            );

                            use_triangle_floor = true;
                            triangle_floor_dir = 1;
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressS = true; suppressE = true; }
                        }
                        // SW corner (S & W)
                        if (S && W)
                        {
                            Vector3 posWorld = world + CornerOffset(east: false, north: false, cell) + baseY;
                            Quaternion rot = Yaw225;
                            Vector3 scale = new Vector3(cell.x * 0.1f, wallH, diagLen);

                            elementStore.AddWall(
                                archetypeId: "DiagonalWall",
                                isDiagonal: true,
                                roomIndex: room_number,
                                cellCoord: new Vector2Int(x, z),
                                heightSteps: ySteps,
                                worldPos: posWorld,
                                rotation: rot,
                                scale: scale,
                                color: Color.white,
                                customFlags: 0
                            );

                            use_triangle_floor = true;
                            triangle_floor_dir = 2;
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressS = true; suppressW = true; }
                        }
                    }
                }
                // -------- end diagonal corner smoothing, start straight walls/cliffs --------

                for (int i = 0; i < 4; i++)
                {
                    Vector2Int d = Dir4[i];
                    int nx = x + d.x;
                    int nz = z + d.y;
                    bool nIsWall = false;
                    bool nIsDoor = false;

                    mywalls = rooms[room_number].cells[cell_number].walls;
                    mydoors = rooms[room_number].cells[cell_number].doors;

                    if (nx < 0 || nz < 0 || nx >= cfg.mapWidth || nz >= cfg.mapHeight)
                    {
                        nIsWall = true;     // off map
                        nIsDoor = false;
                    }

                    if (d.x == 0 && d.y == 1) nIsWall = mywalls.HasFlag(DirFlags.N);
                    if (d.x == 1 && d.y == 0) nIsWall = mywalls.HasFlag(DirFlags.E);
                    if (d.x == 0 && d.y == -1) nIsWall = mywalls.HasFlag(DirFlags.S);
                    if (d.x == -1 && d.y == 0) nIsWall = mywalls.HasFlag(DirFlags.W);

                    if (d.x == 0 && d.y == 1) nIsDoor = mydoors.HasFlag(DirFlags.N);
                    if (d.x == 1 && d.y == 0) nIsDoor = mydoors.HasFlag(DirFlags.E);
                    if (d.x == 0 && d.y == -1) nIsDoor = mydoors.HasFlag(DirFlags.S);
                    if (d.x == -1 && d.y == 0) nIsDoor = mydoors.HasFlag(DirFlags.W);

                    // If current is FLOOR and neighbor is WALL or DOOR => perimeter face
                    if (isFloor && (nIsWall || nIsDoor) && cliffPrefab != null)
                    {
                        if ((d.x == 0 && d.y == 1 && suppressN) ||
                            (d.x == 1 && d.y == 0 && suppressE) ||
                            (d.x == 0 && d.y == -1 && suppressS) ||
                            (d.x == -1 && d.y == 0 && suppressW))
                        {
                            // skip orthogonal; diagonal was already placed
                        }
                        else
                        {
                            nWorld = grid.CellToWorld(new Vector3Int(nx, nz, 0));
                            mid = 0.5f * (world + nWorld);

                            int floorSteps = rooms[room_number].cells[cell_number].height;
                            float ht = Mathf.Max(1, cfg.perimeterWallSteps) * cfg.unitHeight;
                            float baseY = floorSteps * cfg.unitHeight;

                            Vector3 wallPos = mid + new Vector3(0, baseY + (0.5f * ht), 0);
                            Quaternion wallRot = RotFromDir(new Vector2Int(nx - x, nz - z));
                            Vector3 wallScale = new Vector3(cell.x, ht, cell.y * 0.1f);

                            // For now, store doors as walls with a flag + color.
                            // Later you can route nIsDoor into elementStore.AddDoor instead.
                            int customFlags = nIsDoor ? 1 : 0; // 1 = door segment
                            Color wallColor = nIsDoor ? Color.red : Color.white;

                            elementStore.AddWall(
                                archetypeId: nIsDoor ? "Door" : "Wall",
                                isDiagonal: false,
                                roomIndex: room_number,
                                cellCoord: new Vector2Int(x, z),
                                heightSteps: floorSteps,
                                worldPos: wallPos,
                                rotation: wallRot,
                                scale: wallScale,
                                color: wallColor,
                                customFlags: customFlags
                            );
                        }
                    }

                    // Height transitions (ramps / cliffs) between this cell and neighbor
                    int nySteps = GetHeightInNeighborhood(room_number, new Vector2Int(nx, nz));
                    int diff = nySteps - ySteps;
                    if (diff == 0) continue;

                    nWorld = grid.CellToWorld(new Vector3Int(nx, nz, 0));
                    mid = 0.5f * (world + nWorld);

                    if ((Mathf.Abs(diff) >= cfg.minimumRamp) &&
                        (Mathf.Abs(diff) <= cfg.maximumRamp) &&
                        rampPrefab != null)
                    {
                        bool up = diff > 0;
                        if (up) continue; // keep your existing "only one side makes the ramp" rule

                        int upper = up ? nySteps : ySteps;
                        var rot = RotFromDir(d * (up ? 1 : -1)); // face uphill
                        Vector3 rampPos = nWorld + new Vector3(0, upper * cfg.unitHeight, 0);
                        Vector3 rampScale = new Vector3(cell.x, Mathf.Abs(diff) * cfg.unitHeight * 1.2f, cell.y);

                        elementStore.AddRamp(
                            archetypeId: "Ramp",
                            roomIndex: room_number,
                            cellCoord: new Vector2Int(x, z),
                            heightSteps: ySteps,
                            worldPos: rampPos,
                            rotation: rot,
                            scale: rampScale,
                            color: Color.white,
                            heightDelta: diff
                        );
                    }
                } // end 4-direction loop

                // -------- Floor tiles (square or triangle) --------
                if (isFloor && floorPrefab != null && triangleFloorPrefab != null)
                {
                    Quaternion tilt = rooms[room_number].cells[cell_number].tiltFloor;
                    Vector3 position = world + new Vector3(0f, ySteps * cfg.unitHeight, 0f);

                    float rollRad = tilt.eulerAngles.z * Mathf.Deg2Rad;
                    float pitchRad = tilt.eulerAngles.x * Mathf.Deg2Rad;
                    float cosRoll = Mathf.Cos(rollRad);
                    float cosPitch = Mathf.Cos(pitchRad);
                    float scaleX = (Mathf.Abs(cosRoll) > 1e-4f) ? 1f / cosRoll : 1f;
                    float scaleZ = (Mathf.Abs(cosPitch) > 1e-4f) ? 1f / cosPitch : 1f;

                    Vector3 finalScale = new Vector3(scaleX, 1f, scaleZ);

                    if (use_triangle_floor)
                    {
                        Quaternion triangleFloorRot = Quaternion.Euler(-90f, triangle_floor_dir * 90f, 90f);
                        // Approximate final rotation: tilt then triangle orientation
                        Quaternion finalRot = tilt * triangleFloorRot;
                        Vector3 triScale = finalScale * 50f; // keep your existing fudge factor for now

                        elementStore.AddFloorTile(
                            archetypeId: "TriangleFloor",
                            isTriangle: true,
                            roomIndex: room_number,
                            cellCoord: new Vector2Int(x, z),
                            heightSteps: ySteps,
                            worldPos: position,
                            rotation: finalRot,
                            scale: triScale,
                            color: colorFloor
                        );
                    }
                    else
                    {
                        Quaternion finalRot = tilt;

                        elementStore.AddFloorTile(
                            archetypeId: "Floor",
                            isTriangle: false,
                            roomIndex: room_number,
                            cellCoord: new Vector2Int(x, z),
                            heightSteps: ySteps,
                            worldPos: position,
                            rotation: finalRot,
                            scale: finalScale,
                            color: colorFloor
                        );
                    }
                }
            } // end cell loop
        }
        finally
        {
            if (local_tm) tm.End();
        }
    }

} // End class HeightMap3DBuilder