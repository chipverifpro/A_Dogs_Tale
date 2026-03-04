public sealed class LLMBuildContext
{
    public string agentId;
    public string requestId;
    public DogGame.LLM.Core.LLMProfile profile;
    public DogGame.LLM.Core.Sophistication sophistication;

    public DogGame.LLM.Personality.MixedPersonality mixedPersonality;

    // dynamic signals
    public DogGame.LLM.Policy.SophisticationPolicy.Inputs sophisticationInputs;

    // optional: add pointers to your WorldObject/Agent here if helpful
    public UnityEngine.GameObject agentGameObject;
}