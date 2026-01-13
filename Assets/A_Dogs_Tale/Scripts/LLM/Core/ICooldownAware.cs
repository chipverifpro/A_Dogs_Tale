namespace DogGame.LLM.Core
{
    public interface ICooldownAware
    {
        bool IsCoolingDown { get; }
        float CooldownRemainingSeconds { get; }
    }
}