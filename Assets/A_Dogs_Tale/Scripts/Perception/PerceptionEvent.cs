#nullable enable
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
}