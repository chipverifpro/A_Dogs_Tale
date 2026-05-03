using UnityEngine;
using System.Collections.Generic;

public static partial class RoomUseAssigner
{
    public static void ApplyRoomEnvironment(Room room, DungeonSettings cfg)
    {
        if (room == null)
            return;

        bool isPark = cfg != null && cfg.IsPackedParkTheme();
        bool markOutdoor = isPark || (room.placementTypes & PlacementRoomTypeFlags.Outdoor) != 0;

        if (markOutdoor)
            room.placementTypes |= PlacementRoomTypeFlags.Outdoor;

        room.isOutdoor = markOutdoor;
        room.ceilingHeight = markOutdoor ? 0f : Mathf.Max(room.ceilingHeight, 3.5f);
    }

    public static void ApplyRoomPresentation(Room room, DungeonSettings cfg)
    {
        ApplyRoomPresentation(room, cfg, null);
    }

    public static void ApplyRoomPresentation(Room room, DungeonSettings cfg, Dictionary<string, int> roomNameCounts)
    {
        if (room == null)
            return;

        Color roomColor = room.colorFloor;
        float travelCost = 1f;
        string roomLabel = GetRoomLabel(room, cfg);

        if (cfg != null && cfg.IsPackedParkTheme())
        {
            if (room.isCorridor)
            {
                roomColor = new Color(0.73f, 0.66f, 0.50f, 0.65f);
                travelCost = 0.85f;
            }
            else if ((room.placementTypes & PlacementRoomTypeFlags.Grass) != 0)
            {
                roomColor = new Color(0.40f, 0.70f, 0.30f, 0.65f);
                travelCost = 1.05f;
            }
            else if ((room.placementTypes & PlacementRoomTypeFlags.Garden) != 0)
            {
                roomColor = new Color(0.27f, 0.60f, 0.22f, 0.65f);
                travelCost = 1.10f;
            }
            else if ((room.placementTypes & PlacementRoomTypeFlags.SportsField) != 0)
            {
                roomColor = new Color(0.23f, 0.56f, 0.19f, 0.65f);
                travelCost = 0.95f;
            }
            else if ((room.placementTypes & PlacementRoomTypeFlags.Wooded) != 0)
            {
                roomColor = new Color(0.18f, 0.34f, 0.16f, 0.65f);
                travelCost = 1.30f;
            }
            else if ((room.placementTypes & PlacementRoomTypeFlags.PicnicStructure) != 0)
            {
                roomColor = new Color(0.69f, 0.62f, 0.47f, 0.65f);
                travelCost = 1.00f;
            }
        }

        room.colorFloor = roomColor;
        room.name = BuildRoomName(roomLabel, roomNameCounts);

        if (room.cells == null)
            return;

        foreach (Cell cell in room.cells)
        {
            if (cell == null)
                continue;

            cell.colorFloor = roomColor;
            cell.travel_cost = travelCost;
        }
    }

    public static string GetRoomLabel(Room room, DungeonSettings cfg)
    {
        if (room == null)
            return "Room";

        if (cfg != null && cfg.IsPackedParkTheme())
        {
            if (room.isCorridor)
                return "Pathway";
            if ((room.placementTypes & PlacementRoomTypeFlags.Grass) != 0)
                return "Grass";
            if ((room.placementTypes & PlacementRoomTypeFlags.Garden) != 0)
                return "Garden";
            if ((room.placementTypes & PlacementRoomTypeFlags.SportsField) != 0)
                return "Sports Field";
            if ((room.placementTypes & PlacementRoomTypeFlags.Wooded) != 0)
                return "Wooded";
            if ((room.placementTypes & PlacementRoomTypeFlags.PicnicStructure) != 0)
                return "Picnic Structure";
            return "Park Area";
        }

        if (room.isCorridor)
            return "Corridor";
        if ((room.placementTypes & PlacementRoomTypeFlags.Outdoor) != 0)
            return "Outdoor";
        if ((room.placementTypes & PlacementRoomTypeFlags.Bedroom) != 0)
            return "Bedroom";
        if ((room.placementTypes & PlacementRoomTypeFlags.Kitchen) != 0)
            return "Kitchen";
        if ((room.placementTypes & PlacementRoomTypeFlags.Library) != 0)
            return "Library";
        if ((room.placementTypes & PlacementRoomTypeFlags.Living) != 0)
            return "Living Room";
        if ((room.placementTypes & PlacementRoomTypeFlags.Bathroom) != 0)
            return "Bathroom";
        if ((room.placementTypes & PlacementRoomTypeFlags.Utility) != 0)
            return "Utility";
        if ((room.placementTypes & PlacementRoomTypeFlags.Hallway) != 0)
            return "Hallway";
        if ((room.placementTypes & PlacementRoomTypeFlags.Generic) != 0)
            return "Room";

        return "Room";
    }

    private static string BuildRoomName(string roomLabel, Dictionary<string, int> roomNameCounts)
    {
        if (string.IsNullOrWhiteSpace(roomLabel))
            roomLabel = "Room";

        if (roomNameCounts == null)
            return $"{roomLabel} 1";

        if (!roomNameCounts.TryGetValue(roomLabel, out int count))
            count = 0;

        count++;
        roomNameCounts[roomLabel] = count;
        return $"{roomLabel} {count}";
    }
}
