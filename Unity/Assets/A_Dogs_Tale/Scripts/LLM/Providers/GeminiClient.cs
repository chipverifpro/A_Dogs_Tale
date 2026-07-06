#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DogGame.LLM.Providers
{
    /// <summary>
    /// Google Gemini generateContent client adapter.
    /// Endpoint format:
    ///   https://generativelanguage.googleapis.com/v1beta/models/{MODEL}:generateContent?key={API_KEY}
    /// </summary>
    public sealed class GeminiClient : LLMClientBase
    {
        public override string Vendor => "Gemini";
        private readonly string baseUrl = "https://generativelanguage.googleapis.com/v1beta";
        private readonly string model = "gemini-2.5-flash-lite";
        public readonly string apiKeyEnvironmentVariable = "GEMINI_API_KEY";
        public readonly int timeoutSeconds = 60;
        public readonly float temperature = 0.2f;
        public readonly int maxOutputTokens = 1600;
        public readonly string modelUniqueInstructions = "";

        // constructor:
        public GeminiClient(string model = "gemini-2.5-flash-lite")
        {
            this.model = model;
        }

        protected override Task<LLMResponse> SendCoreAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            string requestText = BuildRequestPacketText(request);

            string requestId = request.requestId ?? "";
            string agentId = TryGetMetadata(request, "agentId") ?? "";

            // Gemini wants "contents" with "parts"
            // We keep your same core instruction style: "JSON only", then supply the request packet.
            string prompt =
                modelUniqueInstructions + "\n\n" +
                BuildCommandModeInstruction(request, requestId, agentId) +
                "REQUEST_PACKET:\n" + requestText;

            var payload = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray
                        {
                            new JObject { ["text"] = prompt }
                        }
                    }
                },
                ["generationConfig"] = new JObject
                {
                    ["temperature"] = temperature,
                    ["maxOutputTokens"] = ResolveMaxOutputTokens(request, maxOutputTokens),
                    ["response_mime_type"] = "application/json"
                }
            };

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("[Gemini] baseUrl is not set.");
            string apiKey = ResolveApiKey(apiKeyEnvironmentVariable, "");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("[Gemini] apiKey is not set.");
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("[Gemini] model is not set.");

            string url = $"{baseUrl.TrimEnd('/')}/models/{model}:generateContent?key={apiKey}";  

            var spec = new PostSpec
            {
                url = url,
                payload = payload,
                timeoutSeconds = timeoutSeconds,
                headers = null,
                debugRequestId = requestId,
                debugAgentId = agentId,
                debugRequestPacketJson = global::DogGame.LLM.LLMRequestSerializer.ToJson(request)
            };

            return PostJsonAsync(spec, cancellationToken, ParseGeminiGenerateContent);
        }

        private static ParseResult ParseGeminiGenerateContent(string rawJson)
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
                var root = JObject.Parse(rawJson);

                // Typical success shape:
                // candidates[0].content.parts[0].text
                var candidates = root["candidates"] as JArray;
                if (candidates == null || candidates.Count == 0)
                {
                    result.error = "Missing or empty candidates.";
                    return result;
                }

                var content = candidates[0]?["content"];
                var parts = content?["parts"] as JArray;
                if (parts == null || parts.Count == 0)
                {
                    result.error = "Missing or empty content.parts.";
                    return result;
                }

                string? text = parts[0]?["text"]?.Value<string>();
                if (string.IsNullOrWhiteSpace(text))
                {
                    result.error = "Missing text in first part.";
                    return result;
                }

                result.ok = true;
                result.outputText = text.Trim();
                return result;
            }
            catch (Exception ex)
            {
                result.error = ex.Message;
                return result;
            }
        }
    }
}
