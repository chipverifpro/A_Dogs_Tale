#nullable enable
using UnityEngine;

namespace DogGame
{
    /// <summary>
    /// Central authoritative time source for gameplay ticks.
    /// Handles pause, resume, and delta clamping.
    /// </summary>
    public static class GameTime
    {
        // Public API
        public static float DeltaTime { get; private set; }
        public static float UnscaledDeltaTime { get; private set; }

        public static bool IsPaused => GamePause.IsPaused;

        // Tuning
        public static float MaxDelta = 0.05f; // 50 ms cap (safe for AI & tasks)

        // Internal state
        private static float lastUnscaledTime = -1f;
        private static bool initialized;

        /// <summary>
        /// Call ONCE per frame from a single driver (Update).
        /// </summary>
        public static void Update()
        {
            float now = Time.unscaledTime;

            if (!initialized)
            {
                initialized = true;
                lastUnscaledTime = now;
                DeltaTime = 0f;
                UnscaledDeltaTime = 0f;
                return;
            }

            float rawUnscaledDelta = now - lastUnscaledTime;
            lastUnscaledTime = now;

            // Pause = frozen time
            if (IsPaused)
            {
                DeltaTime = 0f;
                UnscaledDeltaTime = 0f;
                return;
            }

            // Clamp to avoid giant resume spikes
            rawUnscaledDelta = Mathf.Clamp(rawUnscaledDelta, 0f, MaxDelta);

            UnscaledDeltaTime = rawUnscaledDelta;
            DeltaTime = rawUnscaledDelta; // authoritative game dt
        }

        /// <summary>
        /// Forces the next Update() to produce dt=0 (useful on manual resets).
        /// </summary>
        public static void Reset()
        {
            initialized = false;
            DeltaTime = 0f;
            UnscaledDeltaTime = 0f;
        }
    }
}