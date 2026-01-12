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
    /// LLM service using Google Gemini API.
    /// Parallel to RemoteLLMService.
    /// </summary>
    public sealed class GeminiLLMService : MonoBehaviour
    {
        [Header("Gemini")]
        [Tooltip("If empty, we'll try to read from environment variable GEMINI_API_KEY.")]
        [SerializeField] private string apiKey = "";

        [SerializeField] private string apiKeyEnvironmentVariable = "GEMINI_API_KEY";

        [Tooltip("Model name, e.g. gemini-1.5-flash, gemini-1.5-pro, gemini-pro.")]
        [SerializeField] private string model = "gemini-1.5-flash";

        [Tooltip("Gemini API endpoint format.")]
        [SerializeField] private string endpointUrlFormat = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

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

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = Environment.GetEnvironmentVariable(apiKeyEnvironmentVariable) ?? "";
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Debug.LogWarning(
                    $"[GeminiLLMService] No API key set. Put it in the Inspector or set env var '{apiKeyEnvironmentVariable}'.",
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
                Debug.LogWarning("[GeminiLLMService] SubmitRequest called with no API key.", this);
                return;
            }

            StartCoroutine(PostRequestCoroutine(requestId, requestJson, agentId, onResponseJson));
        }

        private IEnumerator PostRequestCoroutine(string requestId, string requestJson, string agentId, Action<string> onResponseJson)
        {
            Debug.Log($"[GeminiLLMService.PostRequestCoroutine] ({requestJson})", this);

            string fullPrompt =
                $"{systemInstructions}\n\n" +
                "IMPORTANT: Output ONLY JSON.\n" +
                "Return ONLY a PlanResponseV1 JSON object with fields: schema, requestId, agentId, intentions, debug.\n" +
                "You MUST set schema=\"PlanResponseV1\" and copy requestId and agentId exactly.\n" +
                "Example shape:\n" +
                "{ \"schema\":\"PlanResponseV1\", \"requestId\":\"...", \"agentId\":\"...", \"intentions\":[], \"debug\":{ \"confidence\":0.5, \"notes\":[] } }\n" +
                $"requestId={requestId} agentId={agentId}\n" +
                "Now plan based on this input packet (may be partial):\n" +
                requestJson;

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
                    ["temperature"] = temperature,
                    ["maxOutputTokens"] = maxOutputTokens,
                    ["response_mime_type"] = "application/json" // Ask for JSON response
                }
            };
            
            var url = string.Format(endpointUrlFormat, model, apiKey);
            byte[] bodyBytes = Encoding.UTF8.GetBytes(payload.ToString());

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = timeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"[GeminiLLMService] SendWebRequest to {url} ({payload.ToString()}) ", this);

            yield return request.SendWebRequest();

            Debug.Log(
                $"[GeminiLLMService] HTTP Response: {request.responseCode}\n" +
                $"Downloaded: {request.downloadHandler?.data?.Length ?? 0} bytes\n" +
                request.downloadHandler?.text, this);

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[GeminiLLMService] HTTP failed: {request.responseCode} {request.error}\n{request.downloadHandler?.text}",
                    this);
                yield break;
            }

            string raw = request.downloadHandler.text;

            if (!TryExtractPlanJson(raw, out string planJson, out string error))
            {
                Debug.LogWarning($"[GeminiLLMService] Could not extract plan JSON: {error}\nRAW:\n{raw}", this);
                yield break;
            }

            onResponseJson?.Invoke(planJson);
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
                    {
                        error = $"Request was blocked. Feedback: {promptFeedback}";
                    }
                    else
                    {
                        error = "Missing or empty 'candidates' array in response.";
                    }
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

                // The model might wrap the JSON in markdown fences.
                planJson = text.Trim();
                if (planJson.StartsWith("```json"))
                {
                    planJson = planJson.Substring(7);
                }
                if (planJson.StartsWith("```"))
                {
                    planJson = planJson.Substring(3);
                }
                if (planJson.EndsWith("```"))
                {
                    planJson = planJson.Substring(0, planJson.Length - 3);
                }

                planJson = planJson.Trim();

                Debug.Log("[GeminiLLMService] Extracted plan JSON:\n" + planJson);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}