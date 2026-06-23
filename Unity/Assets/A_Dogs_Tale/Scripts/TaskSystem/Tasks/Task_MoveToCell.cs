#nullable enable
using UnityEngine;
using DogGame.LLM;

// The difference between MoveToCell and MoveToLocation is integer x,y vs float x,y,(future z)
namespace DogGame.Tasks
{
    public sealed class Task_MoveToCell : IAgentTask
    {
        public string DebugName => $"MoveToCell([{cellX},{cellY}])";
        public string Description = "Moves the agent toward the specified integer cell coordinates until the movement adapter reports arrival, or fails if the move target is rejected.";

        private readonly int cellX;
        private readonly int cellY;

        private Vector3 destinationWorld;

        public Task_MoveToCell(int cellX, int cellY, float stopRadius = 0.35f)
        {
            this.cellX = cellX;
            this.cellY = cellY;
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
                Debug.Log($"{DebugName} succeeded by cell.");
                context.Motion.StopMoving();
                return TaskTickResult.Succeeded();
            }

            if (context.Motion.IsAt(destinationWorld))
            {
                Debug.Log($"{DebugName} succeeded.");
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
