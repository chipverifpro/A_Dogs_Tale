using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.AI
{
    #region enums
    // Keep names stable for save-files and tuning tables.
    public enum MotivationKind
    {
        PackLoyalty,
        Obedience,
        ScentTracking,
        SoundReactivity,
        HumanAttachment,
        Hunger,
        PreyDrive,
        Fear,
        Curiosity,
        Playfulness
    }

    // Where the stimulus is coming from (useful for filtering / debug UI).
    [Flags]
    public enum StimulusChannel
    {
        None   = 0,
        Scent  = 1 << 0,
        Sound  = 1 << 1,
        Sight  = 1 << 2,
        Touch  = 1 << 3,
        Memory = 1 << 4,
        InternalState = 1 << 5
    }
    #endregion

    #region TrainingProfile
    // Training can be a single number, or split into skills.
    [Serializable]
    public struct TrainingProfile
    {
        [Range(0f, 1f)] public float obedience;        // command priority / override capability
        [Range(0f, 1f)] public float focus;            // suppression of distractions
        [Range(0f, 1f)] public float scentDiscipline;  // ignore junk scents / track on command
        [Range(0f, 1f)] public float noiseDiscipline;  // ignore noises unless relevant
        [Range(0f, 1f)] public float bravery;          // fear threshold / faster recovery

        // Handy "one knob" if you want it:
        public float Overall => Mathf.Clamp01(
            (obedience + focus + scentDiscipline + noiseDiscipline + bravery) / 5f);
    }
    #endregion

    #region Inputs
    // Raw "what's happening right now" signals the world model can provide each tick.
    // You can expand this as your game grows.
    [Serializable]
    public struct MotivationInputs
    {
        // Internal states (0..1)
        [Range(0f, 1f)] public float hunger;
        [Range(0f, 1f)] public float fatigue;
        [Range(0f, 1f)] public float stress;

        // Pack / social
        [Range(0f, 1f)] public float packSeparation;     // 0 near pack, 1 far from pack centroid/leader
        [Range(0f, 1f)] public float packDistress;       // danger signals, ally hurt, barking, etc.
        [Range(0f, 1f)] public float humanProximity;     // favored human visible/nearby

        // Sensory events (0..1)
        [Range(0f, 1f)] public float scentStrength;      // strongest salient scent
        [Range(0f, 1f)] public float scentNovelty;       // how new/interesting it is
        [Range(0f, 1f)] public float soundIntensity;     // salient sound intensity
        [Range(0f, 1f)] public float soundFamiliarity;   // 1 = very familiar / recognized

        // Prey / motion / threats
        [Range(0f, 1f)] public float preyCue;            // small fast movement / prey scent
        [Range(0f, 1f)] public float threatProximity;    // nearby threat
        [Range(0f, 1f)] public float healthLow;          // 0 healthy, 1 critical

        // Commands (0..1)
        [Range(0f, 1f)] public float commandStrength;    // recognized command confidence / urgency

        // Context flags
        public bool isStealthPhase;
        public bool isInCombat;
        public bool isOnMissionCriticalTask;
    }
    #endregion

    #region Training Influence
    [Serializable]
    public struct TrainingInfluence
    {
        // Which training dimension affects this motivation most.
        // (Keep as weights; you can tune in inspector.)
        [Range(0f, 2f)] public float obedienceWeight;
        [Range(0f, 2f)] public float focusWeight;
        [Range(0f, 2f)] public float scentDisciplineWeight;
        [Range(0f, 2f)] public float noiseDisciplineWeight;
        [Range(0f, 2f)] public float braveryWeight;

        public float EvaluateSuppression(in TrainingProfile training)
        {
            // Linear combo; clamp so designers don't accidentally go nuts.
            float suppression =
                training.obedience       * obedienceWeight +
                training.focus           * focusWeight +
                training.scentDiscipline * scentDisciplineWeight +
                training.noiseDiscipline * noiseDisciplineWeight +
                training.bravery         * braveryWeight;

            return Mathf.Clamp01(suppression / 3f); // normalize-ish; tune divisor per your taste
        }
    }
    #endregion

    #region Tuning
    [Serializable]
    public struct MotivationTuning
    {
        public MotivationKind kind;

        [Header("Base Response")]
        [Range(0f, 5f)] public float baseWeight;     // how inherently strong this drive is
        [Range(0f, 1f)] public float threshold;      // minimum urge needed to matter
        [Range(0f, 1f)] public float hysteresis;     // reduces flip-flopping near threshold

        [Header("Channels")]
        public StimulusChannel channels;

        [Header("Training Effects")]
        public TrainingInfluence trainingInfluence;

        [Header("Context Multipliers")]
        [Range(0f, 2f)] public float stealthMultiplier;        // if isStealthPhase
        [Range(0f, 2f)] public float combatMultiplier;         // if isInCombat
        [Range(0f, 2f)] public float missionCriticalMultiplier;// if isOnMissionCriticalTask
    }
    #endregion

    #region State
    [Serializable]
    public struct MotivationState
    {
        // Persistent state per motivation (memory / momentum).
        [Range(0f, 1f)] public float momentum;  // rises when indulged, decays over time
        [Range(0f, 1f)] public float lastUrge;  // for hysteresis / debugging
    }
    #endregion

    #region Evaluation
    public struct MotivationEvaluation
    {
        public MotivationKind kind;
        public float stimulus;         // 0..1 raw stimulus score (before weights)
        public float suppression;      // 0..1 from training
        public float contextMultiplier;// 0..2
        public float urge;             // final 0..1
        public bool isActive;          // urge above threshold/hysteresis
        public StimulusChannel channels;
    }
    #endregion

    #region MotivationModel

    // Put this on a ScriptableObject if you want tuning assets per dog/breed/archetype.
    [Serializable]
    public class MotivationModel
    {
        public List<MotivationTuning> tunings = new();
        public Dictionary<MotivationKind, MotivationState> states = new();

        public void EnsureInitialized()
        {
            foreach (var tuning in tunings)
            {
                if (!states.ContainsKey(tuning.kind))
                    states[tuning.kind] = new MotivationState { momentum = 0f, lastUrge = 0f };
            }
        }

        public MotivationEvaluation Evaluate(
            MotivationKind kind,
            in MotivationInputs inputs,
            in TrainingProfile training)
        {
            // Find tuning (fast path: you can cache an array by enum index).
            var tuning = tunings.Find(t => t.kind == kind);

            states.TryGetValue(kind, out var motivationState);

            float stimulus = ComputeStimulus(kind, inputs);
            float suppression = tuning.trainingInfluence.EvaluateSuppression(training);
            float contextMultiplier = ComputeContextMultiplier(tuning, inputs);

            // Momentum biases future urges; feel free to invert for some drives.
            float momentumBoost = Mathf.Lerp(1f, 1.25f, motivationState.momentum);

            // Core urge formula.
            float raw = stimulus * tuning.baseWeight * contextMultiplier * momentumBoost;

            // Suppression reduces raw urge; focus/obedience generally increases suppression.
            raw *= (1f - suppression);

            // Normalize to 0..1 for comparisons (designers can tune baseWeight).
            float urge = Mathf.Clamp01(raw);

            // Hysteresis: once active, stay active a bit longer to reduce thrashing.
            float activationThreshold = tuning.threshold;
            float deactivationThreshold = Mathf.Clamp01(tuning.threshold - tuning.hysteresis);

            bool wasActive = motivationState.lastUrge >= activationThreshold;
            bool isActive = wasActive ? (urge >= deactivationThreshold) : (urge >= activationThreshold);

            // Update last urge
            motivationState.lastUrge = urge;
            states[kind] = motivationState;

            return new MotivationEvaluation
            {
                kind = kind,
                stimulus = stimulus,
                suppression = suppression,
                contextMultiplier = contextMultiplier,
                urge = urge,
                isActive = isActive,
                channels = tuning.channels
            };
        }

        // Call this when a behavior "indulges" a motivation (e.g., the dog chased prey).
        public void RewardMomentum(MotivationKind kind, float amount01)
        {
            if (!states.TryGetValue(kind, out var motivationState))
                return;

            motivationState.momentum = Mathf.Clamp01(motivationState.momentum + amount01);
            states[kind] = motivationState;
        }

        // Call this each tick.
        public void DecayMomentum(float deltaTime, float decayPerSecond = 0.15f)
        {
            var keys = new List<MotivationKind>(states.Keys);
            foreach (var kind in keys)
            {
                var motivationState = states[kind];
                motivationState.momentum = Mathf.Clamp01(motivationState.momentum - decayPerSecond * deltaTime);
                states[kind] = motivationState;
            }
        }

        private static float ComputeContextMultiplier(in MotivationTuning tuning, in MotivationInputs inputs)
        {
            float multiplier = 1f;

            if (inputs.isStealthPhase)          multiplier *= tuning.stealthMultiplier;
            if (inputs.isInCombat)              multiplier *= tuning.combatMultiplier;
            if (inputs.isOnMissionCriticalTask) multiplier *= tuning.missionCriticalMultiplier;

            return multiplier;
        }

        // This is where your world model maps to 0..1 for each motivation.
        private static float ComputeStimulus(MotivationKind kind, in MotivationInputs inputs)
        {
            switch (kind)
            {
                case MotivationKind.PackLoyalty:
                    return Mathf.Clamp01(Mathf.Max(inputs.packSeparation, inputs.packDistress));

                case MotivationKind.Obedience:
                    return inputs.commandStrength;

                case MotivationKind.ScentTracking:
                    // Strong + novel scent is most distracting.
                    return Mathf.Clamp01(inputs.scentStrength * Mathf.Lerp(0.6f, 1.2f, inputs.scentNovelty));

                case MotivationKind.SoundReactivity:
                    // Loud unfamiliar sounds are more disruptive.
                    float unfamiliar = 1f - inputs.soundFamiliarity;
                    return Mathf.Clamp01(inputs.soundIntensity * Mathf.Lerp(0.7f, 1.3f, unfamiliar));

                case MotivationKind.HumanAttachment:
                    return inputs.humanProximity;

                case MotivationKind.Hunger:
                    return inputs.hunger;

                case MotivationKind.PreyDrive:
                    return inputs.preyCue;

                case MotivationKind.Fear:
                    // Threat proximity + low health drives fear.
                    return Mathf.Clamp01(Mathf.Max(inputs.threatProximity, inputs.healthLow));

                case MotivationKind.Curiosity:
                    // Curiosity can be tied to novelty (scent) + not-in-combat.
                    float combatDamp = inputs.isInCombat ? 0.6f : 1f;
                    return Mathf.Clamp01(inputs.scentNovelty * combatDamp);

                case MotivationKind.Playfulness:
                    // Play drops when stressed/hungry/in combat.
                    float play = 1f - Mathf.Max(inputs.stress, inputs.hunger);
                    if (inputs.isInCombat) play *= 0.4f;
                    return Mathf.Clamp01(play);

                default:
                    return 0f;
            }
        }
    }
    #endregion
}