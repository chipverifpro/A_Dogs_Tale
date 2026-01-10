#nullable enable
using System;
using UnityEngine;

namespace DogGame.Routines
{
    [Serializable]
    public sealed class RoutineStats
    {
        [SerializeField] private int attempts;
        [SerializeField] private int successes;
        [SerializeField] private int failures;

        // Exponentially weighted moving average for success rate.
        [SerializeField] private float ewmaSuccess = 0.5f;
        [SerializeField] private float ewmaAlpha = 0.15f;

        [SerializeField] private float avgDurationSeconds;
        [SerializeField] private int durationSamples;
        [SerializeField] private string lastFailureReason = "";
        [SerializeField] private long lastUsedTicks;

        public int Attempts => attempts;
        public int Successes => successes;
        public int Failures => failures;
        public float EWMASuccess => ewmaSuccess;
        public float AvgDurationSeconds => avgDurationSeconds;
        public string LastFailureReason => lastFailureReason;

        public void Record(bool succeeded, float durationSeconds, string? failureReason)
        {
            attempts++;
            if (succeeded) successes++; else failures++;

            // EWMA update
            float x = succeeded ? 1f : 0f;
            ewmaSuccess = Mathf.Lerp(ewmaSuccess, x, ewmaAlpha);

            // Duration running average
            durationSamples++;
            avgDurationSeconds = Mathf.Lerp(avgDurationSeconds, durationSeconds, 1f / durationSamples);

            if (!succeeded && !string.IsNullOrWhiteSpace(failureReason))
                lastFailureReason = failureReason!;

            lastUsedTicks = DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// A simple reuse score: favors routines that work and have enough samples.
        /// Tweak later.
        /// </summary>
        public float GetReuseScore()
        {
            float sampleBoost = Mathf.Log(1f + attempts);
            float failurePenalty = failures > 0 ? Mathf.Log(1f + failures) * 0.25f : 0f;
            return (ewmaSuccess * sampleBoost) - failurePenalty;
        }
    }
}