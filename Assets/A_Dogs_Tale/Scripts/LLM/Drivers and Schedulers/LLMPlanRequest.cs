using System;
using DogGame.LLM.Core;
using UnityEngine;

namespace DogGame.LLM
{
    // ---------------------------------------------

    public enum LLMPlanUrgency { Low, Normal, High, Emergency }
    public enum LLMApplyMode { Append, Interrupt, SuspendThenInterrupt }

    public sealed class LLMPlanRequestOnDemand
    {
        public string AgentId = "";
        public string Prompt = "";

        public Vector2Int? EventCell;
        public Vector3? EventWorld;

        public LLMPlanUrgency Urgency = LLMPlanUrgency.Normal;
        public LLMApplyMode ApplyMode = LLMApplyMode.Append;

        public string Tag;          // "new_scent", "enemy_spotted"
        public string RequestId;    // optional correlation id
        
        public Sophistication Sophistication;
        public float PriorityScore;
        public Action<string> OnResponseJson;
        public float RequestTime;  

        private static float UrgencyScore(LLMPlanUrgency u) => u switch
        {
            LLMPlanUrgency.Emergency => 1.0f,
            LLMPlanUrgency.High => 0.85f,
            LLMPlanUrgency.Normal => 0.6f,
            _ => 0.3f
        };

        public LLMPlanRequestOnDemand(
            string agentId,
            string prompt,
            Vector2Int? eventCell,
            Vector3? eventWorld,
            LLMPlanUrgency urgency,
            LLMApplyMode applyMode,
            string tag,

            Sophistication sophistication,
            Action<string> onResponseJson,
            float? priorityScoreOverride = null)
        {
            AgentId = agentId;
            Prompt = prompt;
            EventCell = eventCell;
            EventWorld = eventWorld;
            Urgency = urgency;
            ApplyMode = applyMode;
            Tag = tag;

            Sophistication = sophistication;
            OnResponseJson = onResponseJson;

            RequestId = Guid.NewGuid().ToString("N");
            // Concept for a requestID that isn't a huge random number.
            //RequestId = $"{sessionToken}:{++requestCounter}";
            RequestTime = UnityEngine.Time.time;

            PriorityScore = priorityScoreOverride ?? DefaultPriorityFromUrgency(urgency);
        }

        private static float DefaultPriorityFromUrgency(LLMPlanUrgency urgency)
        {
            return urgency switch
            {
                LLMPlanUrgency.Emergency => 1.0f,
                LLMPlanUrgency.High => 0.85f,
                LLMPlanUrgency.Normal => 0.6f,
                _ => 0.3f
            };
        }
    }
}