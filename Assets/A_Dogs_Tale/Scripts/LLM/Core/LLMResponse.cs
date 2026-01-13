using System;
using System.Collections.Generic;

namespace DogGame.LLM.Core
{
    [Serializable]
    public sealed class LLMResponse
    {
        public string rawText;

        // Parsed tool calls, if any (keep generic; provider adapters populate)
        public List<ToolCall> toolCalls = new();

        // Optional raw JSON payload from provider for debugging
        public string rawProviderPayloadJson;

        public bool succeeded = true;
        public string errorMessage;

        [Serializable]
        public sealed class ToolCall
        {
            public string name;
            public string argumentsJson;
        }
    }
}