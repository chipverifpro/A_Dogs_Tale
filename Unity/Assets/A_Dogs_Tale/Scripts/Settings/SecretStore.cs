#nullable enable
using System;
using UnityEngine;

namespace DogGame.Settings
{
    public static class SecretStore
    {
        public const string OpenAIApiKey = "OPENAI_API_KEY";
        public const string GeminiApiKey = "GEMINI_API_KEY";
        public const string MistralApiKey = "MISTRAL_API_KEY";

        private static ISecretStore? current;

        public static ISecretStore Current => current ??= CreateDefaultStore();

        public static bool TryGetSecret(string key, out string value)
        {
            value = "";

            try
            {
                return Current.IsAvailable && Current.TryGetSecret(key, out value);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SecretStore] Failed to read '{key}' from {Current.BackendName}: {ex.Message}");
                return false;
            }
        }

        public static bool HasSecret(string key)
        {
            return TryGetSecret(key, out string value) && !string.IsNullOrWhiteSpace(value);
        }

        public static bool TrySetSecret(string key, string value, out string error)
        {
            error = "";

            if (string.IsNullOrWhiteSpace(key))
            {
                error = "Secret key name is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                TryDeleteSecret(key, out error);
                return string.IsNullOrWhiteSpace(error);
            }

            try
            {
                if (!Current.IsAvailable)
                {
                    error = $"{Current.BackendName} is not available on this platform.";
                    return false;
                }

                Current.SetSecret(key, value.Trim());
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogWarning($"[SecretStore] Failed to write '{key}' to {Current.BackendName}: {ex.Message}");
                return false;
            }
        }

        public static bool TryDeleteSecret(string key, out string error)
        {
            error = "";

            try
            {
                if (!Current.IsAvailable)
                {
                    error = $"{Current.BackendName} is not available on this platform.";
                    return false;
                }

                Current.DeleteSecret(key);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogWarning($"[SecretStore] Failed to delete '{key}' from {Current.BackendName}: {ex.Message}");
                return false;
            }
        }

        private static ISecretStore CreateDefaultStore()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            return new MacOSKeychainSecretStore();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return new WindowsDpapiSecretStore();
#else
            return new UnsupportedSecretStore();
#endif
        }
    }
}
