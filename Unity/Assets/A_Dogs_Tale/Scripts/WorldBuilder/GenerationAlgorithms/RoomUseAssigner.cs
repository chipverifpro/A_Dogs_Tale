using UnityEngine;

public static partial class RoomUseAssigner
{
    public static void AssignRoomUses(System.Collections.Generic.IEnumerable<Room> rooms, DungeonSettings cfg)
    {
        if (rooms == null) return;

        foreach (Room room in rooms)
        {
            AssignRandomRoomUse(room, cfg);
            ApplyRoomEnvironment(room, cfg);
            ApplyRoomPresentation(room, cfg);
        }
    }

    public static void AssignRandomRoomUse(Room room, DungeonSettings cfg)
    {
        if (room == null) return;

        if (room.isCorridor)
        {
            room.placementTypes = PlacementRoomTypeFlags.Corridor;
            return;
        }

        room.placementTypes = PickWeightedRandom(cfg != null ? cfg.GetActivePackedRoomTypeWeights() : null);
    }

    public static void AssignRandomRoomUseInsane(Room room)
    {
        if (room == null) return;

        if (room.isCorridor)
        {
            room.placementTypes = PlacementRoomTypeFlags.Corridor;
            return;
        }

        PlacementRoomTypeFlags[] baseTypes =
        {
            PlacementRoomTypeFlags.Bedroom,
            PlacementRoomTypeFlags.Kitchen,
            PlacementRoomTypeFlags.Living,
            PlacementRoomTypeFlags.Bathroom,
            PlacementRoomTypeFlags.Hallway,
            PlacementRoomTypeFlags.Utility,
            PlacementRoomTypeFlags.Generic,
            PlacementRoomTypeFlags.Outdoor
        };

        int comboCountRoll = UnityEngine.Random.Range(0, 100);
        int comboCount;
        if (comboCountRoll < 70) comboCount = 1;
        else if (comboCountRoll < 95) comboCount = 2;
        else comboCount = 3;

        comboCount = Mathf.Clamp(comboCount, 1, baseTypes.Length);

        PlacementRoomTypeFlags result = PlacementRoomTypeFlags.None;

        for (int i = 0; i < comboCount; i++)
        {
            var type = baseTypes[UnityEngine.Random.Range(0, baseTypes.Length)];
            result |= type;
        }

        if (result == PlacementRoomTypeFlags.None)
            result = PlacementRoomTypeFlags.Generic;

        room.placementTypes = result;
    }
}
