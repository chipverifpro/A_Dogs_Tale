#nullable enable
using UnityEngine;
using DogGame.Tasks;

namespace DogGame.LLM
{
    public sealed class LLMTaskHarness : MonoBehaviour
    {
        [SerializeField] private string agentId = "npc_023";
        public WorldObject? agent;
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

            var movement = new SimpleMovementAdapter(transform, moveSpeed: 2.5f, cellSize: 1.0f, gridOrigin: Vector3.zero);
            context = new AgentTaskContext(agentId, agent, transform, movement);
        }

        private void Start()
        {
            // Enqueue a couple manual tasks (sanity check)
            taskQueue.Enqueue(new Task_MoveToCell(5, 3, 0.2f));
            taskQueue.Enqueue(new Task_Wait(1.0f));
            taskQueue.Enqueue(new Task_MoveToCell(2, 8, 0.2f));
        }

        private void Update()
        {
            executor.Tick(context, Time.deltaTime);
        }
    }
}