using System;

namespace DogGame.LLM.Core
{
    [Serializable]
    public sealed class LLMProfile
    {
        public string vendor;              // "Gemini", "OpenAI", ...
        public string model;               // provider model id
        public Sophistication level;

        public int maxOutputTokens = 512;
        public float temperature = 0.7f;

        public bool allowTools = false;

        // 0..3 how much context you include (nearby-only vs full)
        public int contextDetail = 1;

        // 0..2: none, light, deep (your policy; does not require hidden chain-of-thought)
        public int planningDepth = 1;

        // Optional throttle/caching knobs
        public float minSecondsBetweenCalls = 0.75f;

        public override string ToString()
        {
            return $"{vendor}/{model} ({level}) tokens={maxOutputTokens} temp={temperature} tools={allowTools}";
        }
    }
}