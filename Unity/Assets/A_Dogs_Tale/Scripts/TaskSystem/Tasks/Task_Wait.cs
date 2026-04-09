#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_Wait : IAgentTask
    {
        public string DebugName => $"Wait({durationSeconds:0.00}s)";
        public string Description = "Stops the agent and waits for the specified duration before succeeding.";

        private readonly float durationSeconds;
        private float remainingSeconds;

        public Task_Wait(float durationSeconds)
        {
            this.durationSeconds = Mathf.Max(0f, durationSeconds);
            remainingSeconds = this.durationSeconds;
            Debug.Log(DebugName);
        }

        public void Start(TaskContext context)
        {
            remainingSeconds = durationSeconds;
            context.Motion.StopMoving();
        }

        private int debugDoubleTick = -1;
        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            // Ensure this isn't being called more than once per frame:
            if (debugDoubleTick == Time.frameCount)
                Debug.LogError("ERROR: Tick run more than once per frame");
            debugDoubleTick = Time.frameCount;

            remainingSeconds -= Mathf.Max(0f, deltaTimeSeconds);
            return remainingSeconds <= 0f ? TaskTickResult.Succeeded() : TaskTickResult.Running();
        }

        public void Stop(TaskContext context)
        {
            // no-op
        }
    }
}
