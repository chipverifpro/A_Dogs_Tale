#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class LLMPlanFromJsonHarness : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string agentId = "player";

        public WorldObject? agent;

        [Header("Plan JSON (PlanResponseV1)")]
        [TextArea(8, 30)]
        [SerializeField] private string planResponseJson = DefaultExampleJson;

        [Header("Execution")]
        [SerializeField] private bool clearQueueBeforeEnqueue = true;

        private AgentTaskQueue taskQueue = null!;
        private AgentTaskExecutor executor = null!;
        private AgentTaskContext context = null!;

        private void Awake()
        {
            if (agent == null)
            {
                Debug.LogError($"{nameof(LLMTaskHarness)}: Agent not assigned.", this);
                enabled = false;
                return;
            }
            taskQueue = new AgentTaskQueue();
            executor = new AgentTaskExecutor(taskQueue);

            // For now, keep using the simple adapter.
            // Later we swap to your real movement/nav adapter.
            var movement = new SimpleMovementAdapter(transform, moveSpeed: 2.5f, cellSize: 1.0f, gridOrigin: Vector3.zero);
            context = new AgentTaskContext(agentId, agent, transform, movement);
        }

        private void Start()
        {
            EnqueueFromJson(planResponseJson);
        }

        private void Update()
        {
            executor.Tick(context, Time.deltaTime);
        }

        [ContextMenu("Enqueue From JSON Now")]
        public void EnqueueFromJsonNow()
        {
            EnqueueFromJson(planResponseJson);
        }

        private void EnqueueFromJson(string json)
        {
            var (plan, validation) = PlanResponseV1Parser.ParseAndValidate(json);

            if (plan == null)
            {
                Debug.LogWarning("PlanResponseV1 parse/validation FAILED:\n" + string.Join("\n", validation.Errors));
                return;
            }

            // Optional: ensure this plan is meant for this agent (or allow "player" to accept anything while testing)
            if (!string.IsNullOrWhiteSpace(plan.AgentId) && plan.AgentId != agentId)
            {
                Debug.LogWarning($"Plan agentId mismatch. Plan targets \"{plan.AgentId}\" but harness agentId is \"{agentId}\".");
                // return; // Uncomment if you want strict behavior
            }

            if (clearQueueBeforeEnqueue)
                taskQueue.Clear();

            if (!PlanIntentMapper.TryEnqueueTasksFromPlan(plan, taskQueue, out var mapError))
            {
                Debug.LogWarning("PlanIntentMapper enqueued nothing: " + mapError);
                return;
            }

            Debug.Log($"Enqueued plan tasks for agentId={agentId}. QueueCount={taskQueue.Count}");
        }

        private const string DefaultExampleJson =
            "{\n" +
            "  \"schema\": \"PlanResponseV1\",\n" +
            "  \"requestId\": \"demo-001\",\n" +
            "  \"agentId\": \"player\",\n" +
            "  \"intentions\": [\n" +
            "    {\n" +
            "      \"type\": \"add_task\",\n" +
            "      \"id\": \"t1\",\n" +
            "      \"priority\": 0.9,\n" +
            "      \"rationale\": \"Move to two points with a pause.\",\n" +
            "      \"parameters\": { \"task\": \"move_to_cell\", \"locationCell\": [5, 3], \"stopRadius\": 0.2 }\n" +
            "    },\n" +
            "    {\n" +
            "      \"type\": \"add_task\",\n" +
            "      \"id\": \"t2\",\n" +
            "      \"priority\": 0.5,\n" +
            "      \"parameters\": { \"task\": \"wait\", \"seconds\": 1.0 }\n" +
            "    },\n" +
            "    {\n" +
            "      \"type\": \"add_task\",\n" +
            "      \"id\": \"t3\",\n" +
            "      \"priority\": 0.8,\n" +
            "      \"parameters\": { \"task\": \"move_to_cell\", \"locationCell\": [2, 8], \"stopRadius\": 0.2 }\n" +
            "    }\n" +
            "  ],\n" +
            "  \"debug\": { \"confidence\": 0.7, \"notes\": [\"json harness\"] }\n" +
            "}\n";
    }
}