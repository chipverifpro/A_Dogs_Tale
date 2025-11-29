using UnityEngine;


[CreateAssetMenu(fileName = "DungeonSettings", menuName = "Scriptable Objects/DungeonSettings")]
public class DungeonSettings : ScriptableObject
{
    // Type enumerations...
    public enum RoomAlgorithm_e { Scatter_Overlap, Scatter_NoOverlap, CellularAutomata, CellularAutomataPerlin, Tavern, PackedRooms }
    public enum TunnelsAlgorithm_e { TunnelsOrthogonal, TunnelsStraight, TunnelsOrganic, TunnelsCurved }

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
    
    // ---- Algorithm selectors per stage ----
    public enum CorridorAlgo { DrunkardsWalk, WanderingMST, MedialAxis, GridMazes }
    public enum RoomSeedAlgo { AlongCorridors, PoissonAlongCorridors, UniformGrid }
    public enum RoomGrowAlgo { CreditWavefrontStrips, PressureField, OrthogonalRays }
    public enum ScrapAlgo    { VoronoiFill, SeedAndGrowUntilPacked, ClosetsOnly, NearestRoom }
    public enum DoorAlgo     { EnsureConnectivity, SparseLoops, ManyLoops }

    [Header("Pipeline Algorithms")]
    public CorridorAlgo corridorAlgo = CorridorAlgo.WanderingMST;
    public RoomSeedAlgo roomSeedAlgo = RoomSeedAlgo.AlongCorridors;
    public RoomGrowAlgo roomGrowAlgo = RoomGrowAlgo.CreditWavefrontStrips;
    public ScrapAlgo    scrapAlgo    = ScrapAlgo.VoronoiFill;
    public DoorAlgo     doorAlgo     = DoorAlgo.EnsureConnectivity;

    // ---- Tunable parameters per stage (keep lean; add as you need) ----
    [Header("Packed Room Params")]
    public bool usePackedRooms = false;
    public bool useRoundPen = false;    // for corridors only???

    [System.Serializable]
    public struct CorridorParams
    {
        public int corridorWidth;      // narrow is 1..2
        public int spineCount;         // long wandering spines
        public float wanderiness;      // 0..1
        public float loopChance;       // 0..1
        
        public int drunkWalkers;                 // Drunkard's Walk
        public int drunkStepsPerWalker;
        public int drunkMinimumStraight;
    }
    [Header("Corridor Params")]
    public CorridorParams corridor = new CorridorParams { spineCount=2, wanderiness=0.25f, loopChance=0.15f, corridorWidth=1, drunkWalkers = 2, drunkStepsPerWalker = 400, drunkMinimumStraight = 10,};

    [System.Serializable]
    public struct SeedParams
    {
        public int spacing;            // seed every N cells along corridors
        public float alternateSides;   // 0..1 (probability to alternate sides)
        public int jitter;             // random offset in cells
    }
    [Header("Room Seeding Params")]
    public SeedParams RoomSeeding = new SeedParams { spacing=8, alternateSides=1f, jitter=2 };


    [System.Serializable]
    public struct GrowParams
    {
        public int stripRounds; // number of growth passes.
        public int areaCreditMin;
        public int areaCreditMax;
        public int wallMoat;           // reserved wall thickness (zero or 1 usually)
        public int splitArea;          // split rooms larger than this
        public float splitAspect;      // split if aspect > this (e.g., 3)
        public int passesBeforeSplit; // checks for splitrooms after this many rounds
        public int targetAspect;   // tune: try to keep rooms from going too skinny
        public int percentSkipGrowth; // more means more varied room sizes.  50% = half of rooms will be skipped each round     
    }
    [Header("Room Growth Params")]
    public GrowParams grow = new GrowParams { stripRounds = 40, areaCreditMin=40, areaCreditMax=140, wallMoat=1, splitArea=300, splitAspect=3f, passesBeforeSplit=20, targetAspect=2, percentSkipGrowth=50 };

    [System.Serializable]
    public struct ScrapParams
    {
        public int closetMaxArea;      // turn scraps <= this into closets
    }
    [Header("Scrap Cleanup Params")]
    public ScrapParams scraps = new ScrapParams { closetMaxArea=12 };

    [System.Serializable]
    public struct DoorParams
    {
        [Range(0f, 1f)] public float loopiness;  // extra loops beyond minimal connectivity
        public int minDoorSpacing;               // Manhattan spacing between doors on same room edge
        public int maxDoorsPerRoom;              // soft cap (not strict)
        public int deadEndReach;                 // how far to look from dead-end corridors
    }
    [Header("Door Params")]
    public DoorParams doors = new DoorParams { loopiness=0.25f, minDoorSpacing=3, maxDoorsPerRoom=6, deadEndReach=6 };

#if UNITY_EDITOR
public RoomAlgorithm_e debugLastAlgorithm;

private void OnValidate()
{
    if (debugLastAlgorithm != RoomAlgorithm)
    {
        Debug.Log($"DungeonSettings: Algorithm changed from {debugLastAlgorithm} to {RoomAlgorithm}", this);
        debugLastAlgorithm = RoomAlgorithm;
    }
}
#endif

public void LogMe(string context)
{
    Debug.Log($"{context}: DungeonSettings instance '{name}', id={GetInstanceID()}, RoomAlgorithm={RoomAlgorithm}", this);
}

}

