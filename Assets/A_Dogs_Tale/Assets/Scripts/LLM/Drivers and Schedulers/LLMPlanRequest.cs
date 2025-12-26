using System;

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
}