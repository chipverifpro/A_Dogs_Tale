#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM.Agent;
using DogGame.LLM.Core;
using DogGame.LLM.Debugging;
using DogGame.LLM.Providers;
using Unity.AppUI.Core;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace DogGame.LLM
{
    public enum RemoteLLMProvider
    {
        OpenAI,
        Gemini,
        Ollama,
        None
    }

    /// <summary>
    /// Global LLM request scheduler.
    /// Ensures fairness, throttling, and model-tier limits.
    /// </summary>
    public sealed class LLMWorldScheduler : MonoBehaviour
    {
        public static LLMWorldScheduler Instance { get; private set; } = null!;

        [Header("LLM Provider")]
        [SerializeField] public RemoteLLMProvider remoteProvider = RemoteLLMProvider.Gemini;

        [Header("Throughput limits")]
        [SerializeField] private int maxConcurrentLocalRequests = 1;
        [SerializeField] private int maxConcurrentRemoteRequests = 1;

        [Header("Scheduling")]
        [SerializeField] private float schedulingIntervalSeconds = 0.25f;

        [Header("Unified LLM Router")]
        [SerializeField] private UnifiedLLMRouter router;

        private readonly List<LLMPlanRequestOnDemand> pendingRequests = new();

        private int activeLocalRequests;
        private int activeRemoteRequests;

        //private OpenAILLMService? openAiService;
        //private GeminiLLMService? geminiService;
        //private OllamaLLMService? ollamaService;
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
            
//            switch (remoteProvider)
//            {
//                case RemoteLLMProvider.OpenAI:
//                    openAiService = gameObject.AddComponent<OpenAILLMService>();
//                    break;
//                case RemoteLLMProvider.Gemini:
//                    geminiService = gameObject.AddComponent<GeminiLLMService>();
//                    break;
//                case RemoteLLMProvider.Ollama:
//                    ollamaService = gameObject.AddComponent<OllamaLLMService>();
//                    break;
//            }

            if (router == null)
            {
                router = FindFirstObjectByType<UnifiedLLMRouter>();
                if (router == null)
                {
                    Debug.LogError("[LLMWorldScheduler] UnifiedLLMRouter not found in scene.");
                }
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
            // Don't start LLM until build is complete.
            if (!Directory.Instance.gen.buildComplete) return;

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

        // Drop-in replacement for LLMWorldScheduler.Dispatch(LLMPlanRequestOnDemand request)
        //
        // Assumptions / fields you should already have in LLMWorldScheduler:
        //   - [SerializeField] private DogGame.LLM.Providers.UnifiedLLMRouter? router;   (or whatever you named it)
        //   - MapSophisticationToModelTier(Sophistication) -> LLMModelTier
        //   - TryResolveAgentModules(agentId, out LLMConfigModule config, out LLMWorldStateModule worldState, out GameObject agentGo)
        //   - IncrementActive(LLMModelTier), DecrementActive(LLMModelTier)
        //   - LLMPacketLogger.LogRequest(...), LLMPacketLogger.LogResponse(...)  (if you have response logging; optional)
        //
        // Notes:
        //   - Uses your existing BuildLLMRequest + serializer for packet logging.
        //   - Sends the *LLMRequest object* to router (recommended), so vendors can unify packet building.
        //   - Filters stale/cancelled responses using LLMResponse.wasStale (from your LLMClientBase sessionToken gate).
        //
        private void Dispatch(LLMPlanRequestOnDemand request)
        {
            if (request == null)
                return;

            // 1) Decide infra tier from planning sophistication (central place)
            LLMModelTier modelTier = MapSophisticationToModelTier(request.Sophistication);

            if (router == null)
            {
                Debug.LogWarning("[LLM Scheduler] Router missing; cannot dispatch LLM request.");
                DecrementActive(modelTier);
                return;
            }

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

            // ✅ Force router vendor to match the selected provider
            // default leaves override value
            llmRequest.profile.vendor = remoteProvider switch
            {
                RemoteLLMProvider.OpenAI => "OpenAI",
                RemoteLLMProvider.Gemini => "Gemini",
                RemoteLLMProvider.Ollama => "Ollama",
                _ => llmRequest.profile.vendor.ToString()
            };

            // (optional) if you also want to force model per provider:
            if (remoteProvider == RemoteLLMProvider.Ollama) llmRequest.profile.model = "Gemma3:1b";
            
            // 4) Mark inflight
            IncrementActive(modelTier);

            // 5) Dispatch via unified router
            // IMPORTANT: router should internally route to OpenAI/Gemini/Ollama client based on selection.
            if (router == null)
            {
                Debug.LogWarning("[LLM Scheduler] UnifiedLLMRouter not assigned.");
                DecrementActive(modelTier);
                return;
            }

            router.Send(llmRequest, (response) =>
            {
                // Always decrement active count exactly once
                DecrementActive(modelTier);

                if (response == null)
                {
                    Debug.LogWarning($"[LLM Scheduler] Null response. requestId={request.RequestId} agentId={request.AgentId}");
                    return;
                }

                // Stale response? (e.g., playmode restarted / new session token)
                if (response.wasStale)
                {
                    Debug.Log($"[LLM Scheduler] Stale response ignored. requestId={request.RequestId} agentId={request.AgentId}");
                    return;
                }

                if (!response.succeeded)
                {
                    Debug.LogWarning($"[LLM Scheduler] LLM failed. requestId={request.RequestId} agentId={request.AgentId} err={response.errorMessage}");
                    return;
                }

                // Choose which field carries the PlanResponseV1 JSON in your project.
                // Prefer rawText if you populate it with the extracted PlanResponseV1 JSON.
                // Fall back to rawProviderPayloadJson if that's where your client stored it.
                string planJson =
                    !string.IsNullOrWhiteSpace(response.rawText) ? response.rawText :
                    !string.IsNullOrWhiteSpace(response.rawProviderPayloadJson) ? response.rawProviderPayloadJson :
                    "";

                if (string.IsNullOrWhiteSpace(planJson))
                {
                    Debug.LogWarning($"[LLM Scheduler] Response succeeded but planJson was empty. requestId={request.RequestId} agentId={request.AgentId}");
                    return;
                }

                // Optional: response packet logging
                // LLMPacketLogger.LogResponse(request.AgentId, request.RequestId, remoteProvider.ToString(), planJson);
                
                // ==== LOGGING VERSION ONLY ====
                string requestJson = LLMRequestSerializer.ToJson(llmRequest);
                LLMPacketLogger.LogRequest(
                    agentId: request.AgentId,
                    requestId: request.RequestId,
                    provider: remoteProvider.ToString(),
                    requestJson: requestJson);
                try { Directory.Instance.llmDebugMonitor.DebugLLMRequest_Input(requestJson); } catch { /* ignore */ }
                // ==== END LOGGING ====

                // Deliver to caller (PlayerDecisionModule etc.)
                try
                {
                    request.OnResponseJson?.Invoke(planJson);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LLM Scheduler] OnResponseJson handler threw. requestId={request.RequestId} agentId={request.AgentId} ex={ex.GetType().Name}: {ex.Message}");
                }
            });

            Debug.Log($"[LLM Scheduler] Dispatched requestId={request.RequestId} agent={agentGo.name} agentId={request.AgentId} soph={request.Sophistication} tier={modelTier} provider={remoteProvider}");
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
//            ollamaService?.CancelAll("scheduler_disabled");
//            openAiService?.CancelAll("scheduler_disabled");
//            geminiService?.CancelAll("scheduler_disabled");
            LLMSessionToken.Bump();
        }
    }
}