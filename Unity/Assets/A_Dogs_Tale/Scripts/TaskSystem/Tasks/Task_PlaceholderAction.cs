#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_PlaceholderAction : IAgentTask
    {
        public string DebugName => $"PlaceholderAction({actionName})";

        private readonly string actionName;
        private readonly string reasoning;
        private readonly string? targetSummary;
        private readonly string? detail;

        public Task_PlaceholderAction(string actionName, string reasoning, string? targetSummary = null, string? detail = null)
        {
            this.actionName = string.IsNullOrWhiteSpace(actionName) ? "unknown_action" : actionName.Trim();
            this.reasoning = string.IsNullOrWhiteSpace(reasoning) ? "No reasoning provided." : reasoning.Trim();
            this.targetSummary = string.IsNullOrWhiteSpace(targetSummary) ? null : targetSummary.Trim();
            this.detail = string.IsNullOrWhiteSpace(detail) ? null : detail.Trim();
        }

        public void Start(TaskContext context)
        {
            context.Blackboard.SetString("llm.placeholder.lastAction", actionName);
            context.Blackboard.SetString("llm.placeholder.lastReasoning", reasoning);

            if (!string.IsNullOrWhiteSpace(targetSummary))
                context.Blackboard.SetString("llm.placeholder.lastTarget", targetSummary!);

            if (!string.IsNullOrWhiteSpace(detail))
                context.Blackboard.SetString("llm.placeholder.lastDetail", detail!);

            Debug.LogWarning(
                $"[{context.AgentId}] Placeholder action executed: action={actionName}, target={targetSummary ?? "none"}, detail={detail ?? "none"}, reasoning={reasoning}");
        }

        public TaskTickResult Tick(TaskContext context, float dt)
        {
            return TaskTickResult.Succeeded();
        }

        public void Stop(TaskContext context) { }
    }
}
