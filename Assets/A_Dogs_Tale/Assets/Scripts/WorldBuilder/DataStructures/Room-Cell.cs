using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System;
using System.Collections;

[System.Serializable]
public class ScentClass             // Everything about ONE scent
{
    public int agentId;

    // Airborne scent
    public float airIntensity;     // current airborne scent strength
    public float airNextDelta;     // next airborne value during decay/spread calc
    public float airLastVisualized = -1f; // for determining whether to bother updating visual cloud
    public int airGOindex = -1;  // index into airborne visual (fog, etc.)

    // Ground (surface) scent
    public float groundIntensity;      // current ground scent strength
    public float groundNextDelta;      // next ground value during decay/spread calc
    public float groundLastVisualized = -1f; // for determining whether to bother updating visual cloud
    public int groundGOindex = -1;   // index into ground visual (if any)
}

public enum DiagonalOpenDirection
{
    None = 0,
    NE = 1,
    SE = 2,
    SW = 3,
    NW = 4
}

// ===================== Cell class ===================
public class Cell       // one cell in a Room
{
    public Vector2Int pos;          // x,y
    public int height;              // z
    public int room_number;         // Based on index into global "rooms" list
    public int type;                // Floor, Solid Stone, Water, etc.
    public DirFlags walls = DirFlags.None;  // walls: N-E-S-W bit field
    public DirFlags doors = DirFlags.None;  // doors: N-E-S-W bit field
    //public DiagonalOpenDirection diagonalOpen = DiagonalOpenDirection.None; // if diagonal wall present, which way is open to room
    public Color colorFloor = new(1f, 0.4f, 0.7f, 0.5f); // default semi-transparent pink
    public Quaternion tiltFloor = Quaternion.identity; // optional tilt of the floor tile
    public float travel_cost = 1f;  // examples: 1 = open floor, 2 = rough terrain, 0.75 = road
    public bool isCorridor = false; // added for PackCell equivalence.  Should use room's value instead.
    // Delegates for behaviors (see notes below)
    public Action<Cell> OnView;     // function triggered when viewed
    public Action<Cell> OnStep;     // function triggered when stepped on
    public List<ScentClass> scents; // tracks who has passed this way before
    public GameObject[] GOs = new GameObject[Enum.GetValues(typeof(GOtypes)).Length]; // GameObjects for floor, walls, doors, etc.
    //public GameObject scentGO = null; // GameObject for scent fog visualization

    public enum GOtypes
    {
        Floor = 0,      // GO_floor = GOs[(int)GOtypes.Floor];
        Ceiling = 1,    // GO_ceiling
        Fog = 2,        // GO_fog      (either weather fog or scent fog or fog of war?)
        Wall_N = 3,     // GO_wall_N
        Wall_E = 4,     // GO_wall_E
        Wall_S = 5,     // GO_wall_S
        Wall_W = 6,     // GO_wall_W
        Wall_Diag = 7,  // GO_wall_Diag
        Door_N = 8,     // GO_door_N
        Door_E = 9,     // GO_door_E
        Door_S = 10,    // GO_door_S
        Door_W = 11     // GO_door_W
        // furniture, stairs, traps, etc. can be added later
    }

    // Constructors:
    public Cell(int x, int y, int z)
    {
        this.pos.x = x;
        this.pos.y = y;
        this.height = z;
    }

    public Cell(int x, int y)
    {
        this.pos.x = x;
        this.pos.y = y;
    }

    public Cell(Vector2Int pos)
    {
        this.pos = pos;
    }

    // shortcuts to read access variations
    public Vector3Int pos3d => new Vector3Int(pos.x, pos.y, height);
    public Vector3 pos3d_f => new Vector3(pos.x, pos.y, height);        // float version
    public Vector3 pos3d_world => new Vector3(pos.x, height, pos.y);    // axis swapped
    
    public int x => pos.x;
    public int y => pos.y;
    public int z => height;

    public DiagonalOpenDirection GetDiagonalOpenDirection()
    {
        if (walls.Count() != 2) return DiagonalOpenDirection.None; // must be exactly two walls to have a diagonal open
        if (doors != DirFlags.None) return DiagonalOpenDirection.None; // if doors present, no diagonal open
        if ((walls & (DirFlags.N | DirFlags.E)) == (DirFlags.N | DirFlags.E))
            return DiagonalOpenDirection.NE;
        if ((walls & (DirFlags.S | DirFlags.E)) == (DirFlags.S | DirFlags.E))
            return DiagonalOpenDirection.SE;
        if ((walls & (DirFlags.S | DirFlags.W)) == (DirFlags.S | DirFlags.W))
            return DiagonalOpenDirection.SW;
        if ((walls & (DirFlags.N | DirFlags.W)) == (DirFlags.N | DirFlags.W))
            return DiagonalOpenDirection.NW;
        return DiagonalOpenDirection.None;
    }

