using System.Collections.Generic;
using DogGame.LLM.Core;
using DogGame.LLM.Personality;
using DogGame.LLM.Tools;
using Newtonsoft.Json.Linq;

namespace DogGame.LLM.Prompting
{
    public sealed class PromptComposer
    {
        /*
        public LLMRequest Compose(
            string requestId,
            LLMProfile profile,
            string userPrompt,
            MixedPersonality personality,
            List<string> contextBlocks,
            JObject toolDefinitions,
            JObject responseSchema,
            Dictionary<string, string> metadata = null)
        {
            var request = new LLMRequest
            {
                requestId = requestId,
                profile = profile,
                userPrompt = userPrompt,
                toolDefinitions = toolDefinitions,
                responseSchema = responseSchema
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

            //In earlier code agentId was stored as request.metadata["agentId"]
            string agentIdValue =
                request.metadata != null && request.metadata.TryGetValue("agentId", out var id) ? id : "";
            PromptBlocks.IdentityEchoBlock(requestId, agentIdValue);

            request.systemBlocks.Add(PromptBlocks.IdentityEchoBlock(requestId, agentIdValue));
            request.systemBlocks.Add(ToolCatalog.PlanIntentionTypeListText);
            request.systemBlocks.Add(ToolCatalog.AvailableIntentionsText);
            
            request.systemBlocks.Add(PromptBlocks.ValidationAwareRulesBlock());
            request.systemBlocks.Add(PromptBlocks.GoldenExamplePlanResponseV1());
            request.systemBlocks.Add(PromptBlocks.OutputOnlyJsonBlock(ResponseSchemas.PlanResponseV1Name, ResponseSchemas.PlanResponseV1ContractText));

            return request;
        }
        */
    }
}
