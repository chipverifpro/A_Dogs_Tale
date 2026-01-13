#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM.Policy;
using UnityEngine;

namespace DogGame.LLM.Agent
{
    /// <summary>
    /// Dynamic, self-updating context for the LLM.
    /// This module should not require per-agent babysitting: it should populate itself
    /// from game systems (player, combat, perception, etc.).
    ///
    /// Start simple: you (or other systems) can set the public fields directly.
    /// Later you can add RefreshFromGameSystems() to auto-fill from your WorldObject framework.
    /// </summary>
    public sealed class LLMWorldStateModule : MonoBehaviour
    {
        [Header("Perception / Signals (auto-populate at runtime)")]
        [Tooltip("Approx distance to player in meters.")]
        public float distanceToPlayerMeters = 999f;

        [Tooltip("True if this agent is currently engaged or threatened.")]
        public bool isInCombat = false;

        [Tooltip("True if this agent is quest/story critical (should think harder).")]
        public bool isQuestCritical = false;

        [Tooltip("True if the player is explicitly focusing this agent (selected/targeted/looked-at).")]
        public bool isPlayerFocusingThisNpc = false;

        [Tooltip("Rough nearby entity count used as a complexity signal.")]
        public int nearbyEntityCount = 0;

        [Header("Context Summaries (auto-populate at runtime)")]
        [TextArea(3, 12)]
        [Tooltip("Short bullet-ish summary of nearby entities/objects/hazards.")]
        public string nearbySummary = "";

        [TextArea(2, 10)]
        [Tooltip("Short summary of agent state: health, stamina, status effects, inventory highlights.")]
        public string statusSummary = "";

        [TextArea(2, 10)]
        [Tooltip("Short summary of current goals/intent framing, if you want it injected as context.")]
        public string goalsSummary = "";

        [TextArea(2, 10)]
        [Tooltip("Very recent events relevant to this agent, if any.")]
        public string recentEventsSummary = "";

        [Header("Context Controls")]
        [Tooltip("Caps the length of each summary block to reduce prompt bloat.")]
        [Range(100, 4000)]
        public int maxCharsPerBlock = 800;

        /// <summary>
        /// Build the inputs used by SophisticationPolicy.
        /// </summary>
        public SophisticationPolicy.Inputs BuildSophisticationInputs(bool isBoss)
        {
            return new SophisticationPolicy.Inputs
            {
                distanceToPlayerMeters = distanceToPlayerMeters,
                isInCombat = isInCombat,
                isQuestCritical = isQuestCritical,
                isBoss = isBoss,
                isPlayerFocusingThisNpc = isPlayerFocusingThisNpc,
                nearbyEntityCount = Mathf.Max(0, nearbyEntityCount)
            };
        }

        /// <summary>
        /// Add dynamic context blocks to the prompt.
        /// Keep these factual and compact (perception, state, goals), not "plans".
        /// </summary>
        public void AddContextBlocks(List<string> contextBlocks)
        {
            if (contextBlocks == null) throw new ArgumentNullException(nameof(contextBlocks));

            TryAddBlock(contextBlocks, "CONTEXT: Nearby", nearbySummary);
            TryAddBlock(contextBlocks, "CONTEXT: Status", statusSummary);
            TryAddBlock(contextBlocks, "CONTEXT: Goals", goalsSummary);
            TryAddBlock(contextBlocks, "CONTEXT: Recent Events", recentEventsSummary);
        }

        private void TryAddBlock(List<string> blocks, string title, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return;

            string trimmedBody = body.Trim();

            if (maxCharsPerBlock > 0 && trimmedBody.Length > maxCharsPerBlock)
                trimmedBody = trimmedBody.Substring(0, maxCharsPerBlock) + "…";

            blocks.Add($"{title}\n{trimmedBody}");
        }

        // --------------------------------------------------------------------
        // Optional expansion point:
        // Add a method you can call from a manager/perception system each tick,
        // or from Update() at a throttled rate.
        // --------------------------------------------------------------------

        /// <summary>
        /// Optional hook: populate fields from your game's systems (player, combat, perception).
        /// Not implemented yet because it depends on your project's architecture.
        /// </summary>
        public void RefreshFromGameSystems()
        {
            // Example future steps:
            // - Find player position and set distanceToPlayerMeters
            // - Pull combat state from your CombatModule
            // - Build nearbySummary from sensed entities list
            // - Summarize health/stamina/status into statusSummary
        }
    }
}