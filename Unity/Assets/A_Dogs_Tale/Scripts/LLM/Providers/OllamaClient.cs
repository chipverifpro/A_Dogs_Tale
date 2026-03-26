#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DogGame.LLM.Providers
{
    /// <summary>
    /// Local Ollama client via OpenAI-compatible Responses API.
    /// Base URL: http://localhost:11434/v1
    /// Endpoint: POST /responses
    /// </summary>
    public sealed class OllamaClient : LLMClientBase
    {
        public override string Vendor => "Ollama";
        public readonly string baseUrl = "http://localhost:11434/v1";
        public readonly string model = "Gemma3:1b";
        public readonly string apiKeyEnvironmentVariable = ""; // none needed
        public readonly string apiKey = ""; // none needed
        public readonly int timeoutSeconds = 300;
        public readonly float temperature = 0.2f;
        public readonly int maxOutputTokens = 800;
        public readonly string modelUniqueInstructions =
            "You MUST output only the requested structured result. No markdown, no commentary, no code fences.";

        // constructor:
        public OllamaClient(string model = "Gemma3:1b")
        {
            this.model = model;
        }

        protected override Task<LLMResponse> SendCoreAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            string requestText = BuildRequestPacketText(request);

            string requestId = request.requestId ?? "";
            string agentId = TryGetMetadata(request, "agentId") ?? "";

            var payload = new JObject
            {
                ["model"] = string.IsNullOrWhiteSpace(request.profile?.model) ? model : request.profile.model,
                ["instructions"] = modelUniqueInstructions,
                ["input"] =
                    BuildCommandModeInstruction(request, requestId, agentId) +
                    "REQUEST_PACKET:\n" + requestText,
                ["temperature"] = temperature,
                ["max_output_tokens"] = maxOutputTokens,
                ["text"] = new JObject
                {
                    ["format"] = new JObject { ["type"] = "json_object" }
                },
                ["metadata"] = new JObject
                {
                    ["requestId"] = requestId,
                    ["agentId"] = agentId,
                    ["provider"] = "ollama"
                }
            };

            //var headers = new Dictionary<string, string>();
            //if (!string.IsNullOrWhiteSpace(apiKey))
            //    headers["Authorization"] = $"Bearer {apiKey}";
                
            var spec = new PostSpec
            {
                url = $"{baseUrl}/responses",
                payload = payload,
                timeoutSeconds = timeoutSeconds,
                headers = null
            };

            return PostJsonAsync(spec, cancellationToken, ParseResponsesApi_OutputText);
        }
    }
}
