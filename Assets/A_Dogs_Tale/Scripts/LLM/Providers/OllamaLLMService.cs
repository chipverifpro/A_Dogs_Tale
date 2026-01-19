#nullable enable
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM
{
    /// <summary>
    /// LLM service using Ollama's OpenAI-compatible API (local by default).
    /// Uses OpenAI Responses API: POST {baseUrl}/responses
    /// Ollama supports /v1/responses in OpenAI-compat mode (non-stateful). 
    /// </summary>
    public sealed class OllamaLLMService : MonoBehaviour
    {
        [Header("Ollama (OpenAI-compatible)")]
        [Tooltip("Base URL including /v1. Example: http://localhost:11434/v1")]
        [SerializeField] private string baseUrl = "http://localhost:11434/v1";

        [Tooltip("Model name in Ollama, e.g. llama3.2, mistral, etc.")]
        [SerializeField] private string model = "Gemma3:1b";

        [Tooltip("HTTP timeout (seconds).")]
        [SerializeField] private int timeoutSeconds = 60;

        [Header("Output control")]
        [Tooltip("Caps how many tokens the model may output.")]
        [SerializeField] private int maxOutputTokens = 800;

        [Tooltip("Low temperature = more deterministic plans.")]
        [Range(0f, 1f)]
        [SerializeField] private float temperature = 0.2f;

        [TextArea(6, 20)]
        [Tooltip("Extra system-level instructions added on top of your requestJson payload.")]
        [SerializeField] private string systemInstructions =
            "You are an NPC planning service for a Unity game.\n" +
            "You MUST output ONLY a single JSON object that matches the PlanResponseV1 schema.\n" +
            "No markdown, no commentary, no code fences. JSON only.";

        public string Model => model;

        private readonly object inflightLock = new();
        private readonly System.Collections.Generic.HashSet<UnityWebRequest> inflightRequests = new();

        private int sessionToken = 0; // increments on enable/disable so late responses can be ignored

        /// <summary>
        /// Submit an LLM request. The callback receives the PlanResponseV1 JSON (string).
        /// </summary>
        public void SubmitRequest(string requestId, string requestJson, string agentId, Action<string> onResponseJson)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                Debug.LogWarning("[OllamaLLMService] baseUrl is empty.", this);
                return;
            }

            StartCoroutine(PostResponsesCoroutine(requestId, requestJson, agentId, onResponseJson));
        }

        private IEnumerator PostResponsesCoroutine(string requestId, string requestJson, string agentId, Action<string> onResponseJson)
        {
            string endpointUrl = $"{baseUrl.TrimEnd('/')}/responses";
            int tokenAtDispatch = sessionToken;

            string inputHeader =
                $"requestId={requestId}\nagentId={agentId}\n" +
                "Return ONLY JSON matching responseSchemaJson.\n";

            JObject payload = new JObject
            {
                ["model"] = model,
                ["instructions"] = systemInstructions,
                ["input"] = inputHeader + "\nREQUEST_PACKET_JSON:\n" + requestJson,
                ["temperature"] = temperature,
                ["max_output_tokens"] = maxOutputTokens,
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
                    ["agentId"] = agentId
                }
            };

            byte[] bodyBytes = Encoding.UTF8.GetBytes(payload.ToString());

            var unityRequest = new UnityWebRequest(endpointUrl, "POST");
            unityRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
            unityRequest.downloadHandler = new DownloadHandlerBuffer();
            unityRequest.timeout = timeoutSeconds;
            unityRequest.SetRequestHeader("Content-Type", "application/json");

            lock (inflightLock)
            {
                inflightRequests.Add(unityRequest);
            }

            Debug.Log($"[OllamaLLMService] POST {endpointUrl} model={model} bytes={bodyBytes.Length} requestId={requestId}", this);
            Directory.Instance.llmDebugMonitor.DebugLLMRequest(payload.ToString());

            try
            {
                yield return unityRequest.SendWebRequest();

                // If play session changed (stop/start), ignore stale completion
                if (tokenAtDispatch != sessionToken)
                {
                    Debug.Log($"[OllamaLLMService] Ignoring stale response (session changed) requestId={requestId}", this);
                    yield break;
                }

                string raw = unityRequest.downloadHandler?.text ?? "";
                Directory.Instance.llmDebugMonitor.DebugLLMResponse(raw);

                Debug.Log($"[OllamaLLMService] HTTP {unityRequest.responseCode} result={unityRequest.result} requestId={requestId}\n{raw}", this);

                if (unityRequest.result != UnityWebRequest.Result.Success)
                {
                    // If we aborted intentionally, don't spam warnings.
                    bool likelyAborted =
                        unityRequest.error != null &&
                        unityRequest.error.IndexOf("aborted", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (!likelyAborted)
                    {
                        Debug.LogWarning(
                            $"[OllamaLLMService] HTTP failed: {unityRequest.responseCode} {unityRequest.error}\n{raw}",
                            this);
                    }
                    yield break;
                }

                if (!TryExtractFirstOutputText(raw, out string planJson, out string error))
                {
                    Debug.LogWarning($"[OllamaLLMService] Could not extract output_text: {error}\nRAW:\n{raw}", this);
                    yield break;
                }

                onResponseJson?.Invoke(planJson);
            }
            finally
            {
                lock (inflightLock)
                {
                    inflightRequests.Remove(unityRequest);
                }

                try { unityRequest.Dispose(); } catch { /* ignore */ }
            }
        }
        // Same helper as your OpenAI service: Responses API wrapper -> output_text
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

        private void OnEnable()
        {
            // New play session (or re-enabled)
            sessionToken++;
        }

        private void OnDisable()
        {
            // Called when you stop play mode in Editor too.
            sessionToken++;
            CancelAll("service_disabled");
        }

        private void OnApplicationQuit()
        {
            sessionToken++;
            CancelAll("application_quit");
        }

        /// <summary>
        /// Abort all inflight HTTP requests (best effort).
        /// This stops Unity from waiting and usually causes Ollama to stop generating shortly after.
        /// </summary>
        public void CancelAll(string reason)
        {
            UnityWebRequest[] toAbort;

            lock (inflightLock)
            {
                toAbort = new UnityWebRequest[inflightRequests.Count];
                inflightRequests.CopyTo(toAbort);
                inflightRequests.Clear();
            }

            // Stop coroutines so we don't keep processing after play-stop.
            try { StopAllCoroutines(); } catch { /* ignore */ }

            Debug.LogWarning($"[OllamaLLMService] CancelAll reason={reason} aborting={toAbort.Length}", this);

            for (int i = 0; i < toAbort.Length; i++)
            {
                try { toAbort[i]?.Abort(); } catch { /* ignore */ }
                try { toAbort[i]?.Dispose(); } catch { /* ignore */ }
            }
        }

    }
}