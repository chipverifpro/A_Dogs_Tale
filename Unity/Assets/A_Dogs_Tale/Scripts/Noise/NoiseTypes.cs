using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.Noise
{
    // -------------------------
    // Core enums
    // -------------------------

    public enum NoiseCategory
    {
        Voice = 0,
        Movement = 1,
        Impact = 2,
        Mechanism = 3,
        Ambient = 4,
        Other = 5
    }

    public enum NoiseSubtype
    {
        // Voice
        Bark,
        Growl,
        Whine,
        Yelp,
        HumanTalk,
        HumanShout,

        // Movement
        FootstepWalk,
        FootstepRun,
        SneakStep,
        Scurry,

        // Impact
        ObjectDropSmall,
        ObjectDropHeavy,
        GlassClink,
        GlassShatter,
        LandThud,
        BodyBump,

        // Mechanism
        DoorOpen,
        DoorClose,
        DoorSlam,
        LockClick,

        // Ambient
        WindGust,
        DistantTraffic,
        Birds,

        // Other / fallback
        Unknown
    }

    [Flags]
    public enum NoiseSemanticTags
    {
        None = 0,

        // High-level meaning
        Alert = 1 << 0,
        Distress = 1 << 1,
        Threat = 1 << 2,
        Investigation = 1 << 3,
        Social = 1 << 4,
        PackSignal = 1 << 5,
        AccessChange = 1 << 6,

        // Surface/material hints (optional)
        OnWood = 1 << 10,
        OnConcrete = 1 << 11,
        OnGrass = 1 << 12,
        OnMetal = 1 << 13,
    }

    public enum NoiseSpeechAct
    {
        Neutral = 0,
        Call = 1,
        Warn = 2,
        Praise = 3,
        Scold = 4,
        Threaten = 5,
        Request = 6
    }

    public enum VoiceTargetingMode
    {
        Unknown = 0,
        Broadcast = 1,
        Directed = 2
    }

    [Flags]
    public enum VoiceTargetHint
    {
        None = 0,
        Pack = 1 << 0,
        Player = 1 << 1,
        AnyDog = 1 << 2,
        Human = 1 << 3,
        SpecificName = 1 << 4
    }

    public enum RoomRelation
    {
        Unknown = 0,
        SameRoom = 1,
        Adjacent = 2,
        Different = 3
    }

    public enum DistanceBand
    {
        Near = 0,
        Mid = 1,
        Far = 2
    }

    public enum SourceAttributionType
    {
        Unknown = 0,
        KnownEmitter = 1,
        GuessedEmitter = 2
    }

    // -------------------------
    // Profiles / authoring data
    // -------------------------

    /// <summary>
    /// Authoring preset for a particular noise type (bark, footstep, door slam, etc).
    /// This is what your NoiseMakerModule uses to emit consistent events.
    /// </summary>
    [Serializable]
    public struct NoiseProfile
    {
        public string profileId;                  // Stable string key, e.g. "Dog.Bark", "Door.Slam"
        public NoiseCategory category;
        public NoiseSubtype subtype;
        public NoiseSemanticTags semanticTags;

        public float sourceLoudnessAtOneMeter;    // Your "raw loudness" at 1m (arbitrary units)
        public float effectiveRangeHintMeters;    // For fast culling (0 = ignore)
        public int priority;                      // Higher = more important, can override thresholds
        public float impulseIntervalSeconds;      // For sustained via impulses (e.g. talk every 0.5s). 0 => one-shot

        public bool IsValid => !string.IsNullOrWhiteSpace(profileId);
    }

    // -------------------------
    // Voice intent / directed speech
    // -------------------------

    /// <summary>
    /// Extra data for voice events: content + intended target information.
    /// Stored on NoiseEvent when known; listeners can also infer targeting when unknown.
    /// </summary>
    [Serializable]
    public struct VoiceIntentData
    {
        public string contentShort;               // Keep short. Intended for LLM context / debug.
        public NoiseSpeechAct speechAct;

        public VoiceTargetingMode targetingMode;

        // Intended target identity
        public WorldObject targetRef;             // nullable reference to intended target (can be null)
        public int targetId;                      // stable id if you have one; 0/negative if unknown
        public VoiceTargetHint targetHintFlags;

        // How clear the targeting/content was at emission time (0..1)
        public float clarity;

        public bool HasAnyData =>
            !string.IsNullOrWhiteSpace(contentShort) ||
            targetingMode != VoiceTargetingMode.Unknown ||
            targetRef != null ||
            targetId != -1 ||
            targetHintFlags != VoiceTargetHint.None;
    }

    // -------------------------
    // Global truth event (stored in NoiseManager)
    // -------------------------

    /// <summary>
    /// A single noise emission event, appended into NoiseManager's recent buffer.
    /// This is "ground truth" of what happened, not what any agent perceived.
    /// </summary>
    [Serializable]
    public struct NoiseEvent
    {
        // Identity
        public ulong noiseId;                 // Unique monotonic id assigned by NoiseManager
        public float timeSeconds;             // Time.time when emitted

        // Emitter identity
        public int emitterId;                 // stable id (recommended)
        public WorldObject emitterRef;        // nullable WorldObject reference

        // What
        public NoiseCategory category;
        public NoiseSubtype subtype;
        public NoiseSemanticTags semanticTags;

        // Where
        public Vector3 position;
        public int roomId;                    // -1 if unknown/unresolved at emission

        // Strength
        public float sourceLoudnessAtOneMeter;
        public float effectiveRangeHintMeters;
        public int priority;

        // Optional: for rare future linger use; usually 0 for impulses
        public float impulseDurationSeconds;

        // Optional voice data (only meaningful when category == Voice)
        public VoiceIntentData voiceIntent;

        // Authoring/debug
        public string profileId;

        public bool HasEmitterRef => emitterRef != null;
        public bool HasVoiceIntent => category == NoiseCategory.Voice && voiceIntent.HasAnyData;
    }

    // -------------------------
    // Per-listener perceived event (produced by HearingModule)
    // -------------------------

    [Serializable]
    public struct HeardNoise
    {
        // Reference to original event
        public ulong noiseId;
        public float timeHeardSeconds;            // when we processed it
        public float timeAgoSeconds;              // derived: now - eventTime (filled by HearingModule)

        // Classification
        public NoiseCategory category;
        public NoiseSubtype subtype;
        public NoiseSemanticTags semanticTags;

        // Perception outputs
        public float perceivedLoudness01;         // 0..1 normalized
        public float audibilityScore;             // ranking metric (can exceed 1)
        public float distanceMeters;
        public DistanceBand distanceBand;
        public Vector3 directionToSource;         // normalized vector listener->source (if distance > 0)

        // Environment
        public int sourceRoomId;
        public RoomRelation roomRelation;

        /// <summary>
        /// 0..1 where 0 = clear LOS, 1 = fully occluded (convention).
        /// </summary>
        public float occlusion01;

        /// <summary>
        /// Overall certainty that our perception/classification is reliable (0..1).
        /// </summary>
        public float confidence01;

        // Attribution / knowledge
        public SourceAttributionType attributionType;
        public int attributedEmitterId;           // stable id if known/guessed; -1 if unknown
        public WorldObject attributedEmitterRef;  // may be null even if id exists
        public float attributionConfidence01;

        // Voice: "was it for me?"
        public bool isIntendedForMe;
        public float intendedConfidence01;
        public NoiseSpeechAct speechAct;          // if known or inferred
        public string heardContentShort;          // optionally degraded; keep short
        public string notesShort;                 // muffled, echoing, approaching, repeated footsteps xN
    }

    // -------------------------
    // Knowledge / learning data (per listener, persistent-ish)
    // -------------------------

    [Serializable]
    public struct NoiseSignatureKey : IEquatable<NoiseSignatureKey>
    {
        public NoiseCategory category;
        public NoiseSubtype subtype;

        public NoiseSignatureKey(NoiseCategory category, NoiseSubtype subtype)
        {
            this.category = category;
            this.subtype = subtype;
        }

        public bool Equals(NoiseSignatureKey other) => category == other.category && subtype == other.subtype;
        public override bool Equals(object obj) => obj is NoiseSignatureKey other && Equals(other);
        public override int GetHashCode() => ((int)category * 397) ^ (int)subtype;
        public override string ToString() => $"{category}/{subtype}";
    }

    /// <summary>
    /// Listener-specific knowledge about a particular emitter: familiarity, labels, last heard.
    /// </summary>
    [Serializable]
    public class NoiseKnowledgeEntry
    {
        public int emitterId;
        public WorldObject emitterRef;                // may become null if destroyed
        public float familiarity01;                   // grows with repeated hearing, decays over time
        public float lastHeardTimeSeconds;

        public Vector3 lastHeardPosition;
        public bool hasLastHeardPosition;

        // Optional labels (e.g. "mailman", "neighbor dog")
        public string learnedLabel;

        // Familiarity per sound signature (bark vs footstep etc.)
        // Key: category/subtype
        public Dictionary<NoiseSignatureKey, float> signatureFamiliarity01 = new();

        public NoiseKnowledgeEntry(int emitterId, WorldObject emitterRef)
        {
            this.emitterId = emitterId;
            this.emitterRef = emitterRef;
            familiarity01 = 0f;
            lastHeardTimeSeconds = -9999f;
            lastHeardPosition = Vector3.zero;
            hasLastHeardPosition = false;
            learnedLabel = string.Empty;
        }
    }

    // -------------------------
    // LLM payload (compact)
    // -------------------------

    [Serializable]
    public struct HeardNoiseForLLM
    {
        public float timeAgoSeconds;
        public string type;                     // "Voice/Bark"
        public float loudness01;
        public string direction;                // "front-right" etc (string token is fine)
        public string distance;                 // "near"|"mid"|"far"
        public string room;                     // "same"|"adjacent"|"different"|"unknown"
        public string source;                   // "known:Name" | "guess:Label" | "unknown"
        public float confidence01;

        // Voice extras
        public bool addressedToMe;
        public float addressedConfidence01;
        public string heardWordsShort;
        public string speechAct;                // enum string

        public string notesShort;
        public string tags;                     // comma-separated tags (or keep as list elsewhere)
    }

    [Serializable]
    public struct NoiseSummaryForLLM
    {
        public int listenerAgentId;
        public int listenerRoomId;
        public string listenerState;            // "idle/alert/sleeping" etc (keep simple)

        public float timeWindowSeconds;
        public List<HeardNoiseForLLM> heard;    // capped list (e.g., 8)
    }

}