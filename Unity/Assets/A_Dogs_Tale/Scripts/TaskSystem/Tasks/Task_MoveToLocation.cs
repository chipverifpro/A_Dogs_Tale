#nullable enable
using UnityEngine;
using DogGame.LLM;

// The difference between MoveToCell and MoveToLocation is integer x,y vs float x,y,(future z)
namespace DogGame.Tasks
{
    public sealed class Task_MoveToLocation : IAgentTask
    {
        public string DebugName => $"MoveToLocation([{mapX},{mapY}])";
        public string Description = "Moves the agent toward the specified map location, currently snapped to integer cell coordinates, until arrival or movement rejection.";

        private readonly float mapX;
        private readonly float mapY;
        private readonly int cellX;
        private readonly int cellY;

        private Vector3 destinationWorld;

        public Task_MoveToLocation(float mapX, float mapY, float stopRadius = 0.35f)
        {
            this.mapX = mapX;
            this.mapY = mapY;
            cellX = Mathf.FloorToInt(mapX);
            cellY = Mathf.FloorToInt(mapY);
            Debug.Log(DebugName);
        }

        public void Start(TaskContext context)
        {
            destinationWorld = context.Motion.CellToWorld(cellX, cellY);
        }

        private int debugDoubleTick = -1;
        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            //Debug.Log ($"Task_MoveToCell.Tick ({context.Agent.DisplayName}, {deltaTimeSeconds})");
            if (IsInDestinationCell(context))
            {
                context.Motion.StopMoving();
                return TaskTickResult.Succeeded();
            }

            if (context.Motion.IsAt(destinationWorld))
            {
                context.Motion.StopMoving();
                return TaskTickResult.Succeeded();
            }

            bool couldMove = context.Motion.SetMoveTarget(destinationWorld);
            if (!couldMove)
                return TaskTickResult.Failed("Movement adapter refused move target (blocked/unavailable).");

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            // executor stops movement for us
        }

        private bool IsInDestinationCell(TaskContext context)
        {
            if (context.Agent == null || context.Agent.locationModule == null)
                return false;

            return context.Agent.locationModule.x == cellX &&
                   context.Agent.locationModule.y == cellY;
        }
    }
}
