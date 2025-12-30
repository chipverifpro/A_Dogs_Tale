namespace DogGame.Input
{
    public interface IPlayerInputSource
    {
        PlayerInputState CurrentState { get; }
    }
}