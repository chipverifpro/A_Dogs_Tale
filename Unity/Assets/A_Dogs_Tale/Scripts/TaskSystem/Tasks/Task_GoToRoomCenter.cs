#nullable enable
using UnityEngine;
using DogGame.LLM;
using DogGame.Modules;

namespace DogGame.Tasks
{
    public sealed class Task_GoToRoomCenter : IAgentTask
    {
        public string DebugName => "GoToRoomCenter";
        public string Description = "Finds the center-most cell of the current room and moves there using pathfinding.";

        private readonly WalkMode walkMode;
        private Vector3 roomCenterMap;
        private string? startFailure;

        public Task_GoToRoomCenter(WalkMode walkMode = WalkMode.Walk)
        {
            this.walkMode = walkMode;
        }

        public void Start(TaskContext context)
        {
            startFailure = null;
            roomCenterMap = default;

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

            if (!TryGetRoomCenterMap(context.Agent, currentCell.room_number, out roomCenterMap))
            {
                startFailure = "room_center_not_found";
                return;
            }

            context.Agent.agentMovementModule.SetDesiredTargetLocationMap(
                roomCenterMap,
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

            if (!context.Agent.agentMovementModule.MoveToDestinationInProgress)
                return TaskTickResult.Succeeded();

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            context.Motion.StopMoving();
        }

        private static bool TryGetRoomCenterMap(WorldObject agent, int roomIndex, out Vector3 roomCenterMap)
        {
            roomCenterMap = default;

            if (agent == null || agent.dir == null || agent.dir.gen == null || agent.dir.gen.rooms == null)
                return false;

            if (roomIndex < 0 || roomIndex >= agent.dir.gen.rooms.Count)
                return false;

            Room room = agent.dir.gen.rooms[roomIndex];
            if (room == null || room.cells == null || room.cells.Count == 0)
                return false;

            Vector3 averageCenter = Vector3.zero;
            for (int i = 0; i < room.cells.Count; i++)
                averageCenter += room.cells[i].center3d_f;

            averageCenter /= room.cells.Count;

            Cell bestCell = room.cells[0];
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < room.cells.Count; i++)
            {
                Cell candidate = room.cells[i];
                float distance = (candidate.center3d_f - averageCenter).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCell = candidate;
                }
            }

            roomCenterMap = bestCell.center3d_f;
            return true;
        }
    }
}
