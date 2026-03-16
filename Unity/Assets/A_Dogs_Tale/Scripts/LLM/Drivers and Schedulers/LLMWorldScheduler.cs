#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM.Agent;
using DogGame.LLM.Core;
using DogGame.LLM.Providers;
using UnityEngine;
using DogGame.LLM;

namespace DogGame.LLM
{
    [System.Flags]
    public enum LLMVendorAndModel
    {
        None = 0,
        OpenAI_gpt_4_1_mini = 1 << 0,
        OpenAI_gpt_5_mini = 1 << 1,

        Gemini_gemini_2_5_flash_lite = 1 << 10,

        Ollama_Qwen3_4b = 1 << 20,
        Ollama_Qwen3_8b = 1 << 21,
        Ollama_Gemma3 = 1 << 22
    }

    [Serializable]
    public class LLMModelSelection
    {
        [HideInInspector]
        public string displayName;
        public LLMVendorAndModel llmVendorAndModel;

        public LLMVendor llmVendor;
        public string llmModelString;
        [Tooltip("Unchecked means runs locally.")]
        public bool remote;
        [Tooltip("Capability of creating complex plans.")]
        public Sophistication sophistication;

        [Tooltip("Game imposed limit of number of simultaneous requests")]
        public int vendorMaxConcurrentRequests;
        [Header("Statistics")]
        public float successRate;
        public float typicalResponseTime;
        public float typicalCost;
        public int totalRequests = 0;
        public int totalFailures = 0;
        [Header("Currently running requests")]
        public List<string> currentRequests = new();

        public LLMModelSelection(LLMVendorAndModel vendorAndModel,
                          LLMVendor vendor,
                          string modelString,
                          bool remote,
                          Sophistication sophistication,
                          float TypicalResponseTime,
                          float SuccessRate,
                          float TypicalCost,
                          int vendorMaxConcurrentRequests)
        {
            this.llmVendorAndModel = vendorAndModel;
            this.llmVendor = vendor;
            this.llmModelString = modelString;
            this.remote = remote;
            this.sophistication = sophistication;
            this.typicalResponseTime = TypicalResponseTime;
            this.successRate = SuccessRate;
            this.typicalCost = TypicalCost;
            this.vendorMaxConcurrentRequests = vendorMaxConcurrentRequests;
            this.displayName = $"{this.llmVendor} · {this.llmModelString} · {this.sophistication}";
        }

        [NonSerialized] private LLMClientBase? cachedClient;

        public string VendorName => llmVendor.ToString();

        /// <summary>
        /// Get or create the client for THIS (vendor, model) selection.
        /// Put all per-vendor wiring here so there is a 1:1 mapping between selection and client.
        /// </summary>
        /// <summary>
        /// 1:1 mapping: this model selection constructs the correct client instance.
        /// Clients fetch env vars / defaults internally.
        /// </summary>
        public LLMClientBase GetOrCreateClient()
        {
            if (cachedClient != null)
                return cachedClient;

            switch (llmVendor)
            {
                case LLMVendor.OpenAI:
                    // If your OpenAIClient ctor supports "model" only:
                    cachedClient = new DogGame.LLM.Providers.OpenAIClient(model: llmModelString);
                    break;

                case LLMVendor.Gemini:
                    cachedClient = new DogGame.LLM.Providers.GeminiClient(model: llmModelString);
                    break;

                case LLMVendor.Ollama:
                    cachedClient = new DogGame.LLM.Providers.OllamaClient(model: llmModelString);
                    break;

                default:
                    throw new InvalidOperationException($"[LLMModelSelection] Unsupported vendor {llmVendor}.");
            }

            return cachedClient!;
        }

        /*
        /// <summary>
        /// Optional: call this if you change settings at runtime and need fresh clients.
        /// </summary>
        public void ResetClient()
        {
            cachedClient = null;
        }
        */

        // --- runtime-only ---
        [NonSerialized] private readonly Dictionary<string, float> requestStartTimes = new();

        public bool HasOpenSlot =>
            currentRequests.Count < vendorMaxConcurrentRequests;

