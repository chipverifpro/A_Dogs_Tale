using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public partial class DungeonGenerator : MonoBehaviour
{

    // Cellular Automata Algorithm:
    // 1. Fill a 2D map with random or noisy structured data (perlin noise).
    // 1a. Threshold the 2D map into only floors and walls
    // 2. Run the game of life on the cells using rules about survivability based on number of neighbors,
    //    through several iterations (smoothes out areas, gives structure to noise)
    // 3. Clean up result (remove tiny areas (rooms or rocks), replace solid wall fields with void.
    // 4. Convert result into Rooms.

    //TODO: modify floor height of cells with a gentle perlin function for a natural rough feeling floor

    public IEnumerator RunCellularAutomation(TimeTask tm = null)
    {
        bool local_tm = false;
        if (tm == null) { tm = TimeManager.Instance.BeginTask("RunCellularAutomation"); local_tm = true; }
        try
        {
            map = new byte[cfg.mapWidth, cfg.mapHeight];
            RandomFillMap(map);

            // Draw initial map
            DrawMapFromByteArray();
            yield return tm.YieldOrDelay(cfg.stepDelay);

            for (int step = 0; step < cfg.CellularGrowthSteps; step++)
            {
                map = RunSimulationStep(map);
                DrawMapFromByteArray();
                yield return tm.YieldOrDelay(cfg.stepDelay);
            }
        }
        finally { if (local_tm) tm.End(); }
    }

    // RandomFillMap with either white noise or Perlin-based 'noise'.
    //   For perlin, two different wavelengths are weighted and averaged to get
    //   (low wavelength) odd shaped rooms + (higher wavelength) bumps and rocks especially around room borders
    public byte[,] RandomFillMap(byte[,] map)
    {
        rng = new System.Random();

        float seedX = UnityEngine.Random.Range(0f, 10000f);
        float seedY = UnityEngine.Random.Range(0f, 10000f);
        for (int x = 0; x < cfg.mapWidth; x++)
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                if (!IsPointInWorld(new Vector2Int(x, y)))
                {
                    map[x, y] = WALL;
                    continue;
                }
                int borderDistance = Mathf.Min(x, y, cfg.mapWidth - x - 1, cfg.mapHeight - y - 1);
                if (borderDistance == 1)
                    map[x, y] = WALL; // Set hard border tile to wall
                else if (borderDistance <= cfg.softBorderSize)
                    // Setting a wide random border makes square world edges less sharp
                    map[x, y] = rng.Next(0, 100) < cfg.cellularFillPercent ? WALL : FLOOR;
                else
                    if (cfg.usePerlin)
                {
                    float perlin1 = Mathf.PerlinNoise((x + seedX) * cfg.perlinWavelength, (y + seedY) * cfg.perlinWavelength);
                    float perlin2 = Mathf.PerlinNoise((x - seedX) * cfg.perlin2Wavelength, (y - seedY) * cfg.perlin2Wavelength);
                    float noise = (perlin1 + (perlin2 * cfg.perlin2Amplitude)) / (1f + cfg.perlin2Amplitude); // Combine two noise layers
                    map[x, y] = noise > cfg.perlinThreshold ? WALL : FLOOR;
                }
                else // Non Perlin noise
                {
                    map[x, y] = rng.Next(0, 100) < cfg.cellularFillPercent ? WALL : FLOOR;
                }
            }
        return map;
    }

    // RunSimulationStep runs the Cellular Automata routine (aka the game of Life), where
    // cells either die or grow based on the number of neighbors they have.
    // Results in smoothing out the noise input into useful cavern shapes.
    byte[,] RunSimulationStep(byte[,] oldMap)
    {
        byte[,] newMap = new byte[cfg.mapWidth, cfg.mapHeight];

        for (int x = 0; x < cfg.mapWidth; x++)
            for (int y = 0; y < cfg.mapHeight; y++)
            {
                int walls = CountWallNeighbors(oldMap, x, y);
                if (oldMap[x, y] == WALL)
                    newMap[x, y] = walls >= 3 ? WALL : FLOOR;
                else
                    newMap[x, y] = walls > 4 ? WALL : FLOOR;
            }

        return newMap;
    }

    // CountWallNeighbors is a helper to the Cellular Growth phase.
    int CountWallNeighbors(byte[,] map, int x, int y)
    {
        int count = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
            for (int ny = y - 1; ny <= y + 1; ny++)
            {
                if (nx == x && ny == y) continue;
                if (nx < 0 || ny < 0 || nx >= cfg.mapWidth || ny >= cfg.mapHeight)
                    count++;
                else if (map[nx, ny] == WALL)
                    count++;
            }

        return count;
    }

}
