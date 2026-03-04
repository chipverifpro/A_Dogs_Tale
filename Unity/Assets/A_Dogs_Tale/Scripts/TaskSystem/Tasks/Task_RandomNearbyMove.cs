#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_RandomNearbyMove : IAgentTask
    {
        public string DebugName => $"RandomNearbyMove(r={radiusCells})";

        private readonly int radiusCells;
        private readonly float stopRadius;
        private Task_MoveToCell? move;

        public Task_RandomNearbyMove(int radiusCells = 3, float stopRadius = 0.35f)
        {
            this.radiusCells = Mathf.Clamp(radiusCells, 1, 10);
            this.stopRadius = Mathf.Clamp(stopRadius, 0.05f, 2f);
        }

        public void Start(TaskContext context)
        {
            var here = context.CurrentCellPos;

            // Random offset excluding (0,0)
            int dx = 0, dy = 0;
            for (int tries = 0; tries < 10; tries++)
            {
                dx = Random.Range(-radiusCells, radiusCells + 1);
                dy = Random.Range(-radiusCells, radiusCells + 1);
                if (dx != 0 || dy != 0) break;
            }

            var target = new Vector2Int(here.x + dx, here.y + dy);
            move = new Task_MoveToCell(target.x, target.y, stopRadius);
            move.Start(context);
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (move == null)
                return TaskTickResult.Failed("no_move_task");

            return move.Tick(context, deltaTimeSeconds);
        }

        public void Stop(TaskContext context)
        {
            move?.Stop(context);
        }
    }
}