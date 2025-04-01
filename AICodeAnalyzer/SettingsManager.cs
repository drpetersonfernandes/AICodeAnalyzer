using System;
using System.IO;
using System.Xml.Serialization;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer;

public class SettingsManager
{
    private const string SettingsFileName = "settings.xml";
    private readonly string _settingsFilePath;

    public ApplicationSettings Settings { get; private set; }

    public SettingsManager()
    {
        _settingsFilePath = Path.Combine(AppContext.BaseDirectory, SettingsFileName);
        Settings = LoadSettings();
    }

    /// <summary>
    /// Loads settings from the XML file or creates new settings if file doesn't exist
    /// </summary>
    private ApplicationSettings LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
            return new ApplicationSettings();

        try
        {
            using var reader = new StreamReader(_settingsFilePath);
            var serializer = new XmlSerializer(typeof(ApplicationSettings));
            var result = serializer.Deserialize(reader) as ApplicationSettings;
            return result ?? new ApplicationSettings();
        }
        catch (Exception)
        {
            // If there's any error, return default settings
            return new ApplicationSettings();
        }
    }

    /// <summary>
    /// Saves the current settings to the XML file
    /// </summary>
    public void SaveSettings()
    {
        try
        {
            using var writer = new StreamWriter(_settingsFilePath);
            var serializer = new XmlSerializer(typeof(ApplicationSettings));
            serializer.Serialize(writer, Settings);
        }
        catch (Exception)
        {
            // Silently fail for now
        }
    }
}