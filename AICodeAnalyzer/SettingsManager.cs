using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using AICodeAnalyzer.Models;
using System.Linq;
using System.Windows;

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

        // Clean up duplicates and ensure default prompt exists
        CleanupDuplicatePrompts();
        EnsureDefaultPrompt();
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
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, // Disable DTD processing
                XmlResolver = null // Prevent external resource resolution
            };

            using var reader = XmlReader.Create(_settingsFilePath, settings);
            var serializer = new XmlSerializer(typeof(ApplicationSettings));
            var result = serializer.Deserialize(reader) as ApplicationSettings;

            // Handle null result or migration from older versions
            var appSettings = result ?? new ApplicationSettings();

            // Perform backwards compatibility checks
            MigrateSettingsIfNeeded(appSettings);

            return appSettings;
        }
        catch (Exception ex)
        {
            // In a real app, we might want to log this exception
            Console.WriteLine($"Error loading settings: {ex.Message}");

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
            // Ensure we're not saving corrupted settings
            CleanupDuplicatePrompts();
            EnsureDefaultPrompt();

            using var writer = new StreamWriter(_settingsFilePath);
            var serializer = new XmlSerializer(typeof(ApplicationSettings));
            serializer.Serialize(writer, Settings);
        }
        catch (Exception ex)
        {
            // In a real app, we might want to log this exception
            Console.WriteLine($"Error saving settings: {ex.Message}");

            ErrorLogger.LogError(ex, $"Error saving settings: {ex.Message}");
            MessageBox.Show("Error saving settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Removes duplicate prompts with the same name, keeping only the first occurrence
    /// </summary>
    private void CleanupDuplicatePrompts()
    {
        if (Settings.CodePrompts.Count <= 1)
            return;

        // Create a dictionary to store first occurrences of each prompt name
        var uniquePrompts = new Dictionary<string, CodePrompt>();

        foreach (var prompt in Settings.CodePrompts)
        {
            uniquePrompts.TryAdd(prompt.Name, prompt);
        }

        // Replace the prompts list with the unique prompts
        Settings.CodePrompts.Clear();
        Settings.CodePrompts.AddRange(uniquePrompts.Values);

        // Ensure selected prompt still exists
        if (!string.IsNullOrEmpty(Settings.SelectedPromptName) && Settings.CodePrompts.All(p => p.Name != Settings.SelectedPromptName))
        {
            // If selected prompt was removed, select the Default prompt or the first available
            var defaultPrompt = Settings.CodePrompts.FirstOrDefault(p => p.Name == "Analyze Source Code");
            Settings.SelectedPromptName = defaultPrompt?.Name ?? Settings.CodePrompts.FirstOrDefault()?.Name;
        }
    }

    /// <summary>
    /// Ensures that at least the default prompt exists in the settings
    /// </summary>
    private void EnsureDefaultPrompt()
    {
        // Check if default prompt already exists
        var defaultPrompt = Settings.CodePrompts.FirstOrDefault(p => p.Name == "Analyze Source Code");

        if (defaultPrompt == null)
        {
            // Default prompt does not exist, create it
            var defaultSettings = new ApplicationSettings();
            defaultPrompt = new CodePrompt("Analyze Source Code", defaultSettings.InitialPrompt);
            Settings.CodePrompts.Add(defaultPrompt);
        }

        // Ensure we have a selected prompt
        if (string.IsNullOrEmpty(Settings.SelectedPromptName) ||
            !Settings.CodePrompts.Exists(p => p.Name == Settings.SelectedPromptName))
        {
            // Default to the first prompt if none is selected
            Settings.SelectedPromptName = Settings.CodePrompts[0].Name;
        }
    }

    /// <summary>
    /// Handles migrating settings from older versions to the new format
    /// </summary>
    private static void MigrateSettingsIfNeeded(ApplicationSettings settings)
    {
        // For older versions that don't have CodePrompts or only have InitialPrompt
        if (settings.CodePrompts.Count == 0 && !string.IsNullOrEmpty(settings.InitialPrompt))
        {
            // Add the existing InitialPrompt as a "Default" template
            settings.CodePrompts.Add(new CodePrompt("Analyze Source Code", settings.InitialPrompt));
            settings.SelectedPromptName = "Analyze Source Code";
        }

        // Add more migration logic here if needed for future versions
    }
}