#nullable enable
using System;
using UnityEngine;

namespace DogGame.LLM
{
    /// <summary>
    /// Async plan driver: periodically requests a plan (fake LLM for now),
    /// receives JSON later, validates, maps to tasks, executes tasks.
    /// </summary>
    public sealed class LLMAsyncPlanDriver : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string agentId = "player";

        [Header("Request cadence")]
        [Tooltip("Minimum time between LLM requests for this agent.")]
        [SerializeField] private float minSecondsBetweenRequests = 6.0f;

        [Tooltip("If true, only request when there are no tasks running or queued.")]
        [SerializeField] private bool requestOnlyWhenIdle = true;

        [Header("Queue behavior")]
        [SerializeField] private bool clearQueueOnNewPlan = true;

        [Header("Fake LLM Latency")]
        [SerializeField] private Vector2 simulatedLatencyRangeSeconds = new(0.4f, 1.4f);

        // Core runtime
        private AgentTaskQueue taskQueue = null!;
        private AgentTaskExecutor executor = null!;
        private AgentTaskContext context = null!;

        // Async state
        private float nextEligibleRequestTime;
        private bool requestInFlight;

        private FakeLLMService fakeService = null!;

        private void Awake()
        {
            taskQueue = new AgentTaskQueue();
            executor = new AgentTaskExecutor(taskQueue);

            // Keep the simple adapter for now; later replace with your real movement adapter.
            var movement = new SimpleMovementAdapter(transform, moveSpeed: 2.5f, cellSize: 1.0f, gridOrigin: Vector3.zero);
            context = new AgentTaskContext(agentId, transform, movement);

            // Ensure we have a FakeLLMService on this object (or add one).
            fakeService = GetComponent<FakeLLMService>();
            if (fakeService == null)
                fakeService = gameObject.AddComponent<FakeLLMService>();

            fakeService.SetLatencyRange(simulatedLatencyRangeSeconds);

            nextEligibleRequestTime = Time.time + UnityEngine.Random.Range(0f, 0.5f);
        }

        private void Update()
        {
            // Always tick tasks
            executor.Tick(context, Time.deltaTime);

            // Decide whether we should request a new plan
            if (!ShouldRequestPlanNow())
                return;

            SendPlanRequest();
        }

        private bool ShouldRequestPlanNow()
        {
            if (requestInFlight)
                return false;

            if (Time.time < nextEligibleRequestTime)
                return false;

            if (requestOnlyWhenIdle)
            {
                bool idle = !executor.HasTask && taskQueue.Count == 0;
                if (!idle)
                    return false;
            }

            return true;
        }

        private void SendPlanRequest()
        {
            requestInFlight = true;
            nextEligibleRequestTime = Time.time + minSecondsBetweenRequests;

            // For now, request JSON can be minimal. Later this will be your full request payload.
            string requestId = Guid.NewGuid().ToString("N");
            string requestJson = $"{{\"requestId\":\"{requestId}\",\"agentId\":\"{agentId}\",\"note\":\"fake request\"}}";

            fakeService.SubmitRequest(
                requestId: requestId,
                requestJson: requestJson,
                agentId: agentId,
                onResponseJson: OnPlanResponseJson);
        }

        private void OnPlanResponseJson(string responseJson)
        {
            requestInFlight = false;

            var (plan, validation) = PlanResponseV1Parser.ParseAndValidate(responseJson);

            if (plan == null)
            {
                Debug.LogWarning("LLM plan FAILED validation:\n" + string.Join("\n", validation.Errors));
                return;
            }

            // Optional strict check: only accept plans for this agent
            if (!string.Equals(plan.AgentId, agentId, StringComparison.Ordinal))
            {
                Debug.LogWarning($"Received plan for agentId={plan.AgentId} but this driver is agentId={agentId}. Ignoring.");
                return;
            }

            if (clearQueueOnNewPlan)
                taskQueue.Clear();

            if (!PlanIntentMapper.TryEnqueueTasksFromPlan(plan, taskQueue, out var mapError))
            {
                Debug.LogWarning("Plan mapped to zero tasks: " + mapError);
                return;
            }

            Debug.Log($"LLM plan accepted. Enqueued {taskQueue.Count} tasks for {agentId}.");
        }
    }
}