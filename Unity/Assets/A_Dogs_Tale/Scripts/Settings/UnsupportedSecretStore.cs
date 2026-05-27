#nullable enable
namespace DogGame.Settings
{
    public sealed class UnsupportedSecretStore : ISecretStore
    {
        public bool IsAvailable => false;
        public string BackendName => "No secure secret store";

        public void SetSecret(string key, string value)
        {
        }

        public bool TryGetSecret(string key, out string value)
        {
            value = "";
            return false;
        }

        public void DeleteSecret(string key)
        {
        }
    }
}
