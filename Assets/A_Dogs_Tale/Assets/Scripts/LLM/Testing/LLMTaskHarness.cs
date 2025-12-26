#nullable enable
using UnityEngine;

namespace DogGame.LLM
{
    public sealed class LLMTaskHarness : MonoBehaviour
    {
        [SerializeField] private string agentId = "npc_023";

        private AgentTaskQueue taskQueue = null!;
        private AgentTaskExecutor executor = null!;
        private AgentTaskContext context = null!;

        private void Awake()
        {
            taskQueue = new AgentTaskQueue();
            executor = new AgentTaskExecutor(taskQueue);

            var movement = new SimpleMovementAdapter(transform, moveSpeed: 2.5f, cellSize: 1.0f, gridOrigin: Vector3.zero);
            context = new AgentTaskContext(agentId, transform, movement);
        }

        private void Start()
        {
            // Enqueue a couple manual tasks (sanity check)
            taskQueue.Enqueue(new MoveToCellTask(5, 3, 0.2f));
            taskQueue.Enqueue(new WaitTask(1.0f));
            taskQueue.Enqueue(new MoveToCellTask(2, 8, 0.2f));
        }

        private void Update()
        {
            executor.Tick(context, Time.deltaTime);
        }
    }
}