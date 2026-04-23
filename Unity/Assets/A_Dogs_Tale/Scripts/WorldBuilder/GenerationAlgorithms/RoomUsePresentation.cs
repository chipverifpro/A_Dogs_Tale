using UnityEngine;

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
        if (room == null)
            return;

        Color roomColor = room.colorFloor;
        float travelCost = 1f;
        string roomLabel = room.isCorridor ? "Corridor" : "Room";

        if (cfg != null && cfg.IsPackedParkTheme())
        {
            if (room.isCorridor)
            {
                roomColor = new Color(0.73f, 0.66f, 0.50f, 0.65f);
                travelCost = 0.85f;
                roomLabel = "Pathway";
            }
            else if ((room.placementTypes & PlacementRoomTypeFlags.Grass) != 0)
            {
                roomColor = new Color(0.40f, 0.70f, 0.30f, 0.65f);
                travelCost = 1.05f;
                roomLabel = "Grass";
            }
            else if ((room.placementTypes & PlacementRoomTypeFlags.Garden) != 0)
            {
                roomColor = new Color(0.27f, 0.60f, 0.22f, 0.65f);
                travelCost = 1.10f;
                roomLabel = "Garden";
            }
            else if ((room.placementTypes & PlacementRoomTypeFlags.SportsField) != 0)
            {
                roomColor = new Color(0.23f, 0.56f, 0.19f, 0.65f);
                travelCost = 0.95f;
                roomLabel = "Sports Field";
            }
            else if ((room.placementTypes & PlacementRoomTypeFlags.Wooded) != 0)
            {
                roomColor = new Color(0.18f, 0.34f, 0.16f, 0.65f);
                travelCost = 1.30f;
                roomLabel = "Wooded";
            }
            else if ((room.placementTypes & PlacementRoomTypeFlags.PicnicStructure) != 0)
            {
                roomColor = new Color(0.69f, 0.62f, 0.47f, 0.65f);
                travelCost = 1.00f;
                roomLabel = "Picnic Structure";
            }
            else
            {
                roomLabel = "Park Area";
            }
        }
        else if (room.isCorridor)
        {
            roomLabel = "Corridor";
        }
        else if ((room.placementTypes & PlacementRoomTypeFlags.Outdoor) != 0)
        {
            roomLabel = "Outdoor";
        }
        else if ((room.placementTypes & PlacementRoomTypeFlags.Bedroom) != 0)
        {
            roomLabel = "Bedroom";
        }
        else if ((room.placementTypes & PlacementRoomTypeFlags.Kitchen) != 0)
        {
            roomLabel = "Kitchen";
        }
        else if ((room.placementTypes & PlacementRoomTypeFlags.Living) != 0)
        {
            roomLabel = "Living Room";
        }
        else if ((room.placementTypes & PlacementRoomTypeFlags.Bathroom) != 0)
        {
            roomLabel = "Bathroom";
        }
        else if ((room.placementTypes & PlacementRoomTypeFlags.Utility) != 0)
        {
            roomLabel = "Utility";
        }
        else if ((room.placementTypes & PlacementRoomTypeFlags.Hallway) != 0)
        {
            roomLabel = "Hallway";
        }
        else if ((room.placementTypes & PlacementRoomTypeFlags.Generic) != 0)
        {
            roomLabel = "Room";
        }

        room.colorFloor = roomColor;
        room.name = $"{roomLabel} {room.my_room_number}";

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
}
