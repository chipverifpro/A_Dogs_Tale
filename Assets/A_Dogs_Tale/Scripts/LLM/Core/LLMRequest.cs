#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM.Core
{

    public sealed class LLMRequestPacket
    {
        public string requestId = "";
        public LLMProfile profile = new();
        public List<string> systemBlocks = new();
        public string userPrompt = "";
        public JObject? toolDefinitions;
        public JObject? responseSchema;
        public Dictionary<string, string> metadata = new();
    }

    [Serializable]
    public sealed class LLMRequest
    {
        // A stable identifier for caching/dedup (npc id + situation hash, etc.)
        public string requestId = "";

        public string agentName = "";
        // Profile: model, sophistication, planning depth, token limits, etc
        public LLMProfile profile = new();

        // “System” content (persona, rules, schema/tools description)
        public List<string> systemBlocks = new();

        // The immediate user/task prompt (what the agent wants now)
        public string userPrompt = "";

        // ----------------------------
        // Legacy string JSON fields (keep during migration)
        // ----------------------------
        //[Newtonsoft.Json.JsonIgnore]      // do this after nothing relies on them.
        //public string toolDefinitionsJson = "";
        //[Newtonsoft.Json.JsonIgnore]
        public string responseSchemaJson = "";

        // ----------------------------
        // NEW: Structured JSON (preferred)
        // ----------------------------
        public JObject? toolDefinitions;   // ToolCatalogV1 as object
        public JObject? responseSchema;    // PlanResponseV1 schema as object

        // Optional: metadata for logging
        public Dictionary<string, string> metadata = new();
    }
}