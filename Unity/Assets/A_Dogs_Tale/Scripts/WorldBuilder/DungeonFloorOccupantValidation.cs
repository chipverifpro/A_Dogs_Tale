using System.Collections.Generic;
using DogGame.Modules;
using UnityEngine;

public partial class DungeonGenerator
{
    private const float FloorHeightEpsilon = 0.01f;

    public void ValidateAgentsAndItemsOnFloorCells(string context = "")
    {
        if (rooms == null || rooms.Count == 0)
            return;

        List<Cell> floorCells = CollectFloorCells();
        if (floorCells.Count == 0)
            return;

        WorldObjectRegistry registry = WorldObjectRegistry.Instance;
        if (registry == null)
            return;

        Dictionary<Vector2Int, List<Cell>> cellsByPosition = BuildFloorCellLookup(floorCells);
        List<WorldObject> worldObjects = new(registry.GetAllObjects());
        HashSet<Vector3Int> occupiedFloorCells = CollectOccupiedValidFloorCells(worldObjects, cellsByPosition);

        int checkedCount = 0;
        int movedCount = 0;
        int heightAdjustedCount = 0;

        foreach (WorldObject worldObject in worldObjects)
        {
            if (!ShouldValidateFloorOccupant(worldObject))
                continue;

            checkedCount++;

            Cell currentCell = FindCurrentFloorCell(worldObject, cellsByPosition, out bool isOnFloorCell, out bool heightMatchesFloor);
            if (isOnFloorCell && heightMatchesFloor)
                continue;

            Cell targetCell = currentCell;
            bool randomRelocation = !isOnFloorCell;
            if (targetCell == null && !TryPickRandomFloorCell(floorCells, occupiedFloorCells, out targetCell))
                continue;

            Vector3 targetMapPosition = randomRelocation
                ? GetFloorCellCenterMapPosition(targetCell)
                : GetCurrentMapPositionAtFloorHeight(worldObject, targetCell);
            Vector3 targetWorldPosition = worldObject.MapToWorldPosition(targetMapPosition);

            PlaceWorldObjectOnFloor(worldObject, targetWorldPosition);
            occupiedFloorCells.Add(targetCell.pos3d);

            if (randomRelocation)
                movedCount++;
            else
                heightAdjustedCount++;
        }

        if (movedCount > 0 || heightAdjustedCount > 0)
        {
            string prefix = string.IsNullOrWhiteSpace(context) ? "Map floor validation" : $"Map floor validation ({context})";
            Debug.Log($"{prefix}: checked {checkedCount} agents/items, moved {movedCount} to random floor cells, adjusted {heightAdjustedCount} heights.", this);
        }
    }

    private List<Cell> CollectFloorCells()
    {
        List<Cell> floorCells = new();
        foreach (Room room in rooms)
        {
            if (room == null || room.cells == null)
                continue;

            foreach (Cell cell in room.cells)
            {
                if (cell != null)
                    floorCells.Add(cell);
            }
        }

        return floorCells;
    }

    private Dictionary<Vector2Int, List<Cell>> BuildFloorCellLookup(List<Cell> floorCells)
    {
        Dictionary<Vector2Int, List<Cell>> cellsByPosition = new();
        foreach (Cell cell in floorCells)
        {
            if (cell == null)
                continue;

            if (!cellsByPosition.TryGetValue(cell.pos, out List<Cell> cellsAtPosition))
            {
                cellsAtPosition = new List<Cell>();
                cellsByPosition[cell.pos] = cellsAtPosition;
            }

            cellsAtPosition.Add(cell);
        }

        return cellsByPosition;
    }

    private HashSet<Vector3Int> CollectOccupiedValidFloorCells(
        List<WorldObject> worldObjects,
        Dictionary<Vector2Int, List<Cell>> cellsByPosition)
    {
        HashSet<Vector3Int> occupiedFloorCells = new();
        foreach (WorldObject worldObject in worldObjects)
        {
            if (!ShouldValidateFloorOccupant(worldObject))
                continue;

            Cell cell = FindCurrentFloorCell(worldObject, cellsByPosition, out bool isOnFloorCell, out bool heightMatchesFloor);
            if (isOnFloorCell && heightMatchesFloor && cell != null)
                occupiedFloorCells.Add(cell.pos3d);
        }

        return occupiedFloorCells;
    }

