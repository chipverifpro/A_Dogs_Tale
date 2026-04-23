using System.Collections.Generic;

public partial class DungeonGenerator
{
    public void ScatterSceneryOnFloors()
    {
        // After your rooms/floors are built:
        List<Cell> floorCells = new();
        //int numObjects = 50; // or however many you want
        foreach (var room in rooms)
        {
            floorCells.AddRange(room.cells);
        }

        //sceneryScatterer.ScatterScenery(floorCells);
    }
}