    // Helpers to trigger delegates safely
    public void TriggerView() { OnView?.Invoke(this); }
    public void TriggerStep() { OnStep?.Invoke(this); }

    // Example of assigning a functiion to the delegates:
    //   Cell trapCell = new Cell(2, 3);
    //   trapCell.OnStep = (c) => Debug.Log($"Ouch! Trap triggered at {c.x},{c.y}!");
    //   trapCell.OnView = (c) => Debug.Log($"You see a suspicious floor tile at {c.x},{c.y}...");

    // Example of calling the delegates:
    //   currentCell.TriggerView();
    //   currentCell.TriggerStep();
}

// ========================== Room class =================================
public class Room
{
    // == Properties of the room
    public int my_room_number = -1; // Uniquely identifies this room based on global "rooms" list
    public String name = "";

    // Tile-by-tile list of everything about a cell: floors/walls/doors/etc
    public List<Cell> cells = new();

    // NOTE: The above structure will replace these fields below.
    //public List<Vector2Int> tiles = new();
    //public List<Vector2Int> walls = new();
    public List<int> heights = new(); // Heights for each tile in the room, used for 3D generation

    public List<Door> doors = new();  // Details of every door in this room

    public int Size => cells.Count;     // OLD used tiles, NEW will use cells
    public int Last => cells.Count - 1; // Handy index for editing a newly added cell.
    public Color colorFloor = new(1f, 0.4f, 0.7f, 0.5f); // semi-transparent pink; // Color for the whole room, cell may override this
    public List<int> neighbors = new(); // List of neighboring rooms by index into global "rooms" list
    public bool isCorridor = false;     // Indicate if this room was generated as a corridor

    public int area = 0;
    public RectInt bounds; // minX, minY, sizeX, sizeY

    // OLD style: quick lookup in multiple lists for floors, walls, heights.
    // HashSets allow fast check whether room contains something at a position.
    // See the functions below: bool IsTileInRoom(pos), bool IsWallInRoom(pos)
    // Dictionaries are also based on a hash but returns a value at that position.
    // int GetHeightInRoom_OLD(pos)
    //public HashSet<Vector2Int> floor_hash_room = new();
    //public HashSet<Vector2Int> wall_hash_room = new();
    //public Dictionary<Vector2Int, int> heights_lookup_room = new();

    // NEW style: After migrating to using class Cell instead of separate lists.
    // GetCellInRoom(pos) returns the index into this room's "cells" list.
    // on not finding the cell, function returns -1.
    public Dictionary<Vector2Int, int> cell_dictionary_room = new();


    // == constructors...
    public Room() { }

    // NEW
    public Room(List<Vector2Int> initialTileList, List<int> initialHeightsList)
    {
        //List<Vector2Int> pos_list = new List<Vector2Int>(initialTileList);
        //List<int> heights = new List<int>(initialHeightsList);

        cells = new List<Cell>();
        for (int i = 0; i < initialTileList.Count; i++)
        {
            cells.Add(new Cell(initialTileList[i].x, initialTileList[i].y, initialHeightsList[i]));
        }
    }

    // UNUSED
    public Room(List<Vector2Int> initialTileList)
    {

        cells = new List<Cell>();
        for (int i = 0; i < initialTileList.Count; i++)
        {
            cells.Add(new Cell(initialTileList[i].x, initialTileList[i].y, 0));
        }
    }

    // UNUSED
    public Room(List<Cell> initialCells)
    {
        // Note: not deep copy
        this.cells = new(initialCells);
    }

    // UNUSED
    // copy constructor - buggy - not deep copy
    public Room(Room other)
    {
        cells = new List<Cell>(other.cells);
        doors = new List<Door>(other.doors);
        name = other.name;
        colorFloor = other.colorFloor;
        isCorridor = other.isCorridor;

        cells = other.cells;
        // TODO: check what other parameters need copying...
    }

    // NEW
    public bool IsTileInRoom(Vector2Int pos)
    {
        int cell_num = GetCellInRoom(pos);
        return (cell_num >= 0);
    }

