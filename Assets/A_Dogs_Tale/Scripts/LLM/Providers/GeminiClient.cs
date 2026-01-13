#nullable enable
using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;
using DogGame.LLM.Unity;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DogGame.LLM.Providers
{
    /// <summary>
    /// Google Gemini generateContent client adapter.
    /// Mirrors the architecture of OpenAIClient (LLMClientBase + coroutine bridge),
    /// and ports the cooldown / retry-delay parsing from your GeminiLLMService.
    /// </summary>
    public sealed class GeminiClient : LLMClientBase, ICooldownAware
    {
        public override string Vendor => "Gemini";

        private readonly string apiKey;
        private readonly string endpointUrlFormat;
        private readonly int timeoutSeconds;

        // Optional global "service" instructions to prepend to system blocks.
        private readonly string? globalSystemInstructions;

        // Rate-limit cooldown
        private float cooldownUntilRealtime = -1f; // Time.realtimeSinceStartup

        public bool IsCoolingDown => cooldownUntilRealtime > Time.realtimeSinceStartup;
        public float CooldownRemainingSeconds => Mathf.Max(0f, cooldownUntilRealtime - Time.realtimeSinceStartup);

        public GeminiClient(
            string apiKey,
            string endpointUrlFormat = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}",
            int timeoutSeconds = 60,
            string? globalSystemInstructions = null)
        {
            this.apiKey = apiKey ?? "";
            this.endpointUrlFormat = endpointUrlFormat ?? "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
            this.timeoutSeconds = timeoutSeconds;
            this.globalSystemInstructions = globalSystemInstructions;
        }

        protected override async Task<LLMResponse> SendCoreAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return new LLMResponse
                {
                    succeeded = false,
                    errorMessage = "[GeminiClient] No API key provided."
                };
            }

            if (IsCoolingDown)
            {
                return new LLMResponse
                {
                    succeeded = false,
                    errorMessage = $"[GeminiClient] Cooling down ({CooldownRemainingSeconds:0.0}s). Skipping requestId={request.requestId}."
                };
            }

            var responseHolder = new ResponseHolder();

            IEnumerator routine = PostRequestCoroutine(request, responseHolder);

            // Same cancellation approach as OpenAIClient: stop awaiting if caller cancels.
            Task coroutineTask = CoroutineRunner.Instance.Run(routine);

            Task completed = await Task.WhenAny(coroutineTask, Task.Delay(Timeout.Infinite, cancellationToken));
            if (completed != coroutineTask)
                throw new OperationCanceledException(cancellationToken);

            await coroutineTask; // propagate coroutine exceptions

            return responseHolder.response ?? new LLMResponse
            {
                succeeded = false,
                errorMessage = "[GeminiClient] Coroutine finished but produced no response."
            };
        }

        private IEnumerator PostRequestCoroutine(LLMRequest request, ResponseHolder responseHolder)
        {
            string fullPrompt = BuildFullPrompt(request);

            JObject payload = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray
                        {
                            new JObject { ["text"] = fullPrompt }
                        }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["temperature"] = request.profile.temperature,
                    ["maxOutputTokens"] = request.profile.maxOutputTokens,
                    ["response_mime_type"] = "application/json"
                }
            };

            string url = string.Format(endpointUrlFormat, request.profile.model, apiKey);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(payload.ToString());

            using var unityRequest = new UnityWebRequest(url, "POST");
            unityRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
            unityRequest.downloadHandler = new DownloadHandlerBuffer();
            unityRequest.timeout = timeoutSeconds;
            unityRequest.SetRequestHeader("Content-Type", "application/json");

            // Debug (optional)
            // Debug.Log($"[GeminiClient] POST {url}\n{payload}");

            yield return unityRequest.SendWebRequest();

            string raw = unityRequest.downloadHandler?.text ?? "";

            if (unityRequest.result != UnityWebRequest.Result.Success)
            {
                // Rate limit cooldown handling (ported from GeminiLLMService)
                if (unityRequest.responseCode == 429)
                {
                    float retrySeconds = TryExtractRetryDelaySeconds(raw, out float parsed)
                        ? parsed
                        : 20f;

                    retrySeconds = Mathf.Clamp(retrySeconds + UnityEngine.Random.Range(0.2f, 0.8f), 1f, 120f);
                    cooldownUntilRealtime = Time.realtimeSinceStartup + retrySeconds;

                    responseHolder.response = new LLMResponse
                    {
                        succeeded = false,
                        isRateLimited = true,
                        retryAfterSeconds = retrySeconds,
                        errorMessage = $"[GeminiClient] HTTP 429 rate limited. Cooling down {retrySeconds:0.0}s.\n{raw}",
                        rawProviderPayloadJson = raw
                    };
                    yield break;
                }

                responseHolder.response = new LLMResponse
                {
                    succeeded = false,
                    errorMessage =
                        $"[GeminiClient] HTTP failed: {unityRequest.responseCode} {unityRequest.error}\n{raw}",
                    rawProviderPayloadJson = raw
                };
                yield break;
            }

            if (!TryExtractPlanJson(raw, out string planJson, out string error))
            {
                responseHolder.response = new LLMResponse
                {
                    succeeded = false,
                    errorMessage = $"[GeminiClient] Could not extract plan JSON: {error}",
                    rawProviderPayloadJson = raw
                };
                yield break;
            }

            responseHolder.response = new LLMResponse
            {
                succeeded = true,
                rawText = planJson,
                rawProviderPayloadJson = raw
            };
        }

        private string BuildFullPrompt(LLMRequest request)
        {
            // We mirror your previous GeminiLLMService behavior:
            // system instructions + JSON-only contract + then the request packet.
            var builder = new StringBuilder(2048);

            if (!string.IsNullOrWhiteSpace(globalSystemInstructions))
            {
                builder.AppendLine(globalSystemInstructions!.Trim());
                builder.AppendLine();
            }

            if (request.systemBlocks != null)
            {
                for (int i = 0; i < request.systemBlocks.Count; i++)
                {
                    string block = request.systemBlocks[i] ?? "";
                    if (string.IsNullOrWhiteSpace(block)) continue;

                    builder.AppendLine(block.Trim());
                    builder.AppendLine();
                }
            }

            // Contract reinforcement (keep it generic; your schema block can specify PlanResponseV1, etc.)
            builder.AppendLine("IMPORTANT: Output ONLY JSON.");
            builder.AppendLine("No markdown, no commentary, no code fences. JSON only.");
            builder.AppendLine();

            // The request.userPrompt should already contain your composed packet (persona/context/task/schema contract).
            if (!string.IsNullOrWhiteSpace(request.userPrompt))
            {
                builder.AppendLine(request.userPrompt.Trim());
            }

            return builder.ToString().Trim();
        }

        private static bool TryExtractPlanJson(string geminiApiJson, out string planJson, out string error)
        {
            planJson = "";
            error = "";

            try
            {
                var root = JObject.Parse(geminiApiJson);
                var candidates = root["candidates"] as JArray;
                if (candidates == null || candidates.Count == 0)
                {
                    var promptFeedback = root["promptFeedback"];
                    if (promptFeedback != null)
                        error = $"Request was blocked. Feedback: {promptFeedback}";
                    else
                        error = "Missing or empty 'candidates' array in response.";

                    return false;
                }

                var firstCandidate = candidates[0];
                var content = firstCandidate?["content"];
                var parts = content?["parts"] as JArray;
                if (parts == null || parts.Count == 0)
                {
                    error = "Missing or empty 'parts' array in the first candidate.";
                    return false;
                }

                string? text = parts[0]?["text"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(text))
                {
                    error = "Found text part but it was empty.";
                    return false;
                }

                planJson = StripMarkdownFences(text.Trim());
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string StripMarkdownFences(string text)
        {
            // The model might wrap JSON in markdown fences; strip common forms.
            string trimmed = text.Trim();

            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(7);

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
                trimmed = trimmed.Substring(3);

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
                trimmed = trimmed.Substring(0, trimmed.Length - 3);

            return trimmed.Trim();
        }

        private static bool TryExtractRetryDelaySeconds(string responseJson, out float seconds)
        {
            seconds = 0f;

            try
            {
                var root = JObject.Parse(responseJson);
                var error = root["error"] as JObject;
                if (error == null) return false;

                var details = error["details"] as JArray;
                if (details != null)
                {
                    foreach (var d in details)
                    {
                        if (d is not JObject obj) continue;
                        string? type = obj.Value<string>("@type");
                        if (!string.Equals(type, "type.googleapis.com/google.rpc.RetryInfo", StringComparison.Ordinal))
                            continue;

                        string? retryDelay = obj.Value<string>("retryDelay"); // e.g. "19s"
                        if (TryParseGoogleRetryDelay(retryDelay, out seconds))
                            return true;
                    }
                }

                // Fallback: parse from message (often: "Please retry in 19.65878916s.")
                string? message = error.Value<string>("message");
                if (TryParseSecondsFromMessage(message, out seconds))
                    return true;
            }
            catch
            {
                // ignore parse errors
            }

            return false;
        }

        private static bool TryParseGoogleRetryDelay(string? retryDelay, out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrWhiteSpace(retryDelay))
                return false;

            string cleaned = retryDelay.Trim();

            if (cleaned.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned.Substring(0, cleaned.Length - 1);

            if (float.TryParse(cleaned, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out seconds))
            {
                seconds = Mathf.Max(0f, seconds);
                return true;
            }

            return false;
        }

        private static bool TryParseSecondsFromMessage(string? message, out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrWhiteSpace(message))
                return false;

            var match = System.Text.RegularExpressions.Regex.Match(
                message,
                @"retry in\s+([0-9]*\.?[0-9]+)\s*s",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success)
                return false;

            if (float.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out seconds))
            {
                seconds = Mathf.Max(0f, seconds);
                return true;
            }

            return false;
        }

        private sealed class ResponseHolder
        {
            public LLMResponse? response;
        }
    }

    public static class GeminiConfig
    {
        public static string GetApiKey(string? inspectorValue, string environmentVariableName = "OPENAI_API_KEY")
        {
            if (!string.IsNullOrWhiteSpace(inspectorValue))
                return inspectorValue!;

            return Environment.GetEnvironmentVariable(environmentVariableName) ?? "";
        }
    }

}