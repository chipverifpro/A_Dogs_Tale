#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using DogGame.Reactions;
using DogGame.LLM;
using DogGame.Tasks;
using DogGame.Perception;
using DogGame.Modules;

namespace DogGame.Routines
{
    /// <summary>
    /// Runtime registry for routines + their stats.
    /// v1: populated in Awake with code-defined routines.
    /// Later: load from ScriptableObjects/JSON and allow LLM to add/modify.
    /// </summary>
    [DefaultExecutionOrder(-850)]
    public sealed class RoutineLibrary : MonoBehaviour
    {
        public static RoutineLibrary? Instance { get; private set; }

        private readonly Dictionary<string, RoutineDefinition> routines = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DogGame.Routines.RoutineStats> stats = new(StringComparer.Ordinal);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            RegisterBuiltins();
        }

        public bool TryGet(string routineId, out RoutineDefinition def) =>
            routines.TryGetValue(routineId, out def!);

        public RoutineStats GetStats(string routineId)
        {
            if (!stats.TryGetValue(routineId, out var s))
            {
                s = new RoutineStats();
                stats[routineId] = s;
            }
            return s;
        }

        public void Register(RoutineDefinition def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (string.IsNullOrWhiteSpace(def.Id)) throw new ArgumentException("Routine id required.");

            routines[def.Id] = def;
            _ = GetStats(def.Id);
        }

        /// <summary>
        /// Expand routine (including nested routine calls), then build a single TaskRequest.
        /// Use Tag="routine:&lt;id&gt;" so executor can record outcomes.
        /// </summary>
        public bool TryBuildRoutineRequest(
            string routineId,
            TaskContext context,
            PerceptionEvent? evt,
            int priority,
            TaskSource source,
            bool canInterrupt,
            bool resumePrevious,
            bool clearStackOnStart,
            out TaskRequest request,
            out string? error)
        {
            request = default;
            error = null;

            if (!TryGet(routineId, out var def))
            {
                error = $"Routine '{routineId}' not found.";
                return false;
            }

            // Expand nested routine calls (composition)
            if (!RoutineExpander.TryExpand(def.Body, this, maxDepth: 5, maxNodes: 250, out var expanded, out error))
                return false;
                
            if (evt==null) return false;

            // Build concrete task
            if (!TaskSpecFactory.TryBuildTask(expanded, context.Agent, (PerceptionEvent)evt, out var builtTask, out error))
                return false;

            request = new TaskRequest(
                task: builtTask!,
                priority: priority,
                source: source,
                canInterrupt: canInterrupt,
                resumePrevious: resumePrevious,
                clearStackOnStart: clearStackOnStart,
                tag: $"routine:{routineId}"
            );

            return true;
        }

        /// <summary>Called by executor when a routine-tagged request finishes.</summary>
        public void RecordOutcomeFromTag(string tag, bool succeeded, float durationSeconds, string? failureReason)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            // Expect "routine:<id>"
            const string prefix = "routine:";
            if (!tag.StartsWith(prefix, StringComparison.Ordinal))
                return;

            string id = tag.Substring(prefix.Length);
            if (string.IsNullOrWhiteSpace(id))
                return;

            GetStats(id).Record(succeeded, durationSeconds, failureReason);
        }

        // ---------------- Built-in routines (starter set) ----------------

        private void RegisterBuiltins()
        {
            // Example: "steal_basic" and "collector_basic" will be added in the next step
            // once Move/IfNoticed/Take/Bury exist.
            //
            // For now, register something simple to prove pipeline:
            Register(new RoutineDefinition(
                id: "curious_bark_sniff",
                body: TS.Sequence(
                    TS.Bark(6),
                    TS.Try(TS.Sniff(1.0f), TS.Wait(0.1f))
                ),
                description: "Simple reaction: bark then sniff; wait briefly if sniff fails.",
                tags: new[] { "starter", "reaction" }
            ));
        }
    }
}