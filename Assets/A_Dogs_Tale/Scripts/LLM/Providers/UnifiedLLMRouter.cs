// Assets/A_Dogs_Tale/Scripts/LLM/Providers/UnifiedLLMRouter.cs
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;
using UnityEngine;

namespace DogGame.LLM.Providers
{
    public enum LLMVendorKind { OpenAI, Gemini, Ollama }

    public sealed class UnifiedLLMRouter : MonoBehaviour
    {
        [Header("Which vendor to use")]
        public LLMVendorKind vendor = LLMVendorKind.OpenAI;

        private CancellationTokenSource? sessionCancellation;

        // Construct/assign these however you prefer (inspector fields, config assets, etc.)
        public ILLMClient? openAiClient;
        public ILLMClient? geminiClient;
        public ILLMClient? ollamaClient;

        private void OnEnable()
        {
            sessionCancellation = new CancellationTokenSource();
        }

        private void OnDisable()
        {
            CancelAll("Router disabled");
        }

        public void BeginNewSession()
        {
            CancelAll("New session");
            sessionCancellation = new CancellationTokenSource();
            LLMClientBase.BeginNewSession();
        }

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
            LLMClientBase.BeginNewSession();

            Debug.Log($"[UnifiedLLMRouter] CancelAll: {reason}", this);
        }

        public async void Send(LLMRequest request, Action<LLMResponse> onDone)
        {
            if (request == null) return;

            var token = sessionCancellation?.Token ?? CancellationToken.None;

            ILLMClient? selected = vendor switch
            {
                LLMVendorKind.OpenAI => openAiClient,
                LLMVendorKind.Gemini => geminiClient,
                LLMVendorKind.Ollama => ollamaClient,
                _ => null
            };

            if (selected == null)
            {
                onDone?.Invoke(new LLMResponse
                {
                    succeeded = false,
                    errorMessage = $"[UnifiedLLMRouter] No client assigned for vendor={vendor}"
                });
                return;
            }

            try
            {
                LLMResponse response = await selected.SendAsync(request, token);
                onDone?.Invoke(response);
            }
            catch (OperationCanceledException)
            {
                onDone?.Invoke(new LLMResponse
                {
                    succeeded = false,
                    wasStale = true,
                    errorMessage = "[UnifiedLLMRouter] Request cancelled."
                });
            }
            catch (Exception ex)
            {
                onDone?.Invoke(new LLMResponse
                {
                    succeeded = false,
                    errorMessage = $"[UnifiedLLMRouter] Exception: {ex.GetType().Name}: {ex.Message}"
                });
            }
        }
    }
}