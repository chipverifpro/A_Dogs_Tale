using System.Collections.Generic;

public partial class DungeonGenerator
{
    public sealed partial class Heightfield
    {
        /// <summary>
        /// Build a heightfield directly from a list/array of cells. You can also call Insert() yourself and then FinalizeColumns().
        /// </summary>
        public static Heightfield BuildFromCells(IEnumerable<RoomCell> cells, int width, int height, int minRoomHeight)
        {
            var hf = new Heightfield(width, height);
            foreach (var c in cells)
            {
                if (c.x < 0 || c.y < 0 || c.x >= hf.Width || c.y >= hf.Height) continue;
                hf.Insert(c.x, c.y, c.z, c.roomId, c.cellId);
            }
            hf.FinalizeColumns(minRoomHeight);
            return hf;
        }
    }
}
