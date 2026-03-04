#nullable enable
using System;
using System.Collections.Generic;

namespace DogGame.LLM.Translation
{
    /// <summary>
    /// A serializable, engine-agnostic plan that can be turned into concrete TaskSystem tasks.
    /// Think of this as "bytecode" between the LLM and your TaskSystem.
    /// </summary>
    [Serializable]
    public sealed class TaskPlan
    {
        public string requestId = "";
        public string agentId = "";
        public List<TaskNode> rootNodes = new();
    }

    [Serializable]
    public sealed class TaskNode
    {
        /// <summary>
        /// A symbolic name for the task to create, e.g. "Task_Wait" or "Task_MoveToObject".
        /// </summary>
        public string taskTypeName = "";

        /// <summary>
        /// Arbitrary parameters, kept JSON-ish. Values should be primitives, lists, or dictionaries.
        /// </summary>
        public Dictionary<string, object?> parameters = new();

        /// <summary>
        /// Optional child nodes for composite tasks like Sequence/Branch/Try.
        /// </summary>
        public List<TaskNode> children = new();

        public override string ToString() => $"{taskTypeName} (params={parameters.Count}, children={children.Count})";
    }
}