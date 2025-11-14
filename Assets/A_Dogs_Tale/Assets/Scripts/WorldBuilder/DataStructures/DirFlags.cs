using UnityEngine;
using System.Collections.Generic;
using System;

[Flags]
public enum DirFlags : byte
{
    None = 0,
    N = 1 << 0,
    E = 1 << 1,
    S = 1 << 2,
    W = 1 << 3
}

public static class DirFlagsEx  // extension functions for the DirFlags enum
{
    // ---- Private cached arrays (no per-call allocations) ----
    private static readonly DirFlags[] kCardinals = { DirFlags.N, DirFlags.E, DirFlags.S, DirFlags.W };
    private static readonly DirFlags[] kDiagonals = { DirFlags.N | DirFlags.E,
                                                      DirFlags.S | DirFlags.E,
                                                      DirFlags.S | DirFlags.W,
                                                      DirFlags.N | DirFlags.W };
    private static readonly DirFlags[] kAll8 = { DirFlags.N,
                                                 DirFlags.N | DirFlags.E,
                                                 DirFlags.E,
                                                 DirFlags.S | DirFlags.E,
                                                 DirFlags.S,
                                                 DirFlags.S | DirFlags.W,
                                                 DirFlags.W,
                                                 DirFlags.N | DirFlags.W };

    public static IReadOnlyList<DirFlags> AllCardinals => kCardinals;
    public static IReadOnlyList<DirFlags> AllDiagonals => kDiagonals;
    public static IReadOnlyList<DirFlags> All8 => kAll8;

    // ---- Classification ----
    public static bool IsCardinal(this DirFlags dir)
        => Count(dir) == 1;

    public static bool IsDiagonal(this DirFlags dir)
        => ((dir & (DirFlags.N | DirFlags.S)) != 0) && ((dir & (DirFlags.E | DirFlags.W))!= 0)
        && Count(dir) == 2;

    public static DirFlags Opposite(this DirFlags dir)
    {
        Vector2Int vect;
        vect = ToVector2Int(dir);
        return FromVector2Int(-vect);
    }

    // ---- Conversions ----
    public static Vector2Int ToVector2Int(this DirFlags dir)
    {
        int x = 0;
        int y = 0;

        if ((dir & DirFlags.E) != 0) x += 1;
        if ((dir & DirFlags.W) != 0) x -= 1;
        if ((dir & DirFlags.N) != 0) y += 1;
        if ((dir & DirFlags.S) != 0) y -= 1;

        return new Vector2Int(x, y);
    }

    public static DirFlags FromVector2Int(Vector2Int v)
    {
        DirFlags flags = DirFlags.None;

        if (v.y > 0) flags |= DirFlags.N;
        if (v.y < 0) flags |= DirFlags.S;
        if (v.x > 0) flags |= DirFlags.E;
        if (v.x < 0) flags |= DirFlags.W;

        return flags;
    }

    //CountBits: This uses Brian Kernighan’s algorithm (v &= v-1) to strip one bit
    //           per loop → very fast for small bitfields like a byte.
    public static int Count(this DirFlags dir)
    {
        byte v = (byte)dir;
        int count = 0;
        while (v != 0)
        {
            v &= (byte)(v - 1); // clear the lowest set bit
            count++;
        }
        return count;
    }
}

/* Examples of things to do with DirFlags and DirFlagsEx:
--------------------------------------------- Example 1
DirFlags dir = DirFlags.N | DirFlags.E;

if ((dir & DirFlags.NE) == DirFlags.NE)
    Debug.Log("Exactly Northeast!");

if (dir.HasFlag(DirFlags.N))
    Debug.Log("Includes North");

-------------------------------------------- Example 2
DirFlags dir = DirFlags.NE;

if (dir.IsDiagonal())
    Debug.Log("Going diagonal!");

Vector2Int step = dir.ToVector2Int();
Debug.Log($"Step = {step}"); // (1,1)

DirFlags back = dir.Opposite();
Debug.Log($"Opposite of {dir} = {back}");

-------------------------------------------- Example 3
Vector2Int v = new Vector2Int(-1, 1);
DirFlags dir = DirFlagsEx.FromVector2Int(v);   // <- uses Ex

Debug.Log(dir);  // outputs "NW"

-------------------------------------------- Example 4
// Loop 8 directions (N,E,S,W,NE,SE,SW,NW)
Vector2Int here = new Vector2Int(10,2);
Vector2Int neighbor;

foreach (var dir in DirFlagsEx.All8())      // <- uses Ex
{
    Vector2Int neighbor = here + dir.ToVector2Int();
    // Check neighbors in map
}

-------------------------------------------- Example 5
DirFlags d = DirFlags.N | DirFlags.E | DirFlags.S;

int bits = d.CountBits();

Debug.Log($"{d} has {bits} bits set."); 
// Output: "N, E, S has 3 bits set."

*/

