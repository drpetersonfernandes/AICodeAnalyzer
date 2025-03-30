using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace AICodeAnalyzer;

public class ApiKeyManager
{
    private const string KeysFileName = "keys.xml";
    private readonly string _keysFilePath;
        
    public ApiKeyStorage KeyStorage { get; private set; } // Initialize with non-null value
        
    public ApiKeyManager()
    {
        _keysFilePath = Path.Combine(AppContext.BaseDirectory, KeysFileName);
        KeyStorage = LoadKeys();
    }
        
    public void SaveKey(string provider, string key)
    {
        // KeyStorage is now guaranteed to be non-null so we can remove this check
            
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
        // KeyStorage is now guaranteed to be non-null
        var providerEntry = KeyStorage.Providers.Find(p => p.Name == provider);
        return providerEntry?.Keys ?? new List<string>();
    }
        
    private ApiKeyStorage LoadKeys()
    {
        if (!File.Exists(_keysFilePath))
            return new ApiKeyStorage();
                
        try
        {
            using var reader = new StreamReader(_keysFilePath);
            var serializer = new XmlSerializer(typeof(ApiKeyStorage));
            var result = serializer.Deserialize(reader) as ApiKeyStorage;
            return result ?? new ApiKeyStorage(); // Explicitly handle a null result
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
            using var writer = new StreamWriter(_keysFilePath);
            var serializer = new XmlSerializer(typeof(ApiKeyStorage));
            serializer.Serialize(writer, KeyStorage);
        }
        catch (Exception)
        {
            // Silently fail for now
        }
    }
}
    
[Serializable]
public class ApiKeyStorage
{
    public List<ApiProvider> Providers { get; set; } = new List<ApiProvider>();
}
    
[Serializable]
public class ApiProvider
{
    public string Name { get; set; } = string.Empty;
    public List<string> Keys { get; set; } = new List<string>();
}