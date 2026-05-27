#nullable enable
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DogGame.Settings
{
    public sealed class MacOSKeychainSecretStore : ISecretStore
    {
        private const string ServiceName = "A Dogs Tale";
        private const int ErrSecSuccess = 0;
        private const int ErrSecItemNotFound = -25300;
        private const int ErrSecDuplicateItem = -25299;

        public bool IsAvailable => true;
        public string BackendName => "macOS Keychain";

        public void SetSecret(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Secret key name is empty.", nameof(key));

            byte[] serviceBytes = Encoding.UTF8.GetBytes(ServiceName);
            byte[] accountBytes = Encoding.UTF8.GetBytes(key.Trim());
            byte[] passwordBytes = Encoding.UTF8.GetBytes(value ?? "");

            int status = SecKeychainAddGenericPassword(
                IntPtr.Zero,
                (uint)serviceBytes.Length,
                serviceBytes,
                (uint)accountBytes.Length,
                accountBytes,
                (uint)passwordBytes.Length,
                passwordBytes,
                out IntPtr itemRef);

            if (itemRef != IntPtr.Zero)
                CFRelease(itemRef);

            if (status == ErrSecDuplicateItem)
            {
                UpdateExistingSecret(serviceBytes, accountBytes, passwordBytes, key);
                return;
            }

            ThrowIfError(status, $"write {key}");
        }

        public bool TryGetSecret(string key, out string value)
        {
            value = "";
            if (string.IsNullOrWhiteSpace(key))
                return false;

            byte[] serviceBytes = Encoding.UTF8.GetBytes(ServiceName);
            byte[] accountBytes = Encoding.UTF8.GetBytes(key.Trim());

            int status = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                (uint)serviceBytes.Length,
                serviceBytes,
                (uint)accountBytes.Length,
                accountBytes,
                out uint passwordLength,
                out IntPtr passwordData,
                out IntPtr itemRef);

            if (itemRef != IntPtr.Zero)
                CFRelease(itemRef);

            if (status == ErrSecItemNotFound)
                return false;

            ThrowIfError(status, $"read {key}");

            try
            {
                if (passwordData == IntPtr.Zero || passwordLength == 0)
                    return false;

                byte[] bytes = new byte[passwordLength];
                Marshal.Copy(passwordData, bytes, 0, bytes.Length);
                value = Encoding.UTF8.GetString(bytes);
                return !string.IsNullOrWhiteSpace(value);
            }
            finally
            {
                if (passwordData != IntPtr.Zero)
                    SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            }
        }

        public void DeleteSecret(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            byte[] serviceBytes = Encoding.UTF8.GetBytes(ServiceName);
            byte[] accountBytes = Encoding.UTF8.GetBytes(key.Trim());

            int status = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                (uint)serviceBytes.Length,
                serviceBytes,
                (uint)accountBytes.Length,
                accountBytes,
                out uint _,
                out IntPtr passwordData,
                out IntPtr itemRef);

            if (passwordData != IntPtr.Zero)
                SecKeychainItemFreeContent(IntPtr.Zero, passwordData);

            if (status == ErrSecItemNotFound)
                return;

            ThrowIfError(status, $"find {key}");

            try
            {
                ThrowIfError(SecKeychainItemDelete(itemRef), $"delete {key}");
            }
            finally
            {
                if (itemRef != IntPtr.Zero)
                    CFRelease(itemRef);
            }
        }

        private static void UpdateExistingSecret(byte[] serviceBytes, byte[] accountBytes, byte[] passwordBytes, string key)
        {
            int status = SecKeychainFindGenericPassword(
                IntPtr.Zero,
                (uint)serviceBytes.Length,
                serviceBytes,
                (uint)accountBytes.Length,
                accountBytes,
                out uint _,
                out IntPtr passwordData,
                out IntPtr itemRef);

            if (passwordData != IntPtr.Zero)
                SecKeychainItemFreeContent(IntPtr.Zero, passwordData);

            ThrowIfError(status, $"find {key}");

            try
            {
                status = SecKeychainItemModifyAttributesAndData(
                    itemRef,
                    IntPtr.Zero,
                    (uint)passwordBytes.Length,
                    passwordBytes);
                ThrowIfError(status, $"update {key}");
            }
            finally
            {
                if (itemRef != IntPtr.Zero)
                    CFRelease(itemRef);
            }
        }

        private static void ThrowIfError(int status, string operation)
        {
            if (status == ErrSecSuccess)
                return;

            throw new InvalidOperationException($"macOS Keychain failed to {operation}. OSStatus={status}.");
        }

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainAddGenericPassword(
            IntPtr keychain,
            uint serviceNameLength,
            byte[] serviceName,
            uint accountNameLength,
            byte[] accountName,
            uint passwordLength,
            byte[] passwordData,
            out IntPtr itemRef);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainFindGenericPassword(
            IntPtr keychainOrArray,
            uint serviceNameLength,
            byte[] serviceName,
            uint accountNameLength,
            byte[] accountName,
            out uint passwordLength,
            out IntPtr passwordData,
            out IntPtr itemRef);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainItemModifyAttributesAndData(
            IntPtr itemRef,
            IntPtr attrList,
            uint length,
            byte[] data);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainItemDelete(IntPtr itemRef);

        [DllImport("/System/Library/Frameworks/Security.framework/Security")]
        private static extern int SecKeychainItemFreeContent(IntPtr attrList, IntPtr data);

        [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
        private static extern void CFRelease(IntPtr cf);
    }
}
#endif
