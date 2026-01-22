#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;
using Newtonsoft.Json.Linq;
using Unity.Tutorials.Core.Editor;
using UnityEngine;

namespace DogGame.LLM.Providers
{
    /// <summary>
    /// OpenAI Responses API client.
    /// Endpoint: POST {baseUrl}/responses  (baseUrl includes /v1)
    /// </summary>
    public sealed class OpenAIClient : LLMClientBase
    {
        public override string Vendor => "OpenAI";
        public readonly string baseUrl = "https://api.openai.com/v1";
        public readonly string model = "gpt-4.1-mini";
        public readonly string apiKeyEnvironmentVariable = "OPENAI_API_KEY";
        public readonly string apiKey = ""; // get from environment variable
        public readonly int timeoutSeconds = 60;
        public readonly float temperature = 0.2f;
        public readonly int maxOutputTokens = 800;
        public readonly string modelUniqueInstructions = "";
        
        // constructor:
        public OpenAIClient()
        {
            apiKey = ResolveApiKey(apiKeyEnvironmentVariable, apiKey);
            if (apiKey.IsNullOrEmpty())
                Debug.LogError($"{Vendor}Client apiKey empty. apiKeyEnvironmentVariable={apiKeyEnvironmentVariable}");
        }

        protected override Task<LLMResponse> SendCoreAsync(LLMRequest request, CancellationToken cancellationToken)
        {
            string requestText = BuildRequestPacketText(request);

            Debug.Log($"[LLM] INPUT STRING:\n{requestText}");

            string requestId = request.requestId ?? "";
            string agentId = TryGetMetadata(request, "agentId") ?? "";

            var payload = new JObject
            {
                ["model"] = string.IsNullOrWhiteSpace(request.profile?.model) ? model : request.profile.model,
                ["instructions"] = modelUniqueInstructions,
                ["input"] =
                    $"requestId={requestId}\nagentId={agentId}\n" +
                    "Return ONLY JSON matching responseSchemaJson.\n\n" +
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
                    ["provider"] = "openai"
                }
            };

            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(apiKey))
                headers["Authorization"] = $"Bearer {apiKey}";

            var spec = new PostSpec
            {
                url = $"{baseUrl}/responses",
                payload = payload,
                timeoutSeconds = timeoutSeconds,
                headers = headers
            };

            return PostJsonAsync(spec, cancellationToken, ParseResponsesApi_OutputText);
        }
    }
}