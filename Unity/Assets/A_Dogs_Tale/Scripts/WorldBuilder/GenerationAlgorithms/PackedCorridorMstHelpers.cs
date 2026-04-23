using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    IEnumerator ComputeMST_Yield(
        List<Vector2Int> pts,
        List<(Vector2Int a, Vector2Int b)> outEdges,
        int yieldEvery = 5000)
    {
        int n = pts.Count;
        if (n <= 1) yield break;

        var inTree = new bool[n];
        var best = new int[n];
        var parent = new int[n];

        inTree[0] = true;
        for (int j = 1; j < n; j++)
        {
            best[j] = Manhattan(pts[0], pts[j]);
            parent[j] = 0;
        }
        best[0] = int.MaxValue;
        parent[0] = -1;

        int ops = 0;
        for (int e = 0; e < n - 1; e++)
        {
            int k = -1;
            int bk = int.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (!inTree[i] && best[i] < bk) { bk = best[i]; k = i; }
                if ((++ops % yieldEvery) == 0) yield return null;
            }
            if (k == -1) break;
            inTree[k] = true;
            outEdges.Add((pts[k], pts[parent[k]]));

            for (int j = 0; j < n; j++)
            {
                if (inTree[j]) continue;
                int c = Manhattan(pts[k], pts[j]);
                if (c < best[j]) { best[j] = c; parent[j] = k; }
                if ((++ops % yieldEvery) == 0) yield return null;
            }
        }
    }

    IEnumerator CarveLineWithYield(Room tmpRoom, Vector2Int a, Vector2Int b, int width, int yieldEvery = 256)
    {
        int count = 0;
        foreach (var p in RasterizeLineSafe(a, b))
        {
            CarveDisk(ref tmpRoom, p, width);
            if ((++count % yieldEvery) == 0) yield return null;
        }
    }
}
