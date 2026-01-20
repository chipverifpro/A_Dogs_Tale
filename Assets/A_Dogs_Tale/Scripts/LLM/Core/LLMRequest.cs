using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM.Core
{
    [Serializable]
    public sealed class LLMRequest
    {
        // A stable identifier for caching/dedup (npc id + situation hash, etc.)
        public string requestId;

        public LLMProfile profile;

        // “System” content (persona, rules, schema/tools description)
        public List<string> systemBlocks = new();

        // The immediate user/task prompt (what the agent wants now)
        public string userPrompt;

        // Optional: tool definitions as JSON string, or a normalized structure later
        public string toolDefinitionsJson;

        // Optional: response schema as JSON string
        public string responseSchemaJson;

        // Optional: metadata for logging
        public Dictionary<string, string> metadata = new();

#nullable enable
        public JToken? toolDefinitions;   // or JObject?
        public JToken? responseSchema;
    }
}