using System;
using System.Collections;
using UnityEngine;

public partial class DungeonGenerator
{
    // ------------ Floor tile tilting functions ------------
    public IEnumerator TiltAllFloors(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("TiltAllFloors"); local_tm = true; }
        try
        {
            if (hf == null) yield break;
            if (rooms == null) yield break;


            // Validate config once
            float tileSizeX = Mathf.Max(1e-4f, 1f);           // <- replace 1f if you use different tile size
            float tileSizeZ = Mathf.Max(1e-4f, 1f);
            float heightUnit = Mathf.Max(1e-6f, cfg.unitHeight);
            float maxAngle = Mathf.Clamp(cfg.tiltFloorTilesMaxAngle, 0f, 85f);
            int threshold = Mathf.Max(0, 100);  // make sure you have this int version

            int yieldCounter = 0;


            foreach (var room in rooms)
            {
                if (room?.cells == null) continue;

                foreach (var cell in room.cells)
                {
                    try
                    {
                        // Use the same height unit for sampling & tilt
                        int heightCenter = cell.z;

                        // Safe neighbor sampling: return null if no neighbor within threshold
                        int? hN = TrySampleNeighborHeight(cell.x, cell.y + 1, heightCenter, threshold, out var zn) ? (zn) : (int?)null;
                        int? hE = TrySampleNeighborHeight(cell.x + 1, cell.y, heightCenter, threshold, out var ze) ? (ze) : (int?)null;
                        int? hS = TrySampleNeighborHeight(cell.x, cell.y - 1, heightCenter, threshold, out var zs) ? (zs) : (int?)null;
                        int? hW = TrySampleNeighborHeight(cell.x - 1, cell.y, heightCenter, threshold, out var zw) ? (zw) : (int?)null;

                        // Compute rotation (handles missing neighbors + edge softening)
                        Quaternion rot = ComputeTiltTile(
                            hCenter: heightCenter,
                            hNorth: hN, hEast: hE, hSouth: hS, hWest: hW,
                            tileSizeX: tileSizeX, tileSizeZ: tileSizeZ,
                            heightUnit: heightUnit,
                            baseYawDeg: 180f,
                            maxAbsAngleDeg: maxAngle,
                            edgeTiltScale: cfg.edgeTiltScale
                        );

                        // Guard against NaN/Inf (Unity can crash if these hit transforms)
                        if (IsBadRotation(rot))
                        {
                            rot = Quaternion.identity; // fallback
                        }

                        // Cache it on your cell (adjust field names/types as needed)
                        cell.tiltFloor = rot;
                    }
                    catch (Exception ex)
                    {
                        // Keep going; log once per problematic cell
                        Debug.LogWarning($"TiltAllFloors: exception at cell ({cell.x},{cell.y}) h={cell.height}: {ex.Message}");
                        cell.tiltFloor = Quaternion.identity;
                    }

                    // Cooperative yield
                    if ((yieldCounter++ & 0xFF) == 0) // every 256 cells
                    {
                        if (tm.IfYield()) yield return null;
                    }
                }

                if (tm.IfYield()) yield return null;
            }
        }
        finally
        {
            if (local_tm && tm != null) tm.End();
        }
    }

    // ------------ Data validity checkers for above function ------------

    private bool TrySampleNeighborHeight(int x, int y, int zCenter, int threshold, out int zNeighbor)
    {
        zNeighbor = 0;
        if (x < 0 || y < 0 || x >= hf.Width || y >= hf.Height) return false;

        // Use heightCenter (zCenter) so units match your heightfield
        if (!hf.TryQueryAt(x, y, zCenter, threshold, out var match))
            return false;

        zNeighbor = match.z;
        return true;
    }

    private static bool IsBadRotation(Quaternion q)
    {
        // Reject NaN or Inf
        return float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w) ||
            float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z) || float.IsInfinity(q.w) ||
            (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f); // invalid quaternion
    }
}
