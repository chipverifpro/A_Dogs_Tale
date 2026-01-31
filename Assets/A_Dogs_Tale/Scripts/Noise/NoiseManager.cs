using System.Collections.Generic;
using UnityEngine;

namespace DogGame.Noise
{
    /// <summary>
    /// Global store of recent NoiseEvents. Producers append; listeners query by last seen noiseId.
    /// Ring buffer keeps only a recent window by age and capacity.
    /// </summary>
    public class NoiseManager : MonoBehaviour
    {
        public static NoiseManager Instance { get; private set; }

        [Header("Buffer")]
        [SerializeField] private int capacity = 2048;
        [SerializeField] private float maxAgeSeconds = 12f;

        private NoiseEvent[] buffer;
        private int headIndex;              // points to oldest element
        private int count;                  // number of valid elements

        private ulong nextNoiseId = 1;      // monotonic; 0 reserved for "none"

        // For fast pruning without scanning whole buffer too often
        private float lastPruneTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[NoiseManager] Duplicate instance; destroying this one.");
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (capacity < 64) capacity = 64;
            buffer = new NoiseEvent[capacity];
            headIndex = 0;
            count = 0;
            lastPruneTime = Time.time;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Adds a noise event to the ring buffer and assigns a unique noiseId.
        /// Returns the assigned noiseId.
        /// </summary>
        public ulong Add(ref NoiseEvent noiseEvent)
        {
            if (buffer == null || buffer.Length != capacity)
                buffer = new NoiseEvent[Mathf.Max(64, capacity)];

            // Prune old items occasionally to keep queries cheap.
            // (Cheap cadence: prune at most ~5 times/sec.)
            float now = Time.time;
            if (now - lastPruneTime > 0.2f)
            {
                PruneByAge(now);
                lastPruneTime = now;
            }

            noiseEvent.noiseId = nextNoiseId++;
            if (noiseEvent.timeSeconds <= 0f)
                noiseEvent.timeSeconds = now;

            // If full, overwrite oldest (advance head).
            if (count == buffer.Length)
            {
                buffer[headIndex] = noiseEvent;
                headIndex = (headIndex + 1) % buffer.Length;
            }
            else
            {
                int writeIndex = (headIndex + count) % buffer.Length;
                buffer[writeIndex] = noiseEvent;
                count++;
            }

            //Debug.Log($"Noise added by {noiseEvent.emitterRef.DisplayName}: {noiseEvent.category}/{noiseEvent.subtype} id={noiseEvent.noiseId}");
            
            return noiseEvent.noiseId;
        }

        /// <summary>
        /// Fills results with all events whose noiseId is > lastSeenNoiseId.
        /// Results are in chronological order (oldest to newest).
        /// </summary>
        public void GetEventsAfter(ulong lastSeenNoiseId, List<NoiseEvent> results)
        {
            if (results == null) return;
            results.Clear();

            if (count == 0) return;

            // Optional: prune on query too (safe, helps long pauses)
            PruneByAge(Time.time);

            for (int i = 0; i < count; i++)
            {
                int index = (headIndex + i) % buffer.Length;
                NoiseEvent evt = buffer[index];
                if (evt.noiseId > lastSeenNoiseId)
                    results.Add(evt);
            }
        }

        /// <summary>
        /// Remove events older than maxAgeSeconds from the head of the ring buffer.
        /// Assumes events are stored in chronological order.
        /// </summary>
        private void PruneByAge(float now)
        {
            if (count == 0) return;
            if (maxAgeSeconds <= 0f) return;

            float cutoff = now - maxAgeSeconds;

            // Pop from head while the oldest is too old.
            while (count > 0)
            {
                NoiseEvent oldest = buffer[headIndex];
                if (oldest.timeSeconds >= cutoff)
                    break;

                headIndex = (headIndex + 1) % buffer.Length;
                count--;
            }
        }

        // Debug helper: how many events currently stored
        public int CurrentCount => count;
        public float MaxAgeSeconds => maxAgeSeconds;
    }
}