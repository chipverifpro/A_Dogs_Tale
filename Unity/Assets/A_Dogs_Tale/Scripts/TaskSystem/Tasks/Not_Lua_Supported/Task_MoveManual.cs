#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_MoveManual : IAgentTask
    {
        public string DebugName => "MoveManual";
        public string Description = "Drives agent velocity directly from a manual input callback until the input returns to zero, then stops and succeeds.";

        private readonly System.Func<Vector2> readMoveInput;  // returns XZ intent in [-1..1]
        private readonly float maxSpeed;

        public Task_MoveManual(System.Func<Vector2> readMoveInput, float maxSpeed)
        {
            this.readMoveInput = readMoveInput;
            this.maxSpeed = Mathf.Max(0.1f, maxSpeed);
        }

        public void Start(TaskContext context)
        {
            // no-op
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            Vector2 move = readMoveInput();

            // End the task when player lets go.
            if (move.sqrMagnitude < 0.0001f)
            {
                context.Agent.agentMovementModule.SetDesiredVelocity(Vector3.zero);
                return TaskTickResult.Succeeded();
            }

            // Convert XY input to world XZ. (If you want camera-relative, do it in the provider.)
            Vector3 desiredVelocity = new Vector3(move.x, 0f, move.y) * maxSpeed;
            context.Agent.agentMovementModule.SetDesiredVelocity(desiredVelocity);

            return TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            context.Agent.agentMovementModule.SetDesiredVelocity(Vector3.zero);
            context.Motion.StopMoving();
        }
    }
}
