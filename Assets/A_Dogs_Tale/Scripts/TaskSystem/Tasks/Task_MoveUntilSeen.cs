#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    /// <summary>
    /// Move toward a target until it becomes visible (LOS + optional FOV) or timeout.
    /// Great for "go around the corner until you can see it".
    /// </summary>
    public sealed class Task_MoveUntilSeen : IAgentTask
    {
        public string DebugName => $"MoveUntilSeen({target?.DisplayName ?? "null"})";

        private readonly WorldObject target;
        private readonly float stopRadius;
        private readonly float maxSeconds;
        private readonly float viewRadius;
        private readonly float fovDeg;
        private readonly bool requireFov;

        private float elapsed;

        public Task_MoveUntilSeen(
            WorldObject target,
            float stopRadius = 1.0f,
            float maxSeconds = 4.0f,
            float viewRadius = 12.0f,
            float fovDeg = 160.0f,
            bool requireFov = true)
        {
            this.target = target;
            this.stopRadius = Mathf.Max(0.05f, stopRadius);
            this.maxSeconds = Mathf.Max(0.05f, maxSeconds);
            this.viewRadius = Mathf.Max(0.5f, viewRadius);
            this.fovDeg = Mathf.Clamp(fovDeg, 10f, 360f);
            this.requireFov = requireFov;
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

            // 1) If visible now => succeed
            if (IsVisible(context))
                return TaskTickResult.Succeeded();

            // 2) Otherwise keep moving toward target
            Vector3 targetPos = target.transform.position;
            bool ok = context.Movement.SetMoveTarget(targetPos);
            if (!ok)
                return TaskTickResult.Failed("Movement adapter rejected move");

            // 3) If we arrived near target but still can't see => fail (or succeed; your call)
            if (context.Movement.IsAt(targetPos, stopRadius))
                return TaskTickResult.Failed("Arrived but not visible");

            if (elapsed >= maxSeconds)
                return TaskTickResult.Failed("Timeout");

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            // Let executor stop movement; no-op here.
        }

        private bool IsVisible(TaskContext context)
        {
            Vector3 origin = context.AgentTransform.position + Vector3.up * 0.6f;
            Vector3 targetPos = target.transform.position + Vector3.up * 0.6f;

            Vector3 toTarget = targetPos - origin;
            float dist = toTarget.magnitude;

            if (dist > viewRadius)
                return false;

            // Optional FOV check
            if (requireFov)
            {
                Vector3 forward = context.AgentTransform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                    forward.Normalize();

                Vector3 dirFlat = toTarget;
                dirFlat.y = 0f;
                if (dirFlat.sqrMagnitude < 0.0001f)
                    return true;

                dirFlat.Normalize();
                float angle = Vector3.Angle(forward, dirFlat);
                if (angle > (fovDeg * 0.5f))
                    return false;
            }

            // LOS raycast
            Ray ray = new Ray(origin, toTarget.normalized);
            if (Physics.Raycast(ray, out var hit, dist))
            {
                // Visible if the first thing hit is the target (or one of its children)
                var hitWo = hit.collider.GetComponentInParent<WorldObject>();
                return hitWo == target;
            }

            // No obstruction
            return true;
        }
    }
}