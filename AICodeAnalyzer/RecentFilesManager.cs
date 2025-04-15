using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AICodeAnalyzer;

/// <summary>
/// Manages a list of recently opened files with persistence between sessions.
/// </summary>
public class RecentFilesManager
{
    private readonly string _recentFilesPath;
    private readonly int _maxRecentFiles;
    private List<string> _recentFiles = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Initializes a new instance of the RecentFilesManager class.
    /// </summary>
    /// <param name="maxRecentFiles">Maximum number of recent files to track</param>
    /// <param name="storageFileName">Optional custom filename for storage</param>
    public RecentFilesManager(int maxRecentFiles = 10, string storageFileName = "recent_files.json")
    {
        _maxRecentFiles = maxRecentFiles;
        _recentFilesPath = Path.Combine(AppContext.BaseDirectory, storageFileName);
        LoadRecentFiles();
    }

    /// <summary>
    /// Gets the list of recent files that still exist on disk.
    /// </summary>
    /// <returns>A read-only list of file paths</returns>
    public IReadOnlyList<string> GetRecentFiles()
    {
        // Return a copy of the list as read-only, filtering out files that no longer exist
        return _recentFiles
            .Where(File.Exists)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Adds a file to the recent files list, moving it to the top if it already exists.
    /// </summary>
    /// <param name="filePath">The path of the file to add</param>
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

            // Check if file exists
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
            // Log error but continue - adding to recent files is non-critical
            ErrorLogger.LogError(ex, $"Error adding file to recent files: {filePath}");
        }
    }

    /// <summary>
    /// Clears all recent files from the list and persisted storage.
    /// </summary>
    public void ClearRecentFiles()
    {
        _recentFiles.Clear();
        SaveRecentFiles();
    }

    /// <summary>
    /// Loads recent files from storage.
    /// </summary>
    private void LoadRecentFiles()
    {
        try
        {
            if (!File.Exists(_recentFilesPath)) return;

            var json = File.ReadAllText(_recentFilesPath);
            var loadedFiles = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();

            // Filter out any files that no longer exist
            _recentFiles = loadedFiles
                .Where(File.Exists)
                .Take(_maxRecentFiles)
                .ToList();
        }
        catch (Exception ex)
        {
            // If there's an error loading the recent files, start with an empty list
            _recentFiles = new List<string>();
            ErrorLogger.LogError(ex, "Loading recent files");
        }
    }

    /// <summary>
    /// Saves recent files to storage.
    /// </summary>
    private void SaveRecentFiles()
    {
        try
        {
            var json = JsonSerializer.Serialize(_recentFiles, _jsonOptions);
            File.WriteAllText(_recentFilesPath, json);
        }
        catch (Exception ex)
        {
            // Log error but continue - recent files functionality is non-critical
            ErrorLogger.LogError(ex, "Saving recent files");
        }
    }
}