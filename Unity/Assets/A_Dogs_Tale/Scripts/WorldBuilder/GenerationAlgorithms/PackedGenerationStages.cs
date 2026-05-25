using System.Collections;

public partial class DungeonGenerator
{
    // ---------- Stage switches ----------
    IEnumerator RunCorridors()
    {
        switch (cfg.corridorAlgo)
        {
            case DungeonSettings.CorridorAlgo.WanderingMST: return Corridors_WanderingMST();
            case DungeonSettings.CorridorAlgo.MedialAxis: return Corridors_MedialAxis();
            case DungeonSettings.CorridorAlgo.GridMazes: return Corridors_GridMazes();
            case DungeonSettings.CorridorAlgo.DrunkardsWalk:
                return Corridors_DrunkardsWalk(
                walkers: cfg.corridor.drunkWalkers,
                stepsPerWalker: cfg.corridor.drunkStepsPerWalker,
                minimumStraight: cfg.corridor.drunkMinimumStraight,
                wander: cfg.corridor.wanderiness,
                corridorWidth: cfg.corridor.corridorWidth
            );
            default: return Corridors_WanderingMST();
        }
    }

    IEnumerator RunRoomSeeding()
    {
        switch (cfg.roomSeedAlgo)
        {
            case DungeonSettings.RoomSeedAlgo.AlongCorridors: return Seed_AlongCorridors();
            case DungeonSettings.RoomSeedAlgo.PoissonAlongCorridors: return Seed_PoissonAlongCorridors();
            case DungeonSettings.RoomSeedAlgo.UniformGrid: return Seed_UniformGrid();
            default: return Seed_AlongCorridors();
        }
    }
    IEnumerator RunRoomGrowth()
    {
        switch (cfg.roomGrowAlgo)
        {
            case DungeonSettings.RoomGrowAlgo.CreditWavefrontStrips: return Grow_CreditWavefrontStrips();
            //case DungeonSettings.RoomGrowAlgo.StripThenWavefront: return Grow_StripThenWavefront();
            case DungeonSettings.RoomGrowAlgo.PressureField: return Grow_PressureField();
            case DungeonSettings.RoomGrowAlgo.OrthogonalRays: return Grow_OrthogonalRays();
            default: return Grow_CreditWavefrontStrips();
        }
    }
    IEnumerator RunScraps()
    {
        switch (cfg.scrapAlgo)
        {
            case DungeonSettings.ScrapAlgo.VoronoiFill: return Scraps_VoronoiFill();
            case DungeonSettings.ScrapAlgo.SeedAndGrowUntilPacked: return Scraps_SeedAndGrowUntilPacked(ScrapSeedMode.RandomScatter);//.PerimeterEveryN);
            case DungeonSettings.ScrapAlgo.ClosetsOnly: return Scraps_ClosetsOnly();
            case DungeonSettings.ScrapAlgo.NearestRoom: return Scraps_NearestRoom();
            default: return Scraps_VoronoiFill();
        }
    }
    IEnumerator RunDoors()
    {
        switch (cfg.doorAlgo)
        {
            case DungeonSettings.DoorAlgo.EnsureConnectivity: return PlaceDoors();
            case DungeonSettings.DoorAlgo.SparseLoops: return Doors_SparseLoops();
            case DungeonSettings.DoorAlgo.ManyLoops: return Doors_ManyLoops();
            default: return Doors_EnsureConnectivity();
        }
    }
}
