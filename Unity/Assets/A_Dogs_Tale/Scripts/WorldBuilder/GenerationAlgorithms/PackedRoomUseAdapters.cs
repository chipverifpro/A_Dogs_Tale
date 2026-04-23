public partial class DungeonGenerator
{
    void AssignRoomUses()
    {
        RoomUseAssigner.AssignRoomUses(rooms, cfg);
    }

    void AssignRandomRoomUse(Room room) => RoomUseAssigner.AssignRandomRoomUse(room, cfg);

    void ApplyRoomEnvironment(Room room) => RoomUseAssigner.ApplyRoomEnvironment(room, cfg);

    void ApplyRoomPresentation(Room room) => RoomUseAssigner.ApplyRoomPresentation(room, cfg);

    // this version assigns multiple random types to rooms for "insane millionaire's mansion" mode
    void AssignRandomRoomUse_insane(Room room) => RoomUseAssigner.AssignRandomRoomUseInsane(room);

    PlacementRoomTypeFlags PickWeightedRandom(DungeonSettings.RoomTypeWeight[] entries) => RoomUseAssigner.PickWeightedRandom(entries);
}
