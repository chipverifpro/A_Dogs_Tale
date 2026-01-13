using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DogGame.LLM.Core;
using DogGame.LLM.Policy;
using DogGame.LLM.Personality;
using DogGame.LLM.Prompting;
using DogGame.LLM.Providers;

public sealed class AgentBrain
{
    private readonly SophisticationPolicy sophisticationPolicy = new();
    private readonly PromptComposer promptComposer = new();
    private readonly PersonalityMixer personalityMixer;
    private readonly LLMRouter router;

    public AgentBrain(PersonalityDatabase personalityDatabase, LLMRouter router)
    {
        this.personalityMixer = new PersonalityMixer(personalityDatabase);
        
        string openAIApiKey = OpenAIConfig.GetApiKey(inspectorValue: /* from your bootstrap MonoBehaviour */ "");
        var openAIClient = new OpenAIClient(openAIApiKey);
        
        string geminiApiKey = GeminiConfig.GetApiKey(inspectorValue: /* from your bootstrap MonoBehaviour */ "");
        var geminiClient = new GeminiClient(geminiApiKey);
        
        //var fakeLLMClient = new FakeLLMClient();

        this.router = new LLMRouter(new ILLMClient[]
        {
            openAIClient,
            geminiClient,
            //fakeClient,
        });
    }

    public async Task<LLMResponse> ThinkAsync(
        string npcId,
        SophisticationPolicy.Inputs inputs,
        string userPrompt,
        List<string> contextBlocks,
        CancellationToken cancellationToken)
    {
        Sophistication desired = sophisticationPolicy.Evaluate(inputs);
        desired = sophisticationPolicy.ClampByNpcType(desired, isSimpleCreature: false);

        LLMProfile profile = SelectProfile(desired);

        MixedPersonality personality = personalityMixer.Build(
            stableSeedString: npcId,
            manualArchetypeOverride: null,
            manualQuirkOverrides: null,
            manualComplicationOverride: null,
            randomQuirkCount: desired == Sophistication.High ? 3 : 2);

        var request = promptComposer.Compose(
            requestId: $"{npcId}:{System.DateTime.UtcNow.Ticks}",
            profile: profile,
            userPrompt: userPrompt,
            personality: personality,
            contextBlocks: contextBlocks,
            toolDefinitionsJson: null,
            responseSchemaJson: null,
            metadata: new Dictionary<string, string>
            {
                { "npcId", npcId },
                { "sophistication", desired.ToString() }
            });

        return await router.SendAsync(request, cancellationToken);
    }

    private static LLMProfile SelectProfile(Sophistication level)
    {
        // Replace with your actual model mapping.
        // You can also pick vendor by platform/build (Android vs PC).
        switch (level)
        {
            case Sophistication.Low:
                return new LLMProfile
                {
                    vendor = "Gemini",
                    model = "cheap-fast-model",
                    level = level,
                    maxOutputTokens = 200,
                    temperature = 0.6f,
                    allowTools = false,
                    contextDetail = 1,
                    planningDepth = 0,
                    minSecondsBetweenCalls = 1.0f
                };

            case Sophistication.Medium:
                return new LLMProfile
                {
                    vendor = "Gemini",
                    model = "balanced-model",
                    level = level,
                    maxOutputTokens = 500,
                    temperature = 0.7f,
                    allowTools = true,
                    contextDetail = 2,
                    planningDepth = 1,
                    minSecondsBetweenCalls = 0.75f
                };

            default:
                return new LLMProfile
                {
                    vendor = "Gemini",
                    model = "best-reasoning-model",
                    level = level,
                    maxOutputTokens = 1000,
                    temperature = 0.7f,
                    allowTools = true,
                    contextDetail = 3,
                    planningDepth = 2,
                    minSecondsBetweenCalls = 0.5f
                };
        }
    }
}