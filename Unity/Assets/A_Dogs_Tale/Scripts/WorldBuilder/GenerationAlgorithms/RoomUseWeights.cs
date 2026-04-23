using UnityEngine;

public static partial class RoomUseAssigner
{
    public static PlacementRoomTypeFlags PickWeightedRandom(DungeonSettings.RoomTypeWeight[] entries)
    {
        if (entries == null || entries.Length == 0)
            return PlacementRoomTypeFlags.Generic;

        int total = 0;
        foreach (var e in entries)
            total += Mathf.Max(0, e.weight);

        if (total == 0)
            return PlacementRoomTypeFlags.Generic;

        int roll = UnityEngine.Random.Range(0, total);

        foreach (var e in entries)
        {
            roll -= Mathf.Max(0, e.weight);
            if (roll < 0)
                return e.type;
        }

        return entries[entries.Length - 1].type;
    }
}
