#nullable enable
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DogGame.LLM.Providers
{
    /// <summary>
    /// Local Ollama client via OpenAI-compatible Responses API.
    /// Base URL: http://localhost:11434/v1
    /// Endpoint: POST /responses
    /// Note: Ollama supports non-stateful Responses API (no previous_response_id). (Ollama v0.13.3+)
    /// </summary>
    public sealed class OllamaClient : LLMClientBase
    {
        public override string Vendor => "Ollama";

        private readonly string baseUrl;
        private readonly string model;
        private readonly int timeoutSeconds;

        private readonly object inflightLock = new();
        private readonly System.Collections.Generic.Dictionary<string, UnityWebRequest> inflightRequestsById = new();

        // Keep these conservative; you can later source them from request.profile if desired.
        private readonly float temperatureDefault;
        private readonly int maxOutputTokensDefault;

        // You already have system blocks + schema elsewhere; but we still enforce JSON.
        private const string DefaultInstructions =
            "You are an NPC planning service for a Unity game.\n" +
            "You MUST output ONLY a single JSON object that matches the PlanResponseV1 schema.\n" +
            "No markdown, no commentary, no code fences. JSON only.";

        public OllamaClient(
            string baseUrl = "http://localhost:11434/v1",
            string model = "Gemma3:1b",
            int timeoutSeconds = 60,
            float temperature = 0.2f,
            int maxOutputTokens = 800)
        {
            this.baseUrl = baseUrl.TrimEnd('/');
            this.model = model;
            this.timeoutSeconds = Mathf.Clamp(timeoutSeconds, 5, 300);
            temperatureDefault = Mathf.Clamp01(temperature);
            maxOutputTokensDefault = Mathf.Clamp(maxOutputTokens, 64, 4096);
        }

        protected override Task<LLMResponse> SendCoreAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            // Build the string packet you already log + pass to providers.
            // If you already have a dedicated serializer/composer, you can swap this one line.
            string requestJson = BuildRequestPacketJson(request);

            return PostResponsesAsync(
                requestId: request.requestId,
                agentId: TryGetMetadata(request, "agentId") ?? "",
                requestJson: requestJson,
                cancellationToken: cancellationToken);
        }

        private string BuildRequestPacketJson(LLMRequest request)
        {
            // Best-effort: keep using the data you already carry in LLMRequest.
            // If you have PromptComposer/LLMRequestSerializer, replace this implementation with that call.
            //
            // Important: include the word "JSON" somewhere to avoid "infinite whitespace" style failures in JSON mode.
            var sb = new StringBuilder(2048);

            if (request.systemBlocks != null && request.systemBlocks.Count > 0)
            {
                sb.AppendLine("SYSTEM BLOCKS:");
                for (int i = 0; i < request.systemBlocks.Count; i++)
                {
                    sb.AppendLine(request.systemBlocks[i]);
                    sb.AppendLine();
                }
            }

            sb.AppendLine("USER PROMPT:");
            sb.AppendLine(request.userPrompt ?? "");
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(request.toolDefinitionsJson))
            {
                sb.AppendLine("TOOLS JSON:");
                sb.AppendLine(request.toolDefinitionsJson);
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(request.responseSchemaJson))
            {
                sb.AppendLine("RESPONSE SCHEMA JSON:");
                sb.AppendLine(request.responseSchemaJson);
                sb.AppendLine();
            }

            // This is the "input packet" string that the service will embed in the provider payload.
            // We keep it as a plain string because your provider services already do the wrapping.
            return sb.ToString().Trim();
        }

        private async Task<LLMResponse> PostResponsesAsync(
            string requestId,
            string agentId,
            string requestJson,
            CancellationToken cancellationToken)
        {
            string url = $"{baseUrl}/responses";

            JObject payload = new JObject
            {
                ["model"] = model,
                ["instructions"] = DefaultInstructions,
                ["input"] =
                    "IMPORTANT: Output ONLY JSON.\n" +
                    "Return ONLY a PlanResponseV1 JSON object with fields: schema, requestId, agentId, intentions, debug.\n" +
                    "You MUST set schema=\"PlanResponseV1\" and copy requestId and agentId exactly.\n" +
                    $"requestId={requestId} agentId={agentId}\n" +
                    "Now plan based on this input packet:\n" +
                    requestJson,
                ["temperature"] = temperatureDefault,
                ["max_output_tokens"] = maxOutputTokensDefault,
                ["text"] = new JObject
                {
                    ["format"] = new JObject
                    {
                        ["type"] = "json_object"
                    }
                },
                ["metadata"] = new JObject
                {
                    ["requestId"] = requestId,
                    ["agentId"] = agentId,
                    ["provider"] = "ollama"
                }
            };

            byte[] bodyBytes = Encoding.UTF8.GetBytes(payload.ToString());

            using var unityRequest = new UnityWebRequest(url, "POST");
            unityRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
            unityRequest.downloadHandler = new DownloadHandlerBuffer();
            unityRequest.timeout = timeoutSeconds;
            unityRequest.SetRequestHeader("Content-Type", "application/json");

            Directory.Instance.llmDebugMonitor.DebugLLMRequest(payload.ToString());
            unityRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
            unityRequest.downloadHandler = new DownloadHandlerBuffer();
            unityRequest.timeout = timeoutSeconds;
            unityRequest.SetRequestHeader("Content-Type", "application/json");

            // Track inflight so we can Abort() on Play stop.
            lock (inflightLock)
            {
                inflightRequestsById[requestId] = unityRequest;
            }

            // UnityWebRequest isn't natively awaitable; use completed callback.
            var tcs = new TaskCompletionSource<LLMResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Cancellation: abort the request.
            CancellationTokenRegistration ctr = cancellationToken.Register(() =>
            {
                try { unityRequest.Abort(); } catch { /* ignore */ }
            });

            Directory.Instance.llmDebugMonitor.DebugLLMRequest(payload.ToString());

            unityRequest.SendWebRequest().completed += _ =>
            {
                try
                {
                    Directory.Instance.llmDebugMonitor.DebugLLMResponse(unityRequest.downloadHandler?.text);

                    ctr.Dispose();

                    // Remove from inflight tracking
                    lock (inflightLock)
                    {
                        inflightRequestsById.Remove(requestId);
                    }

                    string raw = unityRequest.downloadHandler?.text ?? "";
                    bool ok = unityRequest.result == UnityWebRequest.Result.Success;

                    if (!ok)
                    {
                        // If this was an Abort() from shutdown, treat it as canceled (don’t spam warnings).
                        bool aborted = unityRequest.result == UnityWebRequest.Result.ConnectionError &&
                                    (unityRequest.error != null && unityRequest.error.IndexOf("aborted", StringComparison.OrdinalIgnoreCase) >= 0);

                        tcs.TrySetResult(new LLMResponse
                        {
                            succeeded = false,
                            isRateLimited = unityRequest.responseCode == 429,
                            retryAfterSeconds = unityRequest.responseCode == 429 ? 1f : 0f,
                            errorMessage = aborted
                                ? $"[{Vendor}] Aborted requestId={requestId} (likely Play stop)."
                                : $"[{Vendor}] HTTP failed: {unityRequest.responseCode} {unityRequest.error}\n{raw}"
                        });
                        return;
                    }

                    if (!TryExtractFirstOutputText(raw, out string planJson, out string error))
                    {
                        tcs.TrySetResult(new LLMResponse
                        {
                            succeeded = false,
                            errorMessage = $"[{Vendor}] Could not extract output_text: {error}\nRAW:\n{raw}"
                        });
                        return;
                    }

                    tcs.TrySetResult(new LLMResponse
                    {
                        succeeded = true,
                        rawProviderPayloadJson = planJson
                    });
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult(new LLMResponse
                    {
                        succeeded = false,
                        errorMessage = $"[{Vendor}] Exception: {ex.GetType().Name}: {ex.Message}"
                    });
                }
                finally
                {
                    // IMPORTANT: Dispose here (not with 'using var') so Abort() is possible while inflight.
                    try { unityRequest.Dispose(); } catch { /* ignore */ }
                }
            };

            return await tcs.Task.ConfigureAwait(false);
        }

        // Matches your OpenAI Responses extraction logic: root.output[].content[].type=="output_text"
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

                        outputText = text.Trim();
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

        private static string? TryGetMetadata(LLMRequest request, string key)
        {
            if (request.metadata == null) return null;
            return request.metadata.TryGetValue(key, out var value) ? value : null;
        }


    }
}