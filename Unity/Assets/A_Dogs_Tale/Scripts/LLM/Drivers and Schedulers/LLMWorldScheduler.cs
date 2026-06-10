#nullable enable
using System;
using System.Collections.Generic;
using DogGame.LLM.Agent;
using DogGame.LLM.Core;
using DogGame.LLM.Providers;
using CoreLLMResponse = DogGame.LLM.Core.LLMResponse;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        Mistral_mistral_small_latest = 1 << 11,

        //Ollama_Qwen3_4b = 1 << 20,
        //Ollama_Qwen3_8b = 1 << 21,
        Ollama_Gemma3 = 1 << 22,
        Ollama_Qwen2_5_1_5b = 1 << 23,
        Ollama_Mistral = 1 << 24
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
        [NonSerialized] private Dictionary<string, float> requestStartTimes = new();

        public bool HasOpenSlot =>
            GetCurrentRequests().Count < vendorMaxConcurrentRequests;

        public void OnDispatchStart(string requestId)
        {
            totalRequests++;
            GetCurrentRequests().Add(requestId);
            GetRequestStartTimes()[requestId] = Time.realtimeSinceStartup;
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
            GetCurrentRequests().Remove(requestId);

            Dictionary<string, float> startTimes = GetRequestStartTimes();
            if (startTimes.TryGetValue(requestId, out float start))
            {
                float duration = Time.realtimeSinceStartup - start;
                UpdateTypicalResponseTime(duration);
                startTimes.Remove(requestId);
            }

            UpdateSuccessRate();
        }

        private List<string> GetCurrentRequests()
        {
            currentRequests ??= new List<string>();
            return currentRequests;
        }

        private Dictionary<string, float> GetRequestStartTimes()
        {
            requestStartTimes ??= new Dictionary<string, float>();
            return requestStartTimes;
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
    Mistral,
    Ollama,
    None
}

public enum RemoteLLMModel
{
    ChatGPT,
    Gemini,
    Ollama_Qwen,
    Ollama_Gemma,
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

