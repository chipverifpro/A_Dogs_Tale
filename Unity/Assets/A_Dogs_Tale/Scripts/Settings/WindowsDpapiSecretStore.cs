#nullable enable
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DogGame.Settings
{
    public sealed class WindowsDpapiSecretStore : ISecretStore
    {
        private const string StoreDirectoryName = "DogsTaleSaves";
        private const string SecretDirectoryName = "Secrets";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("A Dogs Tale API Keys v1");

        public bool IsAvailable => true;
        public string BackendName => "Windows DPAPI";

        public void SetSecret(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Secret key name is empty.", nameof(key));

            Directory.CreateDirectory(GetSecretsDirectory());

            byte[] plainBytes = Encoding.UTF8.GetBytes(value ?? "");
            byte[] encryptedBytes = ProtectedData.Protect(
                plainBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            File.WriteAllBytes(GetSecretPath(key), encryptedBytes);
        }

        public bool TryGetSecret(string key, out string value)
        {
            value = "";
            if (string.IsNullOrWhiteSpace(key))
                return false;

            string path = GetSecretPath(key);
            if (!File.Exists(path))
                return false;

            byte[] encryptedBytes = File.ReadAllBytes(path);
            if (encryptedBytes.Length == 0)
                return false;

            byte[] plainBytes = ProtectedData.Unprotect(
                encryptedBytes,
                Entropy,
                DataProtectionScope.CurrentUser);

            value = Encoding.UTF8.GetString(plainBytes);
            return !string.IsNullOrWhiteSpace(value);
        }

        public void DeleteSecret(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            string path = GetSecretPath(key);
            if (File.Exists(path))
                File.Delete(path);
        }

        private static string GetSecretsDirectory()
        {
            string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userHome, StoreDirectoryName, SecretDirectoryName);
        }

        private static string GetSecretPath(string key)
        {
            return Path.Combine(GetSecretsDirectory(), Sanitize(key) + ".bin");
        }

        private static string Sanitize(string value)
        {
            foreach (char invalidChar in Path.GetInvalidFileNameChars())
                value = value.Replace(invalidChar, '_');

            return value.Trim();
        }
    }
}
#endif
