#nullable enable
using System;
using UnityEngine;

namespace DogGame
{
    /// <summary>
    /// One global pause state for the whole game.
    /// - Sets Time.timeScale (stops Update-driven motion/physics using scaled time).
    /// - Controls FixedUpdate cadence (physics).
    /// - Provides "unscaled delta" helpers for systems that need them.
    /// </summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        public static event Action<bool>? OnPauseChanged;

        // For restoring engine settings
        private static float priorTimeScale = 1f;
        private static float priorFixedDeltaTime = 0.02f;

        // Used for clamping first-tick deltas after resume
        private static float resumeUnscaledTime = -1f;

        /// <summary>Pause the game globally.</summary>
        public static void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;

            priorTimeScale = Time.timeScale;
            priorFixedDeltaTime = Time.fixedDeltaTime;

            // Freeze scaled time + physics progression
            Time.timeScale = 0f;

            // Optional: set fixedDeltaTime to something stable (won't matter while timeScale=0)
            // Keep it anyway so you can restore correctly.
            // Time.fixedDeltaTime = priorFixedDeltaTime;

            OnPauseChanged?.Invoke(true);
        }

        /// <summary>Resume the game globally.</summary>
        public static void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;

            Time.timeScale = priorTimeScale <= 0f ? 1f : priorTimeScale;
            Time.fixedDeltaTime = priorFixedDeltaTime;

            // Mark a resume time so tick drivers can ignore a "huge first delta"
            resumeUnscaledTime = Time.unscaledTime;

            OnPauseChanged?.Invoke(false);
        }

        public static void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        /// <summary>
        /// If you have systems using unscaled time, call this and clamp deltas after resume.
        /// </summary>
        public static float ClampAfterResumeUnscaledDelta(float unscaledDelta, float maxDelta = 0.05f)
        {
            // If we just resumed this frame (or very recently), clamp the first delta.
            if (resumeUnscaledTime > 0f && (Time.unscaledTime - resumeUnscaledTime) < 0.2f)
                return Mathf.Min(unscaledDelta, maxDelta);

            return Mathf.Min(unscaledDelta, maxDelta);
        }
    }
}