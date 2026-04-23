using System.Collections.Generic;
using UnityEngine;

public static class DungeonGridUtility
{
    public static bool InBounds(int x, int y, int width, int height)
    {
        return (uint)x < (uint)width && (uint)y < (uint)height;
    }

    public static bool InBoundsWithKeepout(int x, int y, int width, int height, int keepout)
    {
        return x >= keepout
            && y >= keepout
            && x < width - keepout
            && y < height - keepout;
    }

    public static IEnumerable<Vector2Int> FourNeighbors(int x, int y, int width, int height)
    {
        if (x > 0) yield return new Vector2Int(x - 1, y);
        if (x < width - 1) yield return new Vector2Int(x + 1, y);
        if (y > 0) yield return new Vector2Int(x, y - 1);
        if (y < height - 1) yield return new Vector2Int(x, y + 1);
    }

    public static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    public static int Manhattan((int x, int y) a, (int x, int y) b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    public static Vector2Int TurnLeft(Vector2Int direction, bool left)
    {
        return left
            ? new Vector2Int(-direction.y, direction.x)
            : new Vector2Int(direction.y, -direction.x);
    }

    public static Vector2Int Perpendicular(Vector2Int direction, bool left)
    {
        return TurnLeft(direction, left);
    }

    public static Vector2Int RandomCardinal(System.Random rng)
    {
        switch (rng.Next(0, 4))
        {
            case 0: return new Vector2Int(1, 0);
            case 1: return new Vector2Int(-1, 0);
            case 2: return new Vector2Int(0, 1);
            default: return new Vector2Int(0, -1);
        }
    }

    public static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; --i)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