    public enum SchedulerCommandMode
    {
        JsonCommands,
        McpToolCalls
    }

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
//        new(LLMVendorAndModel.Ollama_Gemma3,
//            LLMVendor.Ollama,
//            "Gemma3",
//            false,
//            Sophistication.Medium,
//            TypicalResponseTime: 20f,
//            SuccessRate: 0.8f,
//            TypicalCost: 0f,
//            vendorMaxConcurrentRequests: 1),
//        new(LLMVendorAndModel.Ollama_Qwen3_4b,
//            LLMVendor.Ollama,
//            "Qwen3:4b",
//            false,
//            Sophistication.Low,
//            TypicalResponseTime: 70f,
//            SuccessRate: 0.5f,
//            TypicalCost: 0f,
//            vendorMaxConcurrentRequests: 1),
        new(LLMVendorAndModel.Ollama_Qwen2_5_1_5b,
            LLMVendor.Ollama,
            "Qwen2.5:1.5b",
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

    [Header("Command Mode")]
    [SerializeField] private SchedulerCommandMode commandMode = SchedulerCommandMode.JsonCommands;

    [SerializeField] public List<LLMPlanRequestOnDemand> pendingRequests = new();

    [Serializable]
    public sealed class SaveData
    {
        public int commandMode;
        public List<RequestSaveData> pendingRequests = new();
        public List<RequestSaveData> inflightRequests = new();
    }

    [Serializable]
    public sealed class RequestSaveData
    {
        public string agentId = "";
        public string prompt = "";
        public bool hasEventCell;
        public int eventCellX;
        public int eventCellY;
        public bool hasEventWorld;
        public float eventWorldX;
        public float eventWorldY;
        public float eventWorldZ;
        public int urgency;
        public int applyMode;
        public string tag = "";
        public string requestId = "";
        public int sophistication;
        public float priorityScore;
        public float requestTime;

        public static RequestSaveData FromRequest(LLMPlanRequestOnDemand request)
        {
            RequestSaveData data = new();
            if (request == null)
                return data;

            data.agentId = request.AgentId;
            data.prompt = request.Prompt;
            data.hasEventCell = request.EventCell.HasValue;
            if (request.EventCell.HasValue)
            {
                data.eventCellX = request.EventCell.Value.x;
                data.eventCellY = request.EventCell.Value.y;
            }

            data.hasEventWorld = request.EventWorld.HasValue;
            if (request.EventWorld.HasValue)
            {
                data.eventWorldX = request.EventWorld.Value.x;
                data.eventWorldY = request.EventWorld.Value.y;
                data.eventWorldZ = request.EventWorld.Value.z;
            }

            data.urgency = (int)request.Urgency;
            data.applyMode = (int)request.ApplyMode;
            data.tag = request.Tag;
            data.requestId = request.RequestId;
            data.sophistication = (int)request.Sophistication;
            data.priorityScore = request.PriorityScore;
            data.requestTime = request.RequestTime;
            return data;
        }
    }

    //private OpenAILLMService? openAiService;
    //private GeminiLLMService? geminiService;
    //private OllamaLLMService? ollamaService;
    private float nextScheduleTime;

    //public string requestJSON;

    // One in-flight request per agent.
    private readonly HashSet<string> inflightAgents = new();
    private readonly Dictionary<string, string> inflightRequestIdByAgent = new();
    private readonly Dictionary<string, LLMPlanRequestOnDemand> inflightRequestByAgent = new();
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

        PersistentGameSettings.ApplySavedToScheduler(this);
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

        ApplyCommandMode(llmRequest);

        // Force vendor/model to match the selection (prevents the “why is it using OpenAI” bug)
        llmRequest.profile.vendor = selection.VendorName;
        llmRequest.profile.model = selection.llmModelString;

        // Track inflight on the selection (per-model concurrency)
        if (!string.IsNullOrWhiteSpace(request.AgentId))
        {
            inflightAgents.Add(request.AgentId);
            inflightRequestIdByAgent[request.AgentId] = request.RequestId;
            inflightRequestByAgent[request.AgentId] = request;
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
            ReleaseInflightAgent(request.AgentId, request.RequestId);
            selection.OnDispatchFailure(requestName);
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
        string failureStage = "send";
        try
        {
            var response = await client.SendAsync(llmRequest, default);
            ReleaseInflightAgent(schedulerRequest.AgentId, schedulerRequest.RequestId);
            failureStage = "handle response";

            if (response == null)
            {
                selection.OnDispatchFailure(schedulerRequest.RequestId);
                ShowLlmFailureBanner(agentName, "LLM failed: null response.");
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
                ShowLlmFailureBanner(agentName, $"LLM failed: {response.errorMessage}");
                UnityEngine.Debug.LogWarning($"[LLM Scheduler] LLM failed requestId={schedulerRequest.RequestId} agentId={schedulerRequest.AgentId} err={response.errorMessage}");
                return;
            }

            string rawPayload =
                !string.IsNullOrWhiteSpace(response.rawText) ? response.rawText :
                !string.IsNullOrWhiteSpace(response.rawProviderPayloadJson) ? response.rawProviderPayloadJson :
                "";

            if (!TryBuildSchedulerResponsePayload(llmRequest, response, schedulerRequest, rawPayload, out string callbackPayload, out string? modeError))
            {
                selection.OnDispatchFailure(schedulerRequest.RequestId);
                ShowLlmFailureBanner(agentName, $"LLM returned invalid command: {modeError}");
                UnityEngine.Debug.LogWarning($"[LLM Scheduler] Response hook failed requestId={schedulerRequest.RequestId} agentId={schedulerRequest.AgentId} err={modeError}");
                return;
            }

            UnityEngine.Debug.Log(
                $"[LLM Scheduler] Delivering callback requestId={schedulerRequest.RequestId} agentId={schedulerRequest.AgentId} mode={llmRequest.commandMode} chars={callbackPayload.Length}");
            failureStage = "response callback";
            schedulerRequest.OnResponseJson?.Invoke(callbackPayload);
            selection.OnDispatchSuccess(schedulerRequest.RequestId);
            ShowLlmReceivedBanner(agentName, selection.VendorName);
        }
        catch (Exception ex)
        {
            ReleaseInflightAgent(schedulerRequest.AgentId, schedulerRequest.RequestId);
            selection.OnDispatchFailure(schedulerRequest.RequestId);
            string failureMessage = failureStage == "send"
                ? $"LLM failed: {ex.GetType().Name}: {ex.Message}"
                : $"LLM {failureStage} failed: {ex.GetType().Name}: {ex.Message}";
            ShowLlmFailureBanner(agentName, failureMessage);
            UnityEngine.Debug.LogWarning($"[LLM Scheduler] {failureStage} failed vendor={selection.llmVendor} model={selection.llmModelString} requestId={schedulerRequest.RequestId}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void ShowLlmReceivedBanner(string agentName, string vendor)
    {
        ShowLlmBannerWithIcon(
            agentName,
            $"received LLM response from {FormatVendorForBanner(vendor)}",
            "LLM_Receive_Package");
    }

    private static void ShowLlmFailureBanner(string agentName, string reason)
    {
        ShowLlmBannerWithIcon(agentName, SummarizeBannerReason(reason), "Sad");
    }

    private static void ShowLlmBannerWithIcon(string agentName, string message, string iconSpriteName)
    {
        try
        {
            string actor = string.IsNullOrWhiteSpace(agentName) ? "Unknown agent" : agentName.Trim();
            BottomBanner.LogAgentMessageWithIcon(
                agentName,
                BannerSense.None,
                BannerLevel.None,
                $"{actor} {message}",
                iconSpriteName);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LLM Scheduler] Failed to show bottom banner: {ex.Message}");
        }
    }

    private static string FormatVendorForBanner(string vendor)
    {
        if (string.Equals(vendor, "OpenAI", StringComparison.OrdinalIgnoreCase))
            return "ChatGPT";

        return string.IsNullOrWhiteSpace(vendor) ? "LLM" : vendor.Trim();
    }

    private static string SummarizeBannerReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "LLM failed.";

        string oneLine = reason.Replace("\r", " ").Replace("\n", " ").Trim();
        const int maxLength = 220;
        return oneLine.Length <= maxLength ? oneLine : oneLine.Substring(0, maxLength - 3) + "...";
    }

    private void ReleaseInflightAgent(string? agentId, string? requestId = null)
    {
        if (string.IsNullOrWhiteSpace(agentId))
            return;

        if (!string.IsNullOrWhiteSpace(requestId) &&
            inflightRequestIdByAgent.TryGetValue(agentId, out string currentRequestId) &&
            !string.Equals(currentRequestId, requestId, StringComparison.Ordinal))
        {
            return;
        }

        inflightAgents.Remove(agentId);
        inflightRequestIdByAgent.Remove(agentId);
        inflightRequestByAgent.Remove(agentId);
    }

    public SaveData CaptureSaveData()
    {
        SaveData data = new()
        {
            commandMode = (int)commandMode,
            pendingRequests = new List<RequestSaveData>(),
            inflightRequests = new List<RequestSaveData>()
        };

        if (pendingRequests != null)
        {
            foreach (LLMPlanRequestOnDemand request in pendingRequests)
            {
                if (request != null)
                    data.pendingRequests.Add(RequestSaveData.FromRequest(request));
            }
        }

        foreach (LLMPlanRequestOnDemand request in inflightRequestByAgent.Values)
        {
            if (request != null)
                data.inflightRequests.Add(RequestSaveData.FromRequest(request));
        }

        return data;
    }

    public void RestoreSaveData(SaveData data)
    {
        DogGame.LLM.Core.LLMSessionToken.Bump();
        ClearRuntimeRequestState();

        if (data == null)
            return;

        commandMode = (SchedulerCommandMode)data.commandMode;
        RefreshAgentModuleCache();

        RestoreSavedRequestList(data.inflightRequests);
        RestoreSavedRequestList(data.pendingRequests);
        nextScheduleTime = Mathf.Min(nextScheduleTime, Time.time);
    }

    private void RestoreSavedRequestList(List<RequestSaveData> savedRequests)
    {
        if (savedRequests == null)
            return;

        foreach (RequestSaveData savedRequest in savedRequests)
        {
            LLMPlanRequestOnDemand? request = CreateRequestFromSaveData(savedRequest);
            if (request == null)
                continue;

            pendingRequests.Add(request);
            MarkAgentHasRestoredOutstandingRequest(request.AgentId);
        }
    }

    private LLMPlanRequestOnDemand? CreateRequestFromSaveData(RequestSaveData savedRequest)
    {
        if (savedRequest == null || string.IsNullOrWhiteSpace(savedRequest.agentId))
            return null;

        Vector2Int? eventCell = savedRequest.hasEventCell
            ? new Vector2Int(savedRequest.eventCellX, savedRequest.eventCellY)
            : null;
        Vector3? eventWorld = savedRequest.hasEventWorld
            ? new Vector3(savedRequest.eventWorldX, savedRequest.eventWorldY, savedRequest.eventWorldZ)
            : null;

        LLMPlanRequestOnDemand request = new(
            agentId: savedRequest.agentId,
            prompt: savedRequest.prompt,
            eventCell: eventCell,
            eventWorld: eventWorld,
            urgency: (LLMPlanUrgency)savedRequest.urgency,
            applyMode: (LLMApplyMode)savedRequest.applyMode,
            tag: savedRequest.tag,
            sophistication: (Sophistication)savedRequest.sophistication,
            onResponseJson: planJson => DeliverRestoredResponse(savedRequest.agentId, planJson),
            priorityScoreOverride: savedRequest.priorityScore);

        // Reloaded requests are re-issued as fresh provider calls. A new request id prevents
        // a late stale response from the pre-load session from matching the replacement request.
        request.RequestId = DogGame.LLM.Core.LLMRequestId.NewShortHex();
        request.RequestTime = savedRequest.requestTime > 0f ? savedRequest.requestTime : Time.time;
        return request;
    }

    private void DeliverRestoredResponse(string agentId, string planJson)
    {
        if (!TryResolveAgentModules(agentId, out _, out _, out GameObject agentGameObject))
        {
            Debug.LogWarning($"[LLM Scheduler] Restored response could not resolve agentId={agentId}.");
            return;
        }

        LLMThinkModule thinkModule = agentGameObject.GetComponent<LLMThinkModule>();
        if (thinkModule == null)
        {
            Debug.LogWarning($"[LLM Scheduler] Restored response found no LLMThinkModule for agentId={agentId}.");
            return;
        }

        thinkModule.ReceivePlanJsonFromScheduler(planJson);
    }

    private void MarkAgentHasRestoredOutstandingRequest(string agentId)
    {
        if (!TryResolveAgentModules(agentId, out _, out _, out GameObject agentGameObject))
            return;

        LLMThinkModule thinkModule = agentGameObject.GetComponent<LLMThinkModule>();
        if (thinkModule != null)
            thinkModule.RestoreOutstandingRequestState(hasOutstandingRequest: true);
    }

    private void ClearRuntimeRequestState()
    {
        pendingRequests ??= new List<LLMPlanRequestOnDemand>();
        pendingRequests.Clear();
        inflightAgents.Clear();
        inflightRequestIdByAgent.Clear();
        inflightRequestByAgent.Clear();

        if (llmModelsAvailable != null)
        {
            foreach (LLMModelSelection model in llmModelsAvailable)
                model.currentRequests?.Clear();
        }
    }

    private void ApplyCommandMode(LLMRequest llmRequest)
    {
        llmRequest.commandMode = commandMode == SchedulerCommandMode.McpToolCalls
            ? LLMCommandMode.McpToolCalls
            : LLMCommandMode.JsonCommands;

        if (llmRequest.commandMode == LLMCommandMode.McpToolCalls)
        {
            llmRequest.responseSchema = null;
            llmRequest.responseSchemaJson = "";
            llmRequest.systemBlocks.Add("COMMAND MODE OVERRIDE: Use MCP tool call JSON, not PlanResponseV3.");
        }
        else
        {
            llmRequest.toolDefinitions = null;
            llmRequest.systemBlocks.Add("COMMAND MODE OVERRIDE: Use PlanResponseV3 JSON.");
        }
    }

    private static bool TryBuildSchedulerResponsePayload(
        LLMRequest llmRequest,
        CoreLLMResponse response,
        LLMPlanRequestOnDemand schedulerRequest,
        string rawPayload,
        out string callbackPayload,
        out string? error)
    {
        callbackPayload = rawPayload;
        error = null;

        if (llmRequest.commandMode != LLMCommandMode.McpToolCalls)
            return true;

        if (!TryExtractMcpToolCalls(response, rawPayload, out var toolCalls, out error))
            return false;

        var plan = new JObject
        {
            ["schema"] = "PlanResponseV3",
            ["requestId"] = schedulerRequest.RequestId ?? "",
            ["agentId"] = schedulerRequest.AgentId ?? "",
            ["intentions"] = ConvertToolCallsToIntentions(toolCalls)
        };

        callbackPayload = plan.ToString(Formatting.None);
        return true;
    }

    private static bool TryExtractMcpToolCalls(
        CoreLLMResponse response,
        string rawPayload,
        out List<CoreLLMResponse.ToolCall> toolCalls,
        out string? error)
    {
        toolCalls = new List<CoreLLMResponse.ToolCall>();
        error = null;

        if (response.toolCalls != null && response.toolCalls.Count > 0)
        {
            toolCalls.AddRange(response.toolCalls);
            return true;
        }

        string? extracted = ExtractFirstJsonObject(rawPayload);
        if (string.IsNullOrWhiteSpace(extracted))
        {
            error = "Could not find MCP tool call JSON in provider output.";
            return false;
        }

        JToken rootToken;
        try
        {
            rootToken = JToken.Parse(extracted);
        }
        catch (Exception ex)
        {
            error = $"MCP tool call JSON parse failed: {ex.Message}";
            return false;
        }

        JArray? callsArray = rootToken as JArray;
        if (callsArray == null && rootToken is JObject rootObject)
        {
            callsArray =
                rootObject["tool_calls"] as JArray ??
                rootObject["toolCalls"] as JArray ??
                rootObject["calls"] as JArray;
        }

        if (callsArray == null || callsArray.Count == 0)
        {
            error = "MCP tool call JSON did not contain any tool calls.";
            return false;
        }

        for (int i = 0; i < callsArray.Count; i++)
        {
            if (callsArray[i] is not JObject callObject)
                continue;

            string? name =
                callObject.Value<string>("name") ??
                callObject.Value<string>("tool_name");

            if (string.IsNullOrWhiteSpace(name))
                continue;

            string argumentsJson = "{}";
            if (callObject["arguments"] is JObject argumentsObject)
                argumentsJson = argumentsObject.ToString(Formatting.None);
            else if (callObject["arguments"] is JValue argumentsValue && argumentsValue.Type == JTokenType.String)
                argumentsJson = argumentsValue.Value<string>() ?? "{}";

            toolCalls.Add(new CoreLLMResponse.ToolCall
            {
                name = name.Trim(),
                argumentsJson = string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson
            });
        }

        if (toolCalls.Count == 0)
        {
            error = "MCP tool call JSON contained no valid tool calls.";
            return false;
        }

        return true;
    }

    private static JArray ConvertToolCallsToIntentions(List<CoreLLMResponse.ToolCall> toolCalls)
    {
        var intentions = new JArray();

        for (int i = 0; i < toolCalls.Count; i++)
        {
            var call = toolCalls[i];
            var intention = new JObject
            {
                ["action"] = call.name ?? ""
            };

            if (!string.IsNullOrWhiteSpace(call.argumentsJson))
            {
                try
                {
                    if (JObject.Parse(call.argumentsJson) is JObject argumentsObject)
                        intention.Merge(argumentsObject, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
                }
                catch
                {
                    intention["reasoning"] = $"Invalid MCP arguments JSON for tool {call.name}.";
                }
            }

            if (string.IsNullOrWhiteSpace(intention.Value<string>("reasoning")))
                intention["reasoning"] = $"Selected tool {call.name}.";

            intentions.Add(intention);
        }

        return intentions;
    }

    private static string? ExtractFirstJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Replace("```json", "", StringComparison.OrdinalIgnoreCase)
                .Replace("```", "", StringComparison.OrdinalIgnoreCase)
                .Trim();

        int firstBracket = text.IndexOf('[');
        int firstBrace = text.IndexOf('{');
        int firstIndex;
        char openChar;
        char closeChar;

        if (firstBracket >= 0 && (firstBrace < 0 || firstBracket < firstBrace))
        {
            firstIndex = firstBracket;
            openChar = '[';
            closeChar = ']';
        }
        else if (firstBrace >= 0)
        {
            firstIndex = firstBrace;
            openChar = '{';
            closeChar = '}';
        }
        else
        {
            return null;
        }

        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = firstIndex; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (escape)
                {
                    escape = false;
                    continue;
                }

                if (c == '\\')
                {
                    escape = true;
                    continue;
                }

                if (c == '"')
                    inString = false;

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == openChar)
            {
                depth++;
                continue;
            }

            if (c != closeChar)
                continue;

            depth--;
            if (depth == 0)
                return text.Substring(firstIndex, i - firstIndex + 1);
        }

        return null;
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
        inflightRequestByAgent.Clear();
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
