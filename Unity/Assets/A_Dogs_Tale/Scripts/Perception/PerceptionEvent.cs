#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.Modules
{
    public enum PerceptionSense
    {
        Scent = 0,
        Vision,
        Sound,
        Touch,
        Taste
        // Add Memory, ESP, Pain, Emotion, etc.
    }

    public enum PerceptionEventType
    {
        // --- Scent ---
        NewSmell,
        SmellStrengthChanged,

        // --- Vision ---
        TargetSeen,
        TargetNewlySeen,
        TargetLostSight,
        TargetMoving,
        TargetMovingFast,
        PackLeaderNotVisible,

        // --- Sound (future) ---
        BarkHeard,
        LoudNoise,

        // --- Generic / misc ---
        SomethingInteresting
    }

    /// <summary>
    /// Unified perception event for all senses.
    ///
    /// Design goals:
    /// - ReactionEngine can rank/filter using Interest01/Strength01/Novelty01 and Type/Sense.
    /// - Sense-specific data is optional and carried in the corresponding Details struct.
    /// - You can keep producing scent events exactly as before, but now the type supports vision too.
    /// </summary>
    public readonly struct PerceptionEvent
    {
        // who perceived this event
        public readonly WorldObject Observer;

        // Core identifiers
        public readonly PerceptionSense Sense;
        public readonly PerceptionEventType Type;

        // Where the agent believes the event is (for reactions / movement)
        public readonly Vector3 WorldPos;

        // Optional: which WorldObject this is about (vision usually fills this; scent often null)
        public readonly WorldObject? Target;

        // Normalized metrics 0..1 used for ranking / thresholds
        public readonly float Strength01;   // “how strong / salient”
        public readonly float Novelty01;    // “how new/unexpected”
        public readonly float Interest01;   // “how important overall”

        // Optional sense-specific payload
        public readonly ScentDetails? Scent;
        public readonly VisionDetails? Vision;
        public readonly SoundDetails? Sound;

        public PerceptionEvent(
            WorldObject observer,
            PerceptionSense sense,
            PerceptionEventType type,
            Vector3 worldPos,
            WorldObject? target,
            float strength01,
            float novelty01,
            float interest01,
            ScentDetails? scent = null,
            VisionDetails? vision = null,
            SoundDetails? sound = null)
        {
            Observer = observer;
            Sense = sense;
            Type = type;
            WorldPos = worldPos;
            Target = target;
            Strength01 = Mathf.Clamp01(strength01);
            Novelty01 = Mathf.Clamp01(novelty01);
            Interest01 = Mathf.Clamp01(interest01);
            Scent = scent;
            Vision = vision;
            Sound = sound;
        }

        // Convenience factory for your existing scent events (minimal call-site changes)
        public static PerceptionEvent MakeScent(
            WorldObject observer,
            PerceptionEventType type,
            Vector3 worldPos,
            string scentKey,
            ScentCategory category,
            string scentName,
            float strength01,
            float novelty01,
            float interest01)
        {
            return new PerceptionEvent(
                observer: observer,
                sense: PerceptionSense.Scent,
                type: type,
                worldPos: worldPos,
                target: null,
                strength01: strength01,
                novelty01: novelty01,
                interest01: interest01,
                scent: new ScentDetails(scentKey, category, scentName));
        }

        // Convenience factory for vision events
        public static PerceptionEvent MakeVision(
            WorldObject observer,
            PerceptionEventType type,
            Vector3 worldPos,
            WorldObject target,
            float strength01,
            float novelty01,
            float interest01,
            float distanceMeters,
            float speedMps,
            float angleDeg,
            VisionTargetKind kind,
            SocialRelation relation)
        {
            return new PerceptionEvent(
                observer: observer,
                sense: PerceptionSense.Vision,
                type: type,
                worldPos: worldPos,
                target: target,
                strength01: strength01,
                novelty01: novelty01,
                interest01: interest01,
                vision: new VisionDetails(distanceMeters, speedMps, angleDeg, kind, relation));
        }
    }

    /// <summary>Scent-specific payload.</summary>
    public readonly struct ScentDetails
    {
        public readonly string ScentKey;
        public readonly ScentCategory Category;
        public readonly string ScentName;

        public ScentDetails(string scentKey, ScentCategory category, string scentName)
        {
            ScentKey = scentKey;
            Category = category;
            ScentName = scentName;
        }
    }

    /// <summary>Vision-specific payload.</summary>
    public readonly struct VisionDetails
    {
        public readonly float DistanceMeters;
        public readonly float SpeedMps;
        public readonly float AngleDeg;
        public readonly VisionTargetKind Kind;
        public readonly SocialRelation Relation;

        public VisionDetails(float distanceMeters, float speedMps, float angleDeg, VisionTargetKind kind, SocialRelation relation)
        {
            DistanceMeters = distanceMeters;
            SpeedMps = speedMps;
            AngleDeg = angleDeg;
            Kind = kind;
            Relation = relation;
        }
    }

    /// <summary>Sound-specific payload (stub for later).</summary>
    public readonly struct SoundDetails
    {
        public readonly float Loudness01;
        public readonly float DistanceMeters;

        public SoundDetails(float loudness01, float distanceMeters)
        {
            Loudness01 = Mathf.Clamp01(loudness01);
            DistanceMeters = distanceMeters;
        }
    }

    // These enums can live here or be referenced from your VisionPerceptionModule.
    public enum VisionTargetKind { Unknown = 0, Dog, Human, Animal, Item, Threat }
    public enum SocialRelation { Self = 0, PackLeader, Packmate, NonPack }

    // Helper class to display the perception event in a human readable way.
    // Example usage
    // Debug.Log(event.ToDebugString(worldObject)} ");

    public static class PerceptionEventExtensions
    {
        public static string ToLLMLine(this PerceptionEvent e)
        {
            // Keep this intentionally compact and consistent.
            switch (e.Sense)
            {
                case PerceptionSense.Vision:
                {
                    string targetName = e.Target != null ? e.Target.DisplayName : "unknown";
                    if (!e.Vision.HasValue)
                        return $"VISION: {e.Type} {targetName}";

                    string targetId = e.Target != null ? e.Target.ObjectId.ToString() : "unknown";
                    var v = e.Vision.Value;

                    // Small “motion word” derived from type
                    string motion =
                        e.Type == PerceptionEventType.TargetMovingFast ? "running" :
                        e.Type == PerceptionEventType.TargetMoving ? "moving" :
                        e.Type == PerceptionEventType.TargetLostSight ? "lost" :
                        (e.Type == PerceptionEventType.TargetNewlySeen ? "spotted" : "seen");

                    // Dist/speed are very useful for planning, but keep formatting tight
                    return $"VISION: {motion} {targetName} entityId={targetId} ({v.Kind},{v.Relation}) dist={v.DistanceMeters:0.0}m speed={v.SpeedMps:0.0}m/s position=[{e.WorldPos.x:0},{e.WorldPos.y:0}].";
                }

                case PerceptionSense.Scent:
                {
                    if (!e.Scent.HasValue)
                        return $"SCENT: {e.Type}";

                    var s = e.Scent.Value;
                    return $"SCENT: {e.Type} {s.Category} '{s.ScentName}' strength={e.Strength01:0.00}";
                }

                default:
                    return $"{e.Sense.ToString().ToUpperInvariant()}: {e.Type}";
            }
        }

        public static List<string> ToLLMLines(this List<PerceptionEvent> events, int maxLines)
        {
            var lines = new List<string>();
            if (events == null || events.Count == 0) return lines;

            // Prefer high-interest events; stable tie-breaks
            events.Sort((a, b) =>
            {
                int c = b.Interest01.CompareTo(a.Interest01);
                if (c != 0) return c;
                c = string.Compare(a.Sense.ToString(), b.Sense.ToString(), StringComparison.Ordinal);
                if (c != 0) return c;
                return string.Compare(a.Type.ToString(), b.Type.ToString(), StringComparison.Ordinal);
            });

            for (int i = 0; i < events.Count && lines.Count < maxLines; i++)
                lines.Add(events[i].ToLLMLine());

            return lines;
        }

        public static string ToDebugString(this PerceptionEvent e)
        {
            string description = string.Empty;
            switch (e.Sense)
            {
                case PerceptionSense.Scent:
                    if (e.Scent.HasValue)
                    {
                        var s = e.Scent.Value;
                        description =
                            $"[PerceptionEvent] {e.Observer.DisplayName} noticed {e.Type} SCENT " +
                            $"{s.Category} '{s.ScentName}' " +
                            $"strength={e.Strength01:0.00} novelty={e.Novelty01:0.00} interest={e.Interest01:0.00}";
                    }
                    break;

                case PerceptionSense.Vision:
                    if (e.Vision.HasValue)
                    {
                        var v = e.Vision.Value;
                        description =
                            $"[PerceptionEvent] {e.Observer.DisplayName} saw {e.Type} " +
                            $"{v.Kind} {v.Relation} " +
                            $"dist={v.DistanceMeters:0.0}m speed={v.SpeedMps:0.0}m/s " +
                            $"interest={e.Interest01:0.00}";
                    }
                    break;

                default:
                    description =
                        $"[PerceptionEvent] {e.Observer.DisplayName} perceived {e.Type} " +
                        $"interest={e.Interest01:0.00}";
                    break;
            }
            return description;
        }

        public static string AllEventsToDebugString(this List<PerceptionEvent> events)
        {
            string description = string.Empty;
            if (events==null || events.Count==0)
            {
                description = "[PerceptionEvent] No events in list";
                return description;
            }
            foreach (PerceptionEvent e in events)
            {
                var eventDescription = e.ToDebugString();
                description = description + Environment.NewLine + eventDescription;
            }
            return description;
        }
    }
}