        public void OnDispatchStart(string requestId)
        {
            totalRequests++;
            currentRequests.Add(requestId);
            requestStartTimes[requestId] = Time.realtimeSinceStartup;
        }

        public void OnDispatchSuccess(string requestId)
        {
            FinishRequest(requestId, success: true);
        }

        public void OnDispatchFailure(string requestId)
        {
            totalFailures++;
            FinishRequest(requestId, success: false);
        }

        private void FinishRequest(string requestId, bool success)
        {
            currentRequests.Remove(requestId);

            if (requestStartTimes.TryGetValue(requestId, out float start))
            {
                float duration = Time.realtimeSinceStartup - start;
                UpdateTypicalResponseTime(duration);
                requestStartTimes.Remove(requestId);
            }

            UpdateSuccessRate();
        }

        private void UpdateTypicalResponseTime(float newSample)
        {
            // Exponential moving average (stable, cheap)
            const float alpha = 0.2f;
            typicalResponseTime = typicalResponseTime <= 0
                ? newSample
                : Mathf.Lerp(typicalResponseTime, newSample, alpha);
        }

        private void UpdateSuccessRate()
        {
            successRate = totalRequests > 0
                ? 1f - (float)totalFailures / totalRequests
                : 0f;
        }

    }
}
/// <summary>
/// A single place in the scene/inspector to assign provider settings assets/structs.
/// This replaces the old per-service MonoBehaviours.
/// </summary>
//    [Serializable]
//    public sealed class ProviderSettingsBundle
//    {
//        public DogGame.LLM.Providers.OpenAIProviderSettings? openAI;
//        public DogGame.LLM.Providers.GeminiProviderSettings? gemini;
//        public DogGame.LLM.Providers.OllamaProviderSettings? ollama;
//    }

public enum LLMVendor
{
    OpenAI,
    Gemini,
    Ollama,
    None
}

public enum RemoteLLMModel
{
    gpt_4_1_mini,
    Gemini,
    Ollama,
    None
}

public class LLMDatabase
{
    public List<LLMModelSelection> llmModelDatabase = new List<LLMModelSelection>();

}

/// <summary>
/// Global LLM request scheduler.
/// Ensures fairness, throttling, and model-tier limits.
/// </summary>
public class LLMWorldScheduler : MonoBehaviour
{
    public static LLMWorldScheduler Instance { get; private set; } = null!;

    [Header("LLM Providers Available")]
    [Tooltip("Enable/Disable available models")]
    public LLMVendorAndModel llmVendorAndModel;

    //[Tooltip("These are all available models")]
    [HideInInspector]
    private List<LLMModelSelection> masterLLMModelDatabase = new List<LLMModelSelection>()
        {
            new(LLMVendorAndModel.OpenAI_gpt_4_1_mini,
                LLMVendor.OpenAI,
                "gpt-4.1-mini",
                true,
                Sophistication.Medium,
                TypicalResponseTime: 10f,
                SuccessRate: 0.9f,
                TypicalCost: .01f,
                vendorMaxConcurrentRequests: 2),
            new(LLMVendorAndModel.Gemini_gemini_2_5_flash_lite,
                LLMVendor.Gemini,
                "gemini-2.5-flash-lite",
                true,
                Sophistication.Medium,
                TypicalResponseTime: 10f,
                SuccessRate: 0.9f,
                TypicalCost: .01f,
                vendorMaxConcurrentRequests: 2),
            new(LLMVendorAndModel.Ollama_Gemma3,
                LLMVendor.Ollama,
                "Gemma3",
                false,
                Sophistication.Medium,
                TypicalResponseTime: 20f,
                SuccessRate: 0.8f,
                TypicalCost: 0f,
                vendorMaxConcurrentRequests: 1),
            new(LLMVendorAndModel.Ollama_Qwen3_4b,
                LLMVendor.Ollama,
                "Qwen3:4b",
                false,
                Sophistication.Low,
                TypicalResponseTime: 70f,
                SuccessRate: 0.5f,
                TypicalCost: 0f,
                vendorMaxConcurrentRequests: 1),
        };

