using System;
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class LLMPlanRequest
    {
        public readonly string AgentId;
        public readonly LLMModelTier ModelTier;
        public readonly float PriorityScore;
        public readonly Action<string> OnResponseJson;

        public readonly string RequestId;
        public readonly float RequestTime;

        public LLMPlanRequest(
            string agentId,
            LLMModelTier modelTier,
            float priorityScore,
            Action<string> onResponseJson)
        {
            AgentId = agentId;
            ModelTier = modelTier;
            PriorityScore = priorityScore;
            OnResponseJson = onResponseJson;

            RequestId = Guid.NewGuid().ToString("N");
            RequestTime = UnityEngine.Time.time;
        }
    }

    // ---------------------------------------------
    // This second version has different parameters,
    // it comes from Task_RequestLLMPlan.cs
    // TODO: unify the two versions.

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
        
        public LLMModelTier ModelTier;
        public Action<string> OnResponseJson;
        public float RequestTime;  
        
        public LLMPlanRequestOnDemand(
            string agentId,
            string prompt,
            Vector2Int? eventCell,
            Vector3? eventWorld,
            LLMPlanUrgency urgency,
            LLMApplyMode applyMode,
            string tag,

            LLMModelTier modelTier,
            Action<string> onResponseJson)
        {
            AgentId = agentId;
            Prompt = prompt;
            EventCell = eventCell;
            EventWorld = eventWorld;
            Urgency = urgency;
            ApplyMode = applyMode;
            Tag = tag;

            ModelTier = modelTier;
            OnResponseJson = onResponseJson;

            RequestId = Guid.NewGuid().ToString("N");
            RequestTime = UnityEngine.Time.time;
        }
    }
}