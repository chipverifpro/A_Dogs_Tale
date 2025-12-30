#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace DogGame.LLM
{
    /// <summary>
    /// Global LLM request scheduler.
    /// Ensures fairness, throttling, and model-tier limits.
    /// </summary>
    public sealed class LLMWorldScheduler : MonoBehaviour
    {
        public static LLMWorldScheduler Instance { get; private set; } = null!;

        [Header("Throughput limits")]
        [SerializeField] private int maxConcurrentLocalRequests = 2;
        [SerializeField] private int maxConcurrentRemoteRequests = 1;

        [Header("Scheduling")]
        [SerializeField] private float schedulingIntervalSeconds = 0.25f;

        private readonly List<LLMPlanRequest> pendingRequests = new();

        private int activeLocalRequests;
        private int activeRemoteRequests;

        private FakeLLMService fakeService = null!;
        private float nextScheduleTime;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            fakeService = gameObject.AddComponent<FakeLLMService>();
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
                LLMModelTier.LocalSmall  => activeLocalRequests < maxConcurrentLocalRequests,
                LLMModelTier.RemotePaid => activeRemoteRequests < maxConcurrentRemoteRequests,
                _ => false
            };
        }

        private void Dispatch(LLMPlanRequest request)
        {
            IncrementActive(request.ModelTier);

            fakeService.SubmitRequest(
                requestId: request.RequestId,
                requestJson: "{ \"note\": \"scheduled fake request\" }",
                agentId: request.AgentId,
                onResponseJson: (json) =>
                {
                    DecrementActive(request.ModelTier);
                    request.OnResponseJson(json);
                });

            Debug.Log($"[LLM Scheduler] Dispatched {request.ModelTier} request for {request.AgentId}");
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