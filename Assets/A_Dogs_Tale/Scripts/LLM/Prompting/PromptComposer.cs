using System.Collections.Generic;
using DogGame.LLM.Core;
using DogGame.LLM.Personality;

namespace DogGame.LLM.Prompting
{
    public sealed class PromptComposer
    {
        public LLMRequest Compose(
            string requestId,
            LLMProfile profile,
            string userPrompt,
            MixedPersonality personality,
            List<string> contextBlocks,
            string toolDefinitionsJson,
            string responseSchemaJson,
            Dictionary<string, string> metadata = null)
        {
            var request = new LLMRequest
            {
                requestId = requestId,
                profile = profile,
                userPrompt = userPrompt,
                toolDefinitionsJson = toolDefinitionsJson,
                responseSchemaJson = responseSchemaJson
            };

            request.systemBlocks.Add(PromptBlocks.GlobalRulesBlock());
            request.systemBlocks.Add(PromptBlocks.PlanningGuidanceBlock(profile.planningDepth));

            if (personality != null && !string.IsNullOrWhiteSpace(personality.personaBlock))
                request.systemBlocks.Add(personality.personaBlock);

            if (contextBlocks != null)
                request.systemBlocks.AddRange(contextBlocks);

            if (metadata != null)
            {
                foreach (var pair in metadata)
                    request.metadata[pair.Key] = pair.Value;
            }

            return request;
        }
    }
}