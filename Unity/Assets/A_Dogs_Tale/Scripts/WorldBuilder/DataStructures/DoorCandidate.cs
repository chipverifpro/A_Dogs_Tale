public class DoorCandidate
{
    public int x, y;            // room cell location (the near side anchor)
    public DirFlags dir;        // direction the door faces from (x,y)
    public int span;            // number of empty cells to punch through (0..moat)
    public bool toCorridor;     // true if target is corridor
    public int targetRoomId;    // if !toCorridor, room id on the far side
    public int roomId;          // source room id
    public int score;           // lower is better for connectivity
    public bool placed;         // shows if this was successfully placed
    public Cell cellA;
    public Cell cellB;
}
