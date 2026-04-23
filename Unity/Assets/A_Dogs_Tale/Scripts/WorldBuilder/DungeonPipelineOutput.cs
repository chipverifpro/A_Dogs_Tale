using System.Collections;
using UnityEngine;

public partial class DungeonGenerator
{
    private IEnumerator RebuildWallLists()
    {
        BottomBanner.Show("Building Wall Lists...");
        yield return null;
        DrawMapByRooms(rooms);  // update the 2D map before finishing.
        DrawWalls();

        yield return StartCoroutine(BuildWallsAroundFloorsInRooms(tm: null));
        DrawMapByRooms(rooms);  // update the 2D map before finishing.
        DrawWalls();
    }

    private IEnumerator ApplyOptionalFloorTileTilt()
    {
        // Optionally tilt individual floor tiles
        if (!cfg.enableTiltedTiles || cfg.tiltFloorTilesMaxAngle == 0)
            yield break;

        BottomBanner.Show("Calculating Floor Tilts...");
        yield return new WaitForSeconds(.2f);
        // Build the heightfield hf if it doesn't exist yet
        //if (hf == null) PrepareHeightfield();
        yield return StartCoroutine(TiltAllFloors(tm: null));
    }

    private IEnumerator BuildFinalDungeonOutput()
    {
        // Scatter scenery props on floor tiles
        BottomBanner.Show("Scattering Scenery...");
        ScatterSceneryOnFloors();

        BottomBanner.Show("Height Map Build...");
        yield return null;
        DrawMapByRooms(rooms);  // update the 2D map before finishing.
        DrawWalls();
        yield return StartCoroutine(Build3DFromRooms(tm: null));

        DrawMapByRooms(rooms);  // update the 2D map before finishing.
        DrawWalls();

        UpdateCellGridFromRooms(rooms);  // update the master cellGrid from the rooms list
        // Build the heightfield hf if it doesn't exist yet
        if (hf == null) PrepareHeightfield();
    }
}