    // NEW
    public int GetCellInRoom(Vector2Int pos)
    {
        if (cell_dictionary_room.Count == 0) // then build cache
        {
            //Debug.Log($"Building cell_dictionary_room.");
            // Build dictionary once and keep it.
            //   Auto-regenerates if "cells" list length changes.
            //   Note that you must manually call ResetCellDictionary()
            //   yourself if you modify the list
            cell_dictionary_room = new(cells.Count);
            int cell_number = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (cell_dictionary_room.TryAdd(cells[i].pos, cell_number))
                    cell_number++;
            }
        }
        // Here is the actual function.  Everything above was calculating the cache.
        return cell_dictionary_room.TryGetValue(pos, out var v) ? v : -1;
    }

    // NEW
    public void ResetCellDictionary()
    {
        Debug.Log($"Clearing cell_dictionary_room.");
        cell_dictionary_room = new();   // will force list to be regenerated next time it is used.
    }

    // NEW
    // simple helper lookup function for height.
    // Other fields could be done the same way.
    public int GetHeightInRoom(Vector2Int pos)
    {
        int index = GetCellInRoom(pos);
        //Debug.Log($"GetHeightInRoom: index = {index}, cells.Count = {cells.Count}");
        if (index >= 0) return cells[index].height;
        else return 999; // not found
    }

    public RectInt GetBounds()
    {
        if (cells == null || cells.Count == 0)
            return new RectInt(0, 0, 0, 0);

        int minX = cells[0].x;
        int maxX = cells[0].x;
        int minY = cells[0].y;
        int maxY = cells[0].y;

        foreach (var cell in cells)
        {
            if (cell.x < minX) minX = cell.x;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.y > maxY) maxY = cell.y;
        }
        // cache the value and then return it.
        bounds = new RectInt(minX, minY, (maxX - minX + 1), (maxY - minY + 1));
        return bounds;
    }


    // ==================== Color Helper functions...

    //setColorFloor sets all floors of a room to a color.

    // Set the color for the floor tiles in this room many ways...
    // room.setColorFloor(Color.white);        // White
    // room.setColorFloor(rgb: "#FF0000FF"); // Red
    // room.setColorFloor();                   // Bright Random
    // room.setColorFloor(highlight: false);   // Dark   Random
    // room.setColorFloor(highlight: true);    // Bright Random
    public Color setColorFloor(Color? color = null, bool highlight = true, string rgba = "")
    {
        colorFloor = getColor(color: color, highlight: highlight, rgba: rgba);
        return colorFloor;
    }

    //getColor is a simple helper to generate a Color based on various ways to specify a color
    // (see setColorFloor for examples)
    public Color getColor(Color? color = null, bool highlight = true, string rgba = "")
    {
        Color colorrgba; // temp
        Color return_color = Color.white;

        if (color != null)
            return_color = (Color)color;
        else if ((!string.IsNullOrEmpty(rgba)) && (ColorUtility.TryParseHtmlString(rgba, out colorrgba)))
            colorFloor = colorrgba;
        else if (highlight)
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f);   // Bright Random
        else // highlight == false
            return_color = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.1f, 0.4f); // Dark Random

        return return_color;
    }

}

public partial class DungeonGenerator : MonoBehaviour
{
    public Heightfield hf;
    public bool hf_valid = false; // so we only create it once.
    DirFlags wall_dirs;

    // =======================================================
    // helper routines for Rooms

    // NEW Draw: room -> 2D tiles
    public void DrawMapFromRoomsList(List<Room> rooms)
    {
        tilemap.ClearAllTiles();

        foreach (Room room in rooms)
        {
            foreach (Cell cell in room.cells)
            {
                Vector3Int pos = new Vector3Int(cell.x, cell.y, 0);
                tilemap.SetTile(pos, floorTile);
                tilemap.SetTileFlags(pos, TileFlags.None); // Allow color changes
                tilemap.SetColor(pos, cell.colorFloor);
            }
        }
    }

    // NEW Apply this function to a room to give subtle ripples to the floor heights.
    public Room AddPerlinToFloorHeights(Room room)
    {
        if (cfg.perlinFloorHeights == 0) return room;

        int perlin_floor;
        float seedX = cfg.GlobalPerlinSeed ? perlinSeedX : UnityEngine.Random.Range(0f, 9999f);
        float seedY = cfg.GlobalPerlinSeed ? perlinSeedY : UnityEngine.Random.Range(0f, 9999f);
        foreach (Cell cell in room.cells)
        {
            perlin_floor = (int)(Mathf.PerlinNoise((cell.x + seedX) * cfg.perlinFloorWavelength,
                                                   (cell.y + seedY) * cfg.perlinFloorWavelength)
                                                   * cfg.perlinFloorHeights);
            cell.height += perlin_floor;
        }
        return room;
    }

