// Assets/A_Dogs_Tale/Scripts/LLM/Providers/UnifiedLLMRouter.cs
#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using DogGame.LLM.Core;
using UnityEngine;

namespace DogGame.LLM.Providers
{
    public sealed class UnifiedLLMRouter : MonoBehaviour
    {
        public LLMWorldScheduler? llmWorldScheduler = null;

        private CancellationTokenSource? sessionCancellation;

        // Construct these in InitializeClients()
        public ILLMClient? openAiClient;
        public ILLMClient? geminiClient;
        public ILLMClient? ollamaClient;

        private void OnEnable()
        {
            InitializeClients();
        }

        private void OnDisable()
        {
            CancelAll("Router disabled");
        }

        public void Awake()
        {
            InitializeClients(); 
        }

        /*
        public void BeginNewSession()
        {
            CancelAll("New session");
            sessionCancellation = new CancellationTokenSource();
            openAiClient?.BumpSessionToken();
            geminiClient?.BumpSessionToken();
            ollamaClient?.BumpSessionToken();
        }
        */

        public void CancelAll(string reason)
        {
            if (sessionCancellation == null) return;

            try { sessionCancellation.Cancel(); }
            catch { /* ignore */ }
            finally
            {
                sessionCancellation.Dispose();
                sessionCancellation = null;
            }

            // session token makes any late results no-ops even if a provider can’t abort
            openAiClient?.BumpSessionToken();
            geminiClient?.BumpSessionToken();
            ollamaClient?.BumpSessionToken();

            //Debug.Log($"[UnifiedLLMRouter] CancelAll: {reason}", this);
        }

        public void InitializeClients()
        {
            if (sessionCancellation==null) 
                sessionCancellation = new CancellationTokenSource();
            
            if (llmWorldScheduler==null) 
            {
                llmWorldScheduler = GetComponent<LLMWorldScheduler>();
                if (llmWorldScheduler==null) Debug.LogError("UnifiedLLMRouter.OnEnable failed to find LLMWorldScheduler");
            }

            if (openAiClient==null)
                openAiClient=new OpenAIClient();

            if (geminiClient==null) 
                geminiClient=new GeminiClient();

            if (ollamaClient==null) 
                ollamaClient=new OllamaClient();
        }

        // You likely already have these (or similar)
        //[SerializeField] private OpenAIProviderSettings? openAISettings;
        //[SerializeField] private GeminiProviderSettings? geminiSettings;
        //[SerializeField] private OllamaProviderSettings? ollamaSettings;

        // Client cache: one client instance per (vendor, model) so different models can coexist.
        private readonly Dictionary<string, LLMClientBase> clientsByVendorModel = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Route an LLMRequest to the correct vendor client (and model) and return a normalized PlanResponse JSON string.
        /// </summary>
        public async void Send(LLMRequest request, Action<LLMResponse> onDone)
        {
            if (onDone == null) return;

            if (request == null)
            {
                onDone(new LLMResponse
                {
                    succeeded = false,
                    errorMessage = "[UnifiedLLMRouter] Request was null."
                });
                return;
            }

            string vendor = (request.profile?.vendor ?? "").Trim();
            string model  = (request.profile?.model  ?? "").Trim();

            if (string.IsNullOrWhiteSpace(vendor))
            {
                onDone(new LLMResponse
                {
                    succeeded = false,
                    errorMessage = "[UnifiedLLMRouter] request.profile.vendor was empty."
                });
                return;
            }

            // If model was not specified, let vendor defaults apply (but still keep stable cache keys).
            if (string.IsNullOrWhiteSpace(model))
                model = "(default)";

            string key = $"{vendor}|{model}";

            if (!clientsByVendorModel.TryGetValue(key, out var client) || client == null)
            {
                client = CreateClientFor(vendor, model);
                if (client == null)
                {
                    onDone(new LLMResponse
                    {
                        succeeded = false,
                        errorMessage = $"[UnifiedLLMRouter] No client available for vendor={vendor} model={model}"
                    });
                    return;
                }

                clientsByVendorModel[key] = client;
            }

            try
            {
                // Send via common base (retries, rate-limit handling, sessionToken gating lives there).
                var response = await client.SendAsync(request, default);

                if (response == null)
                {
                    onDone(new LLMResponse
                    {
                        succeeded = false,
                        errorMessage = $"[UnifiedLLMRouter] {vendor} returned null response."
                    });
                    return;
                }

                // If the vendor client stored JSON in rawProviderPayloadJson, prefer that.
                // Otherwise use rawText.
                string raw = !string.IsNullOrWhiteSpace(response.rawText)
                    ? response.rawText
                    : (response.rawProviderPayloadJson ?? "");

                if (response.succeeded)
                {
                    // Normalize common failure modes:
                    // - ```json fences
                    // - leading reasoning/thinking text
                    // - extra content around the JSON object
                    string cleaned = ExtractFirstJsonObject(raw);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                    {
                        response.rawText = cleaned; // make downstream always read rawText for plan JSON
                    }
                }

                onDone(response);
            }
            catch (Exception ex)
            {
                onDone(new LLMResponse
                {
                    succeeded = false,
                    errorMessage = $"[UnifiedLLMRouter] Exception: {ex.GetType().Name}: {ex.Message}"
                });
            }
        }

        private LLMClientBase? CreateClientFor(string vendor, string modelKey)
        {
/*            // If modelKey is "(default)", pass "" so each client can apply its own default.
            string model = string.Equals(modelKey, "(default)", StringComparison.OrdinalIgnoreCase) ? "" : modelKey;

            if (vendor.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                if (openAISettings == null) return null;
                return new OpenAIClient(openAISettings, model: model);
            }

            if (vendor.Equals("Gemini", StringComparison.OrdinalIgnoreCase))
            {
                if (geminiSettings == null) return null;
                return new GeminiClient(geminiSettings, model: model);
            }

            if (vendor.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            {
                if (ollamaSettings == null) return null;
                return new OllamaClient(ollamaSettings, model: model);
            }
*/
            return null;
        }

        /// <summary>
        /// Pulls the first top-level JSON object from a string.
        /// Handles ```json fences and leading/trailing chatter.
        /// </summary>
        private static string ExtractFirstJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            string t = text.Trim();

            // Strip code fences if present
            if (t.StartsWith("```", StringComparison.Ordinal))
            {
                int firstNewline = t.IndexOf('\n');
                if (firstNewline >= 0) t = t[(firstNewline + 1)..];
                int fenceEnd = t.LastIndexOf("```", StringComparison.Ordinal);
                if (fenceEnd >= 0) t = t[..fenceEnd];
                t = t.Trim();
            }

            int start = t.IndexOf('{');
            int end   = t.LastIndexOf('}');
            if (start < 0 || end < 0 || end <= start)
                return t.Trim(); // best-effort fallback

            return t.Substring(start, end - start + 1).Trim();
        }
    }
}
