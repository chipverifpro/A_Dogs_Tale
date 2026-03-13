#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_Bark : IAgentTask
    {
        public string DebugName => $"Bark({volume})";

        private int volume;
        private readonly float barkVolume01;
        private readonly float durationSeconds;
        private float remainingSeconds;

        private static Dir? dir;

        public Task_Bark(float volume)
        { 
            // Clamp + normalize
            this.barkVolume01 = Mathf.Clamp(volume, 1, 10) / 10f;
            var duration = 0.5f;    // should get this from the audio clip chosen, or rewrite delay to wait for audio to complete.
            bool success = false;
            
            // Start the bark audio
            if (dir==null) dir=Object.FindFirstObjectByType<Dir>();
            if (dir!=null && dir.audioPlayer!=null)
                success = dir.audioPlayer.PlayClip("Bark");

            // for now, wait a half second for the audio to play
            this.durationSeconds = Mathf.Max(0f, duration);
            remainingSeconds = this.durationSeconds;
            Debug.Log(DebugName+" "+success);
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