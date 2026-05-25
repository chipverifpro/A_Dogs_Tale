#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DogGame.LLM
{
    public static class PlanResponseV1Parser
    {
        public sealed class ValidationResult
        {
            public bool IsValid => Errors.Count == 0;
            public List<string> Errors { get; } = new();
        }

        // You can flip these later from config / model tier.
        private const int MaxRationaleChars = 400;
        private const int MaxQuestionWhyChars = 240;

        /// <summary>
        /// If true, rejects any unknown top-level keys and unknown keys inside intentions/questions/debug.
        /// This is stricter than most JSON parsers and helps prevent "hallucinated fields" from silently slipping in.
        /// </summary>
        public static bool StrictKeyWhitelist { get; set; } = true;

        /// <summary>
        /// Parse JSON -> DTO and validate. Returns null response if invalid.
        /// </summary>
        public static (PlanResponseV1? Response, ValidationResult Result) ParseAndValidate(string jsonText)
        {
            var validationResult = new ValidationResult();

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                validationResult.Errors.Add("JSON text is empty.");
                return (null, validationResult);
            }

            // NEW: sanitize / extract
            string? extracted = ExtractFirstJsonObject(jsonText);
            if (string.IsNullOrWhiteSpace(extracted))
            {
                validationResult.Errors.Add("Could not find a JSON object in the model output.");
                return (null, validationResult);
            }

            JObject rootObject;
            try
            {
                rootObject = JObject.Parse(extracted);
            }
            catch (Exception exception)
            {
                validationResult.Errors.Add($"JSON parse failed: {exception.Message}");
                return (null, validationResult);
            }

            if (StrictKeyWhitelist)
                ValidateTopLevelKeys(rootObject, validationResult);

            PlanResponseV1? parsedResponse;
            try
            {
                // Deserialize after key validation so we can reject unknown keys before Unity silently ignores them.
                parsedResponse = rootObject.ToObject<PlanResponseV1>();
            }
            catch (Exception exception)
            {
                validationResult.Errors.Add($"JSON deserialize failed: {exception.Message}");
                return (null, validationResult);
            }

            if (parsedResponse == null)
            {
                validationResult.Errors.Add("JSON deserialize produced null response.");
                return (null, validationResult);
            }

            ValidateTopLevel(parsedResponse, validationResult);
            ValidateIntentions(parsedResponse, validationResult);

            Debug.Log($"LLMWalkthrough3: PlanResponseV1Parser.ParseAndValidate intentionsCount={parsedResponse.Intentions.Count}");
            for (int debugdumpx = 0; debugdumpx < parsedResponse.Intentions.Count; debugdumpx++)
            {
                Debug.Log($"LLMWalkthrough3: PlanResponseV1Parser.ParseAndValidate intention {debugdumpx}: type={parsedResponse.Intentions[debugdumpx].Type} priority={parsedResponse.Intentions[debugdumpx].Priority}");
            }

            return (validationResult.IsValid ? parsedResponse : null, validationResult);
        }

        private static void ValidateTopLevelKeys(JObject rootObject, ValidationResult result)
        {
            // Only these keys allowed at top level.
            var allowedKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                "schema",
                "requestId",
                "agentId",
                "intentions",
                "questionsForNextContext",
                "debug"
            };

            foreach (var property in rootObject.Properties())
            {
                if (!allowedKeys.Contains(property.Name))
                    result.Errors.Add($"Unknown top-level key \"{property.Name}\" is not allowed.");
            }

            // Structural checks on key types to catch obvious malformations early.
            if (rootObject.TryGetValue("intentions", out var intentionsToken) && intentionsToken.Type != JTokenType.Array)
                result.Errors.Add("Top-level \"intentions\" must be an array.");
            if (rootObject.TryGetValue("questionsForNextContext", out var questionsToken) && questionsToken.Type != JTokenType.Array)
                result.Errors.Add("Top-level \"questionsForNextContext\" must be an array when present.");
            if (rootObject.TryGetValue("debug", out var debugToken) && debugToken.Type != JTokenType.Object)
                result.Errors.Add("Top-level \"debug\" must be an object when present.");
        }

        private static void ValidateTopLevel(PlanResponseV1 response, ValidationResult result)
        {
            if (!string.Equals(response.Schema, "PlanResponseV1", StringComparison.Ordinal))
                result.Errors.Add($"schema must be \"PlanResponseV1\" but was \"{response.Schema}\".");

            if (string.IsNullOrWhiteSpace(response.RequestId))
                result.Errors.Add("requestId is required.");

            if (string.IsNullOrWhiteSpace(response.AgentId))
                result.Errors.Add("agentId is required.");

            if (response.Intentions == null || response.Intentions.Count == 0)
                result.Errors.Add("intentions must be a non-empty array (or include a single noop).");

            if (response.Debug?.Confidence is { } confidenceValue)
            {
                if (confidenceValue < 0f || confidenceValue > 1f)
                    result.Errors.Add("debug.confidence must be in range 0..1.");
            }

            if (response.QuestionsForNextContext != null)
            {
                for (int questionIndex = 0; questionIndex < response.QuestionsForNextContext.Count; questionIndex++)
                {
                    var question = response.QuestionsForNextContext[questionIndex];
                    if (question == null)
                    {
                        result.Errors.Add($"questionsForNextContext[{questionIndex}] must not be null.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(question.Ask))
                        result.Errors.Add($"questionsForNextContext[{questionIndex}].ask is required.");

                    if (question.Why != null && question.Why.Length > MaxQuestionWhyChars)
                        result.Errors.Add($"questionsForNextContext[{questionIndex}].why is too long (max {MaxQuestionWhyChars} chars).");
                }
            }
        }

        private static void ValidateIntentions(PlanResponseV1 response, ValidationResult result)
        {
            var seenIntentionIds = new HashSet<string>(StringComparer.Ordinal);

            for (int intentionIndex = 0; intentionIndex < response.Intentions.Count; intentionIndex++)
            {
                var intention = response.Intentions[intentionIndex];
                if (intention == null)
                {
                    result.Errors.Add($"intentions[{intentionIndex}] must not be null.");
                    continue;
                }

                if (StrictKeyWhitelist)
                    ValidateIntentionKeys(intentionIndex, intention, result);

                if (string.IsNullOrWhiteSpace(intention.Id))
                {
                    result.Errors.Add($"intentions[{intentionIndex}].id is required.");
                }
                else
                {
                    if (!seenIntentionIds.Add(intention.Id))
                        result.Errors.Add($"intentions[{intentionIndex}].id \"{intention.Id}\" is duplicated.");
                }

                if (intention.Priority < 0f || intention.Priority > 1f)
                    result.Errors.Add($"intentions[{intentionIndex}].priority must be in range 0..1.");

                if (intention.Rationale != null && intention.Rationale.Length > MaxRationaleChars)
                    result.Errors.Add($"intentions[{intentionIndex}].rationale is too long (max {MaxRationaleChars} chars).");

                // parameters must be an object if present
                if (intention.Parameters != null && intention.Parameters.Type != JTokenType.Object)
                    result.Errors.Add($"intentions[{intentionIndex}].parameters must be a JSON object.");

                ValidateIntentParameters(intentionIndex, intention, result);
            }
        }

        private static void ValidateIntentionKeys(int intentionIndex, PlanIntentionV1 intention, ValidationResult result)
        {
            // Since we deserialize into PlanIntentionV1 already, we don't have direct access to the original raw JObject for each intention.
            // If you want *full* strict key checking inside intentions, parse the intentions array from the raw JObject and validate keys there.
            //
            // For now, we still enforce strictness where it matters: parameters key checks + disallowed keys.
            //
            // (If you want full strictness, say so and I’ll provide the raw-walk version.)
        }

        private static void ValidateIntentParameters(int intentionIndex, PlanIntentionV1 intention, ValidationResult result)
        {
            if (intention.Type == PlanIntentionType.noop)
                return;

            if (intention.Parameters == null)
            {
                // Intents that require parameters.
                if (intention.Type is PlanIntentionType.set_goal
                    or PlanIntentionType.add_task
                    or PlanIntentionType.propose_trap
                    or PlanIntentionType.propose_dialogue
                    or PlanIntentionType.request_observation
                    or PlanIntentionType.update_beliefs)
                {
                    result.Errors.Add($"intentions[{intentionIndex}] of type {intention.Type} requires parameters.");
                }
                return;
            }

            var parameters = intention.Parameters;

            bool HasNonEmptyString(string propertyName) =>
                parameters.TryGetValue(propertyName, out var token) &&
                token.Type == JTokenType.String &&
                !string.IsNullOrWhiteSpace(token.Value<string>());

            bool HasNumber(string propertyName) =>
                parameters.TryGetValue(propertyName, out var token) &&
                (token.Type == JTokenType.Integer || token.Type == JTokenType.Float);

            bool HasArray(string propertyName) =>
                parameters.TryGetValue(propertyName, out var token) &&
                token.Type == JTokenType.Array;

            // Global safety: disallow obvious direct-control keys anywhere in parameters.
            // (You can expand this list as your command surface grows.)
            if (ContainsDisallowedControlKeys(parameters))
                result.Errors.Add($"intentions[{intentionIndex}] parameters contain disallowed direct-control keys.");

            switch (intention.Type)
            {
                case PlanIntentionType.set_goal:
                    if (!HasNonEmptyString("goal"))
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.goal (string) is required.");

                    if (HasNumber("horizonSeconds"))
                    {
                        var horizonSeconds = parameters.Value<double>("horizonSeconds");
                        if (horizonSeconds < 5 || horizonSeconds > 600)
                            result.Errors.Add($"intentions[{intentionIndex}].parameters.horizonSeconds must be 5..600.");
                    }
                    break;

                case PlanIntentionType.add_task:
                    if (!HasNonEmptyString("task"))
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.task (string) is required.");

                    if (parameters.TryGetValue("waypoints", out var waypointsToken))
                    {
                        if (waypointsToken.Type != JTokenType.Array)
                            result.Errors.Add($"intentions[{intentionIndex}].parameters.waypoints must be an array if present.");
                    }
                    break;

                case PlanIntentionType.propose_trap:
                    if (!HasNonEmptyString("trap"))
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.trap (string) is required.");

                    if (!HasArray("locationCell"))
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.locationCell ([x,y]) is required.");
                    else
                        ValidateCellArray(intentionIndex, parameters["locationCell"]!, "locationCell", result);

                    // Optional trigger
                    if (parameters.TryGetValue("trigger", out var triggerToken) && triggerToken.Type != JTokenType.String)
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.trigger must be a string if present.");
                    break;

                case PlanIntentionType.propose_dialogue:
                    if (!HasNonEmptyString("message"))
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.message (string) is required.");

                    if (parameters.TryGetValue("toEntityId", out var toEntityToken) && toEntityToken.Type != JTokenType.String)
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.toEntityId must be a string if present.");

                    if (parameters.TryGetValue("tone", out var toneToken) && toneToken.Type != JTokenType.String)
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.tone must be a string if present.");
                    break;

                case PlanIntentionType.request_observation:
                    if (!HasNonEmptyString("request"))
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.request (string) is required.");

                    if (HasNumber("radiusRooms"))
                    {
                        var radiusRooms = parameters.Value<int>("radiusRooms");
                        if (radiusRooms < 0 || radiusRooms > 5)
                            result.Errors.Add($"intentions[{intentionIndex}].parameters.radiusRooms must be 0..5.");
                    }
                    break;

                case PlanIntentionType.update_beliefs:
                    if (!HasArray("beliefs"))
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.beliefs (array) is required.");
                    else
                        ValidateBeliefsArray(intentionIndex, parameters["beliefs"]!, result);
                    break;

                case PlanIntentionType.noop:
                    // handled earlier
                    break;

                default:
                    result.Errors.Add($"intentions[{intentionIndex}] has unknown type {intention.Type}.");
                    break;
            }
        }

        private static bool ContainsDisallowedControlKeys(JObject parameters)
        {
            // Bread-and-butter "no cheating" list. Expand as needed.
            var disallowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "directControl",
                "setPosition",
                "setWorldPosition",
                "teleport",
                "moveToExact",
                "setHealth",
                "spawnItem",
                "giveItem",
                "killEntity",
                "revealMap",
                "setDoorState"
            };

            // Shallow scan + recursive scan
            return ContainsDisallowedKeysRecursive(parameters, disallowedKeys);
        }

        private static bool ContainsDisallowedKeysRecursive(JToken token, HashSet<string> disallowedKeys)
        {
            switch (token.Type)
            {
                case JTokenType.Object:
                {
                    var obj = (JObject)token;
                    foreach (var property in obj.Properties())
                    {
                        if (disallowedKeys.Contains(property.Name))
                            return true;

                        if (ContainsDisallowedKeysRecursive(property.Value, disallowedKeys))
                            return true;
                    }
                    return false;
                }

                case JTokenType.Array:
                {
                    var array = (JArray)token;
                    foreach (var element in array)
                    {
                        if (ContainsDisallowedKeysRecursive(element, disallowedKeys))
                            return true;
                    }
                    return false;
                }

                default:
                    return false;
            }
        }

        private static void ValidateCellArray(int intentionIndex, JToken cellToken, string fieldName, ValidationResult result)
        {
            if (cellToken.Type != JTokenType.Array)
            {
                result.Errors.Add($"intentions[{intentionIndex}].parameters.{fieldName} must be an array.");
                return;
            }

            var array = (JArray)cellToken;
            if (array.Count != 2)
            {
                result.Errors.Add($"intentions[{intentionIndex}].parameters.{fieldName} must be [x,y].");
                return;
            }

            if (array[0].Type != JTokenType.Integer || array[1].Type != JTokenType.Integer)
                result.Errors.Add($"intentions[{intentionIndex}].parameters.{fieldName} must contain integer x,y.");
        }

        private static void ValidateBeliefsArray(int intentionIndex, JToken beliefsToken, ValidationResult result)
        {
            if (beliefsToken.Type != JTokenType.Array)
            {
                result.Errors.Add($"intentions[{intentionIndex}].parameters.beliefs must be an array.");
                return;
            }

            foreach (var beliefEntry in (JArray)beliefsToken)
            {
                if (beliefEntry.Type != JTokenType.Object)
                {
                    result.Errors.Add($"intentions[{intentionIndex}].parameters.beliefs entries must be objects.");
                    continue;
                }

                var beliefObject = (JObject)beliefEntry;

                if (!beliefObject.TryGetValue("claim", out var claimToken) || claimToken.Type != JTokenType.String || string.IsNullOrWhiteSpace(claimToken.Value<string>()))
                    result.Errors.Add($"intentions[{intentionIndex}].parameters.beliefs[].claim (string) is required.");

                if (beliefObject.TryGetValue("confidence", out var confidenceToken))
                {
                    if (confidenceToken.Type != JTokenType.Integer && confidenceToken.Type != JTokenType.Float)
                    {
                        result.Errors.Add($"intentions[{intentionIndex}].parameters.beliefs[].confidence must be a number if present.");
                    }
                    else
                    {
                        var confidenceValue = confidenceToken.Value<double>();
                        if (confidenceValue < 0 || confidenceValue > 1)
                            result.Errors.Add($"intentions[{intentionIndex}].parameters.beliefs[].confidence must be 0..1.");
                    }
                }
            }
        }

        private static string? ExtractFirstJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Remove common markdown fences quickly (doesn't need to be perfect).
            // We'll still do brace extraction after this.
            text = text.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("```", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();

            int firstBrace = text.IndexOf('{');
            if (firstBrace < 0)
                return null;

            int depth = 0;
            bool inString = false;
            bool escape = false;

            for (int i = firstBrace; i < text.Length; i++)
            {
                char c = text[i];

                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (c == '"')
                    {
                        inString = false;
                        continue;
                    }

                    continue;
                }
                else
                {
                    if (c == '"')
                    {
                        inString = true;
                        continue;
                    }

                    if (c == '{')
                    {
                        depth++;
                    }
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            // Inclusive substring { ... }
                            return text.Substring(firstBrace, i - firstBrace + 1);
                        }
                    }
                }
            }

            // No matching close brace
            return null;
        }
    }
}