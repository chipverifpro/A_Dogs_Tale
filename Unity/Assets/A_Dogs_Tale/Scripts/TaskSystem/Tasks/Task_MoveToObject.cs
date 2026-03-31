#nullable enable
using UnityEngine;
using DogGame.LLM;
using DogGame.Modules;

namespace DogGame.Tasks
{
    public sealed class Task_MoveToObject : IAgentTask
    {
        public string DebugName => $"MoveToObject({target?.DisplayName ?? "null"})";

        private readonly WorldObject target;

        public Task_MoveToObject(WorldObject target, float stopRadius = 0.5f)
        {
            this.target = target;
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

            if (context.Motion.IsAt(targetWorld))
                return TaskTickResult.Succeeded();

            context.Motion.SetMoveTarget(targetWorld);
            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            context.Motion.StopMoving();
        }
    }
}
