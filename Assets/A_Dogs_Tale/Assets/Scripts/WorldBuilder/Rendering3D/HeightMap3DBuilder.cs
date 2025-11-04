using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour
{
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

            Destroy3D(); // Clear old objects

            for (int room_number = 0; room_number < rooms.Count; room_number++)
            {
                //Debug.Log($"Build3DFromOneRoom START room_number = {room_number}");
                yield return StartCoroutine(Build3DFromOneRoom(room_number, tm: null));
                //Debug.Log($"Build3DFromOneRoom DONE room_number = {room_number}");
                //if (tm.IfYield()) yield return null;
            }
        }
        finally { if (local_tm) tm.End(); }
    }
    
    public IEnumerator Build3DFromOneRoom(int room_number, TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("Build3DFromOneRoom"); local_tm = true; }
        try
        {
            //Debug.Log($"Build3DFromOneRoom room_number={room_number}");
            Vector3 mid = new();
            Vector3 world = new();
            Vector3 nWorld = new();
            Vector3 cell = grid.cellSize;
            bool use_triangle_floor = false;
            int triangle_floor_dir = 0;
            //bool includes_ramp = false;
            DirFlags mywalls = DirFlags.None;
            DirFlags mydoors = DirFlags.None;
            
            
            // Cache once:
            var floorMR = (floorPrefab != null) ? floorPrefab.GetComponent<MeshRenderer>() : null;
            var wallMR  = (cliffPrefab != null) ? cliffPrefab.GetComponent<MeshRenderer>() : null;
            var triangleFloorMR = (triangleFloorPrefab != null) ? triangleFloorPrefab.GetComponent<MeshRenderer>() : null;

            //if (tm.IfYield()) yield return null;
            string room_name = rooms[room_number].name;
            int num_cells = rooms[room_number].cells.Count;
            for (int cell_number = 0; cell_number < num_cells; cell_number++)
            {
                if ((cell_number % 500) == 0) if (tm.IfYield()) yield return null;
                Vector2Int pos = rooms[room_number].cells[cell_number].pos;
                int x = pos.x;
                int z = pos.y;
                int ySteps = rooms[room_number].cells[cell_number].height;
                bool isFloor = true;
                use_triangle_floor = false;
                Color colorFloor = rooms[room_number].cells[cell_number].colorFloor;
                if (colorFloor == colorDefault) // if cell has no color, use room's color
                    colorFloor = rooms[room_number].colorFloor;
                //bool isWall = false; //unused

                // Base world position of this tile center
                world = grid.CellToWorld(new Vector3Int(x, z, 0));

                // If your Grid's tile anchor isn't centered, you may want to offset by cell * 0.5f
                // world += new Vector3(cell.x * 0.5f, 0, cell.y * 0.5f); // uncomment if needed

                mydoors = rooms[room_number].cells[cell_number].doors;
                bool ND = mydoors.HasFlag(DirFlags.N);
                bool ED = mydoors.HasFlag(DirFlags.E);
                bool SD = mydoors.HasFlag(DirFlags.S);
                bool WD = mydoors.HasFlag(DirFlags.W);
                
                int num_doors = (ND ? 1 : 0) + (SD ? 1 : 0) + (ED ? 1 : 0) + (WD ? 1 : 0);
                // prevent diagonal walls if there are doors in this cell.

                // -------- diagonal corner smoothing (before orthogonal perimeter faces) --------
                bool suppressN = false, suppressE = false, suppressS = false, suppressW = false;

                if (num_doors==0 && cfg.useDiagonalCorners && isFloor && diagonalWallPrefab != null)
                {
                    // include neighborhood, which is immediately connected rooms
                    // this allows for proper finding of walls at intersection.
                    
                    mywalls = rooms[room_number].cells[cell_number].walls;
                    bool N = mywalls.HasFlag(DirFlags.N);
                    bool E = mywalls.HasFlag(DirFlags.E);
                    bool S = mywalls.HasFlag(DirFlags.S);
                    bool W = mywalls.HasFlag(DirFlags.W);

                    // if zero or one sides are walls, then nothing will happen here, so skip extra calculations
                    // if three sides are walls, don't replace with diagonals and leave as three walls (yucky X arrangement)
                    int num_walls = (N ? 1 : 0) + (S ? 1 : 0) + (E ? 1 : 0) + (W ? 1 : 0);
                    
                    if (num_walls == 2)  // must have exactly two walls to use diagonal wall
                    {
                        float floorY = ySteps * cfg.unitHeight;
                        float wallH = Mathf.Max(1, cfg.perimeterWallSteps) * cfg.unitHeight;
                        float diagLen = DiagonalInsideLength(cell);
                        Vector3 baseY = new Vector3(0f, floorY + wallH * 0.5f, 0f);

                        // NE corner (N & E)
                        if (N && E /* && NE */)
                        {
                            var t = Instantiate(diagonalWallPrefab,
                                world + CornerOffset(east: true, north: true, cell) + baseY,
                                Yaw45, root);
                            t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                            //t.name = $"Wall({room_name})";
                            use_triangle_floor = true;
                            triangle_floor_dir = 0; // correct
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressN = true; suppressE = true; }
                        }
                        // NW corner (N & W)
                        if (N && W /* && NW */)
                        {
                            var t = Instantiate(diagonalWallPrefab,
                                world + CornerOffset(east: false, north: true, cell) + baseY,
                                Yaw315, root);
                            t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                            //t.name = $"Wall({room_name})";
                            use_triangle_floor = true;
                            triangle_floor_dir = 3; // correct
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressN = true; suppressW = true; }
                        }
                        // SE corner (S & E)
                        if (S && E /* && SE */)
                        {
                            var t = Instantiate(diagonalWallPrefab,
                                world + CornerOffset(east: true, north: false, cell) + baseY,
                                Yaw135, root);
                            t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                            //t.name = $"Wall({room_name})";
                            use_triangle_floor = true;
                            triangle_floor_dir = 1; // correct
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressS = true; suppressE = true; }
                        }
                        // SW corner (S & W)
                        if (S && W /* && SW */)
                        {
                            var t = Instantiate(diagonalWallPrefab,
                                world + CornerOffset(east: false, north: false, cell) + baseY,
                                Yaw225, root);
                            t.transform.localScale = new Vector3(cell.x * 0.1f, wallH, diagLen);
                            //t.name = $"Wall({room_name})";
                            use_triangle_floor = true;
                            triangle_floor_dir = 2; // correct
                            if (cfg.skipOrthogonalWhenDiagonal) { suppressS = true; suppressW = true; }
                        }
                    }
                }
                // -------- end diagonal corner smoothing, start straight walls/cliffs --------

                // Compare with 4 neighbors and add perimeter walls or ramps/cliffs
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
                    //bool nIsFloor = IsTileInNeighborhood(room_number, rooms[room_number].neighbors, new Vector2Int(nx, nz));
                    //bool nIsWall = IsWallInNeighborhood(room_number, new Vector2Int(nx, nz));
                    //bool nIsWall = !nIsFloor;

                    if (d.x == 0 && d.y == 1) nIsWall = mywalls.HasFlag(DirFlags.N);
                    if (d.x == 1 && d.y == 0) nIsWall = mywalls.HasFlag(DirFlags.E);
                    if (d.x == 0 && d.y == -1) nIsWall = mywalls.HasFlag(DirFlags.S);
                    if (d.x == -1 && d.y == 0) nIsWall = mywalls.HasFlag(DirFlags.W);

                    if (d.x == 0 && d.y == 1) nIsDoor = mydoors.HasFlag(DirFlags.N);
                    if (d.x == 1 && d.y == 0) nIsDoor = mydoors.HasFlag(DirFlags.E);
                    if (d.x == 0 && d.y == -1) nIsDoor = mydoors.HasFlag(DirFlags.S);
                    if (d.x == -1 && d.y == 0) nIsDoor = mydoors.HasFlag(DirFlags.W);

                    // If current is FLOOR and neighbor is WALL => perimeter face (unless diagonal suppressed)
                    if (isFloor && (nIsWall || nIsDoor) && cliffPrefab != null)
                    {
                        // Respect suppress flags for the matching direction
                        if ((d.x == 0 && d.y == 1 && suppressN) ||
                            (d.x == 1 && d.y == 0 && suppressE) ||
                            (d.x == 0 && d.y == -1 && suppressS) ||
                            (d.x == -1 && d.y == 0 && suppressW))
                        {
                            // skip orthogonal face; diagonal already placed
                        }
                        else
                        {
                            // location of the neighboring cell
                            nWorld = grid.CellToWorld(new Vector3Int(nx, nz, 0));
                            // midpoint between the two cells
                            mid = 0.5f * (world + nWorld);

                            //int floorSteps = GetHeightFromRoom(pos);
                            int floorSteps = rooms[room_number].cells[cell_number].height;

                            float ht = Mathf.Max(1, cfg.perimeterWallSteps) * cfg.unitHeight;
                            float baseY = floorSteps * cfg.unitHeight;

                            var face = Instantiate(cliffPrefab,
                                mid + new Vector3(0, baseY + (0.5f * ht), 0),
                                RotFromDir(new Vector2Int((nx - x), (nz - z))),
                                root);
                            //face.name = $"Wall({room_name})";
                            face.transform.localScale = new Vector3(cell.x, ht, cell.y * 0.1f);

                            // make the wall red to indicate a simple door...
                            var rend = face.GetComponent<MeshRenderer>(); // ok once per object, but avoid if not needed
                            if (nIsDoor /*&& (rend != null)*/) rend.material.color = Color.red;
                        }
                    }

                    // Only consider transitions between walkable tiles, or visualize room->void edges as cliffs if you prefer
                    //if (!(isFloor && nIsFloor)) continue;

                    int nySteps = GetHeightInNeighborhood(room_number, new Vector2Int(nx, nz));
                    int diff = nySteps - ySteps;
                    if (diff == 0) continue;

                    // Place transition geometry centered between the two tiles (for walls only)
                    nWorld = grid.CellToWorld(new Vector3Int(nx, nz, 0));
                    mid = (world + nWorld) * 0.5f;

                    // Ramps or cliffs

                    if ((Mathf.Abs(diff) >= cfg.minimumRamp) && (Mathf.Abs(diff) <= cfg.maximumRamp) && (rampPrefab != null))
                    {
                        // NOTE: Ramp size/placement is still a little off.  Needs work, possibly new origin for tile.

                        // Ramp spans from lower to higher tile
                        bool up = diff > 0;
                        if (up) continue; // don't create two ramps, one from each side, instead pick 'down'
                                            // Place ramp slightly biased toward lower side so the top aligns cleanly
                        int lower = up ? ySteps : nySteps;
                        float midheight = (ySteps + nySteps) / 2f;
                        int upper = up ? nySteps : ySteps;
                        var rot = RotFromDir(d * (up ? 1 : -1)); // face uphill
                        var ramp = Instantiate(rampPrefab, nWorld + new Vector3(0, (upper) * cfg.unitHeight /*- .35f*/, 0), rot, root);
                        ramp.name = $"Ramp({Math.Abs(diff)})";
                        ramp.transform.localScale = new Vector3(cell.x, Math.Abs(diff) * cfg.unitHeight * 1.2f, cell.y); // length matches cell, height equals one step
                                                                                                                            //includes_ramp = true;
                    }
                } // end for i
                
                // Place floor at its height (Y is up)
                if (/*!includes_ramp &&*/ isFloor && floorPrefab != null && triangleFloorPrefab != null)
                {
                    Quaternion tilt = rooms[room_number].cells[cell_number].tiltFloor;
                    Vector3 position = world + new Vector3(-0.0f, ySteps * cfg.unitHeight, 0.0f);

                    // Adjust scale to keep tile edges flush with neighbors when tilted
                    // This assumes tilt is only around X and Z axes, not Yaw.
                    // If you have extreme tilts, this can cause large scale changes.
                    // You might want to clamp the scale to a maximum.
                    // Alternatively, you could redesign your tiles to be larger than the cell size,
                    // and overlap slightly to avoid gaps when tilted.
                    float rollRad = tilt.eulerAngles.z * Mathf.Deg2Rad;
                    float pitchRad = tilt.eulerAngles.x * Mathf.Deg2Rad;
                    float cosRoll = Mathf.Cos(rollRad);
                    float cosPitch = Mathf.Cos(pitchRad);
                    float scaleX = (Mathf.Abs(cosRoll) > 1e-4f) ? 1f / cosRoll : 1f;
                    float scaleZ = (Mathf.Abs(cosPitch) > 1e-4f) ? 1f / cosPitch : 1f;

                    //float scaleX = 1f / Mathf.Cos(rollRad);
                    //float scaleZ = 1f / Mathf.Cos(pitchRad);

                    Vector3 finalScale = new Vector3(scaleX, 1f, scaleZ);

                    // When you instantiate, skip naming in bulk builds:
                    GameObject f;
                    if (use_triangle_floor)
                    {
                        Quaternion triangle_floor_rot = Quaternion.Euler(-90f, (triangle_floor_dir) * 90f, 90f);
                        //f = Instantiate(triangleFloorPrefab, world + new Vector3(-0.0f, ySteps * unitHeight, 0.0f), Quaternion.Euler(90f, 0f, (2+triangle_floor_dir) * 90), root);
                        //f = Instantiate(triangleFloorPrefab, position, tilt * Quaternion.Euler(90f, 0f, (triangle_floor_dir) * 90), root);
                        f = Instantiate(triangleFloorPrefab, position, triangle_floor_rot /** tilt*/, root);
                        f.transform.localScale = finalScale * 50f;  // WHY 50?
                        f.transform.Rotate(tilt.eulerAngles, Space.World);
                        f.name = $"Triangle({room_number}:{room_name},{ySteps},{triangle_floor_dir})"; // comment out in perf builds
                    }
                    else
                    {

                        //Debug.Log("Tilt: " + tilt.eulerAngles);  // DEBUG
                        f = Instantiate(floorPrefab, position, tilt, root);
                        f.transform.localScale = finalScale;
                        f.name = $"Floor({room_number}:{room_name},{ySteps})"; // comment out in perf builds
                    }
                                                                // Cache renderer on prefab variant or:
                    var rend = f.GetComponent<MeshRenderer>(); // ok once per object, but avoid if not needed
                    if (rend != null) rend.material.color = colorFloor;
                }
            }
        }
        finally { if (local_tm) tm.End(); }
    }
    
} // End class HeightMap3DBuilder