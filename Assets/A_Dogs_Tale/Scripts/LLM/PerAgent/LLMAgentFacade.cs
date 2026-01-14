#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;
using UnityEngine;

namespace DogGame.LLM.Agent
{
    /// <summary>
    /// Agent-level bridge: Build an LLMRequest via LLMConfigModule + LLMWorldStateModule,
    /// serialize it, and send it to the configured provider service.
    /// </summary>
    public sealed class LLMAgentFacade : MonoBehaviour
    {
        [Header("Modules (on same agent)")]
        [SerializeField] private LLMConfigModule? config;
        [SerializeField] private LLMWorldStateModule? worldState;

        [Header("Provider Service (assign ONE)")]
        [Tooltip("Assign RemoteLLMService (OpenAI) or GeminiLLMService here.")]
        [SerializeField] private MonoBehaviour? llmServiceBehaviour;

        private void Awake()
        {
            config ??= GetComponent<LLMConfigModule>();
            worldState ??= GetComponent<LLMWorldStateModule>();
        }

        public async Task<LLMResponse> RequestPlanAsync(string userTaskPrompt, CancellationToken cancellationToken)
        {
            if (config == null)
                return LLMResponse.Fail("[LLMAgentFacade] Missing LLMConfigModule.");
            if (worldState == null)
                return LLMResponse.Fail("[LLMAgentFacade] Missing LLMWorldStateModule.");
            if (llmServiceBehaviour == null)
                return LLMResponse.Fail("[LLMAgentFacade] Missing llmServiceBehaviour (RemoteLLMService or GeminiLLMService).");

            if (!TryGetSubmitRequest(llmServiceBehaviour, out var submitRequest, out var reason))
                return LLMResponse.Fail(reason);

            // Agent identity is ultimately set in LLMConfigModule (IdentitySection).
            // Facade just needs a unique requestId.
            string requestId = $"{gameObject.name}:{DateTime.UtcNow.Ticks}";

            LLMRequest request;
            try
            {
                request = config.BuildLLMRequest(worldState, requestId, userTaskPrompt);
            }
            catch (Exception ex)
            {
                return LLMResponse.Fail("[LLMAgentFacade] BuildLLMRequest failed: " + ex.Message);
            }

            // AgentId is placed into request.metadata by BuildLLMRequest().
            string agentId = gameObject.name; // default
            if (request.metadata != null && request.metadata.TryGetValue("agentId", out var id))
                agentId = id;

            string requestJson = LLMRequestSerializer.ToJson(request);

            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

            try
            {
                submitRequest(requestId, requestJson, agentId, responseJson =>
                {
                    if (string.IsNullOrWhiteSpace(responseJson))
                        tcs.TrySetException(new Exception("LLM returned empty response."));
                    else
                        tcs.TrySetResult(responseJson);
                });

                string raw = await tcs.Task.ConfigureAwait(false);
                return LLMResponse.Ok(raw, requestId, agentId);
            }
            catch (OperationCanceledException)
            {
                return LLMResponse.Fail("[LLMAgentFacade] Request canceled.");
            }
            catch (Exception ex)
            {
                return LLMResponse.Fail("[LLMAgentFacade] Request failed: " + ex.Message);
            }
        }

        // --- Provider bridge: SubmitRequest(string requestId, string requestJson, string agentId, Action<string> cb) ---
        private delegate void SubmitRequestDelegate(string requestId, string requestJson, string agentId, Action<string> onResponseJson);

        private static bool TryGetSubmitRequest(MonoBehaviour service, out SubmitRequestDelegate submit, out string reason)
        {
            submit = default!;
            reason = "";

            var method = service.GetType().GetMethod("SubmitRequest",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            if (method == null)
            {
                reason = $"[LLMAgentFacade] Service '{service.GetType().Name}' has no public SubmitRequest(...) method.";
                return false;
            }

            try
            {
                submit = (SubmitRequestDelegate)Delegate.CreateDelegate(typeof(SubmitRequestDelegate), service, method);
                return true;
            }
            catch
            {
                reason =
                    $"[LLMAgentFacade] Service '{service.GetType().Name}' SubmitRequest signature mismatch.\n" +
                    "Expected: SubmitRequest(string requestId, string requestJson, string agentId, Action<string> onResponseJson)";
                return false;
            }
        }
    }

    public readonly struct LLMResponse
    {
        public readonly bool succeeded;
        public readonly string rawText;
        public readonly string errorMessage;
        public readonly string requestId;
        public readonly string agentId;

        private LLMResponse(bool ok, string raw, string err, string reqId, string agId)
        {
            succeeded = ok;
            rawText = raw;
            errorMessage = err;
            requestId = reqId;
            agentId = agId;
        }

        public static LLMResponse Ok(string raw, string requestId, string agentId)
            => new(true, raw, "", requestId, agentId);

        public static LLMResponse Fail(string error)
            => new(false, "", error, "", "");
    }
}