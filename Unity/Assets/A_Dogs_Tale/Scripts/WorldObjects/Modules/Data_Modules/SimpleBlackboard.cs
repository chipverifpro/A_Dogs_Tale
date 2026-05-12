#nullable enable
using System;
using System.Collections.Generic;
using InspectorTools;

namespace DogGame.Tasks
{
    [InspectorNote("Data_Modules/Simple Blackboard Module", "BASE MODULE ONLY.  DO NOT INSTANTIATE! Instantiated by Blackboard Module.", InspectorNoteMessageType.Error)]
    public sealed class SimpleBlackboard : IBlackboard
    {
        private readonly Dictionary<string, object> data = new(StringComparer.Ordinal);

        public bool HasKey(string key) => data.ContainsKey(key);
        public bool Remove(string key) => data.Remove(key);
        public void Clear() => data.Clear();

        public void SetBool(string key, bool value) => data[key] = value;
        public bool TryGetBool(string key, out bool value) => TryGet(key, out value);

        public void SetInt(string key, int value) => data[key] = value;
        public bool TryGetInt(string key, out int value) => TryGet(key, out value);

        public void SetFloat(string key, float value) => data[key] = value;
        public bool TryGetFloat(string key, out float value) => TryGet(key, out value);

        public void SetString(string key, string value) => data[key] = value;
        public bool TryGetString(string key, out string value) => TryGet(key, out value);

        private bool TryGet<T>(string key, out T value)
        {
            if (data.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }

            value = default!;
            return false;
        }
    }
}
