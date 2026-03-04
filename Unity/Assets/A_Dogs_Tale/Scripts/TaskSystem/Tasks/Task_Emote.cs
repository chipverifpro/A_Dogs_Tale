#nullable enable
using UnityEngine;
using DogGame.LLM;

namespace DogGame.Tasks
{
    public sealed class Task_Emote : IAgentTask
    {
        public string DebugName => $"Emote({emote})";
        private readonly string emote;

        public Task_Emote(string emote)
        {
            this.emote = string.IsNullOrWhiteSpace(emote) ? "emote" : emote.Trim();
        }

        public void Start(TaskContext context)
        {
            Debug.Log($"[{context.AgentId}] EMOTE: {emote}");
        }

        public TaskTickResult Tick(TaskContext context, float deltaTimeSeconds)
        {
            return TaskTickResult.Succeeded();
        }

        public void Stop(TaskContext context) { }
    }
}