    [Tooltip("These models are enabled (above) as usable")]
    [SerializeField] public List<LLMModelSelection> llmModelsAvailable = new List<LLMModelSelection>();

    //[Header("Scheduling")]
    [HideInInspector] private float schedulingIntervalSeconds = 1f;

    [Header("Unified LLM Router")]
    [SerializeField] private UnifiedLLMRouter? router;

    [SerializeField] public List<LLMPlanRequestOnDemand> pendingRequests = new();

    //private OpenAILLMService? openAiService;
    //private GeminiLLMService? geminiService;
    //private OllamaLLMService? ollamaService;
    private float nextScheduleTime;

    //public string requestJSON;

    // One in-flight request per agent.
    private readonly HashSet<string> inflightAgents = new();
    private readonly Dictionary<string, string> inflightRequestIdByAgent = new();
    private readonly Dictionary<string, AgentModuleCacheEntry> agentModuleCache = new();

    private readonly struct AgentModuleCacheEntry
    {
        public readonly LLMConfigModule Config;
        public readonly LLMWorldStateModule WorldState;
        public readonly GameObject AgentGameObject;

        public AgentModuleCacheEntry(LLMConfigModule config, LLMWorldStateModule worldState, GameObject agentGameObject)
        {
            Config = config;
            WorldState = worldState;
            AgentGameObject = agentGameObject;
        }
    }

    public void OnValidate()
    {
        llmModelsAvailable = new();
        foreach (LLMModelSelection model in masterLLMModelDatabase)
        {
            if (llmVendorAndModel.HasFlag(model.llmVendorAndModel))
            {
                llmModelsAvailable.Add(model);
            }
        }
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        OnValidate();   // initialize llmModelsAvailable to match llmVendorAndModel.

        if (router == null)
        {
            router = FindFirstObjectByType<UnifiedLLMRouter>();
            if (router == null)
            {
                router = gameObject.AddComponent<UnifiedLLMRouter>();
                if (router == null)
                {
                    Debug.LogError("[LLMWorldScheduler] UnifiedLLMRouter not found or created.");
                }
            }
        }

        RefreshAgentModuleCache();
        //Debug.Log($"LLMWalkthroughScheduler.Awake: pendingRequests(initial)={pendingRequests.Count}", this);
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
        for (int i = pendingRequests.Count - 1; i >= 0; i--)
        {
            if (pendingRequests[i].AgentId == request.AgentId)
            {
                pendingRequests[i] = request; // replace old pending with newest
                nextScheduleTime = Mathf.Min(nextScheduleTime, Time.time);
                return;
            }
        }
        pendingRequests.Add(request);
        nextScheduleTime = Mathf.Min(nextScheduleTime, Time.time); // dispatch soon
    }

    private void Dispatch(LLMPlanRequestOnDemand request, LLMModelSelection selection)
    {
        if (request == null) return;

        if (!TryResolveAgentModules(request.AgentId, out var config, out var worldState, out var agentGo))
        {
            UnityEngine.Debug.LogWarning($"[LLM Scheduler] Dispatch failed: cannot resolve agent/modules for AgentId={request.AgentId}");
            return;
        }

        LLMRequest llmRequest = config.BuildLLMRequest(
            worldState: worldState,
            requestId: request.RequestId,
            agentId: request.AgentId,
            userTaskPrompt: request.Prompt
        );

        // Force vendor/model to match the selection (prevents the “why is it using OpenAI” bug)
        llmRequest.profile.vendor = selection.VendorName;
        llmRequest.profile.model = selection.llmModelString;

        // Track inflight on the selection (per-model concurrency)
        if (!string.IsNullOrWhiteSpace(request.AgentId))
        {
            inflightAgents.Add(request.AgentId);
            inflightRequestIdByAgent[request.AgentId] = request.RequestId;
        }
        //string requestName = request.AgentId + ":" + request.RequestId;
        string requestName = request.RequestId;
        selection.OnDispatchStart(requestName);

        LLMClientBase client;
        try
        {
            client = selection.GetOrCreateClient();
        }
        catch (Exception ex)
        {
            selection.currentRequests.Remove(request.RequestId);
            UnityEngine.Debug.LogWarning($"[LLM Scheduler] Client creation failed vendor={selection.llmVendor} model={selection.llmModelString}: {ex.Message}");
            return;
        }

        _ = SendAndHandleAsync(client, llmRequest, request, selection, agentGo.name);
    }

