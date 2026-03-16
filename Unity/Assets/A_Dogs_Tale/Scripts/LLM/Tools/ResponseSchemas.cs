#nullable enable
namespace DogGame.LLM.Tools
{
    public static class ResponseSchemas
    {
        /*
        public const string PlanResponseV1Name = "PlanResponseV1";

        // This is written to match PlanResponseV1Parser's expectations.
        public static string PlanResponseV1ContractText =>
@"RESPONSE SCHEMA: PlanResponseV1 (JSON only)

TOP-LEVEL OBJECT:
Required:
- schema: string (MUST be ""PlanResponseV1"")
- requestId: string (copy exactly from input)
- agentId: string (copy exactly from input)
- intentions: array (MUST be non-empty; include a noop if you truly have nothing)

Optional:
- questionsForNextContext: array of Question objects
- debug: Debug object

STRICTNESS:
- Do NOT include any other top-level keys.
- When StrictKeyWhitelist is enabled, unknown keys cause validation failure.

INTENTION OBJECT (each item in intentions):
Required:
- id: string (unique within this response; do NOT repeat)
- type: string (one of PlanIntentionType values listed below)
- priority: number in range 0..1 (0=lowest, 1=highest)

Optional:
- rationale: string (max 400 chars; keep short)
- parameters: object (JSON object). Required for some types.

QUESTION OBJECT (each item in questionsForNextContext):
Required:
- ask: string (short)
Optional:
- why: string (max 240 chars)

DEBUG OBJECT:
Optional:
- confidence: number in range 0..1
- notes: array of short strings
- (You may include other debug fields ONLY if your DTO allows them; otherwise omit.)

PARAMETERS REQUIREMENTS BY type:
- noop: parameters MUST be omitted or empty object.
- set_goal: parameters must include { goal: string }. Optional: horizonSeconds number (5..600).
- add_task: parameters must include { task: string }. Optional: waypoints array.
- propose_trap: parameters must include { trap: string, locationCell: [x,y] }. Optional: trigger string.
- propose_dialogue: parameters must include { message: string }. Optional: toEntityId string, tone string.
- request_observation: parameters must include { request: string }. Optional: radiusRooms number (0..5).
- update_beliefs: parameters must include { beliefs: [ { claim: string, confidence?: number 0..1 }, ... ] }.

SAFETY:
- Do NOT include disallowed control keys anywhere inside parameters (examples: teleport, setPosition, setHealth, killEntity, spawnItem, revealMap, setDoorState, etc).";
        */
    }
}
