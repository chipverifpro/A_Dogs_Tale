#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.LLM
{
    public enum RemoteLLMProvider
    {
        OpenAI,
        Gemini
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
        [SerializeField] private int maxConcurrentLocalRequests = 2;
        [SerializeField] private int maxConcurrentRemoteRequests = 1;

        [Header("Scheduling")]
        [SerializeField] private float schedulingIntervalSeconds = 0.25f;

        private readonly List<LLMPlanRequest> pendingRequests = new();

        private int activeLocalRequests;
        private int activeRemoteRequests;

        private RemoteLLMService? openAiService;
        private GeminiLLMService? geminiService;
        private float nextScheduleTime;

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
                    openAiService = gameObject.AddComponent<RemoteLLMService>();
                    break;
                case RemoteLLMProvider.Gemini:
                    geminiService = gameObject.AddComponent<GeminiLLMService>();
                    break;
            }
        }

        private void Update()
        {
            if (Time.time < nextScheduleTime)
                return;

            nextScheduleTime = Time.time + schedulingIntervalSeconds;
            TryDispatchRequests();
        }

        /// <summary>
        /// Agents call this to request a planning slot.
        /// </summary>
        public void EnqueueRequest(LLMPlanRequest request)
        {
            pendingRequests.Add(request);
        }

        private void TryDispatchRequests()
        {
            if (pendingRequests.Count == 0)
                return;

            // Sort by priority, then age (fairness)
            pendingRequests.Sort((a, b) =>
            {
                int priorityCompare = b.PriorityScore.CompareTo(a.PriorityScore);
                if (priorityCompare != 0)
                    return priorityCompare;

                return a.RequestTime.CompareTo(b.RequestTime);
            });

            for (int i = pendingRequests.Count - 1; i >= 0; i--)
            {
                var request = pendingRequests[i];

                if (!CanDispatch(request.ModelTier))
                    continue;

                Dispatch(request);
                pendingRequests.RemoveAt(i);
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

        private void Dispatch(LLMPlanRequest request)
        {
            IncrementActive(request.ModelTier);
            string RequestJson_text =
    "IMPORTANT: Output ONLY JSON.\n" +
    "Return ONLY a PlanResponseV1 JSON object in EXACTLY this format:\n" +
    "{\n" +
    "  \"schema\":\"PlanResponseV1\",\n" +
    "  \"requestId\":\"<copy from requestId below>\",\n" +
    "  \"agentId\":\"<copy from agentId below>\",\n" +
    "  \"intentions\":[\n" +
    "    {\"type\":\"add_task\",\"id\":\"t1\",\"priority\":0.9,\"parameters\":{\"task\":\"move_to_cell\",\"locationCell\":[5,3],\"stopRadius\":0.2}},\n" +
    "    {\"type\":\"add_task\",\"id\":\"t2\",\"priority\":0.5,\"parameters\":{\"task\":\"wait\",\"seconds\":1.0}}\n" +
    "  ],\n" +
    "  \"debug\":{\"confidence\":0.5,\"notes\":[]}\n" +
    "}\n" +
    "RULES:\n" +
    "- intentions entries MUST contain: type, id, priority, parameters.\n" +
    "- Use type=\"add_task\".\n" +
    "- parameters.task must be one of: \"move_to_cell\", \"wait\".\n" +
    "- move_to_cell parameters: locationCell [int,int], stopRadius float.\n" +
    "- wait parameters: seconds float.\n" +
    "- Do NOT output intentions as {task, params}.\n\n" +
    "INPUT PACKET:\n"
                + "{"
                + "\"schema\":\"PlanRequestV1\","
                //+ $"\"requestId\":\"{request.RequestId}\","
                //+ $"\"agentId\":\"{request.AgentId}\","
                + "\"allowedTasks\":["
                + "{\"task\":\"wait\",\"params\":{\"seconds\":\"float 0..30\"}},"
                + "{\"task\":\"move_to_cell\",\"params\":{\"locationCell\":\"[int,int]\",\"stopRadius\":\"float 0.05..2.0\"}}"
                + "],"
                + "\"goal\":\"Pick 2-4 simple tasks: move somewhere nearby, maybe wait."
                + " Return 1–4 add_task intentions unless impossible. If impossible, return a single wait.\""
                + "}";

            Action<string> onResponse = (json) =>
            {
                DecrementActive(request.ModelTier);
                request.OnResponseJson(json);
            };

            switch (remoteProvider)
            {
                case RemoteLLMProvider.OpenAI:
                    openAiService?.SubmitRequest(
                        requestId: request.RequestId,
                        requestJson: RequestJson_text,
                        agentId: request.AgentId,
                        onResponseJson: onResponse);
                    break;
                case RemoteLLMProvider.Gemini:
                    geminiService?.SubmitRequest(
                        requestId: request.RequestId,
                        requestJson: RequestJson_text,
                        agentId: request.AgentId,
                        onResponseJson: onResponse);
                    break;
            }

            Debug.Log($"[LLM Scheduler] Dispatched {request.ModelTier} request for {request.AgentId} using {remoteProvider}");
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
    }
}