#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM
{
    public sealed class PlanResponseV3
    {
        [JsonProperty("schema", Required = Required.Always)]
        public string Schema { get; set; } = "";

        [JsonProperty("requestId", Required = Required.Always)]
        public string RequestId { get; set; } = "";

        [JsonProperty("agentId", Required = Required.Always)]
        public string AgentId { get; set; } = "";

        [JsonProperty("plan_summary")]
        public string? PlanSummary { get; set; }

        [JsonProperty("intentions", Required = Required.Always)]
        public List<JObject> Intentions { get; set; } = new();

        [JsonProperty("questionsForNextContext")]
        public List<PlanQuestionV1>? QuestionsForNextContext { get; set; }

        [JsonProperty("debug")]
        public PlanDebugV1? Debug { get; set; }
    }
}
