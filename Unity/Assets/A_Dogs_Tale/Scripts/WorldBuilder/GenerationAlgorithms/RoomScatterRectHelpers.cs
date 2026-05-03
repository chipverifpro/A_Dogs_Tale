using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator
{
    // Returns true if the proposed room bounds fit inside the world according to config flags.
    // (1) cfg.ovalRooms   - RectInt room treated as an inscribed axis-aligned ellipse when true.
    // (2) cfg.roundWorld - world bounds are an axis-aligned ellipse inscribed in the map when true;
    //                       otherwise world is the rectangular map.
    // "samples" controls boundary sampling for oval-in-oval; "margin" shrinks world a bit to avoid edge bleed.
    //
    // Works for: Rect in Rect, Oval in Rect, Rect in Oval, and Oval in Oval.
    public bool RoomFitsWorld(RectInt roomRect, int samples = 32, float margin = 0.5f)
    {
        // Quick early-reject: roomRect must at least fit inside the map rectangle.
        // works for all room shapes and map shapes.
        if (roomRect.xMin < 0 || roomRect.yMin < 0 ||
                    roomRect.xMax > cfg.mapWidth || roomRect.yMax > cfg.mapHeight)
            return false;

        // Any shape in a rectangular world:
        if (!cfg.roundWorld)
        {
            return true; // already proved it fits the map rectangle
        }

        // Round world (axis-aligned ellipse) inscribed in the map.
        Vector2 Cw = new Vector2(cfg.mapWidth * 0.5f, cfg.mapHeight * 0.5f);
        float Rx = cfg.mapWidth * 0.5f - margin;
        float Ry = cfg.mapHeight * 0.5f - margin;
        float Rx2 = Rx * Rx, Ry2 = Ry * Ry;

        float UnitCircle(Vector2 p)
        {
            float dx = p.x - Cw.x, dy = p.y - Cw.y;
            return (dx * dx) / Rx2 + (dy * dy) / Ry2; // <= 1 means inside world ellipse
        }

        if (cfg.ovalRooms) // oval room in oval world:
        {
            // Room is an axis-aligned ellipse inscribed in the rect
            Vector2 Cr = new Vector2(roomRect.xMin + roomRect.width * 0.5f,
                                    roomRect.yMin + roomRect.height * 0.5f);
            float rx = roomRect.width * 0.5f;
            float ry = roomRect.height * 0.5f;

            // Sample boundary n times, reject on any violation
            for (int i = 0; i < samples; i++)
            {
                float t = (2f * Mathf.PI * i) / samples;
                Vector2 p = Cr + new Vector2(rx * Mathf.Cos(t), ry * Mathf.Sin(t));
                if (UnitCircle(p) > 1f) return false;
            }
            return true;
        }
        else // rectangular room in oval world:
        {
            // Room is a rectangle: checking corners is exact for axis-aligned containment in an ellipse space
            float cx = roomRect.xMin + roomRect.width * 0.5f;
            float cy = roomRect.yMin + roomRect.height * 0.5f;
            float hx = roomRect.width * 0.5f;
            float hy = roomRect.height * 0.5f;

            Vector2 c1 = new Vector2(cx - hx, cy - hy);
            Vector2 c2 = new Vector2(cx + hx, cy - hy);
            Vector2 c3 = new Vector2(cx - hx, cy + hy);
            Vector2 c4 = new Vector2(cx + hx, cy + hy);

            return UnitCircle(c1) <= 1f && UnitCircle(c2) <= 1f &&
                UnitCircle(c3) <= 1f && UnitCircle(c4) <= 1f;
        }
    }

    bool IsPointInRoomRectOrOval(Vector2Int point, RectInt room_rect)
    {
        // start by checking the rectangular bounds
        bool isInRect = (point.x >= room_rect.xMin && point.x < room_rect.xMax
                        && point.y >= room_rect.yMin && point.y < room_rect.yMax);

        if (!isInRect) return false; // outside rectangular bounds for either shape
        if (cfg.ovalRooms == false) return true; // inside rectangular room bounds

        // Check if the point is within the ellipse defined by the room
        float centerX = room_rect.xMin + room_rect.width / 2f;
        float centerY = room_rect.yMin + room_rect.height / 2f;
        float radiusX = room_rect.width / 2f;
        float radiusY = room_rect.height / 2f;

        return Mathf.Pow((point.x - centerX) / radiusX, 2) + Mathf.Pow((point.y - centerY) / radiusY, 2) <= 1;
    }

    // UNCHANGED
    List<Room> ConvertAllRectToRooms(List<RectInt> room_rects, List<Color> room_rects_color, bool SetTile)
    {
        List<Vector2Int> PointsList;
        List<int> HeightsList = new();
        List<Room> rooms = new List<Room>();
        Debug.Log("Converting " + room_rects.Count + " Rects to Rooms...");
        for (int i = 0; i < room_rects.Count; i++)
        {
            var room_rect = room_rects[i];
            var room_rect_color = room_rects_color[i];
            var room_rect_height = room_rects_heights[i];
            PointsList = ConvertRectToRoomPoints(room_rect, room_rect_color, false/*SetTile*/);
            HeightsList = new();
            for (int h = 0; h < PointsList.Count; h++) HeightsList.Add(room_rect_height);
            //Debug.Log($"Room {i}: Room points = {PointsList.Count}, Room heights = {HeightsList.Count}");
            Room room = new Room(PointsList, HeightsList);
            room.isCorridor = false;
            room.name = "Room " + (rooms.Count + 1);
            room.setColorFloor(room_rect_color);
            rooms.Add(room);
            //DrawMapByRooms(rooms);
            //Debug.Log($"ConvertRectsToRooms: room {i} height = {room.cells[0].height}");
        }
        return rooms;
    }

    // UNCHANGED
    // ConvertRectToRoomPoints generates a list of points within the
    //  given room rectangle or oval.
    // As a side effect, it can also set the corresponding tiles in the tilemap.
    List<Vector2Int> ConvertRectToRoomPoints(RectInt room_rect, Color room_rect_color, bool SetTile)
    {
        //BottomBanner.LogBuildProgress($"Measuring rooms...");
        List<Vector2Int> roomPoints = new List<Vector2Int>();
        for (int x = room_rect.xMin; x < room_rect.xMax; x++)
        {
            for (int y = room_rect.yMin; y < room_rect.yMax; y++)
            {
                if (IsPointInRoomRectOrOval(new Vector2Int(x, y), room_rect))
                {
                    roomPoints.Add(new Vector2Int(x, y));
                    if (SetTile)
                    {
                        tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
                        tilemap.SetTileFlags(new Vector3Int(x, y, 0), TileFlags.None); // Allow color changes
                        tilemap.SetColor(new Vector3Int(x, y, 0), room_rect_color); // Set default color
                    }
                }
            }
        }
        return roomPoints;
    }

    // UNUSED
    public void DrawMapByRects(List<RectInt> room_rects, List<Color> colors)
    {
        for (int i = 0; i < room_rects.Count; i++)
        {
            var room_rect = room_rects[i];
            var color = colors[i];
            //("Drawing Rect " + room_rect);
            DrawRect(room_rect, color);
        }
    }

    public void DrawRect(RectInt room_rect, Color tempcolor)
    {
        //Debug.Log("Drawing Rect ");
        for (int x = room_rect.xMin; x < room_rect.xMax; x++)
        {
            for (int y = room_rect.yMin; y < room_rect.yMax; y++)
            {
                if (IsPointInRoomRectOrOval(new Vector2Int(x, y), room_rect))
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), floorTile);
                    tilemap.SetTileFlags(new Vector3Int(x, y, 0), TileFlags.None); // Allow color changes
                    tilemap.SetColor(new Vector3Int(x, y, 0), tempcolor);

                    map[x, y] = FLOOR;
                }
            }
        }
    }
}
