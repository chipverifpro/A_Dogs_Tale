#nullable enable
using UnityEngine;
using DogGame.LLM;
using DogGame.Modules;

namespace DogGame.Tasks
{
    public sealed class Task_MoveToObject : IAgentTask
    {
        public string DebugName => $"MoveToObject({target?.DisplayName ?? "null"}, r={stopRadius:0.00})";

        private readonly WorldObject target;
        private readonly float stopRadius;

        public Task_MoveToObject(WorldObject target, float stopRadius = 0.5f)
        {
            this.target = target;
            this.stopRadius = Mathf.Clamp(stopRadius, 0.05f, 5.0f);
        }

        public void Start(TaskContext context)
        {
            // no-op
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (target == null || target.locationModule == null)
                return TaskTickResult.Failed("target_missing");

            Vector3 targetWorld = target.locationModule.pos3d_world;

            if (context.Movement.IsAt(targetWorld, stopRadius))
                return TaskTickResult.Succeeded();

            context.Movement.SetMoveTarget(targetWorld);
            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            context.Movement.StopMoving();
        }
    }
}