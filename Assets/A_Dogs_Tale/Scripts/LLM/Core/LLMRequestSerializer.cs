#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM
{
    public static class LLMRequestSerializer
    {
        public static string ToJson(LLM.Core.LLMRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var root = new JObject
            {
                ["requestId"] = request.requestId ?? "",
                ["profile"] = JObject.FromObject(request.profile ?? new LLM.Core.LLMProfile()),
                ["systemBlocks"] = new JArray(request.systemBlocks ?? new List<string>()),
                ["userPrompt"] = request.userPrompt ?? "",
                ["metadata"] = JObject.FromObject(request.metadata ?? new Dictionary<string, string>())
            };

            // Prefer structured tokens if present
            if (request.toolDefinitions != null)
            {
                root["toolDefinitions"] = request.toolDefinitions;
            }
            else
            {
                TryAddParsedJson(root, "toolDefinitions", request.toolDefinitionsJson);
            }

            if (request.responseSchema != null)
            {
                root["responseSchema"] = request.responseSchema;
            }
            else
            {
                TryAddParsedJson(root, "responseSchema", request.responseSchemaJson);
            }

            return root.ToString(Formatting.Indented);
        }

        private static void TryAddParsedJson(JObject root, string fieldName, string? jsonText)
        {
            if (string.IsNullOrWhiteSpace(jsonText))
                return;

            // raw JSON
            if (TryParseJson(jsonText, out var parsed))
            {
                root[fieldName] = parsed;
                return;
            }

            // possibly a JSON string literal containing JSON
            try
            {
                string? unescaped = JsonConvert.DeserializeObject<string>(jsonText);
                if (!string.IsNullOrWhiteSpace(unescaped) && TryParseJson(unescaped, out parsed))
                    root[fieldName] = parsed;
            }
            catch { /* ignore */ }
        }

        private static bool TryParseJson(string jsonText, out JToken? token)
        {
            token = null;
            try { token = JToken.Parse(jsonText); return true; }
            catch { return false; }
        }
    }
}