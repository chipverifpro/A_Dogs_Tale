#nullable enable
namespace DogGame.Tasks
{
    public interface IBlackboard
    {
        void SetBool(string key, bool value);
        bool TryGetBool(string key, out bool value);

        void SetInt(string key, int value);
        bool TryGetInt(string key, out int value);

        void SetFloat(string key, float value);
        bool TryGetFloat(string key, out float value);

        void SetString(string key, string value);
        bool TryGetString(string key, out string value);
    }
}