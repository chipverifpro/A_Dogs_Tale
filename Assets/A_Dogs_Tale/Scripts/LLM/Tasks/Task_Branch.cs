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

        private readonly int branchPriority;
        private readonly TaskSource branchSource;
        private readonly string? branchTag;

        private bool evaluated;

        public Task_Branch(
            AgentTaskQueue queue,
            Func<AgentTaskContext, bool> condition,
            IAgentTask[] thenTasks,
            IAgentTask[] elseTasks,
            string debugName = "Branch",
            int branchPriority = 60,
            TaskSource branchSource = TaskSource.AI,
            string? branchTag = "branch")
        {
            this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
            this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.thenTasks = thenTasks ?? Array.Empty<IAgentTask>();
            this.elseTasks = elseTasks ?? Array.Empty<IAgentTask>();

            DebugName = debugName;

            this.branchPriority = Mathf.Clamp(branchPriority, 0, 100);
            this.branchSource = branchSource;
            this.branchTag = branchTag;
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

            // Enqueue tasks in order with ONE shared priority so they maintain ordering as a group.
            for (int i = 0; i < tasksToEnqueue.Length; i++)
            {
                var t = tasksToEnqueue[i];
                if (t == null)
                {
                    Debug.LogWarning($"[{DebugName}] Null task in branch list at index {i}");
                    continue;
                }

                queue.Enqueue(new TaskRequest(
                    task: t,
                    priority: branchPriority,
                    source: branchSource,
                    canInterrupt: false,          // spawned steps should not preempt
                    resumePrevious: false,
                    clearStackOnStart: false,
                    tag: branchTag
                ));
            }

            return TaskTickResult.Succeeded();
        }

        public void Stop(AgentTaskContext context)
        {
            // No-op. We don't own any running subtask.
        }
    }
}