using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using AICodeAnalyzer.Services;

namespace AICodeAnalyzer;

public class RecentFilesManager
{
    private readonly string _recentFilesPath;
    private readonly int _maxRecentFiles;
    private List<string> _recentFiles = [];

    public RecentFilesManager(int maxRecentFiles = 10, string storageFileName = "recent_files.xml")
    {
        _maxRecentFiles = maxRecentFiles;
        _recentFilesPath = Path.Combine(AppContext.BaseDirectory, storageFileName);
        LoadRecentFiles();
    }

    public IReadOnlyList<string> GetRecentFiles()
    {
        // Return a copy of the list as read-only, filtering out files that no longer exist
        return _recentFiles
            .Where(File.Exists)
            .ToList()
            .AsReadOnly();
    }

    public void AddRecentFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            // Make sure the path exists and is absolute
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.GetFullPath(filePath);
            }

            // Check if the file exists
            if (!File.Exists(filePath))
                return;

            // Remove the file if it already exists in the list (to prevent duplicates)
            _recentFiles.RemoveAll(f => f.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            // Add the file to the start of the list
            _recentFiles.Insert(0, filePath);

            // Trim the list if it exceeds the maximum number of recent files
            if (_recentFiles.Count > _maxRecentFiles)
            {
                _recentFiles = _recentFiles.Take(_maxRecentFiles).ToList();
            }

            // Save the updated list
            SaveRecentFiles();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error adding file to recent files: {filePath}");
        }
    }

    public void ClearRecentFiles()
    {
        _recentFiles.Clear();
        SaveRecentFiles();
    }

    private void LoadRecentFiles()
    {
        try
        {
            if (!File.Exists(_recentFilesPath)) return;

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, // Disable DTD processing
                XmlResolver = null // Prevent external resource resolution
            };

            using var reader = XmlReader.Create(_recentFilesPath, settings);
            var serializer = new XmlSerializer(typeof(RecentFilesStorage));

            if (serializer.Deserialize(reader) is RecentFilesStorage storage)
            {
                // Filter out any files that no longer exist
                _recentFiles = storage.Files
                    .Where(File.Exists)
                    .Take(_maxRecentFiles)
                    .ToList();
            }
            else
            {
                _recentFiles = [];
            }
        }
        catch (Exception ex)
        {
            // If there's an error loading the recent files, start with an empty list
            _recentFiles = [];

            Logger.LogError(ex, "Loading recent files");
        }
    }


    private void SaveRecentFiles()
    {
        try
        {
            // Create a storage object
            var storage = new RecentFilesStorage { Files = _recentFiles };

            // Serialize to XML
            var serializer = new XmlSerializer(typeof(RecentFilesStorage));
            using var writer = new StreamWriter(_recentFilesPath);
            serializer.Serialize(writer, storage);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error saving recent files");
        }
    }
}

// Create a serializable class to hold the list of recent files
[Serializable]
public class RecentFilesStorage
{
    public List<string> Files { get; set; } = [];
}