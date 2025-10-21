using System;
using System.Collections.Generic;
using UnityEngine;

/* ChatGPT developed this interesting class for caching neighbor offsets in a grid.
   It supports different shapes (square, circle, diamond) and allows for various configurations
   such as radius, border-only, and diagonal inclusion.
   I'm putting it here for possible future use or reference.
*/
// now used in RemoveTinyRooms and RemoveTinyRocks

public static class NeighborCache
{
    public enum Shape
    {
        Square,   // Chebyshev (when includeDiagonals = true) or axis-only cross (when false)
        Circle,   // Euclidean
        Diamond   // Manhattan
    }

    // Cache key
    private readonly struct Key : IEquatable<Key>
    {
        public readonly int Radius;
        public readonly Shape Pattern;
        public readonly bool BorderOnly;
        public readonly bool IncludeDiagonals; // only meaningful for Square

        public Key(int radius, Shape pattern, bool borderOnly, bool includeDiagonals)
        {
            Radius = radius;
            Pattern = pattern;
            BorderOnly = borderOnly;
            IncludeDiagonals = includeDiagonals;
        }

        public bool Equals(Key other) =>
            Radius == other.Radius &&
            Pattern == other.Pattern &&
            BorderOnly == other.BorderOnly &&
            IncludeDiagonals == other.IncludeDiagonals;

        public override bool Equals(object obj) => obj is Key k && Equals(k);
        public override int GetHashCode() => HashCode.Combine(Radius, (int)Pattern, BorderOnly, IncludeDiagonals);
    }

    // Cache exposes read-only views; we do not expose the underlying arrays
    private static readonly Dictionary<Key, IReadOnlyList<Vector3Int>> Cache = new();

    /// <summary>
    /// Get a precomputed, read-only list of neighbor offsets.
    /// Excludes (0,0,0). For Shape.Square, includeDiagonals controls 8-neighborhood vs axis-only.
    /// </summary>
    public static IReadOnlyList<Vector3Int> Get(
        int radius,
        Shape shape,
        bool borderOnly = false,
        bool includeDiagonals = true // only used for Square
    )
    {
        if (radius < 1) throw new ArgumentOutOfRangeException(nameof(radius), "radius must be >= 1");

        var key = new Key(radius, shape, borderOnly, includeDiagonals);
        if (Cache.TryGetValue(key, out var ro)) return ro;

        // Build underlying array once
        var list = new List<Vector3Int>(EstimateCount(radius, shape, borderOnly, includeDiagonals));

        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dy == 0) continue; // skip center

                bool include = shape switch
                {
                    Shape.Square => InSquare(dx, dy, radius, borderOnly, includeDiagonals),
                    Shape.Circle => InCircle(dx, dy, radius, borderOnly),
                    Shape.Diamond => InDiamond(dx, dy, radius, borderOnly),
                    _ => false
                };

                if (include) list.Add(new Vector3Int(dx, dy, 0));
            }

        // Wrap in a read-only collection so callers cannot modify
        var arr = list.ToArray();
        var readOnly = Array.AsReadOnly(arr);
        Cache[key] = readOnly;
        return readOnly;
    }

    // -------- inclusion predicates --------

    // Square:
    // - includeDiagonals = true  => Chebyshev distance (classic filled square or ring)
    // - includeDiagonals = false => axis-only (a cross): dx==0 xor dy==0, within radius (or exactly radius for border)
    private static bool InSquare(int dx, int dy, int r, bool borderOnly, bool includeDiagonals)
    {
        if (includeDiagonals)
        {
            int cheb = Math.Max(Math.Abs(dx), Math.Abs(dy));
            return borderOnly ? cheb == r : cheb <= r;
        }
        else
        {
            bool axisOnly = (dx == 0) ^ (dy == 0); // exactly one axis non-zero
            if (!axisOnly) return false;

            int dist = Math.Abs(dx) + Math.Abs(dy); // reduces to max(|dx|,|dy|) since one is zero
            return borderOnly ? dist == r : dist <= r;
        }
    }

    private static bool InCircle(int dx, int dy, int r, bool borderOnly)
    {
        int d2 = dx * dx + dy * dy;
        int r2 = r * r;
        if (borderOnly)
        {
            // Thin ring around radius with small tolerance (half-tile feel).
            const float tol = 0.25f;
            return d2 >= (r2 - tol) && d2 <= (r2 + tol);
        }
        return d2 <= r2;
    }

    private static bool InDiamond(int dx, int dy, int r, bool borderOnly)
    {
        int man = Math.Abs(dx) + Math.Abs(dy);
        return borderOnly ? man == r : man <= r;
    }

    // -------- sizing hint --------

    private static int EstimateCount(int r, Shape s, bool borderOnly, bool includeDiagonals)
    {
        return s switch
        {
            Shape.Square when includeDiagonals => borderOnly
                ? (r == 0 ? 0 : 8 * r) // perimeter of an r-square ring
                : (2 * r + 1) * (2 * r + 1) - 1,

            Shape.Square when !includeDiagonals => borderOnly
                ? 4 * r // four axis tips at distance r
                : 4 * r, // axis-only filled cross has 4 per layer; sum_{k=1..r} 4 = 4r

            Shape.Diamond => borderOnly
                ? 4 * r
                : 2 * r * (r + 1),

            Shape.Circle => borderOnly
                ? (int)(2 * Math.PI * r)
                : (int)(Math.PI * r * r),

            _ => 8
        };
    }
}

/* Usage notes:

var origin = new Vector3Int(10, 10, 0);

// Classic 8-neighborhood (radius 1, square WITH diagonals)
var n8 = NeighborCache.Get(1, NeighborCache.Shape.Square, borderOnly: false, includeDiagonals: true);

// Axis-only cross within radius 2 (no diagonals)
var crossR2 = NeighborCache.Get(2, NeighborCache.Shape.Square, borderOnly: false, includeDiagonals: false);

// Chebyshev ring at radius 3 (square border, with diagonals)
var squareRingR3 = NeighborCache.Get(3, NeighborCache.Shape.Square, borderOnly: true, includeDiagonals: true);

// Circle radius 3 ring
var circleRingR3 = NeighborCache.Get(3, NeighborCache.Shape.Circle, borderOnly: true);

// Diamond (Manhattan) filled radius 2
var diamondR2 = NeighborCache.Get(2, NeighborCache.Shape.Diamond);

foreach (var offset in n8)
{
    var neighbor = origin + offset; 
    // process...
}
*/