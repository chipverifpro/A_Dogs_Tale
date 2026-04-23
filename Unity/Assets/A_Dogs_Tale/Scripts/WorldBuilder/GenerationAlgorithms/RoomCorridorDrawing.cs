using System;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    public Room DrawCorridorSloped(Vector2Int start, Vector2Int end, int start_height, int end_height, int start_room, int end_room)
    {
        List<Vector2Int> path;
        HashSet<Vector2Int> hashPath = new();
        HashSet<Vector2Int> neighbor_start_hashPath = new();
        HashSet<Vector2Int> neighbor_end_hashPath = new();
        Room room = new();

        switch (cfg.TunnelsAlgorithm)
        {
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsOrthogonal:
                BottomBanner.Show("Drawing orthogonal ..");
                path = OrthogonalLine(start, end);
                break;
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsStraight:
                BottomBanner.Show("Drawing straight ..");
                path = BresenhamLine(start, end);
                break;
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsOrganic:
                BottomBanner.Show("Drawing organic ..");
                path = OrganicLine(start, end);
                break;
            case DungeonSettings.TunnelsAlgorithm_e.TunnelsCurved:
                BottomBanner.Show("Drawing curved ..");
                path = BezierLine(start, end);
                break;
            default:
                BottomBanner.Show("Drawing Noisy Bresenham ..");
                path = NoisyBresenhamLine(start, end);
                break;
        }

        int path_length = path.Count;
        if (path_length <= 1)
        {
            //Debug.Log($"path_length = {path_length}, must be > 1");
            //TODO: make a vertical ladder?
            return (room); // empty room
        }
        float delta_h = (float)(end_height - start_height) / (float)(path_length - 1);
        // pre-seed the hashPath with both end rooms so we don't add corridor tiles there.

        foreach (Cell cell in rooms[start_room].cells)
            neighbor_start_hashPath.Add(cell.pos);
        foreach (Cell cell in rooms[end_room].cells)
            neighbor_end_hashPath.Add(cell.pos);

        if (cfg.limit_slope && (Math.Abs(delta_h) > 1f))
        {
            Debug.Log($"Slope of corridor is too great Abs({delta_h}) > 1");
            delta_h = Math.Clamp(delta_h, -1f, 1f); // Don't allow ramps too steep to climb.
            // Should we generate a new corridor that is longer? TODO
        }

        //Debug.Log("Drawing corridor length " + path.Count + " from " + start + " to " + end + " width " + cfg.corridorWidth + " using " + cfg.TunnelsAlgorithm);
        //Debug.Log("Corridor: start_height=" + start_height + " end_height=" + end_height + " length=" + path_length);
        int brush_neg = -cfg.corridorWidth / 2;
        int brush_pos = brush_neg + cfg.corridorWidth;

        //foreach (Vector2Int point in path)
        for (int i = 0; i < path.Count; i++)
        {
            Vector2Int point = path[i];
            int height = start_height + (int)Math.Round(i * delta_h);
            // Square brush around each line point
            for (int dx = brush_neg; dx < brush_pos; dx++)
            {
                for (int dy = brush_neg; dy < brush_pos; dy++)
                {
                    // At both endpoints, only use centerpoint of brush.
                    // Going beyond this can make odd transitions.
                    if ((i == 0 || i == path.Count - 1) && ((dx != 0) || (dy != 0)))
                        continue;

                    Vector3Int tilePos3d = new Vector3Int(point.x + dx, point.y + dy, 0);
                    if (tilePos3d.x < 0 || tilePos3d.x >= cfg.mapWidth || tilePos3d.y < 0 || tilePos3d.y >= cfg.mapHeight)
                    {
                        continue; // Skip out-of-bounds tiles
                    }
                    tilemap.SetTile(tilePos3d, floorTile);

                    Vector2Int tilePos2 = new Vector2Int(tilePos3d.x, tilePos3d.y);

                    // Keep highest point
                    //bool overlap = false;
                    int neighborheight;
                    height = CalculateRampHeightFromPosition(tilePos2, start, end, start_height, end_height);

                    if (!hashPath.Contains(tilePos2))
                    {
                        // check if neighbor overlap.  If so, remove from neighbor.
                        if (neighbor_start_hashPath.Contains(tilePos2))
                        {
                            //overlap = true;
                            neighborheight = rooms[start_room].GetHeightInRoom(tilePos2);
                            if (((neighborheight - height) > 0) && ((neighborheight - height) < 30))
                            {
                                // punch hole in ceiling of start_room
                                int cell_num = rooms[start_room].GetCellInRoom(tilePos2);
                                rooms[start_room].cells.RemoveAt(cell_num);
                                rooms[start_room].ResetCellDictionary();
                            }
                        }
                        if (neighbor_end_hashPath.Contains(tilePos2))
                        {
                            //overlap = true;
                            neighborheight = rooms[end_room].GetHeightInRoom(tilePos2);
                            if (((neighborheight - height) > 0) && ((neighborheight - height) < 30))
                            {
                                // punch hole in ceiling of end_room
                                int cell_num = rooms[end_room].GetCellInRoom(tilePos2);
                                rooms[end_room].cells.RemoveAt(cell_num);
                                rooms[end_room].ResetCellDictionary();
                            }
                        }
                    }
                    // Add the corridor cell
                    room.cells.Add(new Cell(tilePos2.x, tilePos2.y, height));
                }
            }
        }
        room.isCorridor = true;
        return room;
    }

    // based on distances from both ends, calculate height.
    // problem: past the ends, height goes down.  TODO: better algorithm.
    int CalculateRampHeightFromPosition(Vector2Int target, Vector2Int start, Vector2Int end, int start_height, int end_height)
    {
        float target_to_start;
        float target_to_end;
        float pct_distance;

        Vector2Int delta;
        float target_height;

        delta = (target - start);
        target_to_start = (float)Math.Sqrt(delta.sqrMagnitude);
        delta = (target - end);
        target_to_end = (float)Math.Sqrt(delta.sqrMagnitude);
        pct_distance = target_to_start / (target_to_start + target_to_end);

        target_height = (end_height - start_height) * pct_distance + start_height;
        return (int)Math.Round(target_height);
    }
}