    private async System.Threading.Tasks.Task SendAndHandleAsync(
        LLMClientBase client,
        LLMRequest llmRequest,
        LLMPlanRequestOnDemand schedulerRequest,
        LLMModelSelection selection,
        string agentName)
    {
        try
        {
            var response = await client.SendAsync(llmRequest, default);

            // Always release slot
            selection.currentRequests.Remove(schedulerRequest.RequestId);
            if (!string.IsNullOrWhiteSpace(schedulerRequest.AgentId))
            {
                inflightAgents.Remove(schedulerRequest.AgentId);
                inflightRequestIdByAgent.Remove(schedulerRequest.AgentId);
            }
            if (response == null)
            {
                selection.OnDispatchFailure(schedulerRequest.RequestId);
                UnityEngine.Debug.LogWarning($"[LLM Scheduler] Null response requestId={schedulerRequest.RequestId} agentId={schedulerRequest.AgentId}");
                return;
            }

            if (response.wasStale)
            {
                selection.OnDispatchFailure(schedulerRequest.RequestId);
                UnityEngine.Debug.Log($"[LLM Scheduler] Stale response ignored requestId={schedulerRequest.RequestId} agentId={schedulerRequest.AgentId}");
                return;
            }

            if (!response.succeeded)
            {
                selection.OnDispatchFailure(schedulerRequest.RequestId);
                UnityEngine.Debug.LogWarning($"[LLM Scheduler] LLM failed requestId={schedulerRequest.RequestId} agentId={schedulerRequest.AgentId} err={response.errorMessage}");
                return;
            }

            selection.OnDispatchSuccess(schedulerRequest.RequestId);
            string planJson =
                !string.IsNullOrWhiteSpace(response.rawText) ? response.rawText :
                !string.IsNullOrWhiteSpace(response.rawProviderPayloadJson) ? response.rawProviderPayloadJson :
                "";

            schedulerRequest.OnResponseJson?.Invoke(planJson);
        }
        catch (Exception ex)
        {
            selection.currentRequests.Remove(schedulerRequest.RequestId);
            selection.OnDispatchFailure(schedulerRequest.RequestId);
            UnityEngine.Debug.LogWarning($"[LLM Scheduler] Send failed vendor={selection.llmVendor} model={selection.llmModelString} requestId={schedulerRequest.RequestId}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void TryDispatchRequests()
    {
        if (!Dir.Instance.gen.buildComplete) return;
        if (!HasPendingRequests()) return;

        // Sort by priority (high first), then age (oldest first)
        pendingRequests.Sort((a, b) =>
        {
            int priorityCompare = b.PriorityScore.CompareTo(a.PriorityScore);
            if (priorityCompare != 0) return priorityCompare;
            return a.RequestTime.CompareTo(b.RequestTime);
        });

        int index = 0;
        while (index < pendingRequests.Count)
        {
            var request = pendingRequests[index];

            var agentId = request.AgentId;
            if (!string.IsNullOrWhiteSpace(agentId) && inflightAgents.Contains(agentId))
            {
                // Agent already has an inflight request; keep this queued.
                index++;
                continue;
            }
            LLMModelSelection? chosenModel = ChooseBestModelForRequest(request);

            if (chosenModel == null)
            {
                // No model has an open slot right now; leave it queued.
                index++;
                continue;
            }

            // We have a slot: dispatch and remove from queue.
            Dispatch(request, chosenModel);
            pendingRequests.RemoveAt(index);
        }
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

        if (TryGetCachedAgentModules(agentId, out config, out worldState, out agentGameObject))
            return true;

        RefreshAgentModuleCache();
        return TryGetCachedAgentModules(agentId, out config, out worldState, out agentGameObject);
    }

    private bool TryGetCachedAgentModules(
        string agentId,
        out LLMConfigModule config,
        out LLMWorldStateModule worldState,
        out GameObject agentGameObject)
    {
        config = null!;
        worldState = null!;
        agentGameObject = null!;

        if (!agentModuleCache.TryGetValue(agentId, out AgentModuleCacheEntry cached))
            return false;

        if (cached.Config == null || cached.WorldState == null || cached.AgentGameObject == null)
        {
            agentModuleCache.Remove(agentId);
            return false;
        }

        config = cached.Config;
        worldState = cached.WorldState;
        agentGameObject = cached.AgentGameObject;
        return true;
    }

    private void RefreshAgentModuleCache()
    {
        agentModuleCache.Clear();

        var configs = UnityEngine.Object.FindObjectsByType<LLMConfigModule>(FindObjectsSortMode.None);
        foreach (var c in configs)
        {
            if (c == null)
                continue;

            string resolvedId = c.identity.ResolveAgentId(c.gameObject);
            if (string.IsNullOrWhiteSpace(resolvedId))
                continue;

            var ws = c.GetComponent<LLMWorldStateModule>();
            if (ws == null)
            {
                Debug.LogWarning($"[LLM Scheduler] Agent {c.gameObject.name} matches id={resolvedId} but has no LLMWorldStateModule.");
                continue;
            }

            agentModuleCache[resolvedId] = new AgentModuleCacheEntry(c, ws, c.gameObject);
        }
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
        inflightAgents.Clear();
        inflightRequestIdByAgent.Clear();
        agentModuleCache.Clear();
    }

    private static bool MeetsSophistication(LLMModelSelection model, Sophistication needed)
    {
        // "Sufficient sophistication" = model tier >= needed
        // Adjust ordering if your enum isn't ordered Low < Medium < High
        return model.sophistication >= needed;
    }

    private bool HasOpenSlot(LLMModelSelection model)
    {
        if (model.vendorMaxConcurrentRequests <= 0)
            return false;

        model.currentRequests ??= new List<string>();
        return model.currentRequests.Count < model.vendorMaxConcurrentRequests;
    }

    private LLMModelSelection? ChooseBestModelForRequest(LLMPlanRequestOnDemand request)
    {
        if (llmModelsAvailable == null || llmModelsAvailable.Count == 0)
            return null;

        // 1) Prefer local first (remote == false)
        LLMModelSelection? local = ChooseFromPool(request, remote: false);
        if (local != null) return local;

        // 2) Fall back to remote
        LLMModelSelection? remoteModel = ChooseFromPool(request, remote: true);
        if (remoteModel != null) return remoteModel;

        return null;
    }

    private LLMModelSelection? ChooseFromPool(LLMPlanRequestOnDemand request, bool remote)
    {
        LLMModelSelection? best = null;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < llmModelsAvailable.Count; i++)
        {
            var model = llmModelsAvailable[i];
            if (model == null) continue;
            if (model.remote != remote) continue;
            if (!MeetsSophistication(model, request.Sophistication)) continue;
            if (!HasOpenSlot(model)) continue;

            // Scoring: tune to taste.
            // - Higher sophistication headroom is good (but mild)
            // - Higher successRate is good
            // - Lower response time is good
            // - Lower cost is good (mostly matters for remote)
            float sophHeadroom = (float)(model.sophistication - request.Sophistication); // 0..?
            float score =
                (model.successRate * 100f) +
                (sophHeadroom * 2f) +
                (-model.typicalResponseTime * 1f) +
                (-model.typicalCost * 50f);

            if (score > bestScore)
            {
                bestScore = score;
                best = model;
            }
        }

        return best;
    }

    /*
    private void MarkInFlight(LLMModelSelection model, string requestId)
    {
        model.currentRequests ??= new List<string>();
        if (!model.currentRequests.Contains(requestId))
            model.currentRequests.Add(requestId);
        model.totalRequests++;
    }

    private void MarkCompleted(LLMModelSelection model, string requestId, bool succeeded)
    {
        model.currentRequests ??= new List<string>();
        model.currentRequests.Remove(requestId);

        if (!succeeded)
            model.totalFailures++;
    }
    */
}
