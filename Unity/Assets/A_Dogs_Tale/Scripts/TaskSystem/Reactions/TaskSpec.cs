#nullable enable
using System;
using System.Collections.Generic;

namespace DogGame.Reactions
{
    /// <summary>
    /// A small, LLM-friendly description of a task: name + parameters.
    /// Mapped to concrete IAgentTask via TaskSpecFactory.
    /// </summary>
    public readonly struct TaskSpec
    {
        public readonly string Name;
        public readonly IReadOnlyDictionary<string, object> Args;

        public TaskSpec(string name, IReadOnlyDictionary<string, object>? args = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Args = args ?? new Dictionary<string, object>();
        }

        public override string ToString() => Name;
    }
}