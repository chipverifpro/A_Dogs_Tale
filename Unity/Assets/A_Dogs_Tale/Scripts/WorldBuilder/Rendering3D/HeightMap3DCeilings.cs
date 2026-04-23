using UnityEngine;

public partial class DungeonGenerator
{
    public void BuildCeilings()
    {
        var rooms = dir.gen.rooms;
        if (rooms == null || rooms.Count == 0)
        {
            Debug.LogWarning("CeilingBuilder: No rooms found to build ceilings.");
            return;
        }

        int addedCount = 0;

        foreach (var room in rooms)
        {
            if (room == null || room.cells == null || room.cells.Count == 0)
                continue;

            // Rule: no ceiling if height <= 0, or if room is outdoor.
            // If you have IsOutdoorRoom, use it here.
            if (room.ceilingHeight <= 0f)
                continue;

            // If you added an Outdoor flag:
            // if ((room.placementTypes & PlacementRoomTypeFlags.Outdoor) != 0)
            //     continue;

            foreach (var cell in room.cells)
            {
                if (cell == null) continue;

                float zHeight = room.ceilingHeight + ceilingZOffset;

                int idx = dir.elementStore.AddCeiling(cell, zHeight, room.colorCeiling);
                if (idx >= 0) addedCount++;
            }
        }

        // Now ask ManufactureGO to actually build the GameObjects for this layer
        dir.manufactureGO.BuildNewInstancesForLayer(ElementLayerKind.Ceiling);
        dir.manufactureGO.ApplyPendingUpdates();

        Debug.Log($"CeilingBuilder: Created {addedCount} ceiling instances via ElementStore.");
    }
}
