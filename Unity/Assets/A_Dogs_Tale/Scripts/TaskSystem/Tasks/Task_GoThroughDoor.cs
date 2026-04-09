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
        private string? startFailure;

        public Task_GoThroughDoor(int doorId, WalkMode walkMode = WalkMode.Walk)
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

            Vector3 doorDelta = (throughMap - doorMap).normalized;
            if (doorDelta.sqrMagnitude > 0.0001f)
                throughMap += doorDelta * 0.3f;

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

            if (phase == DoorPhase.MoveToDoor && !context.Agent.agentMovementModule.MoveToDestinationInProgress)
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
                return TaskTickResult.Succeeded();

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
    }
}
