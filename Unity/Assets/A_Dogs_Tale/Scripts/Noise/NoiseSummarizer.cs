using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.Noise
{
    /// <summary>
    /// Collapses raw HeardNoise impulses into a small, high-signal list for LLM + gameplay.
    /// - Dedup footsteps / talking
    /// - Detect approaching/receding for movement groups
    /// - Cap ambient items
    /// - Keeps alarms/impacts high priority
    /// Stateless helper.
    /// </summary>
    public static class NoiseSummarizer
    {
        // Grouping window: "these are the same continuing sound" (seconds)
        private const float GroupWindowSeconds = 2.0f;

        // Direction bucketization (8-way)
        private const float DegPerSector = 45f;

        /// <summary>
        /// Build a summarized list from raw heard noises.
        /// Output is already capped to maxItems and ordered by relevance.
        /// </summary>
        public static void SummarizeForLLM(
            IReadOnlyList<HeardNoise> rawHeard,
            int maxItems,
            List<HeardNoise> summarizedOut)
        {
            summarizedOut.Clear();
            if (rawHeard == null || rawHeard.Count == 0 || maxItems <= 0)
                return;

            // 1) Group items
            Dictionary<GroupKey, GroupAccumulator> groups = new(64);

            for (int i = 0; i < rawHeard.Count; i++)
            {
                HeardNoise h = rawHeard[i];

                // Ignore super-low ambient early (optional)
                // (HearingModule already thresholds, so this is mostly redundant)
                if (h.category == NoiseCategory.Ambient && h.perceivedLoudness01 < 0.05f)
                    continue;

                GroupKey key = BuildGroupKey(h);

                if (!groups.TryGetValue(key, out GroupAccumulator acc))
                {
                    acc = new GroupAccumulator(h);
                    groups[key] = acc;
                }
                else
                {
                    acc.Add(h);
                    groups[key] = acc;
                }
            }

            if (groups.Count == 0)
                return;

            // 2) Convert groups to representative summarized items
            List<HeardNoise> candidates = new(groups.Count);
            foreach (var kvp in groups)
            {
                GroupAccumulator acc = kvp.Value;
                HeardNoise rep = acc.BuildRepresentative();
                candidates.Add(rep);
            }

            // 3) Apply ambient cap (keep at most 1 ambient unless ambient is all we have)
            ApplyAmbientCap(candidates);

            // 4) Sort by audibilityScore (descending)
            candidates.Sort((a, b) => b.audibilityScore.CompareTo(a.audibilityScore));

            // 5) Take top maxItems
            int take = Mathf.Min(maxItems, candidates.Count);
            for (int i = 0; i < take; i++)
                summarizedOut.Add(candidates[i]);
        }

        // ----------------------------
        // Grouping
        // ----------------------------

        private static GroupKey BuildGroupKey(in HeardNoise h)
        {
            // Emitter bucket:
            // - Prefer attributed emitter id when valid; else use -1 bucket
            int emitterBucket = DogGame.Noise.NoiseIdUtil.IsValidWorldObjectId(h.attributedEmitterId)
                ? h.attributedEmitterId
                : -1;

            int dirSector = DirectionSector8(h.directionToSource);

            // Rollups should unify repetitive footsteps & talking better:
            // - Talking: group by category+subtype+emitter+room+dir
            // - Footsteps: group by subtype class + emitter + room + dir (already via subtype)
            return new GroupKey(
                h.category,
                h.subtype,
                emitterBucket,
                h.roomRelation,
                dirSector
            );
        }

        private static int DirectionSector8(Vector3 dir)
        {
            if (dir == Vector3.zero)
                return -1;

            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg; // 0 = +Z
            if (yaw < 0f) yaw += 360f;
            int sector = Mathf.FloorToInt((yaw + (DegPerSector * 0.5f)) / DegPerSector) % 8;
            return sector;
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            private readonly NoiseCategory category;
            private readonly NoiseSubtype subtype;
            private readonly int emitterBucket;
            private readonly RoomRelation roomRelation;
            private readonly int directionSector;

            public GroupKey(
                NoiseCategory category,
                NoiseSubtype subtype,
                int emitterBucket,
                RoomRelation roomRelation,
                int directionSector)
            {
                this.category = category;
                this.subtype = subtype;
                this.emitterBucket = emitterBucket;
                this.roomRelation = roomRelation;
                this.directionSector = directionSector;
            }

            public bool Equals(GroupKey other)
            {
                return category == other.category
                    && subtype == other.subtype
                    && emitterBucket == other.emitterBucket
                    && roomRelation == other.roomRelation
                    && directionSector == other.directionSector;
            }

            public override bool Equals(object obj) => obj is GroupKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)category;
                    hash = (hash * 397) ^ (int)subtype;
                    hash = (hash * 397) ^ emitterBucket;
                    hash = (hash * 397) ^ (int)roomRelation;
                    hash = (hash * 397) ^ directionSector;
                    return hash;
                }
            }
        }

        // ----------------------------
        // Accumulator -> Representative
        // ----------------------------

        private struct GroupAccumulator
        {
            // Classification & identity
            private readonly NoiseCategory category;
            private readonly NoiseSubtype subtype;

            // Representative base (we will clone & modify)
            private HeardNoise strongest;

            // Stats
            private int count;
            private float maxAudibilityScore;

            // Time windowing
            private float newestTimeAgo;
            private float oldestTimeAgo;

            // Distance trend
            private float newestDistance;
            private float oldestDistance;

            // For voice targeting
            private bool anyAddressedToMe;
            private float maxAddressedConfidence;

            // Content snippet (prefer most confident / loudest)
            private string bestContent;
            private float bestContentScore;

            public GroupAccumulator(in HeardNoise first)
            {
                category = first.category;
                subtype = first.subtype;

                strongest = first;

                count = 0;
                maxAudibilityScore = 0f;

                newestTimeAgo = float.MaxValue;
                oldestTimeAgo = float.MinValue;

                newestDistance = 0f;
                oldestDistance = 0f;

                anyAddressedToMe = false;
                maxAddressedConfidence = 0f;

                bestContent = string.Empty;
                bestContentScore = -1f;

                Add(first);
            }

            public void Add(in HeardNoise h)
            {
                // GroupWindowSeconds: if something is outside, we still keep it,
                // because HearingModule already fetched "since last tick".
                // This window is mainly for trend notes; not for inclusion.

                count++;

                if (h.audibilityScore > maxAudibilityScore)
                {
                    maxAudibilityScore = h.audibilityScore;
                    strongest = h;
                }

                // newest = smallest timeAgo
                if (h.timeAgoSeconds <= newestTimeAgo)
                {
                    newestTimeAgo = h.timeAgoSeconds;
                    newestDistance = h.distanceMeters;
                }

                // oldest = largest timeAgo
                if (h.timeAgoSeconds > oldestTimeAgo)
                {
                    oldestTimeAgo = h.timeAgoSeconds;
                    oldestDistance = h.distanceMeters;
                }

                if (h.isIntendedForMe)
                {
                    anyAddressedToMe = true;
                    if (h.intendedConfidence01 > maxAddressedConfidence)
                        maxAddressedConfidence = h.intendedConfidence01;
                }

                // Prefer content for voice from the strongest/most confident instance
                if (!string.IsNullOrWhiteSpace(h.heardContentShort))
                {
                    float contentScore = h.perceivedLoudness01 * 0.7f + h.confidence01 * 0.3f;
                    if (contentScore > bestContentScore)
                    {
                        bestContentScore = contentScore;
                        bestContent = h.heardContentShort;
                    }
                }
            }

            public HeardNoise BuildRepresentative()
            {
                HeardNoise rep = strongest;

                // Add rollup notes for certain categories/subtypes
                if (IsFootstepSubtype(rep.subtype))
                {
                    // If we have multiple impulses, summarize as continuing movement
                    if (count >= 2)
                    {
                        float distanceDelta = newestDistance - oldestDistance;
                        if (distanceDelta < -0.1f)
                        {
                            rep.notesShort = Append(rep.notesShort, "footsteps approaching");
                        }
                        else if (distanceDelta > 0.1f)
                        {
                            rep.notesShort = Append(rep.notesShort, "footsteps receding");
                        }
                        //rep.notesShort = Append(rep.notesShort, $"footsteps x{count}");

                        // Make footsteps a little more relevant when repeated
                        rep.audibilityScore *= Mathf.Lerp(1.0f, 1.18f, Mathf.Clamp01((count - 1) / 6f));
                    }
                }
                else if (rep.category == NoiseCategory.Voice && rep.subtype == NoiseSubtype.HumanTalk)
                {
                    if (count >= 2)
                        rep.notesShort = Append(rep.notesShort, $"talking continues x{count}");

                    if (!string.IsNullOrWhiteSpace(bestContent))
                        rep.heardContentShort = bestContent;

                    if (anyAddressedToMe)
                    {
                        rep.isIntendedForMe = true;
                        rep.intendedConfidence01 = Mathf.Max(rep.intendedConfidence01, maxAddressedConfidence);
                        rep.notesShort = Append(rep.notesShort, "addressed to me");
                        rep.audibilityScore *= 1.12f;
                    }
                }
                else if (rep.category == NoiseCategory.Voice && rep.subtype == NoiseSubtype.Bark)
                {
                    // Pack signaling: repeated bark matters
                    if (count >= 2)
                    {
                        rep.notesShort = Append(rep.notesShort, $"repeated barking x{count}");
                        rep.audibilityScore *= Mathf.Lerp(1.0f, 1.25f, Mathf.Clamp01((count - 1) / 5f));
                    }
                }
                else if (rep.category == NoiseCategory.Impact || rep.category == NoiseCategory.Mechanism)
                {
                    // Keep impacts/mechanisms salient; repeated doesn’t necessarily mean “less”
                    if (count >= 2)
                        rep.notesShort = Append(rep.notesShort, $"repeated x{count}");
                }
                else if (rep.category == NoiseCategory.Ambient)
                {
                    // Ambient should generally not dominate
                    rep.audibilityScore *= 0.85f;
                }

                return rep;
            }

            private static bool IsFootstepSubtype(NoiseSubtype subtype)
            {
                return subtype == NoiseSubtype.FootstepWalk
                    || subtype == NoiseSubtype.FootstepRun
                    || subtype == NoiseSubtype.SneakStep
                    || subtype == NoiseSubtype.Scurry;
            }

            private static string Append(string existing, string add)
            {
                if (string.IsNullOrWhiteSpace(add)) return existing;
                if (string.IsNullOrWhiteSpace(existing)) return add;
                return $"{existing}; {add}";
            }
        }

        // ----------------------------
        // Ambient cap
        // ----------------------------

        private static void ApplyAmbientCap(List<HeardNoise> candidates)
        {
            if (candidates.Count <= 1) return;

            int ambientCount = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].category == NoiseCategory.Ambient)
                    ambientCount++;
            }

            if (ambientCount <= 1) return;

            // Keep the single best ambient (highest audibilityScore)
            int bestIndex = -1;
            float bestScore = float.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].category != NoiseCategory.Ambient)
                    continue;

                float score = candidates[i].audibilityScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            // Remove all ambient except bestIndex
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if (candidates[i].category == NoiseCategory.Ambient && i != bestIndex)
                    candidates.RemoveAt(i);
            }
        }
    }
}
