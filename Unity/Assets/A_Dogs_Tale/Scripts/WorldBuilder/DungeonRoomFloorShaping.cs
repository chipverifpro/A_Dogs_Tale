using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    /// <summary>
    /// Compute tilt Euler (pitch=x, yaw=y(=0), roll=z) with robust handling of missing neighbors.
    /// Pass null for any neighbor that doesn't exist.
    /// h* are in grid height units; heightUnit converts to world units.
    /// edgeTiltScale in [0..1]: 1 = full one-sided tilt, 0 = flatten at edges.
    /// </summary>
    public static Quaternion ComputeTiltTile(
        float hCenter,
        float? hNorth, float? hEast, float? hSouth, float? hWest,
        float tileSizeX, float tileSizeZ,
        float heightUnit = 1f,
        float maxAbsAngleDeg = 75f,
        float edgeTiltScale = 0.8f, // soften edge tilts slightly
        float baseYawDeg = 0f
    )
    {
        float dx = Mathf.Max(1e-6f, tileSizeX);
        float dz = Mathf.Max(1e-6f, tileSizeZ);

        // --- slope along X (east-west) ---
        float gx;
        bool hasE = hEast.HasValue, hasW = hWest.HasValue;
        if (hasE && hasW)
        {
            gx = ((hEast.Value - hWest.Value) * heightUnit) / (2f * dx);
        }
        else if (hasE)
        {
            gx = ((hEast.Value - hCenter) * heightUnit) / dx;
            gx *= edgeTiltScale;
        }
        else if (hasW)
        {
            gx = ((hCenter - hWest.Value) * heightUnit) / dx;
            gx *= edgeTiltScale;
        }
        else
        {
            gx = 0f;
        }

        // --- slope along Z (north-south) ---
        float gz;
        bool hasN = hNorth.HasValue, hasS = hSouth.HasValue;
        if (hasN && hasS)
        {
            gz = ((hNorth.Value - hSouth.Value) * heightUnit) / (2f * dz);
        }
        else if (hasN)
        {
            gz = ((hNorth.Value - hCenter) * heightUnit) / dz;
            gz *= edgeTiltScale;
        }
        else if (hasS)
        {
            gz = ((hCenter - hSouth.Value) * heightUnit) / dz;
            gz *= edgeTiltScale;
        }
        else
        {
            gz = 0f;
        }

        // Convert slopes to angles
        float pitchDeg = Mathf.Rad2Deg * Mathf.Atan(gz);   // tilt around X toward +Z when gz>0
        float rollDeg = -Mathf.Rad2Deg * Mathf.Atan(gx);  // tilt around Z toward +X when gx>0

        // Clamp extremes for stability.  leaves cliffs looking funky
        //pitchDeg = Mathf.Clamp(pitchDeg, -maxAbsAngleDeg, maxAbsAngleDeg);
        //rollDeg = Mathf.Clamp(rollDeg, -maxAbsAngleDeg, maxAbsAngleDeg);

        // if extreme, just flatten.  better for cliffs.
        if (Mathf.Abs(pitchDeg) > maxAbsAngleDeg) pitchDeg = 0f;
        if (Mathf.Abs(rollDeg) > maxAbsAngleDeg) rollDeg = 0f;

        var e = Quaternion.Euler(pitchDeg, baseYawDeg, rollDeg);
        return e;
    }

}
