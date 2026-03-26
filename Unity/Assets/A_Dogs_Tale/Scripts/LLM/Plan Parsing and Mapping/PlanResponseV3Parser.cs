#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM
{
    public static class PlanResponseV3Parser
    {
        public sealed class ValidationResult
        {
            public bool IsValid => Errors.Count == 0;
            public List<string> Errors { get; } = new();
        }

        private const int MaxReasoningChars = 240;
        private const int MaxQuestionWhyChars = 240;

        public static bool StrictKeyWhitelist { get; set; } = true;

        public static (PlanResponseV3? Response, ValidationResult Result) ParseAndValidate(string jsonText)
        {
            var validationResult = new ValidationResult();

            if (string.IsNullOrWhiteSpace(jsonText))
            {
                validationResult.Errors.Add("JSON text is empty.");
                return (null, validationResult);
            }

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

            PlanResponseV3? parsedResponse;
            try
            {
                parsedResponse = rootObject.ToObject<PlanResponseV3>();
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

            return (validationResult.IsValid ? parsedResponse : null, validationResult);
        }

        private static void ValidateTopLevelKeys(JObject rootObject, ValidationResult result)
        {
            var allowedKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                "schema",
                "requestId",
                "agentId",
                "plan_summary",
                "intentions",
                "questionsForNextContext",
                "debug"
            };

            foreach (var property in rootObject.Properties())
            {
                if (!allowedKeys.Contains(property.Name))
                    result.Errors.Add($"Unknown top-level key \"{property.Name}\" is not allowed.");
            }

            if (rootObject.TryGetValue("plan_summary", out var summaryToken) && summaryToken.Type != JTokenType.String)
                result.Errors.Add("Top-level \"plan_summary\" must be a string when present.");
            if (rootObject.TryGetValue("intentions", out var intentionsToken) && intentionsToken.Type != JTokenType.Array)
                result.Errors.Add("Top-level \"intentions\" must be an array.");
            if (rootObject.TryGetValue("questionsForNextContext", out var questionsToken) && questionsToken.Type != JTokenType.Array)
                result.Errors.Add("Top-level \"questionsForNextContext\" must be an array when present.");
            if (rootObject.TryGetValue("debug", out var debugToken) && debugToken.Type != JTokenType.Object)
                result.Errors.Add("Top-level \"debug\" must be an object when present.");
        }

        private static void ValidateTopLevel(PlanResponseV3 response, ValidationResult result)
        {
            if (!string.Equals(response.Schema, "PlanResponseV3", StringComparison.Ordinal))
                result.Errors.Add($"schema must be \"PlanResponseV3\" but was \"{response.Schema}\".");

            if (string.IsNullOrWhiteSpace(response.RequestId))
                result.Errors.Add("requestId is required.");

            if (string.IsNullOrWhiteSpace(response.AgentId))
                result.Errors.Add("agentId is required.");

            if (response.Intentions == null || response.Intentions.Count == 0)
                result.Errors.Add("intentions must be a non-empty array.");

            if (response.Debug?.Confidence is { } confidenceValue && (confidenceValue < 0f || confidenceValue > 1f))
                result.Errors.Add("debug.confidence must be in range 0..1.");

            if (response.QuestionsForNextContext == null)
                return;

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

        private static void ValidateIntentions(PlanResponseV3 response, ValidationResult result)
        {
            for (int index = 0; index < response.Intentions.Count; index++)
            {
                var intention = response.Intentions[index];
                if (intention == null)
                {
                    result.Errors.Add($"intentions[{index}] must not be null.");
                    continue;
                }

                ValidateIntention(index, intention, result);
            }
        }

        private static void ValidateIntention(int index, JObject intention, ValidationResult result)
        {
            string? action = intention.Value<string>("action");
            if (string.IsNullOrWhiteSpace(action))
            {
                result.Errors.Add($"intentions[{index}].action is required.");
                return;
            }

            action = action.Trim();

            switch (action)
            {
                case "bark":
                    ValidateAllowedKeys(index, intention, result, "action", "bark_intent", "target_id", "reasoning");
                    ValidateRequiredString(index, intention, result, "bark_intent");
                    ValidateEnum(index, intention, result, "bark_intent", "social", "suspicious", "alert_pack", "threat", "found", "need_help");
                    ValidateOptionalString(index, intention, result, "target_id");
                    ValidateReasoning(index, intention, result);
                    break;

                case "emote":
                    ValidateAllowedKeys(index, intention, result, "action", "emote_intent", "reasoning");
                    ValidateRequiredString(index, intention, result, "emote_intent");
                    ValidateEnum(index, intention, result, "emote_intent", "friendly", "playful", "excited", "curious", "suspicious", "fearful", "submissive", "happy");
                    ValidateReasoning(index, intention, result);
                    break;

                case "set_walk_mode":
                    ValidateAllowedKeys(index, intention, result, "action", "walk_mode", "reasoning");
                    ValidateRequiredString(index, intention, result, "walk_mode");
                    ValidateEnum(index, intention, result, "walk_mode", "Walk", "Run", "Sneak", "Cautious", "Crawl");
                    ValidateReasoning(index, intention, result);
                    break;

                case "face_object":
                    ValidateAllowedKeys(index, intention, result, "action", "target_id", "persistence", "reasoning");
                    ValidateRequiredString(index, intention, result, "target_id");
                    ValidateOptionalBool(index, intention, result, "persistence");
                    ValidateReasoning(index, intention, result);
                    break;

                case "move_to_object":
                case "examine_object":
                case "take_object":
                case "go_through_door":
                case "open_door":
                case "close_door":
                case "join_pack":
                case "start_follow_scent":
                    ValidateAllowedKeys(index, intention, result, "action", "target_id", "reasoning");
                    ValidateRequiredString(index, intention, result, "target_id");
                    ValidateReasoning(index, intention, result);
                    break;

                case "sniff":
                case "drop_object":
                case "bury_object":
                case "dig":
                case "become_pack_leader":
                case "leave_pack":
                case "start_exploring":
                case "start_follow_pack_leader":
                case "start_follow_on_leash":
                case "start_patrol_room":
                case "start_stay":
                case "stop_current_mode":
                    ValidateAllowedKeys(index, intention, result, "action", "reasoning");
                    ValidateReasoning(index, intention, result);
                    break;

                case "interact_with_object":
                    ValidateAllowedKeys(index, intention, result, "action", "target_id", "interaction", "reasoning");
                    ValidateRequiredString(index, intention, result, "target_id");
                    ValidateRequiredString(index, intention, result, "interaction");
                    ValidateEnum(index, intention, result, "interaction", "open", "close", "push", "pull", "scratch");
                    ValidateReasoning(index, intention, result);
                    break;

                case "interact_with_held_object":
                    ValidateAllowedKeys(index, intention, result, "action", "interaction", "reasoning");
                    ValidateRequiredString(index, intention, result, "interaction");
                    ValidateEnum(index, intention, result, "interaction", "drop", "bury", "eat", "chew", "destroy");
                    ValidateReasoning(index, intention, result);
                    break;

                case "start_follow_object":
                case "start_patrol_around_object":
                    ValidateAllowedKeys(index, intention, result, "action", "target_id", "distance", "reasoning");
                    ValidateRequiredString(index, intention, result, "target_id");
                    ValidateOptionalIntegerRange(index, intention, result, "distance", 0, 5);
                    ValidateReasoning(index, intention, result);
                    break;

                case "nap":
                    ValidateAllowedKeys(index, intention, result, "action", "duration", "reasoning");
                    ValidateRequiredIntegerRange(index, intention, result, "duration", 1, 300);
                    ValidateReasoning(index, intention, result);
                    break;

                case "wait":
                    ValidateAllowedKeys(index, intention, result, "action", "duration", "reasoning");
                    ValidateRequiredIntegerRange(index, intention, result, "duration", 0, 30);
                    ValidateReasoning(index, intention, result);
                    break;

                default:
                    result.Errors.Add($"intentions[{index}].action \"{action}\" is not supported by PlanResponseV3.");
                    break;
            }
        }

        private static void ValidateAllowedKeys(int index, JObject intention, ValidationResult result, params string[] allowedKeys)
        {
            if (!StrictKeyWhitelist)
                return;

            var allowed = new HashSet<string>(allowedKeys, StringComparer.Ordinal);
            foreach (var property in intention.Properties())
            {
                if (!allowed.Contains(property.Name))
                    result.Errors.Add($"intentions[{index}] key \"{property.Name}\" is not allowed for action \"{intention.Value<string>("action")}\".");
            }
        }

        private static void ValidateRequiredString(int index, JObject intention, ValidationResult result, string propertyName)
        {
            if (!intention.TryGetValue(propertyName, out var token) || token.Type != JTokenType.String || string.IsNullOrWhiteSpace(token.Value<string>()))
                result.Errors.Add($"intentions[{index}].{propertyName} (string) is required.");
        }

        private static void ValidateOptionalString(int index, JObject intention, ValidationResult result, string propertyName)
        {
            if (intention.TryGetValue(propertyName, out var token) && token.Type != JTokenType.String)
                result.Errors.Add($"intentions[{index}].{propertyName} must be a string if present.");
        }

        private static void ValidateOptionalBool(int index, JObject intention, ValidationResult result, string propertyName)
        {
            if (intention.TryGetValue(propertyName, out var token) && token.Type != JTokenType.Boolean)
                result.Errors.Add($"intentions[{index}].{propertyName} must be a boolean if present.");
        }

        private static void ValidateRequiredIntegerRange(int index, JObject intention, ValidationResult result, string propertyName, int minInclusive, int maxInclusive)
        {
            if (!intention.TryGetValue(propertyName, out var token) || token.Type != JTokenType.Integer)
            {
                result.Errors.Add($"intentions[{index}].{propertyName} (integer) is required.");
                return;
            }

            int value = token.Value<int>();
            if (value < minInclusive || value > maxInclusive)
                result.Errors.Add($"intentions[{index}].{propertyName} must be in range {minInclusive}..{maxInclusive}.");
        }

        private static void ValidateOptionalIntegerRange(int index, JObject intention, ValidationResult result, string propertyName, int minInclusive, int maxInclusive)
        {
            if (!intention.TryGetValue(propertyName, out var token))
                return;

            if (token.Type != JTokenType.Integer)
            {
                result.Errors.Add($"intentions[{index}].{propertyName} must be an integer if present.");
                return;
            }

            int value = token.Value<int>();
            if (value < minInclusive || value > maxInclusive)
                result.Errors.Add($"intentions[{index}].{propertyName} must be in range {minInclusive}..{maxInclusive}.");
        }

        private static void ValidateEnum(int index, JObject intention, ValidationResult result, string propertyName, params string[] allowedValues)
        {
            if (!intention.TryGetValue(propertyName, out var token) || token.Type != JTokenType.String)
                return;

            string value = token.Value<string>() ?? "";
            for (int allowedIndex = 0; allowedIndex < allowedValues.Length; allowedIndex++)
            {
                if (string.Equals(value, allowedValues[allowedIndex], StringComparison.Ordinal))
                    return;
            }

            result.Errors.Add($"intentions[{index}].{propertyName} has invalid value \"{value}\".");
        }

        private static void ValidateReasoning(int index, JObject intention, ValidationResult result)
        {
            if (!intention.TryGetValue("reasoning", out var token) || token.Type != JTokenType.String)
            {
                result.Errors.Add($"intentions[{index}].reasoning (string) is required.");
                return;
            }

            string reasoning = token.Value<string>() ?? "";
            if (string.IsNullOrWhiteSpace(reasoning))
                result.Errors.Add($"intentions[{index}].reasoning must not be empty.");
            else if (reasoning.Length > MaxReasoningChars)
                result.Errors.Add($"intentions[{index}].reasoning is too long (max {MaxReasoningChars} chars).");
            else if (reasoning.Contains('\n') || reasoning.Contains('\r'))
                result.Errors.Add($"intentions[{index}].reasoning must be single-line.");
        }

        private static string? ExtractFirstJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

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

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return text.Substring(firstBrace, i - firstBrace + 1);
            }

            return null;
        }
    }
}
