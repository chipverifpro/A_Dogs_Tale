using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FurniturePlacer : MonoBehaviour
{
    [Header("Furniture Prefabs")]
    [Tooltip("Prefabs that have a PlacementModule + WorldObject setup (or can have modules auto-added).")]
    public List<GameObject> furniturePrefabs = new();

    [Header("Per-Room Counts")]
    [Tooltip("Minimum number of furniture items per room.")]
    public int minPerRoom = 0;

    [Tooltip("Maximum number of furniture items per room.")]
    public int maxPerRoom = 3;

    [Tooltip("Max placement attempts per item before giving up for that item.")]
    public int maxAttemptsPerItem = 20;

    [Header("Placement Offsets")]
    [Tooltip("Extra Y offset above the cell's world position for placement.")]
    public float baseYOffset = 1f;

    private ObjectDirectory dir;

    private void Awake()
    {
        dir = ObjectDirectory.Instance;
        if (dir == null)
        {
            Debug.LogError("FurniturePlacer: ObjectDirectory.Instance is null. Cannot place furniture.", this);
        }
    }

    private IEnumerator Start()
    {
        if (dir == null || dir.gen == null)
            yield break;

        // Wait for dungeon build to complete before placing furniture
        if (!dir.gen.buildComplete)
        {
            yield return new WaitUntil(() => dir.gen.buildComplete);
        }

        PlaceAllFurniture();
    }

    /// <summary>
    /// Entry point: place furniture in all rooms according to PlacementModule hints.
    /// </summary>
    public void PlaceAllFurniture()
    {
        if (dir == null || dir.gen == null)
        {
            Debug.LogError("FurniturePlacer: missing ObjectDirectory or DungeonGenerator.", this);
            return;
        }

        if (furniturePrefabs == null || furniturePrefabs.Count == 0)
        {
            Debug.LogWarning("FurniturePlacer: No furniture prefabs assigned.");
            return;
        }

        if (dir.gen.rooms == null || dir.gen.rooms.Count == 0)
        {
            Debug.LogWarning("FurniturePlacer: No rooms available in generator.");
            return;
        }

        foreach (var room in dir.gen.rooms)
        {
            if (room == null || room.cells == null || room.cells.Count == 0)
                continue;

            PlaceFurnitureInRoom(room);
        }
    }

    private void PlaceFurnitureInRoom(Room room)
    {
        // Collect prefabs compatible with this room type
        var compatible = new List<GameObject>();

        foreach (var prefab in furniturePrefabs)
        {
            if (prefab == null) continue;

            var placement = prefab.GetComponentInChildren<PlacementModule>();
            if (placement == null) continue;

            if (placement.AllowsRoom(room.placementTypes))
            {
                compatible.Add(prefab);
            }
        }

        if (compatible.Count == 0)
        {
            // Nothing suitable for this room type
            return;
        }

        int countToPlace = Random.Range(minPerRoom, maxPerRoom + 1);
        if (countToPlace <= 0) return;

        for (int i = 0; i < countToPlace; i++)
        {
            var prefab = compatible[Random.Range(0, compatible.Count)];
            var placement = prefab.GetComponentInChildren<PlacementModule>();
            if (placement == null) continue;

            TryPlaceOne(room, prefab, placement);
        }
    }

    private bool TryPlaceOne(Room room, GameObject prefab, PlacementModule placementTemplate)
    {
        if (room.cells == null || room.cells.Count == 0)
            return false;

        for (int attempt = 0; attempt < maxAttemptsPerItem; attempt++)
        {
            if (!PickCellForPlacement(room, placementTemplate, out Cell cell, out DirFlags wallDir))
                continue;

            // Use the *template* PlacementModule to compute base position & rotation
            Vector3 worldPos = placementTemplate.ComputeBaseWorldPosition(cell, baseYOffset);
            Quaternion rot = placementTemplate.ChooseRotation(wallDir);

            GameObject instance = Instantiate(prefab, worldPos, rot);

            // Initialize WorldObject + LocationModule + VisualModule etc.
            InitializeWorldObject(instance, cell);

            // Let the instance's own PlacementModule sync its LocationModule, yaw, etc.
            var instPlacement = instance.GetComponentInChildren<PlacementModule>();
            if (instPlacement != null)
            {
                instPlacement.ApplyPlacement(cell, wallDir, baseYOffset);
            }

            return true; // success
        }

        // Could not find a suitable spot for this item in this room
        return false;
    }

    /// <summary>
    /// Simple heuristic-based cell selection depending on edgeHint.
    /// This version ignores sizeInCells and clearance; those can be layered on later.
    /// </summary>
    private bool PickCellForPlacement(Room room, PlacementModule placement, out Cell chosenCell, out DirFlags chosenWallDir)
    {
        chosenCell = null;
        chosenWallDir = DirFlags.None;

        var cells = room.cells;
        if (cells == null || cells.Count == 0)
            return false;

        // We’ll just try a bunch of random cells until one fits the hint.
        const int maxCellTries = 50;

        for (int attempt = 0; attempt < maxCellTries; attempt++)
        {
            Cell cell = cells[Random.Range(0, cells.Count)];
            if (cell == null) continue;

            DirFlags wallDir = DirFlags.None;

            switch (placement.edgeHint)
            {
                case EdgeHint.Free:
                    // No particular constraint; accept any cell.
                    break;

                case EdgeHint.NearWall:
                case EdgeHint.AgainstWall:
                {
                    if (cell.walls == DirFlags.None)
                        continue;

                    // Pick one wall direction from this cell's walls
                    wallDir = PickOneDirFlag(cell.walls);
                    if (wallDir == DirFlags.None)
                        continue;
                    break;
                }

                case EdgeHint.InCorner:
                {
                    // Require at least two walls (corner).
                    if (DirFlagsEx.Count(cell.walls) < 2)
                        continue;

                    // For corners, just keep all the bits; the rotation helper will handle it.
                    wallDir = cell.walls;
                    break;
                }

                case EdgeHint.CenterOfRoom:
                {
                    // Very simple heuristic: no walls touching this cell.
                    if (cell.walls != DirFlags.None)
                        continue;
                    break;
                }
            }

            // If we require touching a wall but we found none, skip.
            if (placement.mustTouchWall &&
                placement.edgeHint != EdgeHint.Free &&
                wallDir == DirFlags.None)
            {
                continue;
            }

            chosenCell = cell;
            chosenWallDir = wallDir;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Pick a single direction bit from a DirFlags bitfield (N,E,S,W).
    /// </summary>
    private DirFlags PickOneDirFlag(DirFlags flags)
    {
        // Collect all cardinal bits present
        var candidates = new List<DirFlags>(4);
        foreach (var d in DirFlagsEx.AllCardinals)
        {
            if ((flags & d) != 0)
                candidates.Add(d);
        }

        if (candidates.Count == 0)
            return DirFlags.None;

        return candidates[Random.Range(0, candidates.Count)];
    }

    /// <summary>
    /// Ensure the spawned object is a WorldObject with LocationModule, VisualModule, etc.,
    /// and register it. Adapt as needed to match your existing WorldObject API.
    /// </summary>
    private void InitializeWorldObject(GameObject instance, Cell cell)
    {
        if (instance == null || cell == null)
            return;

        // WorldObject
        WorldObject wo = instance.GetComponent<WorldObject>();
        if (wo == null)
            wo = instance.AddComponent<WorldObject>();

        // LocationModule
        LocationModule loc = instance.GetComponent<LocationModule>();
        if (loc == null)
            loc = instance.AddComponent<LocationModule>();

        // VisualModule (nice to have for tints, visibility, etc.)
        VisualModule visual = instance.GetComponent<VisualModule>();
        if (visual == null)
            visual = instance.AddComponent<VisualModule>();

        // Sync location from cell
        loc.cell = cell;
        loc.pos3d_f = cell.pos3d_f;                 // grid-space “truth”
        loc.yawDeg = instance.transform.eulerAngles.y;

        // Make sure the Transform uses WORLD coordinates
        instance.transform.position = cell.pos3d_world + new Vector3(0f, baseYOffset, 0f);

        // Register this WorldObject
        wo.RegisterIfNeeded();
    }
}