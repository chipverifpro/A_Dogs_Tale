// Assets/A_Dogs_Tale/Scripts/LLM/Providers/UnifiedLLMRouter.cs
#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;
using UnityEditor.SettingsManagement;
using UnityEngine;

namespace DogGame.LLM.Providers
{
    public sealed class UnifiedLLMRouter : MonoBehaviour
    {
        public LLMWorldScheduler? llmWorldScheduler = null;

        [Header("Which vendor to use, overrides Agent config")]
        public RemoteLLMProvider vendor = RemoteLLMProvider.OpenAI;

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

        public void BeginNewSession()
        {
            CancelAll("New session");
            sessionCancellation = new CancellationTokenSource();
            openAiClient?.BumpSessionToken();
            geminiClient?.BumpSessionToken();
            ollamaClient?.BumpSessionToken();
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
            openAiClient?.BumpSessionToken();
            geminiClient?.BumpSessionToken();
            ollamaClient?.BumpSessionToken();

            Debug.Log($"[UnifiedLLMRouter] CancelAll: {reason}", this);
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
        
        public async void Send(LLMRequest request, Action<LLMResponse> onDone)
        {
            if (request == null) return;
            InitializeClients();  // make sure everything is valid.

            var token = sessionCancellation?.Token ?? CancellationToken.None;
            
            Debug.Log($"remote provider override: {llmWorldScheduler?.remoteProvider}");

            if (llmWorldScheduler?.remoteProvider!=RemoteLLMProvider.None)
                vendor = llmWorldScheduler!.remoteProvider;

            // select the client based on global override in LLMWorldScheduler, or request's vendor if global override is not set.
            ILLMClient? selected = vendor switch
            {
                RemoteLLMProvider.OpenAI => openAiClient,
                RemoteLLMProvider.Gemini => geminiClient,
                RemoteLLMProvider.Ollama => ollamaClient,
                _ => null
            };
            if (selected == null)
            {
                selected = vendor switch
                {
                    RemoteLLMProvider.OpenAI => openAiClient,
                    RemoteLLMProvider.Gemini => geminiClient,
                    RemoteLLMProvider.Ollama => ollamaClient,
                    _ => null
                };
            }
            if (selected == null)
            {
                onDone?.Invoke(new LLMResponse
                {
                    succeeded = false,
                    errorMessage = $"[UnifiedLLMRouter] No client assigned for vendor={vendor}, llmWorldScheduler?.remoteProvider={llmWorldScheduler?.remoteProvider}"
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