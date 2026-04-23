using System.Collections;

public partial class DungeonGenerator
{
    void StripPackedRoomDoorsForOpenAirThemes()
    {
        PackedRoomDoorConnectivityUtility.StripDoorsForOpenAirTheme(rooms, cfg != null && cfg.IsPackedParkTheme());
    }

    IEnumerator EnsurePackedRoomsConnectToCorridor(int yieldEvery = 64)
    {
        yield return PackedRoomDoorConnectivityUtility.EnsureRoomsConnectToCorridor(
            rooms,
            cellGrid,
            corridors,
            cfg.mapWidth,
            cfg.mapHeight,
            cfg.borderKeepout,
            cfg.GetEffectivePackedWallMoat(),
            cfg.doors.minDoorSpacing,
            yieldEvery);
    }
}
