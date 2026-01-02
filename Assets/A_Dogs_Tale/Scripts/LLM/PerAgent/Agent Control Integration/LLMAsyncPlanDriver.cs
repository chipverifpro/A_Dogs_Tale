#nullable enable
using System;
using UnityEngine;
using DogGame.Modules;

namespace DogGame.LLM
{
    /// <summary>
    /// Async plan driver: periodically requests a plan (fake LLM for now),
    /// receives JSON later, validates, maps to tasks, executes tasks.
    /// </summary>
    public sealed class LLMAsyncPlanDriver : WorldModule
    {
        [Header("Identity")]
        [SerializeField] private string agentId = "player";

        [Header("Request cadence")]
        [Tooltip("Minimum time between LLM requests for this agent.")]
        [SerializeField] private float minSecondsBetweenRequests = 6.0f;

        [Tooltip("If true, only request when there are no tasks running or queued.")]
        [SerializeField] private bool requestOnlyWhenIdle = true;

        [Header("Fake LLM Latency")]
        [SerializeField] private Vector2 simulatedLatencyRangeSeconds = new(0.4f, 1.4f);

        // Core runtime
        private AgentTaskExecutor executor = null!;
        private AgentTaskContext context = null!;
        private AgentTaskController controller = null!;

        // Async state
        private float nextEligibleRequestTime;
        private bool requestInFlight;

        private FakeLLMService fakeService = null!;

        protected override void Awake()
        {
            agentId = worldObject.DisplayName;

            controller = GetComponent<AgentTaskController>();
            if (controller == null)
                controller = gameObject.AddComponent<AgentTaskController>();
                
            executor = new AgentTaskExecutor(controller.TaskQueue);

            // Keep the simple adapter for now; later replace with your real movement adapter.
            var movement = new SimpleMovementAdapter(transform, moveSpeed: 2.5f, cellSize: 1.0f, gridOrigin: Vector3.zero);
            context = new AgentTaskContext(agentId, worldObject, transform, movement);

            // Ensure we have a FakeLLMService on this object (or add one).
            fakeService = GetComponent<FakeLLMService>();
            if (fakeService == null)
                fakeService = gameObject.AddComponent<FakeLLMService>();

            fakeService.SetLatencyRange(simulatedLatencyRangeSeconds);

            nextEligibleRequestTime = Time.time + UnityEngine.Random.Range(0f, 0.5f);
        }

        protected override void Update()
        {
            // Decide whether we should request a new plan
            if (!ShouldRequestPlanNow())
                return;

            SendPlanRequest();
        }

        private void OnEnable()
        {
            //Debug.Log($"[{name}] LLMAsyncPlanDriver ENABLED (activeSelf={gameObject.activeSelf}, enabled={enabled})", this);
        }

        private void OnDisable()
        {
            //Debug.LogWarning(
            //    $"[{name}] LLMAsyncPlanDriver DISABLED (activeSelf={gameObject.activeSelf}, enabled={enabled})\n" +
            //    $"Stack:\n{Environment.StackTrace}",
            //    this);
        }

        private bool ShouldRequestPlanNow()
        {
            if (requestInFlight)
                return false;

            if (Time.time < nextEligibleRequestTime)
                return false;

            if (requestOnlyWhenIdle)
            {
                bool idle = !executor.HasTask && controller.TaskQueue.Count == 0;
                if (!idle)
                    return false;
            }

            if (requestOnlyWhenIdle && controller.IsDriving)
                return false;

            return true;
        }

        private void SendPlanRequest()
        {
            requestInFlight = true;
            nextEligibleRequestTime = Time.time + minSecondsBetweenRequests;

            float priority =
                executor.HasTask ? 0.2f :
                controller.TaskQueue.Count == 0 ? 0.8f :
                0.4f;

            var request = new LLMPlanRequest(
                agentId: agentId,
                modelTier: LLMModelTier.LocalSmall,
                priorityScore: priority,
                onResponseJson: OnPlanResponseJson);

            LLMWorldScheduler.Instance.EnqueueRequest(request);
        }

        private void OnPlanResponseJson(string responseJson)
        {
            requestInFlight = false;

            if (string.IsNullOrWhiteSpace(responseJson))
            {
                Debug.LogWarning("Received empty plan JSON response.");
                return;
            }

            // Single source of truth: controller is responsible for parse/validate/agentId check/queue clearing/mapping/enqueue.
            // This prevents double-enqueue and keeps the driver as a transport layer.
            bool applied = controller.TryApplyPlanJson(responseJson);

            if (!applied)
            {
                Debug.LogWarning($"LLM plan was not applied for {agentId} (controller rejected or failed).");
                return;
            }

            // Optional: if your controller exposes queue count, you can log it here.
            // Otherwise keep a generic success log.
            Debug.Log($"LLM plan applied for {agentId}.");
        }
    }
}