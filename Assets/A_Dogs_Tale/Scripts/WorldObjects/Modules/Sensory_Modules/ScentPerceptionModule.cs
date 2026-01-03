#nullable enable
using System.Collections.Generic;
using UnityEngine;
using DogGame.AI.Perception;

namespace DogGame.Modules
{
    // You can keep this as a WorldModule if that's your base type.
    // For now I'm showing it as a MonoBehaviour so it compiles anywhere.
    public sealed class ScentPerceptionModule : MonoBehaviour
    {
        public Directory? dir;

        [Header("Inputs")]
        public WorldObject worldObject = null!;          // assign in Awake if you prefer
        public ScentAirGround scentSystem = null!;       // assign/reference your global scent system
        public int maxEventsPerTick = 2;

        [Header("Thresholds")]
        [Tooltip("If scent not seen for this long, treat as new-ish again.")]
        public float reNoticeSeconds = 10f;

        [Tooltip("If strength increases by this amount, emit change event.")]
        public float strengthJumpThreshold01 = 0.25f;

        [Tooltip("Minimum strength required to consider the smell at all.")]
        public float minStrength01 = 0.02f;

        [Header("Interest weights")]
        public float strengthWeight = 0.65f;
        public float noveltyWeight = 0.35f;

        private readonly ScentMemory scentMemory = new();

        private void Awake()
        {
            if (worldObject == null)
                worldObject = GetComponentInParent<WorldObject>();
            if (dir == null)
                dir = FindFirstObjectByType<Directory>();
        }

        public void TickScent(float deltaTime)
        {
            if (worldObject == null || scentSystem == null)
                return;

            Cell cell = worldObject.locationModule.cell; // <-- adjust to your actual "where am I" cell getter
            //List<ScentDetection> detections = cell.scents;
            List<ScentDetection> detections = dir!.scentRegistry.CollectScentsAtCell(cell, scentSystem);

            if (detections == null || detections.Count == 0)
                return;

            float timeNow = Time.time;

            var events = new List<PerceptionEvent>(maxEventsPerTick);

            for (int i = 0; i < detections.Count; i++)
            {
                var det = detections[i];
                if (det.scentSource == null)
                    continue;

                float strength01 = Mathf.Clamp01(det.combinedStrength);
                if (strength01 < minStrength01)
                    continue;

                string scentKey = BuildScentKey(det.scentSource);

                bool seenBefore = scentMemory.TryGet(scentKey, out float lastStrength01, out float lastTime);
                float timeSince = seenBefore ? (timeNow - lastTime) : 999f;

                float novelty01 = ComputeNovelty(seenBefore, timeSince);
                float interest01 = ComputeInterest(det.scentSource, strength01, novelty01);

                bool reNotice = !seenBefore || timeSince >= reNoticeSeconds;
                bool strongJump = seenBefore && (strength01 - lastStrength01) >= strengthJumpThreshold01;

                if (!reNotice && !strongJump)
                    continue;

                var type = !seenBefore ? PerceptionEventType.NewSmell : PerceptionEventType.SmellStrengthChanged;

                events.Add(new PerceptionEvent(
                    type: type,
                    worldPos: worldObject.transform.position,
                    scentKey: scentKey,
                    category: det.scentSource.category,
                    scentName: det.scentSource.scentName ?? det.scentSource.category.ToString(),
                    strength01: strength01,
                    novelty01: novelty01,
                    interest01: interest01));

                scentMemory.Update(scentKey, strength01, timeNow);
            }

            if (events.Count == 0)
                return;

            // Sort by interest descending and take top N
            events.Sort((a, b) => b.Interest01.CompareTo(a.Interest01));
            if (events.Count > maxEventsPerTick)
                events.RemoveRange(maxEventsPerTick, events.Count - maxEventsPerTick);

            // Hand off to your reaction engine / task controller
            var reactionEngine = worldObject.GetComponentInParent<ReactionEngine>();
            reactionEngine?.HandlePerceptionEvents(events);
        }

        private static string BuildScentKey(ScentSource source)
        {
            // Prefer stable id when possible
            if (source.agentId >= 0)
                return $"agent:{source.agentId}";

            // Fallback for non-agent sources
            string name = string.IsNullOrWhiteSpace(source.scentName) ? "Unnamed" : source.scentName.Trim();
            return $"{source.category}:{name}";
        }

        private static float ComputeNovelty(bool seenBefore, float timeSinceLast)
        {
            if (!seenBefore) return 1f;
            // after ~20s, smells feel "new-ish" again
            return Mathf.Clamp01(timeSinceLast / 20f);
        }

        private float ComputeInterest(ScentSource source, float strength01, float novelty01)
        {
            float baseInterest = strengthWeight * strength01 + noveltyWeight * novelty01;

            // Category bias (v1): tune later per-agent personality
            float categoryBias = source.category switch
            {
                ScentCategory.Food => 1.30f,
                ScentCategory.Human => 1.10f,
                ScentCategory.Dog => 1.05f,
                _ => 1.0f
            };

            // Familiarity bias (v1): new smells get a little boost
            float familiarityBias = source.familiarity switch
            {
                ScentFamiliarity.New => 1.15f,
                _ => 1.0f
            };

            return Mathf.Clamp01(baseInterest * categoryBias * familiarityBias);
        }
    }
}