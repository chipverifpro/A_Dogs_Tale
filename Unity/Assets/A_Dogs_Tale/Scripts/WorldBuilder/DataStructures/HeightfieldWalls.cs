public partial class DungeonGenerator
{
    public static class HeightfieldWalls
    {
        /// <summary>
        /// Returns which sides of (x,y,z) are exposed (i.e., need walls/railings), as DirFlags.
        /// Coordinates are grid-space; z is the discretized height used by Heightfield.
        ///
        /// Conventions:
        ///   North = (x,   y+1)
        ///   South = (x,   y-1)
        ///   West  = (x-1, y  )
        ///   East  = (x+1, y  )
        /// </summary>
        /// <param name="hf">Built/Finalized Heightfield</param>
        /// <param name="x">Cell X</param>
        /// <param name="y">Cell Y</param>
        /// <param name="z">Cell height (int units consistent with hf)</param>
        /// <param name="threshold">Typically cfg.minRoomHeightInt</param>
        /// <param name="currentRoomId">
        ///   Room id of the floor at (x,y,z). If unknown, pass -1 to disable inter-room policies.
        /// </param>
        /// <param name="policy">How to treat neighbors (see NeighborPolicy)</param>
        /// <param name="treatBoundsAsWalls">
        ///   If true, map edges (out of bounds) are considered walls on that side.
        /// </param>
        public static DirFlags GetExposedDirs(
            Heightfield hf,
            int x, int y, int z,
            int threshold,
            int currentRoomId = -1,
            NeighborPolicy policy = NeighborPolicy.TreatDifferentRoomAsWall, //old default: SameLevelOnly,
            bool treatBoundsAsWalls = true)
        {
            DirFlags flags = DirFlags.None;

            // Direction vectors (dx, dy) in N, S, W, E order to match enum bit meanings above.
            var dirs = new (int dx, int dy, DirFlags bit)[] {
                (0, +1, DirFlags.N),
                (0, -1, DirFlags.S),
                (-1, 0, DirFlags.W),
                (+1, 0, DirFlags.E),
            };

            foreach (var d in dirs)
            {
                int nx = x + d.dx;
                int ny = y + d.dy;

                // Out of bounds?
                //int border = cfg.borderKeepout;
                if (nx < 0 || ny < 0 || nx >= hf.Width || ny >= hf.Height)
                {
                    if (treatBoundsAsWalls)
                    {
                        flags |= d.bit;
                        //Debug.Log($"GetExposedDirs: treatBoundsAsWalls x,y={x},{y} nx,ny={nx},{ny}");
                        //continue;
                    }
                }

                // Is there a neighbor floor near z at (nx,ny)?
                if (!hf.TryQueryAt(nx, ny, z, threshold, out var match))
                {
                    // No neighbor within threshold: exposed
                    flags |= d.bit;
                    continue;
                }

                // There IS a neighbor. Depending on policy we may still consider it "wall-worthy".
                switch (policy)
                {
                    case NeighborPolicy.SameLevelOnly:
                        // A neighbor exists within threshold => not exposed
                        break;

                    case NeighborPolicy.TreatDifferentRoomAsWall:
                        if (currentRoomId >= 0 && match.roomId >= 0 && match.roomId != currentRoomId)
                            flags |= d.bit;
                        break;

                    case NeighborPolicy.TreatDifferentSegmentAsWall:
                        // If your world can have multiple segments at the same column height range,
                        // treat crossings between distinct segments as needing a barrier/rail.
                        // Here we use segmentIndex (valid when isSegment==true). If the current
                        // column also has segments and the current z lies in a different one than
                        // neighbor, count it as exposed. We need our own segment index at (x,y,z):
                        int mySeg = SegmentIndexAt(hf, x, y, z, threshold);
                        int nbSeg = match.isSegment ? match.segmentIndex : -1;
                        if (mySeg != -2 && nbSeg != -2 && mySeg != nbSeg)
                            flags |= d.bit;
                        break;
                }
            }

            return flags;
        }

        /// <summary>
        /// Helper: returns the segment index covering (x,y) at z within threshold, or
        /// -1 if not in a segment kind, -2 if no match at all.
        /// </summary>
        private static int SegmentIndexAt(Heightfield hf, int x, int y, int z, int threshold)
        {
            if (!hf.TryQueryAt(x, y, z, threshold, out var m))
                return -2;
            return m.isSegment ? m.segmentIndex : -1;
        }
    }
}