    public Room TiltRoom(Room room, Vector2 topDir, float angleDeg, float heightUnitsPerTile = 1f)
    {
        // Guard rails
        if (room == null || room.cells == null || room.cells.Count == 0) return room;
        if (topDir.sqrMagnitude < 1e-8f) return room;
        if (Mathf.Abs(angleDeg) < 1e-6f) return room;

        // Normalize direction (toward the "top" of the tilt = higher)
        Vector2 dir = topDir.normalized;

        // Room center from bounds (midpoint between min and max)
        RectInt b = room.GetBounds();                   // ensure this returns inclusive bounds
        float cx = b.xMin + (b.width  - 1) * 0.5f;
        float cy = b.yMin + (b.height - 1) * 0.5f;

        // Per-tile vertical rise given the slope angle.
        // If one grid step in XY equals 1 "height unit", set heightUnitsPerTile = 1.
        // If you quantize heights (e.g., 1 height unit = 0.01 meters), pass that scale in.
        float slopePerTile = Mathf.Tan(angleDeg * Mathf.Deg2Rad) / heightUnitsPerTile;

        // (Optional) compute the maximum signed projection run from center to room edge
        // along the given direction; useful if you want to reason about max delta:
        // float hx = (b.width  - 1) * 0.5f;
        // float hy = (b.height - 1) * 0.5f;
        // float maxProjAbs = Mathf.Abs(dir.x) * hx + Mathf.Abs(dir.y) * hy;
        // float maxHeightDelta = slopePerTile * maxProjAbs;

        Debug.Log($"Tilting room {room.my_room_number} in direction {dir} with max angle {angleDeg}°. Slope per tile = {slopePerTile:F3} height units.");
        foreach (var cell in room.cells)
        {
            // Signed distance of this cell from the center along tilt direction
            float proj = (cell.x - cx) * dir.x + (cell.y - cy) * dir.y;

            // Height change = slope * run (rise over run)
            float delta = slopePerTile * proj;

            // Integer heights: round to nearest (stable & symmetric around center)
            int dInt = Mathf.RoundToInt(delta);
            cell.height += dInt;
        }

        return room;
    }

    // ==================== Tile Tilt functions ====================

    /// <summary>
    /// Compute tilt Euler (pitch=x, yaw=y(=0), roll=z) with robust handling of missing neighbors.
    /// Pass null for any neighbor that doesn't exist.
    /// h* are in grid height units; heightUnit converts to world units.
    /// edgeTiltScale in [0..1]: 1 = full one-sided tilt, 0 = flatten at edges.
    /// </summary>
    public static Quaternion ComputeTiltTile(
        float hCenter,
        float? hNorth, float? hEast, float? hSouth, float? hWest,
        float tileSizeX, float tileSizeZ,
        float heightUnit = 1f,
        float maxAbsAngleDeg = 75f,
        float edgeTiltScale = 0.8f, // soften edge tilts slightly
        float baseYawDeg = 0f
    )
    {
        float dx = Mathf.Max(1e-6f, tileSizeX);
        float dz = Mathf.Max(1e-6f, tileSizeZ);

        // --- slope along X (east-west) ---
        float gx;
        bool hasE = hEast.HasValue, hasW = hWest.HasValue;
        if (hasE && hasW)
        {
            gx = ((hEast.Value - hWest.Value) * heightUnit) / (2f * dx);
        }
        else if (hasE)
        {
            gx = ((hEast.Value - hCenter) * heightUnit) / dx;
            gx *= edgeTiltScale;
        }
        else if (hasW)
        {
            gx = ((hCenter - hWest.Value) * heightUnit) / dx;
            gx *= edgeTiltScale;
        }
        else
        {
            gx = 0f;
        }

        // --- slope along Z (north-south) ---
        float gz;
        bool hasN = hNorth.HasValue, hasS = hSouth.HasValue;
        if (hasN && hasS)
        {
            gz = ((hNorth.Value - hSouth.Value) * heightUnit) / (2f * dz);
        }
        else if (hasN)
        {
            gz = ((hNorth.Value - hCenter) * heightUnit) / dz;
            gz *= edgeTiltScale;
        }
        else if (hasS)
        {
            gz = ((hCenter - hSouth.Value) * heightUnit) / dz;
            gz *= edgeTiltScale;
        }
        else
        {
            gz = 0f;
        }

        // Convert slopes to angles
        float pitchDeg = Mathf.Rad2Deg * Mathf.Atan(gz);   // tilt around X toward +Z when gz>0
        float rollDeg = -Mathf.Rad2Deg * Mathf.Atan(gx);  // tilt around Z toward +X when gx>0

        // Clamp extremes for stability.  leaves cliffs looking funky
        //pitchDeg = Mathf.Clamp(pitchDeg, -maxAbsAngleDeg, maxAbsAngleDeg);
        //rollDeg = Mathf.Clamp(rollDeg, -maxAbsAngleDeg, maxAbsAngleDeg);

        // if extreme, just flatten.  better for cliffs.
        if (Mathf.Abs(pitchDeg) > maxAbsAngleDeg) pitchDeg = 0f;
        if (Mathf.Abs(rollDeg) > maxAbsAngleDeg) rollDeg = 0f;

        var e = Quaternion.Euler(pitchDeg, baseYawDeg, rollDeg);
        return e;
    }

