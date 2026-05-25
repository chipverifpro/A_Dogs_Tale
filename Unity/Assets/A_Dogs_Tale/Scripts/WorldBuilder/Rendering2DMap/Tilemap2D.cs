using UnityEngine;

public partial class DungeonGenerator : MonoBehaviour  // Tilemap2D
{

    // The 2D map and Unity's tilemap functions and data are here.....

    public byte[,] map; // each byte represents one of the below constants
    public int[,] mapHeights; // 2D array to store height information for each tile
    //public bool mapStale = true; // Flag to indicate if map needs to be regenerated from rooms
    [HideInInspector] public const byte WALL = 1;
    [HideInInspector] public const byte FLOOR = 2;
    [HideInInspector] public const byte RAMP = 3;
    [HideInInspector] public const byte UNKNOWN = 99;
    // Additional tile types to be defined here

}
