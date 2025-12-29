#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_MoveToCell : IAgentTask
    {
        public string DebugName => $"MoveToCell([{cellX},{cellY}], r={stopRadius:0.00})";

        private readonly int cellX;
        private readonly int cellY;
        private readonly float stopRadius;

        private Vector3 destinationWorld;

        public Task_MoveToCell(int cellX, int cellY, float stopRadius = 0.35f)
        {
            this.cellX = cellX;
            this.cellY = cellY;
            this.stopRadius = Mathf.Max(0.05f, stopRadius);
        }

        public void Start(AgentTaskContext context)
        {
            destinationWorld = context.Movement.CellToWorld(cellX, cellY);
        }

        public TaskTickResult Tick(AgentTaskContext context, float deltaTimeSeconds)
        {
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