#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace DogGame.LLM.Core
{
    public abstract class LLMClientBase : ILLMClient
    {
        public abstract string Vendor { get; }
        
        public string ResolveApiKey(string apiKeyEnvironmentVariable, string apiKey)
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
                return apiKey.Trim();

            if (!string.IsNullOrWhiteSpace(apiKeyEnvironmentVariable))
            {
                string? env = Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(env))
                    return env.Trim();
            }

            return "";
        }        
        
        // -------------------------
        // Stale-session mechanism:
        // increment this whenever play mode stops / world resets / router resets.
        // Any inflight request from an older session becomes a no-op.
        // -------------------------
        private int sessionToken = 1;

        /// <summary>
        /// Call this when you want all inflight requests to become stale no-ops (e.g., OnDisable / OnDestroy of router).
        /// </summary>
        public int BumpSessionToken()
        {
            return Interlocked.Increment(ref sessionToken);
        }

        protected int CurrentSessionToken => Volatile.Read(ref sessionToken);

        // -------------------------
        // Public entrypoint w/ retries (but respects rate-limit flags)
        // -------------------------
        public async Task<LLMResponse> SendAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.profile == null) throw new ArgumentNullException(nameof(request.profile));

            // If this client tracks cooldown, avoid retrying/spamming during cooldown
            if (this is ICooldownAware cooldownAware && cooldownAware.IsCoolingDown)
            {
                return new LLMResponse
                {
                    succeeded = false,
                    isRateLimited = true,
                    retryAfterSeconds = cooldownAware.CooldownRemainingSeconds,
                    errorMessage = $"[{Vendor}] Cooling down ({cooldownAware.CooldownRemainingSeconds:0.0}s). Skipping requestId={request.requestId}.",
                    wasStale = false
                };
            }

            const int maxAttempts = 3;
            float backoffSeconds = 0.5f;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Re-check cooldown between attempts
                if (this is ICooldownAware cooldownAware2 && cooldownAware2.IsCoolingDown)
                {
                    return new LLMResponse
                    {
                        succeeded = false,
                        isRateLimited = true,
                        retryAfterSeconds = cooldownAware2.CooldownRemainingSeconds,
                        errorMessage = $"[{Vendor}] Cooling down ({cooldownAware2.CooldownRemainingSeconds:0.0}s). Skipping requestId={request.requestId}.",
                        wasStale = false
                    };
                }

                try
                {
                    LLMResponse response = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);

                    if (response == null)
                    {
                        return new LLMResponse
                        {
                            succeeded = false,
                            errorMessage = $"{Vendor}: null response",
                            wasStale = false
                        };
                    }

                    // If it was stale, do not retry (it was intentionally invalidated)
                    if (response.wasStale)
                        return response;

                    // ✅ Critical: if provider says it's rate-limited, STOP retrying automatically
                    if (response.isRateLimited)
                    {
                        Debug.LogWarning($"[{Vendor}] Rate limited. retryAfter={response.retryAfterSeconds:0.0}s requestId={request.requestId}");
                        return response;
                    }

                    return response;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    bool isLastAttempt = attempt == maxAttempts;
                    Debug.LogWarning($"[{Vendor}] LLM attempt {attempt}/{maxAttempts} failed: {exception}");

                    if (isLastAttempt)
                    {
                        return new LLMResponse
                        {
                            succeeded = false,
                            errorMessage = $"{Vendor}: {exception.GetType().Name}: {exception.Message}",
                            wasStale = false
                        };
                    }

                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), cancellationToken).ConfigureAwait(false);
                    backoffSeconds *= 2f;
                }
            }

            return new LLMResponse
            {
                succeeded = false,
                errorMessage = $"{Vendor}: unexpected fallthrough",
                wasStale = false
            };
        }

        protected abstract Task<LLMResponse> SendCoreAsync(LLMRequest request, CancellationToken cancellationToken);

        // ============================================================
        // Shared helpers used by all vendor clients (keeps them uniform)
        // ============================================================

        protected sealed class PostSpec
        {
            public string url = "";
            public JObject payload = new JObject();
            public int timeoutSeconds = 300;
            public Dictionary<string, string>? headers;
        }

        protected struct ParseResult
        {
            public bool ok;
            public string? outputText;
            public bool isRateLimited;
            public float retryAfterSeconds;
            public string? error;
        }

        protected async Task<LLMResponse> PostJsonAsync(
            PostSpec spec,
            CancellationToken cancellationToken,
            Func<string, ParseResult> parse)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (parse == null) throw new ArgumentNullException(nameof(parse));
            if (string.IsNullOrWhiteSpace(spec.url)) throw new ArgumentException("PostSpec.url is empty.");

            // Capture session token at start; any later bump makes this response stale.
            int tokenAtStart = CurrentSessionToken;

            byte[] bodyBytes = Encoding.UTF8.GetBytes(spec.payload.ToString());

            using var unityRequest = new UnityWebRequest(spec.url, "POST");
            unityRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
            unityRequest.downloadHandler = new DownloadHandlerBuffer();
            unityRequest.timeout = Mathf.Clamp(spec.timeoutSeconds, 5, 300);
            unityRequest.SetRequestHeader("Content-Type", "application/json");

            if (spec.headers != null)
            {
                foreach (var kv in spec.headers)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Key))
                        unityRequest.SetRequestHeader(kv.Key, kv.Value);
                }
            }

            // Optional debug monitor hook (your project has Dir.Instance.llmDebugMonitor)
            string payloadJson = spec.payload.ToString();
            string agentId = ExtractAgentId(payloadJson) ?? "<unknown>";
            string requestId = Guid.NewGuid().ToString("N");
            var debugMonitor = Dir.Instance != null ? Dir.Instance.llmDebugMonitor : null;
            if (debugMonitor != null)
            {
                debugMonitor.DebugLLMRequest(
                    payloadJson,
                    agentId,
                    requestId
                );
            }

            var tcs = new TaskCompletionSource<LLMResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            CancellationTokenRegistration ctr = cancellationToken.Register(() =>
            {
                try { unityRequest.Abort(); } catch { /* ignore */ }
            });

            unityRequest.SendWebRequest().completed += _ =>
            {
                try
                {
                    ctr.Dispose();

                    // Stale-session check at completion time
                    bool stale = tokenAtStart != CurrentSessionToken;

                    string raw = unityRequest.downloadHandler?.text ?? "";
                    if (debugMonitor != null)
                    {
                        debugMonitor.DebugLLMResponse(
                            raw,
                            agentId,
                            requestId,
                            stale
                        );
                    }

                    // Stale-session check at completion time
                    if (stale)
                    {
                        tcs.TrySetResult(new LLMResponse
                        {
                            succeeded = false,
                            wasStale = true,
                            errorMessage = $"[{Vendor}] Response ignored (stale session)."
                        });
                        return;
                    }

                    bool okHttp = unityRequest.result == UnityWebRequest.Result.Success;

                    if (!okHttp)
                    {
                        bool is429 = unityRequest.responseCode == 429;
                        tcs.TrySetResult(new LLMResponse
                        {
                            succeeded = false,
                            wasStale = false,
                            isRateLimited = is429,
                            retryAfterSeconds = is429 ? 1f : 0f,
                            errorMessage = $"[{Vendor}] HTTP failed: {unityRequest.responseCode} {unityRequest.error}\n{raw}",
                            rawProviderPayloadJson = raw
                        });
                        return;
                    }

                    ParseResult pr = parse(raw);

                    if (!pr.ok || string.IsNullOrWhiteSpace(pr.outputText))
                    {
                        tcs.TrySetResult(new LLMResponse
                        {
                            succeeded = false,
                            wasStale = false,
                            errorMessage = $"[{Vendor}] Parse failed: {pr.error}\nRAW:\n{raw}",
                            rawProviderPayloadJson = raw
                        });
                        return;
                    }

                    tcs.TrySetResult(new LLMResponse
                    {
                        succeeded = true,
                        wasStale = false,
                        rawText = pr.outputText!,
                        rawProviderPayloadJson = raw,
                        isRateLimited = pr.isRateLimited,
                        retryAfterSeconds = pr.retryAfterSeconds
                    });
                }
                catch (Exception ex)
                {
                    tcs.TrySetResult(new LLMResponse
                    {
                        succeeded = false,
                        wasStale = false,
                        errorMessage = $"[{Vendor}] Exception: {ex.GetType().Name}: {ex.Message}"
                    });
                }
            };

            return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Common request->text builder used by OpenAI/Ollama/Gemini for consistency.
        /// This is the text inserted into provider payload under "input" (or Gemini content).
        /// </summary>
        protected string BuildRequestPacketText(LLMRequest request)
        {
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

            if (request.toolDefinitions != null)
            {
                sb.AppendLine("TOOLS JSON:");
                sb.AppendLine(MinifyJson(LLMPacketJsonPrinter.PrintJson(request.toolDefinitions, pretty: true)));
                sb.AppendLine();
            }
            //else if (!string.IsNullOrWhiteSpace(request.toolDefinitionsJson))
            //{
            //    sb.AppendLine("TOOLS JSON:");
            //    sb.AppendLine(LLMJsonNormalizer.Normalize(request.toolDefinitionsJson));
            //    sb.AppendLine();
            //}

            if (request.responseSchema != null)
            {
                sb.AppendLine("RESPONSE SCHEMA JSON:");
                sb.AppendLine(MinifyJson(LLMPacketJsonPrinter.PrintJson(request.responseSchema, pretty: true)));
                sb.AppendLine();
            }
            else if (!string.IsNullOrWhiteSpace(request.responseSchemaJson))
            {
                sb.AppendLine("RESPONSE SCHEMA JSON:");
                sb.AppendLine(LLMJsonNormalizer.Normalize(request.responseSchemaJson));
                sb.AppendLine();
            }

            sb.AppendLine("END OF REQUEST PACKET (JSON).");

            return sb.ToString().Trim();
        }

        // OpenAI/Ollama Responses API extraction: root.output[].content[].type == "output_text"
        protected static ParseResult ParseResponsesApi_OutputText(string responsesApiJson)
        {
            var result = new ParseResult
            {
                ok = false,
                outputText = null,
                isRateLimited = false,
                retryAfterSeconds = 0f,
                error = ""
            };

            try
            {
                var root = JObject.Parse(responsesApiJson);

                // Sometimes we can detect rate-limit fields; mostly handled by HTTP, but keep hook.
                // If you later add provider-specific fields, wire them here.

                var output = root["output"] as JArray;
                if (output == null || output.Count == 0)
                {
                    result.error = "Missing or empty 'output' array.";
                    return result;
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
                            result.error = "Found output_text but it was empty.";
                            return result;
                        }

                        result.ok = true;
                        result.outputText = text.Trim();
                        return result;
                    }
                }

                result.error = "No content item with type=output_text found.";
                return result;
            }
            catch (Exception ex)
            {
                result.error = ex.Message;
                return result;
            }
        }

        protected static string? TryGetMetadata(LLMRequest request, string key)
        {
            if (request.metadata == null) return null;
            return request.metadata.TryGetValue(key, out var value) ? value : null;
        }

        public static class LLMPacketJsonPrinter
        {
            public static string PrintJson(JToken token, bool pretty)
            {
                return token.ToString(pretty ? Formatting.Indented : Formatting.None);
            }

            public static bool TryParseObject(string json, out JObject? obj, out string? error)
            {
                obj = null;
                error = null;

                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "Empty JSON string.";
                    return false;
                }

                try
                {
                    obj = JObject.Parse(json);
                    return true;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    return false;
                }
            }
        }

        private static string? ExtractAgentId(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            const string key = "\"agentId\"";

            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0)
                return null;

            int colonIndex = json.IndexOf(':', keyIndex + key.Length);
            if (colonIndex < 0)
                return null;

            int firstQuote = json.IndexOf('"', colonIndex + 1);
            if (firstQuote < 0)
                return null;

            int secondQuote = json.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0)
                return null;

            return json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        private static string MinifyJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return "";

            try
            {
                var token = JToken.Parse(json);
                return token.ToString(Formatting.None);
            }
            catch
            {
                // If it isn't valid JSON, just collapse whitespace
                return json.Replace("\r", "")
                        .Replace("\n", " ")
                        .Replace("\t", " ")
                        .Trim();
            }
        }
    }
}
