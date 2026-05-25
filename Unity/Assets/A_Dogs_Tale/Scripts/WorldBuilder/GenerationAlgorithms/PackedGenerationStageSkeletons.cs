using System.Collections;

public partial class DungeonGenerator
{
    // ---------- Stage implementations (skeletons to fill) ----------

    IEnumerator Corridors_MedialAxis()
    {
        BottomBanner.LogBuildProgress("Corridors: MedialAxis");
        // derive corridors from skeleton of blocked mask; prune branches; width locked
        yield return null;
    }

    IEnumerator Corridors_GridMazes()
    {
        BottomBanner.LogBuildProgress("Corridors: GridMazes");
        // uniform or weighted recursive backtracker / Wilson; keep width = cfg.corridor.corridorWidth
        yield return null;
    }

    IEnumerator Seed_PoissonAlongCorridors()
    {
        BottomBanner.LogBuildProgress("Seeding: PoissonAlongCorridors");
        // run 1-D Poisson sampling along paths, project seeds to sides
        yield return null;
    }

    IEnumerator Seed_UniformGrid()
    {
        BottomBanner.LogBuildProgress("Seeding: UniformGrid");
        // grid cells at spacing; skip if too near corridors
        yield return null;
    }

    IEnumerator Grow_PressureField()
    {
        BottomBanner.LogBuildProgress("Growth: PressureField");
        // maintain a pressure scalar; rooms expand into lowest-pressure valid neighbor
        yield return null;
    }

    IEnumerator Grow_OrthogonalRays()
    {
        BottomBanner.LogBuildProgress("Growth: OrthogonalRays");
        // extend axis-aligned slabs until 1-cell before collision; merge slabs
        yield return null;
    }

    IEnumerator Scraps_ClosetsOnly()
    {
        BottomBanner.LogBuildProgress("Scraps: ClosetsOnly");
        // mark small unassigned blobs (<= cfg.scraps.closetMaxArea) as closets; leave others as wall
        yield return null;
    }

    IEnumerator Scraps_NearestRoom()
    {
        BottomBanner.LogBuildProgress("Scraps: NearestRoom");
        // simply flood to nearest room but preserve 1-cell wall between different owners
        yield return null;
    }

    IEnumerator Doors_EnsureConnectivity()
    {
        BottomBanner.LogBuildProgress("Doors: EnsureConnectivity");
        // ensure every room hits a corridor; add minimal doors to connect all components
        yield return null;
    }

    IEnumerator Doors_SparseLoops()
    {
        BottomBanner.LogBuildProgress("Doors: SparseLoops");
        // ensure connectivity + add few room-room doors with far-bias cfg.doors.loopBias
        yield return null;
    }

    IEnumerator Doors_ManyLoops()
    {
        BottomBanner.LogBuildProgress("Doors: ManyLoops");
        // like SparseLoops but add up to cfg.doors.maxRoomToRoomDoors extra room-room doors
        yield return null;
    }
}
