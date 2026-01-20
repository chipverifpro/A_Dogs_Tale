#nullable enable
using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;
using DogGame.LLM.Unity;
using Newtonsoft.Json.Linq;
using Unity.AppUI.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace DogGame.LLM.Providers
{
    /// <summary>
    /// OpenAI Responses API client adapter.
    /// Uses text.format=json_object and extracts first output_text.
    /// </summary>
    public sealed class OpenAIClient : LLMClientBase, ICooldownAware
    {
        public override string Vendor => "OpenAI";

        private readonly string endpointUrl;
        private readonly string apiKey;
        private readonly int timeoutSeconds;

        // Rate-limit cooldown
        private float cooldownUntilRealtime = -1f; // Time.realtimeSinceStartup

        public bool IsCoolingDown => cooldownUntilRealtime > Time.realtimeSinceStartup;
        public float CooldownRemainingSeconds => Mathf.Max(0f, cooldownUntilRealtime - Time.realtimeSinceStartup);

        // Optional global "service" instructions to prepend to system blocks.
        private readonly string? globalSystemInstructions;

        public OpenAIClient(
            string apiKey,
            string endpointUrl = "https://api.openai.com/v1/responses",
            int timeoutSeconds = 60,
            string? globalSystemInstructions = null)
        {
            this.apiKey = apiKey ?? "";
            this.endpointUrl = endpointUrl ?? "https://api.openai.com/v1/responses";
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
                    errorMessage = "[OpenAIClient] No API key provided."
                };
            }

            if (IsCoolingDown)
            {
                return new LLMResponse
                {
                    succeeded = false,
                    errorMessage = $"[OpenAIClient] Cooling down ({CooldownRemainingSeconds:0.0}s). Skipping requestId={request.requestId}."
                };
            }

            var responseHolder = new ResponseHolder();

            IEnumerator routine = PostResponsesCoroutine(request, responseHolder);

            // We can't truly cancel UnityWebRequest cleanly from CancellationToken without extra plumbing.
            // This at least stops awaiting if the caller cancels.
            Task coroutineTask = CoroutineRunner.Instance.Run(routine);

            Task completed = await Task.WhenAny(coroutineTask, Task.Delay(Timeout.Infinite, cancellationToken));
            if (completed != coroutineTask)
                throw new OperationCanceledException(cancellationToken);

            // Propagate coroutine exceptions
            await coroutineTask;

            return responseHolder.response ?? new LLMResponse
            {
                succeeded = false,
                errorMessage = "[OpenAIClient] Coroutine finished but produced no response."
            };
        }

        private IEnumerator PostResponsesCoroutine(LLMRequest request, ResponseHolder responseHolder)
        {
            // Combine system blocks into a single instructions string
            string systemInstructions = BuildInstructions(request);

            // Your "input" is the actual task/prompt packet.
            // If you want a stronger contract, keep the JSON-only constraints here as well.
            string inputText = LLMRequestPacketFormatter.BuildPacketText(request);

            JObject payload = new JObject
            {
                ["model"] = request.profile.model,
                ["instructions"] = systemInstructions,
                ["input"] = inputText,
                ["temperature"] = request.profile.temperature,
                ["max_output_tokens"] = request.profile.maxOutputTokens,
                ["text"] = new JObject
                {
                    ["format"] = new JObject
                    {
                        ["type"] = "json_object"
                    }
                },
                ["metadata"] = BuildMetadata(request)
            };

            byte[] bodyBytes = Encoding.UTF8.GetBytes(payload.ToString());

            using var unityRequest = new UnityWebRequest(endpointUrl, "POST");
            unityRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
            unityRequest.downloadHandler = new DownloadHandlerBuffer();
            unityRequest.timeout = timeoutSeconds;

            unityRequest.SetRequestHeader("Content-Type", "application/json");
            unityRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            // Debug (optional)
            Debug.Log($"[OpenAIClient] POST {endpointUrl}\n{payload}");

            Directory.Instance.llmDebugMonitor.DebugLLMRequest(payload.ToString());

            yield return unityRequest.SendWebRequest();

            string raw = unityRequest.downloadHandler?.text ?? "";

            Directory.Instance.llmDebugMonitor.DebugLLMResponse(raw);

            if (unityRequest.result != UnityWebRequest.Result.Success)
            {
                if (unityRequest.responseCode == 429)
                {
                    float retrySeconds = TryGetOpenAIRetryDelaySeconds(unityRequest, raw, out float parsed)
                        ? parsed
                        : 15f;

                    retrySeconds = Mathf.Clamp(retrySeconds + UnityEngine.Random.Range(0.2f, 0.8f), 1f, 120f);
                    cooldownUntilRealtime = Time.realtimeSinceStartup + retrySeconds;

                    responseHolder.response = new LLMResponse
                    {
                        succeeded = false,
                        isRateLimited = true,
                        retryAfterSeconds = retrySeconds,
                        errorMessage = $"[OpenAIClient] HTTP 429 rate limited. Cooling down {retrySeconds:0.0}s.\n{raw}",
                        rawProviderPayloadJson = raw
                    };
                    yield break;
                }

                responseHolder.response = new LLMResponse
                {
                    succeeded = false,
                    errorMessage =
                        $"[OpenAIClient] HTTP failed: {unityRequest.responseCode} {unityRequest.error}\n{raw}",
                    rawProviderPayloadJson = raw
                };
                yield break;
            }

            if (!TryExtractFirstOutputText(raw, out string outputText, out string error))
            {
                responseHolder.response = new LLMResponse
                {
                    succeeded = false,
                    errorMessage = $"[OpenAIClient] Could not extract output_text: {error}",
                    rawProviderPayloadJson = raw
                };
                yield break;
            }

            responseHolder.response = new LLMResponse
            {
                succeeded = true,
                rawText = outputText,
                rawProviderPayloadJson = raw
            };
        }

        private string BuildInstructions(LLMRequest request)
        {
            var builder = new StringBuilder(1024);

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

            // If you are relying on JSON-only output, reinforce it here (belt + suspenders).
            builder.AppendLine("OUTPUT FORMAT:");
            builder.AppendLine("- You MUST output ONLY a single JSON object. No markdown, no code fences, no commentary.");
            builder.AppendLine("- The JSON must match the expected schema required by the caller.");

            return builder.ToString().Trim();
        }

        private static JObject BuildMetadata(LLMRequest request)
        {
            var metadata = new JObject();

            if (!string.IsNullOrWhiteSpace(request.requestId))
                metadata["requestId"] = request.requestId;

            if (request.metadata != null)
            {
                foreach (var pair in request.metadata)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key)) continue;
                    metadata[pair.Key] = pair.Value ?? "";
                }
            }

            return metadata;
        }

        private static bool TryExtractFirstOutputText(string responsesApiJson, out string outputText, out string error)
        {
            outputText = "";
            error = "";

            try
            {
                var root = JObject.Parse(responsesApiJson);
                var output = root["output"] as JArray;
                if (output == null || output.Count == 0)
                {
                    error = "Missing or empty 'output' array.";
                    return false;
                }

                foreach (var item in output)
                {
                    var content = item?["content"] as JArray;
                    if (content == null) continue;

                    foreach (var c in content)
                    {
                        string? type = c?["type"]?.Value<string>();
                        if (!string.Equals(type, "output_text", StringComparison.Ordinal))
                            continue;

                        string? text = c?["text"]?.Value<string>();
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            error = "Found output_text but it was empty.";
                            return false;
                        }

                        outputText = text;
                        return true;
                    }
                }

                error = "No content item with type=output_text found.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryGetOpenAIRetryDelaySeconds(UnityWebRequest request, string responseBody, out float seconds)
        {
            seconds = 0f;

            // 1) Retry-After header: may be seconds or an HTTP date.
            // UnityWebRequest header names are case-insensitive.
            string retryAfter = request.GetResponseHeader("Retry-After");
            if (!string.IsNullOrWhiteSpace(retryAfter))
            {
                if (TryParseRetryAfterSecondsOrHttpDate(retryAfter, out seconds))
                    return true;
            }

            // 2) Some providers also emit ratelimit reset headers (best-effort).
            // OpenAI commonly uses: x-ratelimit-reset-requests / x-ratelimit-reset-tokens (formats may vary).
            // We'll try to parse them as seconds if present.
            string resetRequests = request.GetResponseHeader("x-ratelimit-reset-requests");
            if (TryParseResetHeaderAsSeconds(resetRequests, out seconds))
                return true;

            string resetTokens = request.GetResponseHeader("x-ratelimit-reset-tokens");
            if (TryParseResetHeaderAsSeconds(resetTokens, out seconds))
                return true;

            // 3) Fallback: parse JSON body "error.message" if present (best-effort).
            if (TryParseSecondsFromMessageInBody(responseBody, out seconds))
                return true;

            return false;
        }

        private static bool TryParseRetryAfterSecondsOrHttpDate(string retryAfterHeaderValue, out float seconds)
        {
            seconds = 0f;
            string value = retryAfterHeaderValue.Trim();

            // Numeric seconds case
            if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsedSeconds))
            {
                seconds = Mathf.Max(0f, parsedSeconds);
                return true;
            }

            // HTTP-date case (RFC 7231). Compute delta to now (UTC).
            if (DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var retryAt))
            {
                double deltaSeconds = (retryAt - DateTimeOffset.UtcNow).TotalSeconds;
                seconds = Mathf.Max(0f, (float)deltaSeconds);
                return true;
            }

            return false;
        }

        private static bool TryParseResetHeaderAsSeconds(string headerValue, out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrWhiteSpace(headerValue))
                return false;

            string value = headerValue.Trim();

            // Sometimes headers look like "10s" or "0.5s"
            if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase))
                value = value.Substring(0, value.Length - 1);

            if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsed))
            {
                seconds = Mathf.Max(0f, parsed);
                return true;
            }

            // Some implementations might put a timestamp; we’ll try parse as date too.
            if (DateTimeOffset.TryParse(headerValue, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var resetAt))
            {
                double deltaSeconds = (resetAt - DateTimeOffset.UtcNow).TotalSeconds;
                seconds = Mathf.Max(0f, (float)deltaSeconds);
                return true;
            }

            return false;
        }

        private static bool TryParseSecondsFromMessageInBody(string responseBody, out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrWhiteSpace(responseBody))
                return false;

            // First try JSON parse for { error: { message: "..." } }
            try
            {
                var root = JObject.Parse(responseBody);
                string message = root["error"]?["message"]?.Value<string>() ?? "";
                if (TryParseSecondsFromMessage(message, out seconds))
                    return true;
            }
            catch
            {
                // ignore parse errors
            }

            // Generic regex fallback on raw text
            return TryParseSecondsFromMessage(responseBody, out seconds);
        }

        private static bool TryParseSecondsFromMessage(string message, out float seconds)
        {
            seconds = 0f;
            if (string.IsNullOrWhiteSpace(message))
                return false;

            // Try patterns like:
            // "Please try again in 2s"
            // "Retry after 10 seconds"
            // "try again in 19.6s"
            var match = System.Text.RegularExpressions.Regex.Match(
                message,
                @"(retry after|try again in|retry in)\s+([0-9]*\.?[0-9]+)\s*(s|sec|secs|second|seconds)?",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success)
                return false;

            if (float.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float,
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

    public static class OpenAIConfig
    {
        public static string GetApiKey(string? inspectorValue, string environmentVariableName = "OPENAI_API_KEY")
        {
            if (!string.IsNullOrWhiteSpace(inspectorValue))
                return inspectorValue!;

            return Environment.GetEnvironmentVariable(environmentVariableName) ?? "";
        }
    }

}