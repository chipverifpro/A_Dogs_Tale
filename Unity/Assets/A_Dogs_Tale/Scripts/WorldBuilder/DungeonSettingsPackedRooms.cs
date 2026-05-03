using UnityEngine;

public partial class DungeonSettings
{
    // ---- Algorithm selectors per stage ----
    public enum CorridorAlgo { DrunkardsWalk, WanderingMST, MedialAxis, GridMazes }
    public enum RoomSeedAlgo { AlongCorridors, PoissonAlongCorridors, UniformGrid }
    public enum RoomGrowAlgo { CreditWavefrontStrips, PressureField, OrthogonalRays }
    public enum ScrapAlgo { VoronoiFill, SeedAndGrowUntilPacked, ClosetsOnly, NearestRoom }
    public enum DoorAlgo { EnsureConnectivity, SparseLoops, ManyLoops }

    [Header("Pipeline Algorithms")]
    public CorridorAlgo corridorAlgo = CorridorAlgo.WanderingMST;
    public RoomSeedAlgo roomSeedAlgo = RoomSeedAlgo.AlongCorridors;
    public RoomGrowAlgo roomGrowAlgo = RoomGrowAlgo.CreditWavefrontStrips;
    public ScrapAlgo scrapAlgo = ScrapAlgo.VoronoiFill;
    public DoorAlgo doorAlgo = DoorAlgo.EnsureConnectivity;

    [Header("Packed Room Params")]
    public bool usePackedRooms = false;
    public bool useRoundPen = false;
    public PackedRoomTheme_e packedRoomTheme = PackedRoomTheme_e.House;

    [System.Serializable]
    public struct CorridorParams
    {
        public int corridorWidth;
        public int spineCount;
        public float wanderiness;
        public float loopChance;
        public int drunkWalkers;
        public int drunkStepsPerWalker;
        public int drunkMinimumStraight;
    }

    [Header("Corridor Params")]
    public CorridorParams corridor = new CorridorParams
    {
        spineCount = 2,
        wanderiness = 0.25f,
        loopChance = 0.15f,
        corridorWidth = 1,
        drunkWalkers = 2,
        drunkStepsPerWalker = 400,
        drunkMinimumStraight = 10,
    };

    [System.Serializable]
    public struct SeedParams
    {
        public int spacing;
        public float alternateSides;
        public int jitter;
    }

    [Header("Room Seeding Params")]
    public SeedParams RoomSeeding = new SeedParams { spacing = 8, alternateSides = 1f, jitter = 2 };

    [System.Serializable]
    public struct RoomTypeWeight
    {
        public PlacementRoomTypeFlags type;
        public int weight;
    }

    [Header("Packed Room Use Weights")]
    [Tooltip("Weighted room-use assignment for the current indoor packed-room workflow.")]
    public RoomTypeWeight[] packedHouseRoomTypes = new[]
    {
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Bedroom,  weight = 3 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Living,   weight = 3 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Kitchen,  weight = 2 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Library,  weight = 1 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Bathroom, weight = 2 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Utility,  weight = 1 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Hallway,  weight = 1 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Generic,  weight = 4 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Outdoor,  weight = 1 },
    };

    [Tooltip("Weighted room-use assignment for a future park-style packed-room workflow. Corridor rooms continue to map to pathways.")]
    public RoomTypeWeight[] packedParkRoomTypes = new[]
    {
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Grass,           weight = 5 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Garden,          weight = 3 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.SportsField,     weight = 2 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.Wooded,          weight = 3 },
        new RoomTypeWeight { type = PlacementRoomTypeFlags.PicnicStructure, weight = 1 },
    };

    [System.Serializable]
    public struct GrowParams
    {
        public int stripRounds;
        public int areaCreditMin;
        public int areaCreditMax;
        public int wallMoat;
        public int splitArea;
        public float splitAspect;
        public int passesBeforeSplit;
        public int targetAspect;
        public int percentSkipGrowth;
    }

    [Header("Room Growth Params")]
    public GrowParams grow = new GrowParams
    {
        stripRounds = 40,
        areaCreditMin = 40,
        areaCreditMax = 140,
        wallMoat = 1,
        splitArea = 300,
        splitAspect = 3f,
        passesBeforeSplit = 20,
        targetAspect = 2,
        percentSkipGrowth = 50
    };

    [System.Serializable]
    public struct ScrapParams
    {
        public int closetMaxArea;
    }

    [Header("Scrap Cleanup Params")]
    public ScrapParams scraps = new ScrapParams { closetMaxArea = 12 };

    [System.Serializable]
    public struct DoorParams
    {
        [Range(0f, 1f)] public float loopiness;
        public int minDoorSpacing;
        public int maxDoorsPerRoom;
        public int deadEndReach;
    }

    [Header("Door Params")]
    public DoorParams doors = new DoorParams { loopiness = 0.25f, minDoorSpacing = 3, maxDoorsPerRoom = 6, deadEndReach = 6 };

    public RoomTypeWeight[] GetActivePackedRoomTypeWeights()
    {
        return packedRoomTheme == PackedRoomTheme_e.Park ? packedParkRoomTypes : packedHouseRoomTypes;
    }

    public bool IsPackedParkTheme()
    {
        return packedRoomTheme == PackedRoomTheme_e.Park;
    }

    public bool UseThinWallsEffective()
    {
        return IsPackedParkTheme() || useThinWalls;
    }

    public bool GenerateWallsBetweenTouchingRoomsEffective()
    {
        return !IsPackedParkTheme() && generateWallsBetweenTouchingRooms;
    }

    public int GetEffectivePackedWallMoat()
    {
        return UseThinWallsEffective() ? 0 : Mathf.Max(0, wallThickness);
    }

    public int GetEffectiveGrowWallMoat()
    {
        return IsPackedParkTheme() ? 0 : Mathf.Max(0, grow.wallMoat);
    }
}
