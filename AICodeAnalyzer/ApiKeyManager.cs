using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using MessagePack; // Added MessagePack namespace

namespace AICodeAnalyzer;

public class ApiKeyManager
{
    private const string KeysFileName = "keys.dat"; // Changed from "keys.xml"
    private readonly string _keysFilePath;

    private ApiKeyStorage KeyStorage { get; set; } // Initialize with non-null value

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

    /// <summary>
    /// Removes an API key for a specific provider
    /// </summary>
    /// <param name="provider">The provider name</param>
    /// <param name="key">The key to remove</param>
    /// <returns>True if the key was removed, false if it wasn't found</returns>
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
            // Read the binary data from file
            var bytes = File.ReadAllBytes(_keysFilePath);

            // Deserialize with MessagePack
            var result = MessagePackSerializer.Deserialize<ApiKeyStorage>(bytes);
            return result;
        }
        catch (Exception)
        {
            // If there's any error, return a new storage
            return new ApiKeyStorage();
        }
    }

    private void SaveKeys()
    {
        try
        {
            // Serialize with MessagePack
            var bytes = MessagePackSerializer.Serialize(KeyStorage);

            // Write the binary data to file
            File.WriteAllBytes(_keysFilePath, bytes);
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Error saving API keys");
            MessageBox.Show("Failed to save API keys. See error log for details.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

[Serializable]
[MessagePackObject] // Add MessagePack attribute
public class ApiKeyStorage
{
    [Key(0)] // Key index for MessagePack serialization
    public List<ApiProvider> Providers { get; set; } = new List<ApiProvider>();
}

[Serializable]
[MessagePackObject] // Add MessagePack attribute
public class ApiProvider
{
    [Key(0)] // Key index for MessagePack serialization
    public string Name { get; set; } = string.Empty;

    [Key(1)] // Key index for MessagePack serialization
    public List<string> Keys { get; set; } = new List<string>();
}