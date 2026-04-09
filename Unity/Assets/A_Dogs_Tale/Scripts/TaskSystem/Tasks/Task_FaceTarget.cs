#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    /// <summary>Rotate to face a target WorldObject. Completes when within tolerance.</summary>
    public sealed class Task_FaceTarget : IAgentTask
    {
        public string DebugName => $"FaceTarget({target?.DisplayName ?? "null"})";
        public string Description = "Rotates the agent to face a target object until within the yaw tolerance, or fails if the target is missing or the timeout expires.";

        private readonly WorldObject target;
        private readonly float toleranceDeg;
        private readonly float maxSeconds;

        private float elapsed;

        public Task_FaceTarget(WorldObject target, float toleranceDeg = 6f, float maxSeconds = 1.0f)
        {
            this.target = target;
            this.toleranceDeg = Mathf.Max(0.1f, toleranceDeg);
            this.maxSeconds = Mathf.Max(0.05f, maxSeconds);
        }

        public void Start(TaskContext context)
        {
            elapsed = 0f;
        }

        public TaskTickResult Tick(TaskContext context, float dt)
        {
            elapsed += dt;

            if (target == null)
                return TaskTickResult.Failed("No target");

            Vector3 from = context.AgentTransform.position;
            Vector3 to = target.transform.position;
            Vector3 dir = to - from;
            dir.y = 0f;

            if (dir.sqrMagnitude < 0.0001f)
                return TaskTickResult.Succeeded();

            // Compute desired yaw rotation.
            Quaternion desired = Quaternion.LookRotation(dir.normalized, Vector3.up);
            float angle = Quaternion.Angle(context.AgentTransform.rotation, desired);

            // Rotate quickly; you can later move this into MotionModule if desired.
            float rotateSpeedDegPerSec = 720f;
            context.AgentTransform.rotation = Quaternion.RotateTowards(
                context.AgentTransform.rotation,
                desired,
                rotateSpeedDegPerSec * dt);

            if (angle <= toleranceDeg)
                return TaskTickResult.Succeeded();

            if (elapsed >= maxSeconds)
                return TaskTickResult.Failed("FaceTarget timeout");

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context) { }
    }
}
