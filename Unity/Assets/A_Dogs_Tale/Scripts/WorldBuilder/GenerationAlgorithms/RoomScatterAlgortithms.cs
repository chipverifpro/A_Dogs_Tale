using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ---------------------- Room Scatter Algorithms ----------------
// ScatterRoom handles either rectangular or oval rooms that are randomly scattered.
// They can be configured to overlap or not.  Overlaps can either merge into an odd
// shaped room, or have different elevations allowing one above the other, or if
// vertical distance is small enough, they will have an elevation between them (cliff
// or steps).
//
// Future: COMPACT: rooms will all touch each other in a contiguous area.
//         They may have thin or thick walls.
// Future: Rooms may have functional purposes and have specific connection rules,
//         like Tavern which has a common area, a service area, and a private area,
//         each consisting of several named rooms.
//
// -------------------- room rect functions -----------------------------
// This file also contains a bunch of routines that deal with room_rects.
public partial class DungeonGenerator : MonoBehaviour
{
    // rooms stored as rectangles, later converted into standard Room format.
    public List<RectInt> room_rects = new(); // List of RectInt rooms for ScatterRooms
    public List<int> room_rects_heights = new(); // List of heights for each room rectangle
    public List<Color> room_rects_color = new(); // Color assigned to a room's floor

    // Scatter rooms performs the main room placement for Rectangular or Oval rooms
    public IEnumerator ScatterRooms(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("ScatterRooms"); local_tm = true; }
        try
        {
            tilemap.ClearAllTiles();
            room_rects.Clear(); // Clear the list of room rectangles
            room_rects_color.Clear(); // Clear the list of colors for room rectangles
            room_rects_heights.Clear(); // Clear heights
            RectInt newRoom = new();
            //rooms.Clear();
            BottomBanner.Show($"Scattering {cfg.roomsMax} Rooms...");
            for (int i = 0; room_rects.Count < cfg.roomsMax && i < cfg.roomAttempts; i++)
            {
                bool fits = false;
                while (fits == false)
                {
                    int w = UnityEngine.Random.Range(cfg.minRoomSize, cfg.maxRoomSize + 1);
                    int h = UnityEngine.Random.Range(cfg.minRoomSize, cfg.maxRoomSize + 1);
                    int x = UnityEngine.Random.Range(1, cfg.mapWidth - w - 1);
                    int y = UnityEngine.Random.Range(1, cfg.mapHeight - h - 1);
                    newRoom = new(x, y, w, h);
                    fits = RoomFitsWorld(newRoom, 32, 0.5f);
                }

                // Check if the new room overlaps with existing rooms
                bool overlaps = false;
                foreach (var r in room_rects)
                {
                    RectInt big_r = new(r.xMin - 1, r.yMin - 1, r.width + 2, r.height + 2);
                    if (newRoom.Overlaps(big_r))
                    {
                        overlaps = true;
                    }
                }

                if (!overlaps || cfg.generateOverlappingRooms)
                {
                    var newColor = UnityEngine.Random.ColorHSV(0f, 1f, 0.6f, 1f, 0.6f, 1f);
                    room_rects.Add(newRoom);
                    room_rects_color.Add(newColor);
                    room_rects_heights.Add(UnityEngine.Random.Range(0, cfg.maxElevation));
                    DrawRect(newRoom, newColor);
                    Debug.Log("Created " + room_rects.Count + " room_rects");
                    yield return tm.YieldOrDelay(cfg.stepDelay / 3);
                }
            }
            Debug.Log("room_rects.Count = " + room_rects.Count);
            yield return tm.YieldOrDelay(cfg.stepDelay);
        }
        finally { if (local_tm) tm.End(); }
    }
}
