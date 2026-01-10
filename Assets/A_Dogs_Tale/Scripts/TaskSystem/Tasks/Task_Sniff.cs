#nullable enable
using UnityEngine;
using DogGame.LLM;

//placeholder Task: basically a renamed wait(durationSeconds)

namespace DogGame.Tasks
{
    public sealed class Task_Sniff : IAgentTask
    {
        public string DebugName => $"Sniff({durationSeconds:0.00}s)";

        private readonly float durationSeconds;
        private float remainingSeconds;

        public Task_Sniff(float durationSeconds)
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