﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using MessagePack;
using System.Security.Cryptography;
using System.Text;
using AICodeAnalyzer.Services;

namespace AICodeAnalyzer;

/// <summary>
/// Manages API keys, including encryption and storage.
/// Note: Keys are encrypted using ProtectedData, which is machine-bound and tied to the current user.
/// This means keys may not be accessible on other machines or user accounts without explicit handling.
/// </summary>
public class ApiKeyManager
{
    private const string KeysFileName = "keys.dat";
    private readonly string _keysFilePath;

    private ApiKeyStorage KeyStorage { get; set; }

    public ApiKeyManager()
    {
        _keysFilePath = Path.Combine(AppContext.BaseDirectory, KeysFileName);
        KeyStorage = LoadKeys();
    }

    public void SaveKey(string provider, string key)
    {
        // Get or create the provider entry
        var providerEntry = KeyStorage.Providers.Find(p => p.Name == provider);
        if (providerEntry == null)
        {
            providerEntry = new ApiProvider { Name = provider };
            KeyStorage.Providers.Add(providerEntry);
        }

        // Check if this key already exists
        if (!providerEntry.Keys.Contains(key))
            providerEntry.Keys.Add(key);

        SaveKeys();
    }

    public List<string> GetKeysForProvider(string provider)
    {
        // KeyStorage is guaranteed to be non-null
        var providerEntry = KeyStorage.Providers.Find(p => p.Name == provider);
        return providerEntry?.Keys ?? new List<string>();
    }

    public bool RemoveKey(string provider, string key)
    {
        var providerEntry = KeyStorage.Providers.Find(p => p.Name == provider);
        if (providerEntry == null)
            return false;

        var removed = providerEntry.Keys.Remove(key);
        if (removed)
            SaveKeys();

        return removed;
    }

    private ApiKeyStorage LoadKeys()
    {
        if (!File.Exists(_keysFilePath))
            return new ApiKeyStorage();

        try
        {
            var bytes = File.ReadAllBytes(_keysFilePath);
            var storage = MessagePackSerializer.Deserialize<ApiKeyStorage>(bytes);

            var keysChanged = false;

            foreach (var provider in storage.Providers)
            {
                var decryptedKeys = new List<string>();
                var keysToRemove = new List<string>();

                foreach (var key in provider.Keys)
                {
                    // Attempt to decrypt the key
                    var decryptedKey = DecryptKey(key);

                    if (string.IsNullOrEmpty(decryptedKey))
                    {
                        // Could NOT decrypt -> probably unencrypted key or corrupted, so discard it
                        keysChanged = true;
                        keysToRemove.Add(key);
                        Logger.LogError(new Exception("Decryption failed for key"), "Key decryption failed in LoadKeys");
                        AlertUserOnDecryptionFailure(); // Alert the user about the failure
                    }
                    else
                    {
                        decryptedKeys.Add(decryptedKey);
                    }
                }

                // Remove unencrypted keys from the provider
                foreach (var badKey in keysToRemove)
                {
                    provider.Keys.Remove(badKey);
                }

                // Replace it with decrypted keys (only valid encrypted keys)
                provider.Keys = decryptedKeys;
            }

            if (keysChanged)
            {
                // Save cleaned-up keys back to disk
                SaveKeysEncrypted(storage);
            }

            return storage;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading API keys");
            return new ApiKeyStorage();
        }
    }

    private void SaveKeysEncrypted(ApiKeyStorage storage)
    {
        try
        {
            var encryptedStorage = new ApiKeyStorage();

            foreach (var provider in storage.Providers)
            {
                var encryptedProvider = new ApiProvider
                {
                    Name = provider.Name
                };

                foreach (var key in provider.Keys)
                {
                    var encryptedKey = EncryptKey(key);
                    if (!string.IsNullOrEmpty(encryptedKey))
                    {
                        encryptedProvider.Keys.Add(encryptedKey);
                    }
                }

                encryptedStorage.Providers.Add(encryptedProvider);
            }

            var bytes = MessagePackSerializer.Serialize(encryptedStorage);
            File.WriteAllBytes(_keysFilePath, bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving encrypted API keys");
            MessageBox.Show("Failed to securely save API keys. See error log for details.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveKeys()
    {
        try
        {
            // Encrypt keys before serialization
            var encryptedStorage = new ApiKeyStorage();

            foreach (var provider in KeyStorage.Providers)
            {
                var encryptedProvider = new ApiProvider
                {
                    Name = provider.Name
                };

                foreach (var key in provider.Keys)
                {
                    var encryptedKey = EncryptKey(key);
                    if (!string.IsNullOrEmpty(encryptedKey))
                    {
                        encryptedProvider.Keys.Add(encryptedKey);
                    }
                }

                encryptedStorage.Providers.Add(encryptedProvider);
            }

            var bytes = MessagePackSerializer.Serialize(encryptedStorage);
            File.WriteAllBytes(_keysFilePath, bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving encrypted API keys");
            MessageBox.Show("Failed to save API keys securely. See error log for details.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Encrypt a plaintext key as base64
    /// <summary>
    /// Encrypts the provided plaintext key using ProtectedData.
    /// Note: This encryption is machine-bound and tied to the current user via DataProtectionScope.CurrentUser.
    /// Keys encrypted this way may not be accessible on other machines.
    /// </summary>
    private static string EncryptKey(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    // Decrypt base64-encrypted key string back to plaintext
    /// <summary>
    /// Decrypts the provided encrypted key string.
    /// Note: This decryption is machine-bound and will fail if attempted on a different machine.
    /// Explicit checks for failures are in place to handle this.
    /// </summary>
    private static string DecryptKey(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            return string.Empty;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch (CryptographicException ex) // Specific exception for decryption failures
        {
            Logger.LogError(ex, "Key decryption failed due to machine-specific restrictions");
            return string.Empty; // Still return empty, but with enhanced logging
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Key decryption failed");
            return string.Empty;
        }
    }

    public void ReloadKeys()
    {
        KeyStorage = LoadKeys();
    }

    // Static method to alert the user on decryption failure
    private static void AlertUserOnDecryptionFailure()
    {
        MessageBox.Show("Decryption of one or more API keys failed. This could be due to machine-specific restrictions or corrupted data. Keys have been discarded for security. Please re-enter your keys if needed.",
            "Decryption Error", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

[Serializable]
[MessagePackObject] // Add MessagePack attribute
public class ApiKeyStorage
{
    [Key(0)] // Key index for MessagePack serialization
    public List<ApiProvider> Providers { get; set; } = new();
}

[Serializable]
[MessagePackObject] // Add MessagePack attribute
public class ApiProvider
{
    [Key(0)] // Key index for MessagePack serialization
    public string Name { get; set; } = string.Empty;

    [Key(1)] // Key index for MessagePack serialization
    public List<string> Keys { get; set; } = new();
}
