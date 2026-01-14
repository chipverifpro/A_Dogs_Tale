namespace DogGame.LLM.Prompting
{
    public static class PromptBlocks
    {
        public static string GlobalRulesBlock()
        {
            return
@"GLOBAL RULES:
- Stay in-character based on the persona.
- Be concise unless the situation is complex.
- If tools are enabled, only call allowed tools.
- If unsure, ask ONE clarifying question or make the safest assumption and proceed.";
        }

        public static string PlanningGuidanceBlock(int planningDepth)
        {
            // Important: we are NOT requesting chain-of-thought. This is just behavioral guidance.
            return planningDepth switch
            {
                0 => "PLANNING: Act reactively. Prefer immediate, simple actions.",
                1 => "PLANNING: Think one step ahead. Prefer safe, robust actions.",
                _ => "PLANNING: Consider 2-3 steps ahead and contingencies. Prefer high-quality decisions."
            };
        }

        public static string ContextHeader(string title)
        {
            return $"CONTEXT: {title}";
        }

        public static string ValidationAwareRulesBlock() =>
@"VALIDATION RULES (must pass):
- intentions must be a non-empty array. If no action is needed, return a single intention with type=""noop"".
- Each intention.id must be unique and non-empty.
- Each intention.priority must be a number in range 0..1.
- parameters must be a JSON object when present.
- Some intention types REQUIRE parameters (set_goal, add_task, propose_trap, propose_dialogue, request_observation, update_beliefs).
- Keep intention.rationale <= 400 chars; keep questionsForNextContext[].why <= 240 chars.
- Do NOT add unknown top-level keys. Only: schema, requestId, agentId, intentions, questionsForNextContext, debug.
- Do NOT include any disallowed direct-control keys inside parameters (teleport, setPosition, setHealth, spawnItem, killEntity, revealMap, setDoorState, etc.).";

        public static string OutputOnlyJsonBlock(string schemaName, string contractText) =>
$@"OUTPUT REQUIREMENTS:
- Output ONLY a single JSON object.
- No markdown, no code fences, no commentary.
- schema MUST be ""{schemaName}"".
- The JSON MUST conform to the schema contract below exactly.

{contractText}";

        public static string IdentityEchoBlock(string requestId, string agentId) =>
$@"REQUEST IDENTITY:
- requestId: {requestId}
- agentId: {agentId}
You MUST copy requestId and agentId exactly into the JSON response.";

        // ============ Golden Example ================
//Optional refinement (recommended later)
//Once things are stable, you can:
//	•	Remove the example in Low sophistication
//	•	Keep it only for Medium / High
//	•	Or swap in a dog-specific example for animal NPCs
//
        public static string GoldenExamplePlanResponseV1() =>
$@"EXAMPLE (for format only; do NOT copy values):
{{
  ""schema"": ""PlanResponseV1"",
  ""requestId"": ""EXAMPLE_REQUEST_ID"",
  ""agentId"": ""EXAMPLE_AGENT_ID"",
  ""intentions"": [
    {{
      ""id"": ""i1"",
      ""type"": ""propose_dialogue"",
      ""priority"": 0.8,
      ""rationale"": ""The player is nearby but not hostile; verbal warning reduces risk."",
      ""parameters"": {{
        ""message"": ""Hey—this area’s off-limits. Please step back."",
        ""tone"": ""firm""
      }}
    }},
    {{
      ""id"": ""i2"",
      ""type"": ""request_observation"",
      ""priority"": 0.4,
      ""parameters"": {{
        ""request"": ""Check for hostile movement nearby"",
        ""radiusRooms"": 1
      }}
    }}
  ],
  ""debug"": {{
    ""confidence"": 0.72,
    ""notes"": [
      ""No immediate threat detected."",
      ""Player within close distance.""
    ],
    ""risks"": [
      ""Player may ignore warning.""
    ]
  }}
}}";

    }
}