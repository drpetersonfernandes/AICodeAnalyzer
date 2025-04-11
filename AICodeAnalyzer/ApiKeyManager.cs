using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
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

            // Create XmlReader settings with safe defaults
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            // Create XmlReader with the safe settings
            using var xmlReader = XmlReader.Create(reader, settings);

            var serializer = new XmlSerializer(typeof(ApiKeyStorage));
            var result = serializer.Deserialize(xmlReader) as ApiKeyStorage;
            return result ?? new ApiKeyStorage();
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

            var settings = new XmlWriterSettings
            {
                Indent = true
            };

            using var xmlWriter = XmlWriter.Create(writer, settings);

            var serializer = new XmlSerializer(typeof(ApiKeyStorage));
            serializer.Serialize(xmlWriter, KeyStorage);
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