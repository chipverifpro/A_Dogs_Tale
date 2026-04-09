using UnityEngine;

namespace DogGame.Tasks
{
    public static class DoorIdUtility
    {
        public static int Build(Vector2Int fromCell, DirFlags direction)
        {
            Vector2Int toCell = fromCell + direction.ToVector2Int();
            Canonicalize(ref fromCell, ref toCell);

            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + fromCell.x;
                hash = (hash * 31) + fromCell.y;
                hash = (hash * 31) + toCell.x;
                hash = (hash * 31) + toCell.y;
                return hash;
            }
        }

        private static void Canonicalize(ref Vector2Int a, ref Vector2Int b)
        {
            if (a.x < b.x)
                return;

            if (a.x == b.x && a.y <= b.y)
                return;

            (a, b) = (b, a);
        }
    }
}