    private bool ShouldValidateFloorOccupant(WorldObject worldObject)
    {
        if (worldObject == null || !worldObject.gameObject.activeInHierarchy)
            return false;

        if (IsHeldByAnotherWorldObject(worldObject))
            return false;

        return IsAgent(worldObject) || worldObject.Kind == WorldObjectKind.Item;
    }

    private static bool IsAgent(WorldObject worldObject)
    {
        return worldObject != null &&
               (worldObject.Kind == WorldObjectKind.Agent ||
                worldObject.agentModule != null ||
                worldObject.GetComponent<AgentModule>() != null);
    }

    private static bool IsHeldByAnotherWorldObject(WorldObject worldObject)
    {
        if (worldObject == null || worldObject.transform.parent == null)
            return false;

        WorldObject parentWorldObject = worldObject.transform.parent.GetComponentInParent<WorldObject>();
        return parentWorldObject != null && parentWorldObject != worldObject;
    }

    private Cell FindCurrentFloorCell(
        WorldObject worldObject,
        Dictionary<Vector2Int, List<Cell>> cellsByPosition,
        out bool isOnFloorCell,
        out bool heightMatchesFloor)
    {
        isOnFloorCell = false;
        heightMatchesFloor = false;

        if (worldObject == null)
            return null;

        Vector3 mapPosition = worldObject.pos3d_map;
        Vector2Int mapCell = new(Mathf.FloorToInt(mapPosition.x), Mathf.FloorToInt(mapPosition.z));
        if (!cellsByPosition.TryGetValue(mapCell, out List<Cell> cellsAtPosition) || cellsAtPosition == null || cellsAtPosition.Count == 0)
            return null;

        isOnFloorCell = true;
        Cell closestCell = cellsAtPosition[0];
        float closestHeightDelta = Mathf.Abs(mapPosition.y - GetFloorMapHeight(closestCell));

        for (int i = 1; i < cellsAtPosition.Count; i++)
        {
            Cell candidate = cellsAtPosition[i];
            if (candidate == null)
                continue;

            float heightDelta = Mathf.Abs(mapPosition.y - GetFloorMapHeight(candidate));
            if (heightDelta < closestHeightDelta)
            {
                closestCell = candidate;
                closestHeightDelta = heightDelta;
            }
        }

        heightMatchesFloor = closestHeightDelta <= FloorHeightEpsilon;
        return closestCell;
    }

    private bool TryPickRandomFloorCell(List<Cell> floorCells, HashSet<Vector3Int> occupiedFloorCells, out Cell chosenCell)
    {
        chosenCell = null;
        if (floorCells == null || floorCells.Count == 0)
            return false;

        int attempts = Mathf.Max(1, floorCells.Count * 2);
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Cell candidate = floorCells[Random.Range(0, floorCells.Count)];
            if (candidate == null || occupiedFloorCells.Contains(candidate.pos3d))
                continue;

            chosenCell = candidate;
            return true;
        }

        for (int i = 0; i < floorCells.Count; i++)
        {
            Cell candidate = floorCells[i];
            if (candidate == null || occupiedFloorCells.Contains(candidate.pos3d))
                continue;

            chosenCell = candidate;
            return true;
        }

        chosenCell = floorCells[Random.Range(0, floorCells.Count)];
        return chosenCell != null;
    }

    private Vector3 GetCurrentMapPositionAtFloorHeight(WorldObject worldObject, Cell floorCell)
    {
        Vector3 mapPosition = worldObject.pos3d_map;
        mapPosition.y = GetFloorMapHeight(floorCell);
        return mapPosition;
    }

    private Vector3 GetFloorCellCenterMapPosition(Cell floorCell)
    {
        return new Vector3(floorCell.x + 0.5f, GetFloorMapHeight(floorCell), floorCell.y + 0.5f);
    }

    private float GetFloorMapHeight(Cell floorCell)
    {
        float unitHeight = cfg != null ? Mathf.Max(0.0001f, cfg.unitHeight) : 1f;
        return floorCell.height * unitHeight;
    }

    private void PlaceWorldObjectOnFloor(WorldObject worldObject, Vector3 targetWorldPosition)
    {
        if (worldObject == null)
            return;

        if (IsAgent(worldObject) && worldObject.motionModule != null)
            worldObject.motionModule.Teleport(targetWorldPosition);
        else
            worldObject.transform.position = targetWorldPosition;

        worldObject.agentMovementModule?.ClearDesiredMovement();
        worldObject.kineticModule?.Stop();
    }
}
