using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class DungeonGenerator
{
    public enum ScrapSeedMode { PerimeterEveryN, RandomScatter }

    // ======================= Scraps: Seed & Grow Until Packed =======================
    // Usage example:
    //   yield return StartCoroutine(Scraps_SeedAndGrowUntilPacked(
    //       mode: ScrapSeedMode.PerimeterEveryN,
    //       perimeterSpacing: 10,
    //       randomSeedsPerRegion: 3,
    //       randomMinSpacing: 6,
    //       maxRounds: 6,
    //       moatOverride: -1,             // -1 uses cfg.grow.wallMoat
    //       yieldEvery: 2048
    //   ));
    IEnumerator Scraps_SeedAndGrowUntilPacked(
        ScrapSeedMode mode,
        int perimeterSpacing = 10,
        int randomSeedsPerRegion = 3,
        int randomMinSpacing = 6,
        int maxRounds = 4,
        int moatOverride = -1,
        int yieldEvery = 2048
    )
    {
        int W = cfg.mapWidth, H = cfg.mapHeight;
        int moat = (moatOverride >= 0) ? moatOverride : cfg.GetEffectiveGrowWallMoat();

        for (int round = 0; round < maxRounds; round++)
        {
            // 1) Build scrap mask
            bool[,] scrap = new bool[W, H];
            int scrapsCount = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = cellGrid[x, y];
                    bool isScrap = !c.isCorridor && c.room_number < 0;
                    scrap[x, y] = isScrap;
                    if (isScrap) scrapsCount++;
                }
            if (scrapsCount == 0) yield break;

            // 2) Extract scrap regions (flood fill)
            var regions = new List<List<(int x, int y)>>();
            var seen = new bool[W, H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (!scrap[x, y] || seen[x, y]) continue;
                    var cells = new List<(int, int)>();
                    var q = new Queue<(int x, int y)>();
                    q.Enqueue((x: x, y: y));   // named tuple

                    while (q.Count > 0)
                    {
                        var p = q.Dequeue();
                        cells.Add(p);
                        foreach (var nb in FourNeighbors(p.x, p.y))
                        {
                            if (!In(nb.x, nb.y)) continue;
                            if (!scrap[nb.x, nb.y] || seen[nb.x, nb.y]) continue;
                            seen[nb.x, nb.y] = true;
                            q.Enqueue(nb);
                        }
                    }
                    regions.Add(cells);
                    if (regions.Count % 16 == 0) yield return null;
                }

            // 3) For each region, compute perimeter (for perimeter seeding) and place seeds
            var newRoomIds = new List<int>();  // track brand-new rooms for filtered growth
            int createdSeeds = 0;

            foreach (var reg in regions)
            {
                if (reg.Count == 0) continue;

                // Quick perimeter extraction
                var perimeter = new List<(int x, int y)>();
                foreach (var p in reg)
                {
                    bool onEdge = false;
                    // perimeter if any 4-neighbor is non-scrap or OOB
                    if (p.x == 0 || p.x == W - 1 || p.y == 0 || p.y == H - 1) onEdge = true;
                    else
                    {
                        if (!scrap[p.x - 1, p.y] || !scrap[p.x + 1, p.y] || !scrap[p.x, p.y - 1] || !scrap[p.x, p.y + 1])
                            onEdge = true;
                    }
                    if (onEdge) perimeter.Add(p);
                }

                // Seed set for this region (positions)
                var seeds = new List<(int x, int y)>();

                if (mode == ScrapSeedMode.PerimeterEveryN)
                {
                    if (perimeter.Count == 0) continue;
                    // walk around perimeter pseudo-order: just iterate by index spacing
                    int step = Mathf.Max(1, perimeterSpacing);
                    for (int i = 0; i < perimeter.Count; i += step)
                    {
                        var s = perimeter[i];
                        if (CanPlaceSeed(s.x, s.y, moat))
                            seeds.Add(s);
                    }
                }
                else // RandomScatter
                {
                    // uniform pick from region, enforce min spacing between seeds
                    int want = Mathf.Max(1, randomSeedsPerRegion);
                    var tried = 0;
                    var rngPick = new System.Random(reg.Count * 73856093 ^ regions.Count);
                    while (seeds.Count < want && tried < reg.Count * 3)
                    {
                        var p = reg[rngPick.Next(reg.Count)];
                        tried++;
                        if (!CanPlaceSeed(p.x, p.y, moat)) continue;
                        bool far = true;
                        for (int j = 0; j < seeds.Count; j++)
                        {
                            if (Manhattan(p, seeds[j]) < randomMinSpacing) { far = false; break; }
                        }
                        if (far) seeds.Add(p);
                    }
                }

                // Create a room per seed and claim the seed cell
                foreach (var s in seeds)
                {
                    int id = rooms.Count;
                    Room room = new Room { my_room_number = id, cells = new List<Cell>() };
                    room.setColorFloor(highlight: true);
                    Cell c = cellGrid[s.x, s.y];
                    if (c.room_number >= 0 || c.isCorridor) continue; // safety
                    c.room_number = room.my_room_number;
                    c.colorFloor = room.colorFloor;
                    c.height = 0; //50 * (round + 1);    // DEBUG: new cells are raised above others.
                    room.cells.Add(c);
                    room.bounds = new RectInt(s.x, s.y, 1, 1);
                    rooms.Add(room);
                    newRoomIds.Add(id); // will be passed to next round of Grow
                    createdSeeds++;
                }

                if (createdSeeds % 64 == 0) yield return null;   // breathe
            }

            // If nothing seeded this round, bail to avoid infinite loop
            if (createdSeeds == 0) yield break;

            if (cfg.showBuildProcess)       // draw the seeds
            {
                DrawMapByRooms(rooms, clearscreen: true);
                yield return new WaitForSeconds(0.025f);
            }
            // 4) Grow *only* the newly created rooms with a filtered credit wavefront
            yield return StartCoroutine(Grow_CreditWavefrontStrips(newRoomIds));
            // 5) Loop and see if more scraps remain next round
        }
        yield break;
    }
}
