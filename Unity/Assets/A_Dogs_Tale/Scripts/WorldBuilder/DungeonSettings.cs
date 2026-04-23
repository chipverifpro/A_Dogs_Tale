using UnityEngine;


[CreateAssetMenu(fileName = "DungeonSettings", menuName = "Scriptable Objects/DungeonSettings")]
public partial class DungeonSettings : ScriptableObject
{
    // Type enumerations...
    public enum RoomAlgorithm_e { Scatter_Overlap, Scatter_NoOverlap, CellularAutomata, CellularAutomataPerlin, Tavern, PackedRooms }
    public enum TunnelsAlgorithm_e { TunnelsOrthogonal, TunnelsStraight, TunnelsOrganic, TunnelsCurved }
    public enum PackedRoomTheme_e { House, Park }

    [Header("Master Configurations")]
    public RoomAlgorithm_e RoomAlgorithm = RoomAlgorithm_e.Scatter_Overlap;
    public TunnelsAlgorithm_e TunnelsAlgorithm = TunnelsAlgorithm_e.TunnelsOrganic;

    [Header("General Settings")]
    public bool showBuildProcess = true;
    public float stepDelay = 0.2f; // how many seconds to wait between generation steps
    public bool randomizeSeed = true;
    public int seed = 0;
    public bool useThinWalls = false;

    [Header("World Map Settings")]
    public int mapWidth = 150;
    public int mapHeight = 150;
    public int borderKeepout = 1;   // should be at least 1 or edge artifacts show up (known bug).
    public bool roundWorld = false; // sometimes not having square map edges is nice.
    public int maxElevation = 100;
    public float unitHeight = 0.1f;  // World units per height unit in the height map. (eg. size of one X tile = 1/unitHeight Z)

    [Header("Scent Parameters")]
    public float scentInterval = 10f;       // interval to decay/spread scents (seconds)
    public float scentDecayRate = 0.1f;     // decay by percent per ScentInterval
    public float scentSpreadAmount = 0.05f;  // neighbors get this percent added per ScentInterval
    public float scentMinimum = 0.001f;       // amount below which the scent completely disappears
    public bool scentPhysicsConsistancey = true; // two algorithms are available for dealing with excessive delays, 'true' is more consistant but slower.

    [Header("Room Floor Bumpiness Settings")]
    public int perlinFloorHeights = 3;  // Height range of added ripple to the floor.
    public float perlinFloorWavelength = 0.05f;  // Frequency of ripple to the floor.
    public bool GlobalPerlinSeed = true; // If true, use same random seed for all rooms.  If false, each room gets its own random seed.

    [Header("Tilt Entire Rooms Settings")]
    public int slopeRoomMaxAngle = 10;  // If > 0, tilt room floors by up to this angle in degrees.

    [Header("Smooth Floor by Tilting every Floor Tile")]
    public bool enableTiltedTiles = true;  // If true, tilt individual floor tiles to match height map.
    public int tiltFloorTilesMaxAngle = 45;  // If > 0, tilt individual floor tiles by up to this angle in degrees.
    public float edgeTiltScale = 0.95f; // Scale down tilt near edges to avoid extreme tilts

    [Header("3D Build Settings")]
    //public float unitHeight = 0.1f;             // world Y per step
    public bool useDiagonalCorners = true;      // if exactly 2 adjacent walls, convert to a diagonal wall
    public bool skipOrthogonalWhenDiagonal = true; // don't add both square and diagonal walls at the same time
    public int perimeterWallSteps = 30; // height of walls in steps
    [Tooltip("If false, touching rooms at the same height are treated as open transitions instead of wall boundaries.")]
    public bool generateWallsBetweenTouchingRooms = true;


    [Header("Scatter Room Settings")]
    public bool useScatterRooms = false;
    public int roomAttempts = 50;
    public int roomsMax = 10;
    public int minRoomSize = 20;
    public int maxRoomSize = 40;
    public bool generateOverlappingRooms = false;
    public bool MergeScatteredRooms = false;
    public bool allowVerticalStacking = true;
    public int minVerticalStackHeight = 5;  // less than this results in merged rooms
    public bool ovalRooms = false;

    // Settings for Cellular Automata
    [Header("Cellular Automata Settings")]
    public bool useCellularAutomata = false;
    [Range(40, 60)] public int cellularFillPercent = 45;
    public int CellularGrowthSteps = 5;

    [Header("Perlin Noise Settings")]
    public bool usePerlin = true;
    [Range(0.01f, 0.1f)]
    [Tooltip("Low = big rooms | High = small rooms")]
    public float perlinWavelength = 0.05f; // Low frequency Perlin for room size
    [Range(0.01f, 0.5f)]
    [Tooltip("Low = lumpy rooms | High = craggy rooms")]
    public float perlin2Wavelength = 0.05f; // Higher frequency Perlin for room roughness
    [Range(0f, 4f)]
    [Tooltip("Low = smooth perlin | High = more roughness")]
    public float perlin2Amplitude = 1f; // Multiplier for perlin2
    [Range(0.4f, 0.6f)]
    [Tooltip("Low = many rooms | High = fewer rooms")]
    public float perlinThreshold = 0.5f;

    [Header("Map Cleanup Settings")]
    public int MinimumRoomSize = 100; // Threshold for tiny rooms filter
    public int MinimumRockSize = 20; // Threshold for minimum size of in-room obstacle
    public int softBorderSize = 5; // Size of the noisy border around the map to soften edge, only works on square maps currently
    public int wallThickness = 1;  // Appearance of perimeter walls in 2D map
    public int minRoomHeight = 30;  // Minimum height difference between floor and ceiling to be considered a room

    [Header("Corridor Settings")]
    public int corridorWidth = 3;  // Width of passages generated between rooms.
    public bool limit_slope = true;  // don't allow slopes to exceed walkability
    public int minimumRamp = 2;  // less than this is not considered a ramp
    public int maximumRamp = 8;  // more than this is considered a cliff

    [Header("Organic Type corridor Settings")]
    public float organicJitterChance = 0.2f; // Chance to introduce a wiggle in "organic" corridors

    [Header("Bezier Corridor Settings")]
    public float bezierControlOffset = 5f; // how curvy to make Bezier corridors
    public float bezierMaxControl = 0.1f; // clip bezierControlOffset for short Bezier corridors

    [Header("Neighbor Cache Settings")]
    public NeighborCache.Shape neighborShape = NeighborCache.Shape.Square;
    public bool includeDiagonals = false;

    [Header("Building Settings")]
    public bool createBuilding = false;
    public int cellar_floor_height = -10;
    public int ground_floor_height = 0;
    public int next_floor_height = 10;
    
}
