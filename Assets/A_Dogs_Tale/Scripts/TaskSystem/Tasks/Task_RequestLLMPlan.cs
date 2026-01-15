#nullable enable
using System;
using UnityEngine;
using DogGame.LLM;
using DogGame.LLM.Core;

namespace DogGame.Tasks
{
    /// <summary>
    /// Fire-and-forget request to the LLM planner. Completes immediately.
    /// The response is applied later through the normal LLM plan pipeline.
    /// </summary>
    public sealed class Task_RequestLLMPlan : IAgentTask
    {
        public string DebugName => $"RequestLLMPlan({tag ?? "generic"}, {urgency}, {applyMode})";

        private readonly string prompt;
        private readonly Vector2Int? eventCell;
        private readonly Vector3? eventWorld;
        private readonly string? tag;

        private readonly LLMPlanUrgency urgency;
        private readonly LLMApplyMode applyMode;

        private bool submitted;

        public Task_RequestLLMPlan(
            string prompt,
            Vector2Int? eventCell = null,
            Vector3? eventWorld = null,
            LLMPlanUrgency urgency = LLMPlanUrgency.Normal,
            LLMApplyMode applyMode = LLMApplyMode.Append,
            string? tag = null)
        {
            this.prompt = string.IsNullOrWhiteSpace(prompt) ? "Unexpected situation. Provide updated plan." : prompt.Trim();
            this.eventCell = eventCell;
            this.eventWorld = eventWorld;
            this.urgency = urgency;
            this.applyMode = applyMode;
            this.tag = tag;
        }

        public void Start(TaskContext context) { }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            if (submitted)
                return TaskTickResult.Succeeded();

            submitted = true;

            // Find planner/scheduler. Choose ONE:
            // A) via Directory (recommended if you already have it globally)

            // TODO: this is really broken, cannot figure out what planner is, so just put null and let it abort for now so it will compile.
            //var planner = context.Agent.scheduler as ILLMPlanner; // adjust to your actual field
            ILLMPlanner? planner = null;
            if (planner == null)
            {
                Debug.LogWarning($"[{context.AgentId}] No ILLMPlanner available. Request ignored.");
                return TaskTickResult.Failed("missing_llm_planner");
            }

            var request = new LLMPlanRequestOnDemand
            (
                agentId: context.AgentId,
                prompt: prompt,
                eventCell: eventCell,
                eventWorld: eventWorld,
                urgency: urgency,
                applyMode: applyMode,
                tag: tag,
                sophistication: Sophistication.Low,         // choose appropriate tier
                onResponseJson: result => Debug.Log(result)    // or pass null if callback is optional
            );

            string requestId = planner.SubmitPlanRequest(request);
            Debug.Log($"[{context.AgentId}] LLM plan requested id={requestId} tag={tag} urgency={urgency} apply={applyMode}");

            // Completes immediately; response arrives later.
            return TaskTickResult.Succeeded();
        }

        public void Stop(TaskContext context) { }
    }
}