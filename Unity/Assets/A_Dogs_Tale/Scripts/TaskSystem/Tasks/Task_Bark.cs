#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_Bark : IAgentTask
    {
        public string DebugName => $"Bark({volume})";
        public string Description = "Agent emits a bark sound at a specified volume between 1 and 10.  Then waits 0.5 seconds.";
        private readonly int volume;
        private readonly float barkVolume01;
        private readonly float durationSeconds;
        private float remainingSeconds;

        public Task_Bark(float volume)
        { 
            // Clamp + normalize
            this.volume = Mathf.RoundToInt(Mathf.Clamp(volume, 1f, 10f));
            this.barkVolume01 = this.volume / 10f;
            var duration = 0.5f;    // should get this from the audio clip chosen, or rewrite delay to wait for audio to complete.

            // for now, wait a half second for the audio to play
            this.durationSeconds = Mathf.Max(0f, duration);
            remainingSeconds = this.durationSeconds;
        }

        public void Start(TaskContext context)
        {
            remainingSeconds = durationSeconds;
            context.Motion.StopMoving();

            if (context.Agent.noiseMakerModule != null)
            {
                context.Agent.noiseMakerModule.Bark(barkVolume01);
            }
            else
            {
                Debug.LogWarning($"[Task_Bark] {context.Agent.DisplayName} has no NoiseMakerModule; bark task produced no sound.");
            }
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
