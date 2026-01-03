#nullable enable
using System;
using UnityEngine;
using DogGame.LLM;
namespace DogGame.Tasks
{
    /// <summary>
    /// Evaluates a condition once, then enqueues either THEN or ELSE tasks.
    /// This task itself completes immediately (Succeeded) after enqueuing.
    /// </summary>
    public sealed class Task_Branch : IAgentTask
    {
        public string DebugName { get; }

        private readonly Func<AgentTaskContext, bool> condition;
        private readonly IAgentTask[] thenTasks;
        private readonly IAgentTask[] elseTasks;
        private readonly AgentTaskQueue queue;

        private bool evaluated;

        public Task_Branch(
            AgentTaskQueue queue,
            Func<AgentTaskContext, bool> condition,
            IAgentTask[] thenTasks,
            IAgentTask[] elseTasks,
            string debugName = "Branch")
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.thenTasks = thenTasks ?? Array.Empty<IAgentTask>();
            this.elseTasks = elseTasks ?? Array.Empty<IAgentTask>();
            DebugName = debugName;
        }

        public void Start(AgentTaskContext context)
        {
            // No-op; we evaluate in Tick so it's consistent with "runtime" branching.
        }

        public TaskTickResult Tick(AgentTaskContext context, float deltaTimeSeconds)
        {
            if (evaluated)
                return TaskTickResult.Succeeded();

            evaluated = true;

            bool takeThen;
            try
            {
                takeThen = condition(context);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{DebugName}] Condition threw: {exception.Message}");
                return TaskTickResult.Failed("Branch condition exception");
            }

            var tasksToEnqueue = takeThen ? thenTasks : elseTasks;

            // Enqueue tasks in order.
            for (int i = 0; i < tasksToEnqueue.Length; i++)
            {
                if (tasksToEnqueue[i] == null)
                {
                    Debug.LogWarning($"[{DebugName}] Null task in branch list at index {i}");
                    continue;
                }
                queue.Enqueue(tasksToEnqueue[i]);
            }

            return TaskTickResult.Succeeded();
        }

        public void Stop(AgentTaskContext context)
        {
            // No-op. We don't own any running subtask.
        }
    }
}