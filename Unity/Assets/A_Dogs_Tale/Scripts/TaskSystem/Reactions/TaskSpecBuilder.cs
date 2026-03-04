#nullable enable
using System;
using System.Collections.Generic;

namespace DogGame.Reactions
{
    public sealed class TaskSpecBuilder
    {
        private readonly string name;
        private readonly Dictionary<string, object> args = new();

        private TaskSpecBuilder(string name)
        {
            this.name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public static TaskSpecBuilder Task(string name) => new TaskSpecBuilder(name.Trim());

        public TaskSpecBuilder Arg(string key, int value) { args[key] = value; return this; }
        public TaskSpecBuilder Arg(string key, float value) { args[key] = value; return this; }
        public TaskSpecBuilder Arg(string key, bool value) { args[key] = value; return this; }
        public TaskSpecBuilder Arg(string key, string value) { args[key] = value; return this; }

        public TaskSpec Build() => new TaskSpec(name, args);
    }
}