#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM.Agent;
using DogGame.LLM.Core;
using DogGame.LLM.Debugging;
using DogGame.LLM.Providers;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace DogGame.LLM
{
    public enum RemoteLLMProvider
    {
        OpenAI,
        Gemini,
        Ollama
    }

    /// <summary>
    /// Global LLM request scheduler.
    /// Ensures fairness, throttling, and model-tier limits.
    /// </summary>
    public sealed class LLMWorldScheduler : MonoBehaviour
    {
        public static LLMWorldScheduler Instance { get; private set; } = null!;

        [Header("LLM Provider")]
        [SerializeField] private RemoteLLMProvider remoteProvider = RemoteLLMProvider.Gemini;

        [Header("Throughput limits")]
        [SerializeField] private int maxConcurrentLocalRequests = 1;
        [SerializeField] private int maxConcurrentRemoteRequests = 1;

        [Header("Scheduling")]
        [SerializeField] private float schedulingIntervalSeconds = 0.25f;

        private readonly List<LLMPlanRequestOnDemand> pendingRequests = new();

        private int activeLocalRequests;
        private int activeRemoteRequests;

        private OpenAILLMService? openAiService;
        private GeminiLLMService? geminiService;
        private OllamaLLMService? ollamaService;
        private float nextScheduleTime;

        //public string requestJSON;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            
            switch (remoteProvider)
            {
                case RemoteLLMProvider.OpenAI:
                    openAiService = gameObject.AddComponent<OpenAILLMService>();
                    break;
                case RemoteLLMProvider.Gemini:
                    geminiService = gameObject.AddComponent<GeminiLLMService>();
                    break;
                case RemoteLLMProvider.Ollama:
                    ollamaService = gameObject.AddComponent<OllamaLLMService>();
                    break;
            }
            Debug.Log($"LLMWalkthroughScheduler.Awake: pendingRequests(initial)={pendingRequests.Count}", this);
        }

        private void Update()
        {
            // 1) Idle: nothing to do
            if (!HasPendingRequests())
                return;

            // 2) Throttle: only attempt dispatch on interval
            if (Time.time < nextScheduleTime)
                return;

            nextScheduleTime = Time.time + schedulingIntervalSeconds;

            TryDispatchRequests();
        }

        private bool HasPendingRequests()
        {
            // Replace with your real queue check(s)
            return pendingRequests != null && pendingRequests.Count > 0;
        }

        /// <summary>
        /// Agents call this to request a planning slot.
        /// </summary>
/*        public void EnqueueRequest(LLMPlanRequest request)
        {
            if (request == null) return;

            Debug.Log($"LLMWalkthroughEnqueue: agentId={request.AgentId} tier={request.ModelTier} priority={request.PriorityScore:0.00} pendingNow={pendingRequests.Count+1}", this);

            pendingRequests.Add(request);

            // Try to dispatch immediately on next frame (or even right now if you want).
            // Next frame is safer to avoid re-entrancy if Enqueue happens during dispatch.
            nextScheduleTime = Mathf.Min(nextScheduleTime, Time.time);
        }
        */

        public void EnqueueRequest(LLMPlanRequestOnDemand request)
        {
            if (request == null) return;
            pendingRequests.Add(request); // change pendingRequests type to List<LLMPlanRequestOnDemand>
            nextScheduleTime = Mathf.Min(nextScheduleTime, Time.time); // dispatch soon
        }

        private static LLMModelTier MapSophisticationToModelTier(Sophistication s)
        {
            return s switch
            {
                Sophistication.High => LLMModelTier.RemotePaid,
                Sophistication.Medium => LLMModelTier.RemotePaid,
                Sophistication.Low => LLMModelTier.LocalSmall,
                _ => LLMModelTier.LocalSmall
            };
        }

        private void TryDispatchRequests()
        {
            if (!HasPendingRequests())
                return;

            Debug.Log($"LLMWalkthroughDispatchTry: pending={pendingRequests.Count}", this);

            // Sort by priority (high first), then age (oldest first)
            pendingRequests.Sort((a, b) =>
            {
                int priorityCompare = b.PriorityScore.CompareTo(a.PriorityScore);
                if (priorityCompare != 0)
                    return priorityCompare;

                return a.RequestTime.CompareTo(b.RequestTime);
            });

            // Dispatch from the front (highest priority first)
            int index = 0;
            while (index < pendingRequests.Count)
            {
                var request = pendingRequests[index];

                if (!CanDispatch(MapSophisticationToModelTier(request.Sophistication)))
                {
                    index++; // keep it queued
                    continue;
                }

                Dispatch(request);
                pendingRequests.RemoveAt(index); // do NOT increment index; list shifts left
            }
        }
        private bool CanDispatch(LLMModelTier tier)
        {
            return tier switch
            {
                LLMModelTier.LocalSmall => activeLocalRequests < maxConcurrentLocalRequests,
                LLMModelTier.RemotePaid => activeRemoteRequests < maxConcurrentRemoteRequests,
                _ => false
            };
        }

#nullable enable

        private void Dispatch(LLMPlanRequestOnDemand request)
        {
            // 1) Decide infra tier from planning sophistication (central place)
            LLMModelTier modelTier = MapSophisticationToModelTier(request.Sophistication);

            // 2) Resolve agent object + modules
            if (!TryResolveAgentModules(request.AgentId, out var config, out var worldState, out var agentGo))
            {
                Debug.LogWarning($"[LLM Scheduler] Dispatch failed: cannot resolve agent/modules for AgentId={request.AgentId}");
                return;
            }

            // 3) Build real request packet (your production path)
            LLMRequest llmRequest = config.BuildLLMRequest(
                worldState: worldState,
                requestId: request.RequestId,
                agentId: request.AgentId,
                userTaskPrompt: request.Prompt
            );

            string requestJson = LLMRequestSerializer.ToJson(llmRequest);
            //requestJSON = requestJson;
            LLMPacketLogger.LogRequest(
                agentId: request.AgentId,
                requestId: request.RequestId,
                provider: remoteProvider.ToString(),
                requestJson: requestJson);
                
            // 4) Mark inflight (use modelTier you just computed)
            IncrementActive(modelTier);

            Action<string> onResponse = (json) =>
            {
                DecrementActive(modelTier);
                request.OnResponseJson?.Invoke(json);
            };

            // 5) Dispatch to provider
            switch (remoteProvider)
            {
                case RemoteLLMProvider.OpenAI:
                    if (openAiService == null)
                    {
                        Debug.LogWarning("[LLM Scheduler] OpenAI service not assigned.");
                        DecrementActive(modelTier);
                        return;
                    }
                    openAiService.SubmitRequest(
                        requestId: request.RequestId,
                        requestJson: requestJson,
                        agentId: request.AgentId,
                        onResponseJson: onResponse);
                    break;

                case RemoteLLMProvider.Gemini:
                    if (geminiService == null)
                    {
                        Debug.LogWarning("[LLM Scheduler] Gemini service not assigned.");
                        DecrementActive(modelTier);
                        return;
                    }
                    geminiService.SubmitRequest(
                        requestId: request.RequestId,
                        requestJson: requestJson,
                        agentId: request.AgentId,
                        onResponseJson: onResponse);
                    break;
                case RemoteLLMProvider.Ollama:
                    if (ollamaService == null)
                    {
                        Debug.LogWarning("[LLM Scheduler] Ollama service not assigned.");
                        DecrementActive(modelTier);
                        return;
                    }
                    ollamaService.SubmitRequest(
                        requestId: request.RequestId,
                        requestJson: requestJson,
                        agentId: request.AgentId,
                        onResponseJson: onResponse);
                    break;

                default:
                    Debug.LogWarning($"[LLM Scheduler] Unknown provider {remoteProvider}");
                    DecrementActive(modelTier);
                    return;
            }

            Debug.Log($"[LLM Scheduler] Dispatched requestId={request.RequestId} agent={agentGo.name} agentId={request.AgentId} soph={request.Sophistication} tier={modelTier} provider={remoteProvider} jsonChars={requestJson.Length}");
        }

        private bool TryResolveAgentModules(
            string agentId,
            out LLMConfigModule config,
            out LLMWorldStateModule worldState,
            out GameObject agentGameObject)
        {
            config = null!;
            worldState = null!;
            agentGameObject = null!;

            if (string.IsNullOrWhiteSpace(agentId))
                return false;

            // Scan active configs (cheap enough for now; can be cached later)
            var configs = UnityEngine.Object.FindObjectsByType<LLMConfigModule>(FindObjectsSortMode.None);
            foreach (var c in configs)
            {
                if (c == null) continue;

                string resolvedId = c.identity.ResolveAgentId(c.gameObject);
                if (!string.Equals(resolvedId, agentId, StringComparison.Ordinal))
                    continue;

                var ws = c.GetComponent<LLMWorldStateModule>();
                if (ws == null)
                {
                    Debug.LogWarning($"[LLM Scheduler] Agent {c.gameObject.name} matches id={agentId} but has no LLMWorldStateModule.");
                    return false;
                }

                config = c;
                worldState = ws;
                agentGameObject = c.gameObject;
                return true;
            }

            return false;
        }

        private void IncrementActive(LLMModelTier tier)
        {
            if (tier == LLMModelTier.LocalSmall) activeLocalRequests++;
            else activeRemoteRequests++;
        }

        private void DecrementActive(LLMModelTier tier)
        {
            if (tier == LLMModelTier.LocalSmall) activeLocalRequests--;
            else activeRemoteRequests--;
        }

        private void OnDisable()
        {
            // Clear pending queue so nothing dispatches next play session
            pendingRequests.Clear();

            // Best-effort cancel inflight provider work
            ollamaService?.CancelAll("scheduler_disabled");
            openAiService?.CancelAll("scheduler_disabled");
            geminiService?.CancelAll("scheduler_disabled");
        }
    }
}