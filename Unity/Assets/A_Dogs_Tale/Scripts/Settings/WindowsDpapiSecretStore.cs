#nullable enable
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
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
            File.WriteAllBytes(GetSecretPath(key), Protect(plainBytes));
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

            byte[] plainBytes = Unprotect(encryptedBytes);
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

        private static byte[] Protect(byte[] plainBytes)
        {
            return WithBlob(plainBytes, plainBlob =>
                WithBlob(Entropy, entropyBlob =>
                {
                    if (!CryptProtectData(
                            ref plainBlob,
                            null,
                            ref entropyBlob,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            0,
                            out DataBlob encryptedBlob))
                    {
                        throw CreateDpapiException("encrypt");
                    }

                    return CopyAndFreeBlob(encryptedBlob);
                }));
        }

        private static byte[] Unprotect(byte[] encryptedBytes)
        {
            return WithBlob(encryptedBytes, encryptedBlob =>
                WithBlob(Entropy, entropyBlob =>
                {
                    if (!CryptUnprotectData(
                            ref encryptedBlob,
                            null,
                            ref entropyBlob,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            0,
                            out DataBlob plainBlob))
                    {
                        throw CreateDpapiException("decrypt");
                    }

                    return CopyAndFreeBlob(plainBlob);
                }));
        }

        private static byte[] WithBlob(byte[] bytes, Func<DataBlob, byte[]> action)
        {
            if (bytes == null)
                bytes = Array.Empty<byte>();

            IntPtr dataPtr = IntPtr.Zero;
            try
            {
                if (bytes.Length > 0)
                {
                    dataPtr = Marshal.AllocHGlobal(bytes.Length);
                    Marshal.Copy(bytes, 0, dataPtr, bytes.Length);
                }

                return action(new DataBlob
                {
                    DataSize = bytes.Length,
                    DataPointer = dataPtr
                });
            }
            finally
            {
                if (dataPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(dataPtr);
            }
        }

        private static byte[] CopyAndFreeBlob(DataBlob blob)
        {
            try
            {
                if (blob.DataPointer == IntPtr.Zero || blob.DataSize <= 0)
                    return Array.Empty<byte>();

                byte[] bytes = new byte[blob.DataSize];
                Marshal.Copy(blob.DataPointer, bytes, 0, bytes.Length);
                return bytes;
            }
            finally
            {
                if (blob.DataPointer != IntPtr.Zero)
                    LocalFree(blob.DataPointer);
            }
        }

        private static Exception CreateDpapiException(string operation)
        {
            return new InvalidOperationException(
                $"Windows DPAPI failed to {operation} secret data.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int DataSize;
            public IntPtr DataPointer;
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string? dataDescription,
            ref DataBlob optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            out DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            string? dataDescription,
            ref DataBlob optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);
    }
}
#endif
