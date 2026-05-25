using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    // ROOM READY
    // score depends on the length-to-height ratio modified by the targetAsp parameter,
    //                  the cooldown penalty for that direction,
    //                  a preference for the short axis.
    // Return sides in best-first order: 0:E,1:W,2:N,3:S
    List<int> ScoreSidesForStrip(int ri, RectInt bb, float targetAsp, float currentAsp, int[] cd)
    {
        int w = Mathf.Max(1, bb.width), h = Mathf.Max(1, bb.height);
        bool preferShortAxis = (w > h * targetAsp); // true => grow N/S; false => E/W preferred if h > w*targetAsp

        var list = new List<(int side, int score)>(4);
        int baseScoreE = (h); // E/W adds a column of 'h' cells
        int baseScoreW = (h);
        int baseScoreN = (w); // N/S adds a row of 'w' cells
        int baseScoreS = (w);

        int cooldownPenalty(int side) => (cd[side] > 0) ? (cd[side] * 1000) : 0;

        // start with base gain
        int sE = baseScoreE - cooldownPenalty(0);
        int sW = baseScoreW - cooldownPenalty(1);
        int sN = baseScoreN - cooldownPenalty(2);
        int sS = baseScoreS - cooldownPenalty(3);

        // compactness bias: push short axis first
        if (preferShortAxis) { sN += 10; sS += 10; }
        else { sE += 10; sW += 10; }

        list.Add((0, sE)); list.Add((1, sW)); list.Add((2, sN)); list.Add((3, sS));
        list.Sort((a, b) => b.score.CompareTo(a.score));
        var order = new List<int>(4) { list[0].side, list[1].side, list[2].side, list[3].side };
        return order;
    }

    // ROOM READY
    // Try to grow a full 1-cell strip on the chosen side.
    // Returns true if the whole strip was claimed.
    // side: 0=E (x=max+1), 1=W (x=min-1), 2=N (y=max+1), 3=S (y=min-1)
    bool TryGrowFullStrip(int ri, ref RectInt bounds, int side, int moatCells)
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int minx = bounds.xMin, maxx = bounds.xMax - 1;
        int miny = bounds.yMin, maxy = bounds.yMax - 1;

        if (side == 0) // E
        {
            int x = maxx + 1;
            //if ((uint)x >= (uint)W) return false;  // bounds checking in CanClaim
            for (int y = miny; y <= maxy; y++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int y = miny; y <= maxy; y++) ClaimCell(ri, x, y);
            bounds.width += 1;
            return true;
        }
        if (side == 1) // W
        {
            int x = minx - 1;
            //if (x < 0) return false;  // bounds checking in CanClaim
            for (int y = miny; y <= maxy; y++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int y = miny; y <= maxy; y++) ClaimCell(ri, x, y);
            bounds.x -= 1; bounds.width += 1;
            return true;
        }
        if (side == 2) // N
        {
            int y = maxy + 1;
            //if ((uint)y >= (uint)H) return false;  // bounds checking in CanClaim
            for (int x = minx; x <= maxx; x++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int x = minx; x <= maxx; x++) ClaimCell(ri, x, y);
            bounds.height += 1;
            return true;
        }
        else // 3:S
        {
            int y = miny - 1;
            //if (y < 0) return false;  // bounds checking in CanClaim
            for (int x = minx; x <= maxx; x++)
                if (!CanClaim(ri, x, y, moatCells)) return false;

            for (int x = minx; x <= maxx; x++) ClaimCell(ri, x, y);
            bounds.y -= 1; bounds.height += 1;
            return true;
        }
    }
}
