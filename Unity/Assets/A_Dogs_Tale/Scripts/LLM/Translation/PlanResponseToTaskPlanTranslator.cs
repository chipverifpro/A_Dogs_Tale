#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using DogGame.LLM;                 // PlanResponseV1, PlanIntentionV1, PlanIntentionType
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DogGame.LLM.Translation
{
    /// <summary>
    /// Converts a validated PlanResponseV1 into a TaskPlan (TaskNode graph).
    /// This is the translator layer between LLM output and TaskSystem execution.
    /// </summary>
    public sealed class PlanResponseToTaskPlanTranslator
    {
        private readonly Dictionary<PlanIntentionType, IIntentionTranslator> translators =
            new();

        public PlanResponseToTaskPlanTranslator()
        {
            // Register defaults. You can replace any of these with game-specific ones.
            Register(new NoopTranslator());
            Register(new SetGoalTranslator());
            Register(new AddTaskTranslator());
            Register(new ProposeDialogueTranslator());
            Register(new RequestObservationTranslator());
            Register(new ProposeTrapTranslator());
            Register(new UpdateBeliefsTranslator());
        }

        public void Register(IIntentionTranslator translator)
        {
            if (translator == null) throw new ArgumentNullException(nameof(translator));
            translators[translator.Type] = translator;
        }

        public TaskPlan Translate(PlanResponseV1 response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            var plan = new TaskPlan
            {
                requestId = response.RequestId ?? "",
                agentId = response.AgentId ?? ""
            };

            // Sort intentions highest priority first (your parser enforces 0..1)
            var sorted = new List<PlanIntentionV1>(response.Intentions ?? new List<PlanIntentionV1>());
            sorted.Sort((a, b) => (b?.Priority ?? 0f).CompareTo(a?.Priority ?? 0f));

            foreach (var intention in sorted)
            {
                if (intention == null) continue;

                if (!translators.TryGetValue(intention.Type, out var translator))
                {
                    // Unknown type -> safe fallback: wait briefly.
                    plan.rootNodes.Add(TaskNodes.WaitSeconds(0.2f, note: $"Unknown intention type: {intention.Type}"));
                    continue;
                }

                var nodes = translator.Translate(intention);
                if (nodes == null || nodes.Count == 0)
                {
                    plan.rootNodes.Add(TaskNodes.WaitSeconds(0.1f, note: $"Translator produced no nodes for {intention.Type}"));
                    continue;
                }

                plan.rootNodes.AddRange(nodes);
            }

            // Guarantee at least one node (your parser requires intentions non-empty; but translator might filter)
            if (plan.rootNodes.Count == 0)
                plan.rootNodes.Add(TaskNodes.WaitSeconds(0.2f, note: "Empty plan fallback"));

            UnityEngine.Debug.Log($"LLMWalkthrough4: PlanResponseToTaskPlanTrasnslator.Translate: Task Plan graph: (TBD)");

            return plan;
        }
    }

    public interface IIntentionTranslator
    {
        PlanIntentionType Type { get; }
        List<TaskNode> Translate(PlanIntentionV1 intention);
    }
}