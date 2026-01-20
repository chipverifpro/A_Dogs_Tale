#nullable enable
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;
using DogGame.LLM.Debugging;

namespace DogGame.LLM
{
    /// <summary>
    /// Remote-first LLM service using OpenAI Responses API.
    /// Drop-in replacement for FakeLLMService: same SubmitRequest signature.
    /// </summary>
    public sealed class OBSOLETE_OpenAILLMService : MonoBehaviour
    {
        [Header("OpenAI")]
        [Tooltip("If empty, we'll try to read from environment variable OPENAI_API_KEY.")]
        [SerializeField] private string apiKey = "";

        [SerializeField] private string apiKeyEnvironmentVariable = "OPENAI_API_KEY";

        [Tooltip("Model name, e.g. gpt-4.1-mini / gpt-4.1 / gpt-5, depending on your account access.")]
        [SerializeField] private string model = "gpt-4.1-mini";

        [Tooltip("Responses API endpoint.")]
        [SerializeField] private string endpointUrl = "https://api.openai.com/v1/responses";

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

        private readonly object inflightLock = new();
        private readonly System.Collections.Generic.Dictionary<string, UnityWebRequest> inflightRequestsById = new();

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable) ?? "";
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogWarning(
                    $"[RemoteLLMService] No API key set. Put it in the Inspector or set env var '{apiKeyEnvironmentVariable}'.",
                    this);
            }
        }

        /// <summary>
        /// Submit an LLM request. The callback receives the PlanResponseV1 JSON (string).
        /// </summary>
        public void SubmitRequest(string requestId, string requestJson, string agentId, Action<string> onResponseJson)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogWarning("[RemoteLLMService] SubmitRequest called with no API key.", this);
                return;
            }

            StartCoroutine(PostResponsesCoroutine(requestId, requestJson, agentId, onResponseJson));
        }

        private IEnumerator PostResponsesCoroutine(string requestId, string requestJson, string agentId, Action<string> onResponseJson)
        {
Debug.Log(
    $"[RemoteLLMService.PostResponsesCoroutine] ({requestJson})",
    this);            
            // Build Responses API payload:
            // - instructions: extra system layer you control here
            // - input: your assembled packet (requestJson)
            // - text.format: json_object to force valid JSON output
            // Docs: use text.format in Responses API for JSON/Structured Outputs
            // (we're using json_object mode here for simplicity).
            JObject payload = new JObject
            {
                ["model"] = model,
                ["instructions"] = systemInstructions,
                ["input"] =
                    "IMPORTANT: Output ONLY JSON.\n" +
                    "Return ONLY a PlanResponseV1 JSON object with fields: schema, requestId, agentId, intentions, debug.\n" +
                    "You MUST set schema=\"PlanResponseV1\" and copy requestId and agentId exactly.\n" +
                    "Example shape:\n" +
                    "{ \"schema\":\"PlanResponseV1\", \"requestId\":\"...\", \"agentId\":\"...\", \"intentions\":[], \"debug\":{ \"confidence\":0.5, \"notes\":[] } }\n" +
                    $"requestId={requestId} agentId={agentId}\n" +
                    "Now plan based on this input packet (may be partial):\n" +
                    requestJson,
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

            using var request = new UnityWebRequest(endpointUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeoutSeconds;

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

Debug.Log(
    $"[RemoteLLMService] SendWebRequest ({payload.ToString()}) ",
    this);

            yield return request.SendWebRequest();

Debug.Log(
    $"[RemoteLLMService] HTTP OK ({request.responseCode}) " +
    $"bytes={request.downloadHandler?.data?.Length ?? 0}\n" +
    request.downloadHandler?.text,
    this);
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[RemoteLLMService] HTTP failed: {request.responseCode} {request.error}\n{request.downloadHandler?.text}",
                    this);
                yield break;
            }

            string raw = request.downloadHandler!.text;

            LLMPacketLogger.LogResponse(
                agentId: agentId,
                requestId: requestId,
                provider: "OpenAI",
                responseJson: raw);

            Debug.Log($"LLMWalkthrough1B: RemoteLLMService(OpenAI).PostResponsesCoroutine response_code={request.responseCode}, raw_response={raw}");

            // Responses API returns a wrapper object. We extract the first output_text.
            // Then we pass that string back as the plan JSON your parser expects.

Debug.Log("[RemoteLLMService] Parsing response JSON...", this);

            if (!TryExtractFirstOutputText(raw, out string planJson, out string error))
            {
                Debug.LogWarning($"[RemoteLLMService] Could not extract output_text: {error}\nRAW:\n{raw}", this);
                yield break;
            }
            
            onResponseJson?.Invoke(planJson);
        }

        private static bool TryExtractFirstOutputText(string responsesApiJson, out string outputText, out string error)
        {

Debug.Log("[RemoteLLMService] Inspecting Responses API output...");

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

                // Find first content item of type "output_text"
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

                        Debug.Log("[RemoteLLMService] Extracted output_text:\n" + outputText);

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

        public void CancelAll(string reason)
        {
            UnityWebRequest[] requests;

            lock (inflightLock)
            {
                requests = new UnityWebRequest[inflightRequestsById.Count];
                int i = 0;
                foreach (var kvp in inflightRequestsById)
                    requests[i++] = kvp.Value;

                inflightRequestsById.Clear();
            }

            Debug.LogWarning($"[OllamaClient] CancelAll reason={reason} aborting={requests.Length}");

            for (int i = 0; i < requests.Length; i++)
            {
                try { requests[i]?.Abort(); } catch { /* ignore */ }
            }
        }
    }
}