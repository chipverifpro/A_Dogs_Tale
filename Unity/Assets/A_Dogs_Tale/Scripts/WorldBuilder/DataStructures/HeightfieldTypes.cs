public partial class DungeonGenerator
{
    // Public "cell" shape used by the builder. Adapt to your own types below.
    public struct RoomCell
    {
        public int x, y, z;   // integer layer or discretized height
        public int roomId;    // source room id
        public int cellId;    // source cell id within room // ADDED, maybe useful later?
        public RoomCell(int x, int y, int z, int roomId, int cellId)
        { this.x = x; this.y = y; this.z = z; this.roomId = roomId; this.cellId = cellId; }
    }

    public struct NeighborMatch
    {
        public int z;             // matched z (or clamped inside segment)
        public int roomId;        // representative room id in that column/segment
        public int cellId;        // ADDED cell id
        public int segmentIndex;  // -1 if not a segment kind
        public bool isSegment;    // true when match came from a merged vertical segment
        public DirFlags walls;    // directions of detected walls
    }

    internal enum ColumnKind : byte { Empty = 0, Single = 1, SmallVec = 2, SegmentVec = 3 }

    // Configure how to treat neighbors
    public enum NeighborPolicy
    {
        // Only absence of neighbor creates a wall
        SameLevelOnly,

        // Absence OR a different room at same level creates a wall
        TreatDifferentRoomAsWall,

        // Absence OR a different "segment" (separated by > threshold in same column)
        // acts like a wall/railing; useful for mezzanines.
        TreatDifferentSegmentAsWall,
    }
}
