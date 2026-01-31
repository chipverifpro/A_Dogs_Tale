#nullable enable
using System.Collections.Generic;

namespace DogGame.AI.Perception
{
    public sealed class ScentMemory
    {
        private struct Entry
        {
            // Existing fields
            public float lastStrength01;
            public float lastTime;

            // New fields
            public float bestStrength01;

            public ScentFamiliarity familiarity;
            public string? displayName;     // learned identity ("Hot Dog Thief", "Mark", etc.)
            public int agentId;             // -1 if non-agent / unknown
        }

        private readonly Dictionary<string, Entry> memory = new();

        // -------------------------
        // Existing API (unchanged)
        // -------------------------
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
            if (!memory.TryGetValue(scentKey, out var entry))
            {
                entry = new Entry
                {
                    bestStrength01 = strength01,
                    familiarity = ScentFamiliarity.New,
                    displayName = null,
                    agentId = -1
                };
            }

            entry.lastStrength01 = strength01;
            entry.lastTime = timeNow;
            if (strength01 > entry.bestStrength01)
                entry.bestStrength01 = strength01;

            memory[scentKey] = entry;
        }

        // -------------------------
        // New API (knowledge)
        // -------------------------
        public bool TryGetInfo(
            string scentKey,
            out float lastStrength01,
            out float lastTime,
            out float bestStrength01,
            out ScentFamiliarity familiarity,
            out string? displayName,
            out int agentId)
        {
            if (memory.TryGetValue(scentKey, out var entry))
            {
                lastStrength01 = entry.lastStrength01;
                lastTime = entry.lastTime;
                bestStrength01 = entry.bestStrength01;
                familiarity = entry.familiarity;
                displayName = entry.displayName;
                agentId = entry.agentId;
                return true;
            }

            lastStrength01 = 0f;
            lastTime = 0f;
            bestStrength01 = 0f;
            familiarity = ScentFamiliarity.New;
            displayName = null;
            agentId = -1;
            return false;
        }

        public void EnsureKnown(string scentKey)
        {
            if (memory.ContainsKey(scentKey))
                return;

            memory[scentKey] = new Entry
            {
                lastStrength01 = 0f,
                lastTime = 0f,
                bestStrength01 = 0f,
                familiarity = ScentFamiliarity.New,
                displayName = null,
                agentId = -1
            };
        }

        public void PromoteFamiliarity(string scentKey, ScentFamiliarity atLeast)
        {
            if (!memory.TryGetValue(scentKey, out var entry))
            {
                entry = new Entry
                {
                    lastStrength01 = 0f,
                    lastTime = 0f,
                    bestStrength01 = 0f,
                    familiarity = ScentFamiliarity.New,
                    displayName = null,
                    agentId = -1
                };
            }

            if ((int)entry.familiarity < (int)atLeast)
                entry.familiarity = atLeast;

            memory[scentKey] = entry;
        }

        public void Identify(string scentKey, string displayName, int agentId = -1)
        {
            if (!memory.TryGetValue(scentKey, out var entry))
            {
                entry = new Entry
                {
                    lastStrength01 = 0f,
                    lastTime = 0f,
                    bestStrength01 = 0f,
                    familiarity = ScentFamiliarity.New,
                    displayName = null,
                    agentId = -1
                };
            }

            entry.displayName = displayName;
            entry.agentId = agentId;

            // “Identified” is stronger than “Scented”
            if ((int)entry.familiarity < (int)ScentFamiliarity.Identified)
                entry.familiarity = ScentFamiliarity.Identified;

            memory[scentKey] = entry;
        }

        // Helpful for debugging / UI
        public IEnumerable<KeyValuePair<string, (float last, float best, float time, ScentFamiliarity fam, string? name, int agentId)>> Enumerate()
        {
            foreach (var kvp in memory)
            {
                var e = kvp.Value;
                yield return new KeyValuePair<string, (float, float, float, ScentFamiliarity, string?, int)>(
                    kvp.Key,
                    (e.lastStrength01, e.bestStrength01, e.lastTime, e.familiarity, e.displayName, e.agentId)
                );
            }
        }
    }
}