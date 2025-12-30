#nullable enable
using System.Collections.Generic;

namespace DogGame.AI.Perception
{
    public sealed class ScentMemory
    {
        private struct Entry
        {
            public float lastStrength01;
            public float lastTime;
        }

        private readonly Dictionary<string, Entry> memory = new();

        public bool TryGet(string scentKey, out float lastStrength01, out float lastTime)
        {
            if (memory.TryGetValue(scentKey, out var entry))
            {
                lastStrength01 = entry.lastStrength01;
                lastTime = entry.lastTime;
                return true;
            }

            lastStrength01 = 0f;
            lastTime = 0f;
            return false;
        }

        public void Update(string scentKey, float strength01, float timeNow)
        {
            memory[scentKey] = new Entry { lastStrength01 = strength01, lastTime = timeNow };
        }
    }
}