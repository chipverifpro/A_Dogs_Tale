using UnityEngine;
using System.Collections.Generic;
using System;

[System.Serializable]
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
    public List<ScentInCell> scents; // tracks who has passed this way before
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
