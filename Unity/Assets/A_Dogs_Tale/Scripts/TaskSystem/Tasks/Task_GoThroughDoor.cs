#nullable enable
using UnityEngine;
using DogGame.LLM;
using DogGame.Modules;

namespace DogGame.Tasks
{
    public sealed class Task_GoThroughDoor : IAgentTask
    {
        private enum DoorPhase
        {
            None,
            MoveToDoor,
            MoveThroughDoor
        }

        public string DebugName => $"GoThroughDoor({doorId})";
        public string Description = "Resolves a doorway by id from the current room, moves to that doorway, and moves through into the neighboring room.";

        private readonly int doorId;
        private readonly WalkMode walkMode;

        private DoorPhase phase;
        private Vector2Int doorCell;
        private Vector2Int throughCell;
        private Vector3 doorMap;
        private Vector3 throughMap;
        private int neighborRoomIndex = -1;
        private string? startFailure;

        public Task_GoThroughDoor(int doorId, WalkMode walkMode = WalkMode.None)
        {
            this.doorId = doorId;
            this.walkMode = walkMode;
        }

        public void Start(TaskContext context)
        {
            phase = DoorPhase.None;
            startFailure = null;

            if (context.Agent == null || context.Agent.locationModule == null || context.Agent.agentMovementModule == null)
            {
                startFailure = "missing_agent_modules";
                return;
            }

            Cell currentCell = context.Agent.locationModule.cell;
            if (currentCell == null)
            {
                startFailure = "missing_current_cell";
                return;
            }

            if (context.Agent.dir == null || context.Agent.dir.gen == null || context.Agent.dir.gen.rooms == null)
            {
                startFailure = "missing_dungeon";
                return;
            }

            int roomIndex = currentCell.room_number;
            if (roomIndex < 0 || roomIndex >= context.Agent.dir.gen.rooms.Count)
            {
                startFailure = "invalid_room";
                return;
            }

            Room room = context.Agent.dir.gen.rooms[roomIndex];
            if (!TryResolveDoorCells(room, doorId, out doorCell, out throughCell))
            {
                startFailure = "door_not_found_in_current_room";
                return;
            }

            doorMap = CellCenterMap(doorCell, context.Agent.locationModule.height);
            throughMap = CellCenterMap(throughCell, context.Agent.locationModule.height);
            neighborRoomIndex = GetRoomIndex(context.Agent, throughCell);

            phase = DoorPhase.MoveToDoor;
            context.Agent.agentMovementModule.SetDesiredTargetLocationMap(
                doorMap,
                walkMode,
                requestPathfinding: true,
                allowDoors: true);
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (!string.IsNullOrEmpty(startFailure))
                return TaskTickResult.Failed(startFailure);

            if (context.Agent == null || context.Agent.agentMovementModule == null)
                return TaskTickResult.Failed("missing_agent_movement_module");

            Cell? currentCell = context.Agent.locationModule != null ? context.Agent.locationModule.cell : null;
            if (currentCell == null)
                return TaskTickResult.Failed("missing_current_cell");

            if (HasReachedThroughSide(currentCell, context.Agent))
            {
                context.Motion.StopMoving();
                return TaskTickResult.Succeeded();
            }

            if (phase == DoorPhase.MoveToDoor &&
                (HasReachedDoorSide(currentCell, context.Agent) || !context.Agent.agentMovementModule.MoveToDestinationInProgress))
            {
                phase = DoorPhase.MoveThroughDoor;
                context.Agent.agentMovementModule.SetDesiredTargetLocationMap(
                    throughMap,
                    walkMode,
                    requestPathfinding: true,
                    allowDoors: true);
                return TaskTickResult.Running();
            }

            if (phase == DoorPhase.MoveThroughDoor && !context.Agent.agentMovementModule.MoveToDestinationInProgress)
            {
                context.Motion.StopMoving();
                return TaskTickResult.Succeeded();
            }

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            context.Motion.StopMoving();
        }

        private static bool TryResolveDoorCells(Room room, int requestedDoorId, out Vector2Int doorCell, out Vector2Int throughCell)
        {
            for (int i = 0; i < room.cells.Count; i++)
            {
                Cell cell = room.cells[i];
                if (cell.doors == DirFlags.None)
                    continue;

                foreach (DirFlags direction in DirFlagsEx.AllCardinals)
                {
                    if ((cell.doors & direction) == 0)
                        continue;

                    int resolvedDoorId = DoorIdUtility.Build(cell.pos, direction);
                    if (resolvedDoorId != requestedDoorId)
                        continue;

                    doorCell = cell.pos;
                    throughCell = cell.pos + direction.ToVector2Int();
                    return true;
                }
            }

            doorCell = default;
            throughCell = default;
            return false;
        }

        private static Vector3 CellCenterMap(Vector2Int cell, float height)
        {
            return new Vector3(cell.x + 0.5f, height, cell.y + 0.5f);
        }

        private bool HasReachedDoorSide(Cell currentCell, WorldObject agent)
        {
            return currentCell.pos == doorCell ||
                   currentCell.pos == throughCell ||
                   IsNearMap(agent, doorMap, GetDoorArrivalRadius(agent));
        }

        private bool HasReachedThroughSide(Cell currentCell, WorldObject agent)
        {
            return currentCell.pos == throughCell ||
                   (neighborRoomIndex >= 0 && currentCell.room_number == neighborRoomIndex);
        }

        private static float GetDoorArrivalRadius(WorldObject agent)
        {
            float stopDistance = agent != null && agent.agentMovementModule != null
                ? agent.agentMovementModule.StopDistance
                : 0.20f;

            float clearance = agent != null ? Mathf.Max(0f, agent.sizeRadius) : 0.30f;
            return Mathf.Max(stopDistance, clearance + 0.10f);
        }

        private static bool IsNearMap(WorldObject agent, Vector3 targetMap, float radius)
        {
            if (agent == null)
                return false;

            Vector3 delta = agent.pos3d_map - targetMap;
            delta.y = 0f;
            return delta.sqrMagnitude <= radius * radius;
        }

        private static int GetRoomIndex(WorldObject agent, Vector2Int cellPos)
        {
            if (agent == null || agent.dir == null || agent.dir.gen == null || agent.dir.gen.cellGrid == null)
                return -1;

            if (!agent.dir.gen.In(cellPos.x, cellPos.y))
                return -1;

            Cell cell = agent.dir.gen.cellGrid[cellPos.x, cellPos.y];
            return cell != null ? cell.room_number : -1;
        }
    }
}
