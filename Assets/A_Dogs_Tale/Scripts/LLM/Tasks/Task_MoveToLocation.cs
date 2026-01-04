#nullable enable
using UnityEngine;
using DogGame.LLM;

// The difference between MoveToCell and MoveToLocation is integer x,y vs float x,y,(future z)
namespace DogGame.Tasks
{
    public sealed class Task_MoveToLocation : IAgentTask
    {
        public string DebugName => $"MoveToLocation([{mapX},{mapY}], r={stopRadius:0.00})";

        private readonly float mapX;
        private readonly float mapY;
        private readonly float stopRadius;

        private Vector3 destinationWorld;

        public Task_MoveToLocation(float mapX, float mapY, float stopRadius = 0.35f)
        {
            this.mapX = mapX;
            this.mapY = mapY;
            this.stopRadius = Mathf.Max(0.05f, stopRadius);
            Debug.Log(DebugName);
        }

        public void Start(AgentTaskContext context)
        {
            destinationWorld = context.Movement.CellToWorld((int)mapX, (int)mapY);  // TODO: change these back to float, and include height
        }

        private int debugDoubleTick = -1;
        public TaskTickResult Tick(AgentTaskContext context, float deltaTimeSeconds)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            Debug.Log ($"Task_MoveToCell.Tick ({context.Agent.DisplayName}, {deltaTimeSeconds})");
            if (context.Movement.IsAt(destinationWorld, stopRadius))
                return TaskTickResult.Succeeded();

            bool couldMove = context.Movement.SetMoveTarget(destinationWorld);
            if (!couldMove)
                return TaskTickResult.Failed("Movement adapter refused move target (blocked/unavailable).");

            return TaskTickResult.Running();
        }

        public void Stop(AgentTaskContext context)
        {
            // executor stops movement for us
        }
    }
}