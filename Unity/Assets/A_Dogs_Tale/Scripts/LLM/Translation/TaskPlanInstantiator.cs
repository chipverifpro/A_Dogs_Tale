#nullable enable
using System;
using System.Collections.Generic;

namespace DogGame.LLM.Translation
{
    public sealed class TaskPlanInstantiator
    {
        private readonly ITaskFactory factory;

        public TaskPlanInstantiator(ITaskFactory factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Creates a root task for execution. Typically you will execute the returned task in your TaskSystem.
        /// </summary>
        public object InstantiateAsSequence(TaskPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            // Always wrap in a Task_Sequence to keep executor simple.
            object rootSequence = factory.CreateTask("Task_Sequence", new Dictionary<string, object?>());

            foreach (var node in plan.rootNodes)
            {
                object child = InstantiateNode(node);
                factory.AddChild(rootSequence, child);
            }

            return rootSequence;
        }

        private object InstantiateNode(TaskNode node)
        {
            object task = factory.CreateTask(node.taskTypeName, node.parameters);

            for (int i = 0; i < node.children.Count; i++)
            {
                object child = InstantiateNode(node.children[i]);
                factory.AddChild(task, child);
            }

            return task;
        }
    }
}