    // ==================== Room Height functions ====================

    // UNCHANGED
    public int GetHeightOfLocationFromOneRoom(Room room, Vector2Int pos)
    {
        //Debug.Log($"room.tiles = {room.tiles.Count}; room.heights = {room.heights.Count}");
        int height = room.GetHeightInRoom(pos);
        if (height != 999) return height; // found it

        //Debug.Log("location not found in room");
        return 999;
    }

    // UNCHANGED
    public int GetHeightOfLocationFromAllRooms(List<Room> rooms, Vector2Int pos)
    {
        int height;
        foreach (var room in rooms)
        {
            height = room.GetHeightInRoom(pos);
            if (height != 999) return height; // found it
        }
        //Debug.Log("location not found in rooms");
        return 999;
    }

    // uses Heightfield.cs:
    public void PrepareHeightfield()
    {
        // count all the cells to allocate enough for the tmp array
        int totalCellCount = 0;
        foreach (var room in rooms) totalCellCount += room.cell_dictionary_room.Count;

        // Prepare cells from your rooms:
        var tmp = new List<RoomCell>(totalCellCount);
        int room_id = 0;
        int cell_id = 0;
        int worldWidth = 1;
        int worldHeight = 1;
        foreach (var room in rooms)
        {
            cell_id = 0;
            foreach (var cell in room.cells)
            { // (x,y,height)
                tmp.Add(new RoomCell(cell.x, cell.y, cell.height, room_id, cell_id));
                if (cell.x > worldWidth) worldWidth = cell.x;
                if (cell.y > worldHeight) worldHeight = cell.y;
                cell_id++;
            }
            room_id++;
        }

        // Build the global array hf
        //hf = Heightfield.BuildFromCells(tmp, worldWidth, worldHeight, cfg.minRoomHeight);
        hf = Heightfield.BuildFromCells(tmp, cfg.mapWidth, cfg.mapHeight, cfg.minRoomHeight);
        hf_valid = true;
    }


