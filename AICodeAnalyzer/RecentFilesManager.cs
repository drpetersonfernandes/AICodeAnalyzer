using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace AICodeAnalyzer;

[Serializable]
public class RecentFilesData
{
    public List<string> FilePaths { get; set; } = new List<string>();
}

public class RecentFilesManager
{
    private const string RecentFilesFileName = "recentfiles.xml";
    private readonly string _recentFilesPath;
    private readonly RecentFilesData _recentFiles;
    private const int MaxRecentFiles = 10; // Maximum number of recent files to remember

    public RecentFilesManager()
    {
        _recentFilesPath = Path.Combine(AppContext.BaseDirectory, RecentFilesFileName);
        _recentFiles = LoadRecentFiles();
    }

    public IReadOnlyList<string> GetRecentFiles()
    {
        // Return the list of recent files, filtering out any that no longer exist
        return _recentFiles.FilePaths
            .Where(File.Exists)
            .ToList()
            .AsReadOnly();
    }

    public void AddRecentFile(string filePath)
    {
        // Get absolute path
        filePath = Path.GetFullPath(filePath);

        // Remove the file if it already exists in the list
        _recentFiles.FilePaths.Remove(filePath);

        // Add the file to the beginning of the list
        _recentFiles.FilePaths.Insert(0, filePath);

        // Trim the list if it exceeds the maximum count
        while (_recentFiles.FilePaths.Count > MaxRecentFiles)
        {
            _recentFiles.FilePaths.RemoveAt(_recentFiles.FilePaths.Count - 1);
        }

        // Save the updated list
        SaveRecentFiles();
    }

    public void ClearRecentFiles()
    {
        _recentFiles.FilePaths.Clear();
        SaveRecentFiles();
    }

    private RecentFilesData LoadRecentFiles()
    {
        if (!File.Exists(_recentFilesPath))
            return new RecentFilesData();

        try
        {
            using var reader = new StreamReader(_recentFilesPath);
            var serializer = new XmlSerializer(typeof(RecentFilesData));
            var result = serializer.Deserialize(reader) as RecentFilesData;
            return result ?? new RecentFilesData();
        }
        catch (Exception)
        {
            // If there's any error, return a new empty list
            return new RecentFilesData();
        }
    }

    private void SaveRecentFiles()
    {
        try
        {
            using var writer = new StreamWriter(_recentFilesPath);
            var serializer = new XmlSerializer(typeof(RecentFilesData));
            serializer.Serialize(writer, _recentFiles);
        }
        catch (Exception)
        {
            // Silently fail for now
        }
    }
}