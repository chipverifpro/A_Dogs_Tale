public static class DungeonGenerationModeApplier
{
    public static void ApplyRoomAlgorithmFlags(DungeonSettings cfg)
    {
        if (cfg == null)
            return;

        switch (cfg.RoomAlgorithm)
        {
            case DungeonSettings.RoomAlgorithm_e.Scatter_Overlap:
                cfg.generateOverlappingRooms = true;
                cfg.useCellularAutomata = false;
                cfg.useScatterRooms = true;
                cfg.usePackedRooms = false;
                cfg.useDiagonalCorners = false;
                break;
            case DungeonSettings.RoomAlgorithm_e.Scatter_NoOverlap:
                cfg.generateOverlappingRooms = false;
                cfg.useCellularAutomata = false;
                cfg.useScatterRooms = true;
                cfg.usePackedRooms = false;
                cfg.useDiagonalCorners = false;
                break;
            case DungeonSettings.RoomAlgorithm_e.CellularAutomata:
                cfg.generateOverlappingRooms = false;
                cfg.useCellularAutomata = true;
                cfg.useScatterRooms = false;
                cfg.usePerlin = false;
                cfg.usePackedRooms = false;
                cfg.useDiagonalCorners = true;
                break;
            case DungeonSettings.RoomAlgorithm_e.CellularAutomataPerlin:
                cfg.generateOverlappingRooms = false;
                cfg.useCellularAutomata = true;
                cfg.useScatterRooms = false;
                cfg.usePerlin = true;
                cfg.usePackedRooms = false;
                cfg.useDiagonalCorners = true;
                break;
            case DungeonSettings.RoomAlgorithm_e.Tavern:
                cfg.generateOverlappingRooms = false;
                cfg.useCellularAutomata = false;
                cfg.useScatterRooms = false;
                cfg.usePackedRooms = false;
                cfg.useDiagonalCorners = false;
                break;
            case DungeonSettings.RoomAlgorithm_e.PackedRooms:
                cfg.useCellularAutomata = false;
                cfg.useScatterRooms = false;
                cfg.usePackedRooms = true;
                cfg.useDiagonalCorners = false;
                break;
        }
    }
}
