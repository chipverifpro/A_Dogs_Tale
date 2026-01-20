#nullable enable
using System;
using DogGame.LLM.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM
{
    public static class LLMRequestSerializer
    {
        /// <summary>
        /// Serialize LLMRequest so nested JSON strings become actual nested objects.
        /// This makes the model see tools/schema as structured JSON (not escaped text blobs).
        /// </summary>
        public static string ToJson(LLMRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            var root = JObject.FromObject(request);

            // Replace string fields with parsed tokens when possible
            ReplaceStringJsonWithToken(
                rootObject: root,
                stringFieldName: "toolDefinitionsJson",
                newFieldName: "toolDefinitions");

            ReplaceStringJsonWithToken(
                rootObject: root,
                stringFieldName: "responseSchemaJson",
                newFieldName: "responseSchema");

            // Optional: remove the original string blobs to reduce prompt bloat
            // root.Remove("toolDefinitionsJson");
            // root.Remove("responseSchemaJson");

            return root.ToString(Formatting.Indented);
        }

        private static void ReplaceStringJsonWithToken(
            JObject rootObject,
            string stringFieldName,
            string newFieldName)
        {
            if (!rootObject.TryGetValue(stringFieldName, out var token))
                return;

            if (token.Type != JTokenType.String)
                return;

            string? jsonText = token.Value<string>();
            if (string.IsNullOrWhiteSpace(jsonText))
                return;

            // Case 1: string contains raw JSON
            if (TryParseJson(jsonText, out JToken? parsed))
            {
                rootObject[newFieldName] = parsed;
                return;
            }

            // Case 2: string is itself a JSON string literal containing JSON
            // e.g. "\"{\\\"schema\\\":...}\""
            try
            {
                string? unescaped = JsonConvert.DeserializeObject<string>(jsonText);
                if (!string.IsNullOrWhiteSpace(unescaped) && TryParseJson(unescaped, out parsed))
                {
                    rootObject[newFieldName] = parsed;
                }
            }
            catch
            {
                // ignore
            }
        }

        private static bool TryParseJson(string jsonText, out JToken? token)
        {
            token = null;
            try
            {
                token = JToken.Parse(jsonText);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}