    public IEnumerator BuildWallsAroundFloorsInRooms(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("BuildWallsAroundFloorsInRooms"); local_tm = true; }
        try
        {
            // Build the heighfield hf if it doesn't yet exist.
            if (!hf_valid || hf == null || hf.Width == 0 || hf.Height == 0)
            {
                PrepareHeightfield();
                if (tm.IfYield()) yield return null;
            }

            // For all cells, find walls around floors
            // use an appropriate policy for the type of map:
            NeighborPolicy policy = cfg.useThinWalls ? NeighborPolicy.TreatDifferentRoomAsWall : NeighborPolicy.SameLevelOnly;
            int room_number = 0;
            foreach (Room room in rooms)
            {
                int cell_num = 0;
                foreach (var cell in room.cells)
                {
                    var dirs = HeightfieldWalls.GetExposedDirs(
                        hf, cell.x, cell.y, cell.height, cfg.minRoomHeight,
                        currentRoomId: room_number,
                        policy: policy,
                        treatBoundsAsWalls: true
                    );
                    cell.walls = dirs;

                    cell_num++;
                }
                room_number++;
                if (tm.IfYield()) yield return null;
            }
        }
        finally { if (local_tm) tm.End(); }
    }

    // UNUSED NEW
    // MoveRoom will shift a room in x,y,and z(height) directions.
    // If allow_collision = false, room doesn't move when it collides with another room.
    // TODO: check for collision.  Also allow rotation, scaling, growing?
    public bool MoveRoom(int room_number, Vector3Int transpose_vector, bool allow_collision = true)
    {
        List<Cell> new_cells = new();
        //List<Vector2Int> new_floors = new();
        //List<int> new_heights = new();
        List<Door> new_doors = new();
        int collisions = 0;

        for (int tile_number = 0; tile_number < rooms[room_number].cells.Count; tile_number++)
        {
            Vector2Int new_floor = (rooms[room_number].cells[tile_number].pos + new Vector2Int(transpose_vector.x, transpose_vector.y));
            int new_height = (rooms[room_number].heights[tile_number] + transpose_vector.z);
            new_cells.Add(new Cell(new_floor.x, new_floor.y, new_height));
            // TODO: Check for collisions to other rooms
        }
        if (collisions == 0 || allow_collision)
        {
            rooms[room_number].cells = new_cells;
            //rooms[room_number].tiles = new_floors;
            //rooms[room_number].heights = new_heights;
            rooms[room_number].doors = new_doors;
            return true; // true = no collisions or ignore them
        }
        else
        {
            return false; // false = collided so don't update room
        }

    }

    // UNCHANGED
    // create a complete list of all rooms connected, ignoring duplicates
    public List<int> get_union_of_connected_room_indexes(int start_room_number, bool everything = true)
    {
        bool added = true;
        List<int> rooms_to_connect = new();
        rooms_to_connect.Add(start_room_number);
        rooms_to_connect.AddRange(rooms[start_room_number].neighbors);

        // if everything, include all neighboring rooms of neighbors
        // if !everything, only include direct neighbors
        if (!everything) return rooms_to_connect;

        // create a complete list of all rooms connected, ignoring duplicates
        // keep going over the list until no more to add
        while (added == true)
        {
            added = false;

            for (int i = 0; i < rooms_to_connect.Count; i++)
            {
                for (int j = 0; j < rooms[rooms_to_connect[i]].neighbors.Count; j++)
                {
                    if (!rooms_to_connect.Contains(rooms[rooms_to_connect[i]].neighbors[j]))
                    {
                        rooms_to_connect.Add(rooms[rooms_to_connect[i]].neighbors[j]);
                        added = true;
                    }
                }
            }
        }
        return rooms_to_connect;
    }

    // NEW
    public List<Vector2Int> get_union_of_connected_room_cells(int start_room_number, bool everything = true)
    {
        List<Vector2Int> union_of_cells = new();
        // create a complete list of all rooms connected, ignoring duplicates
        List<int> rooms_to_connect = get_union_of_connected_room_indexes(start_room_number, everything);

        // add tiles from all connected rooms to the list (union of cells)
        for (int i = 0; i < rooms_to_connect.Count; i++)
        {
            foreach (Cell cell in rooms[rooms_to_connect[i]].cells)
                union_of_cells.Add(cell.pos);
        }

        //Debug.Log("get_union_of_connected_room_cells(" + start_room_number + ") -> length " + union_of_cells.Count + " END");
        return union_of_cells;
    }

    // Neighborhood searches...

    // UNCHANGED
    public int GetHeightInNeighborhood(int room_number, Vector2Int pos)
    {
        int ht = rooms[room_number].GetHeightInRoom(pos);
        if (ht != 999) return ht;
        List<int> myneighbors = rooms[room_number].neighbors;
        for (int i = 0; i < myneighbors.Count; i++)
        {
            ht = rooms[myneighbors[i]].GetHeightInRoom(pos);
            if (ht != 999) return ht;
        }
        return ht;
    }

    // UNCHANGED. UNUSED
    public bool IsTileInNeighborhood(int room_number, List<int> room_neighbors, Vector2Int pos)
    {
        //Debug.Log($"IsTileInNeighborhood: room_neighbors.Count={room_neighbors.Count} pos = {pos.x},{pos.y}");
        bool isit = rooms[room_number].IsTileInRoom(pos);
        if (isit) return isit;
        //List<int> myneighbors = rooms[room_number].neighbors;
        for (int i = 0; i < room_neighbors.Count; i++)
        {
            isit = rooms[room_neighbors[i]].IsTileInRoom(pos);
            if (isit) return isit;
        }
        //Debug.Log($"isit = {isit}, in room {room_number}");
        return isit;
    }

}