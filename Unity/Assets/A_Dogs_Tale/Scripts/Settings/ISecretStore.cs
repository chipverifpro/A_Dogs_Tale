#nullable enable
namespace DogGame.Settings
{
    public interface ISecretStore
    {
        bool IsAvailable { get; }
        string BackendName { get; }
        void SetSecret(string key, string value);
        bool TryGetSecret(string key, out string value);
        void DeleteSecret(string key);
    }
}
