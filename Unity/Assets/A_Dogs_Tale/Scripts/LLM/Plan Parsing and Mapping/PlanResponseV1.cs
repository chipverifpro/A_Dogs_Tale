
#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM
{
    public sealed class PlanResponseV1
    {
        [JsonProperty("schema", Required = Required.Always)]
        public string Schema { get; set; } = "";

        [JsonProperty("requestId", Required = Required.Always)]
        public string RequestId { get; set; } = "";

        [JsonProperty("agentId", Required = Required.Always)]
        public string AgentId { get; set; } = "";

        [JsonProperty("intentions", Required = Required.Always)]
        public List<PlanIntentionV1> Intentions { get; set; } = new();

        [JsonProperty("questionsForNextContext")]
        public List<PlanQuestionV1>? QuestionsForNextContext { get; set; }

        [JsonProperty("debug")]
        public PlanDebugV1? Debug { get; set; }
    }

    public sealed class PlanIntentionV1
    {
        [JsonProperty("type", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public PlanIntentionType Type { get; set; }

        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; set; } = "";

        [JsonProperty("priority", Required = Required.Always)]
        public float Priority { get; set; }

        [JsonProperty("rationale")]
        public string? Rationale { get; set; }

        [JsonProperty("parameters")]
        public JObject? Parameters { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PlanIntentionType
    {
        noop,
        set_goal,
        add_task,
        propose_trap,
        propose_dialogue,
        request_observation,
        update_beliefs
    }

    public sealed class PlanQuestionV1
    {
        [JsonProperty("ask", Required = Required.Always)]
        public string Ask { get; set; } = "";

        [JsonProperty("why")]
        public string? Why { get; set; }
    }

    public sealed class PlanDebugV1
    {
        [JsonProperty("confidence")]
        public float? Confidence { get; set; }

        [JsonProperty("notes")]
        public List<string>? Notes { get; set; }
    }
}