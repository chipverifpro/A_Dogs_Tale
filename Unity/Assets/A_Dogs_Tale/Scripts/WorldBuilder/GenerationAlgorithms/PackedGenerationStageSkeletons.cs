using System.Collections;
using UnityEngine;

public partial class DungeonGenerator
{
    // ---------- Stage implementations (skeletons to fill) ----------

    IEnumerator Corridors_MedialAxis()
    {
        BottomBanner.Show("Corridors: MedialAxis");
        // derive corridors from skeleton of blocked mask; prune branches; width locked
        yield return null;
    }

    IEnumerator Corridors_GridMazes()
    {
        BottomBanner.Show("Corridors: GridMazes");
        // uniform or weighted recursive backtracker / Wilson; keep width = cfg.corridor.corridorWidth
        yield return null;
    }

    IEnumerator Seed_PoissonAlongCorridors()
    {
        BottomBanner.Show("Seeding: PoissonAlongCorridors");
        // run 1-D Poisson sampling along paths, project seeds to sides
        yield return null;
    }

    IEnumerator Seed_UniformGrid()
    {
        BottomBanner.Show("Seeding: UniformGrid");
        // grid cells at spacing; skip if too near corridors
        yield return null;
    }

    IEnumerator Grow_PressureField()
    {
        BottomBanner.Show("Growth: PressureField");
        // maintain a pressure scalar; rooms expand into lowest-pressure valid neighbor
        yield return null;
    }

    IEnumerator Grow_OrthogonalRays()
    {
        BottomBanner.Show("Growth: OrthogonalRays");
        // extend axis-aligned slabs until 1-cell before collision; merge slabs
        yield return null;
    }

    IEnumerator Scraps_ClosetsOnly()
    {
        BottomBanner.Show("Scraps: ClosetsOnly");
        // mark small unassigned blobs (<= cfg.scraps.closetMaxArea) as closets; leave others as wall
        yield return null;
    }

    IEnumerator Scraps_NearestRoom()
    {
        BottomBanner.Show("Scraps: NearestRoom");
        // simply flood to nearest room but preserve 1-cell wall between different owners
        yield return null;
    }

    IEnumerator Doors_EnsureConnectivity()
    {
        BottomBanner.Show("Doors: EnsureConnectivity");
        // ensure every room hits a corridor; add minimal doors to connect all components
        yield return null;
    }

    IEnumerator Doors_SparseLoops()
    {
        BottomBanner.Show("Doors: SparseLoops");
        // ensure connectivity + add few room-room doors with far-bias cfg.doors.loopBias
        yield return null;
    }

    IEnumerator Doors_ManyLoops()
    {
        BottomBanner.Show("Doors: ManyLoops");
        // like SparseLoops but add up to cfg.doors.maxRoomToRoomDoors extra room-room doors
        yield return null;
    }
}
