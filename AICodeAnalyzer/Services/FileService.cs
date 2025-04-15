using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AICodeAnalyzer.Models;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace AICodeAnalyzer.Services;

public class FileService(SettingsManager settingsManager, LoggingService loggingService)
{
    private readonly SettingsManager _settingsManager = settingsManager;
    private readonly LoggingService _loggingService = loggingService;
    private readonly Dictionary<string, List<SourceFile>> _filesByExtension = new();

    public string SelectedFolder { get; private set; } = string.Empty;

    public IReadOnlyDictionary<string, List<SourceFile>> FilesByExtension => _filesByExtension;

    // Initialize the event with an empty delegate to avoid null checks
    public event EventHandler FilesChanged = delegate { };

    public async Task<bool> SelectFolderAsync()
    {
        try
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select Project Folder"
            };

            if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return false;
            // Update selected folder
            SelectedFolder = dialog.FileName;
            _loggingService.LogOperation($"Starting folder scan: {SelectedFolder}");
            _loggingService.StartOperationTimer("FolderScan");

            // Clear collections
            _filesByExtension.Clear();

            // Scan files
            await FindSourceFilesAsync(SelectedFolder);

            _loggingService.EndOperationTimer("FolderScan");

            // Notify that files have changed
            OnFilesChanged();

            return true;
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error selecting folder: {ex.Message}");
            ErrorLogger.LogError(ex, "Opening folder selection dialog");
            return false;
        }
    }

    public async Task<bool> SelectFilesAsync()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Source Files",
                Filter = "All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true) return false;
            // Initialize the file collection if this is the first selection
            if (string.IsNullOrEmpty(SelectedFolder))
            {
                // Use the directory of the first selected file as the base folder
                SelectedFolder = Path.GetDirectoryName(dialog.FileNames[0]) ?? string.Empty;
                _loggingService.LogOperation($"Base folder set to: {SelectedFolder}");
            }

            _loggingService.LogOperation($"Processing {dialog.FileNames.Length} selected files");
            _loggingService.StartOperationTimer("ProcessSelectedFiles");

            await ProcessSelectedFilesAsync(dialog.FileNames);

            var totalFiles = _filesByExtension.Values.Sum(list => list.Count);
            _loggingService.LogOperation($"Total files after selection: {totalFiles}");

            _loggingService.EndOperationTimer("ProcessSelectedFiles");

            // Notify that files have changed
            OnFilesChanged();

            return true;
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error selecting files: {ex.Message}");
            ErrorLogger.LogError(ex, "Selecting files");
            return false;
        }
    }

    public void ClearFiles()
    {
        try
        {
            // Clear the file collections
            _filesByExtension.Clear();
            SelectedFolder = string.Empty;

            _loggingService.LogOperation("File selection cleared");

            // Notify that files have changed
            OnFilesChanged();
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error clearing files: {ex.Message}");
            ErrorLogger.LogError(ex, "Clearing files");
        }
    }

    public Dictionary<string, List<string>> PrepareConsolidatedFiles(List<SourceFile> includedFiles)
    {
        var consolidatedFiles = new Dictionary<string, List<string>>();
        includedFiles.Clear(); // Start fresh

        foreach (var ext in _filesByExtension.Keys)
        {
            var files = _filesByExtension[ext];

            if (!consolidatedFiles.TryGetValue(ext, out var value))
            {
                value = new List<string>();
                consolidatedFiles[ext] = value;
            }

            foreach (var file in files)
            {
                value.Add($"File: {file.RelativePath}\n```{GetLanguageForExtension(ext)}\n{file.Content}\n```\n");
                includedFiles.Add(file); // Add to the list of included files
            }
        }

        return consolidatedFiles;
    }

    public async Task<string> SaveResponseAsync(string responseText, string currentFilePath)
    {
        try
        {
            // If we have a current file path, and we want to overwrite it
            if (!string.IsNullOrEmpty(currentFilePath) && File.Exists(currentFilePath))
            {
                // Ask the user if they want to overwrite or save as a new file
                var result = MessageBox.Show(
                    $"Do you want to overwrite the current file?\n{currentFilePath}\n\nClick 'Yes' to overwrite, 'No' to save as a new file.",
                    "Save Options",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Save on background thread
                    await Task.Run(() => File.WriteAllText(currentFilePath, responseText));

                    _loggingService.LogOperation($"Overwrote file: {currentFilePath}");
                    MessageBox.Show($"File saved: {Path.GetFileName(currentFilePath)}", "Save Successful",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return currentFilePath;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    // User canceled the operation
                    return currentFilePath;
                }
                // If No, continue to the save dialog
            }

            // Create a save file dialog
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "md", // Default to Markdown since the responses are markdown formatted
                Title = "Save AI Analysis Response"
            };

            // If the user has selected a project folder, suggest that as the initial directory
            if (!string.IsNullOrEmpty(SelectedFolder) && Directory.Exists(SelectedFolder))
            {
                saveFileDialog.InitialDirectory = SelectedFolder;

                // Suggest a filename based on the project folder name and timestamp
                var folderName = new DirectoryInfo(SelectedFolder).Name;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                saveFileDialog.FileName = $"{folderName}_analysis_{timestamp}.md";
            }
            else
            {
                // Default filename with timestamp if no folder is selected
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                saveFileDialog.FileName = $"ai_analysis_{timestamp}.md";
            }

            // Show the dialog and get the result
            var dialogResult = saveFileDialog.ShowDialog();

            // If the user clicked OK, save the file
            if (dialogResult != true) return currentFilePath;
            // Save on background thread
            await Task.Run(() => File.WriteAllText(saveFileDialog.FileName, responseText));

            _loggingService.LogOperation($"Saved response to: {saveFileDialog.FileName}");
            MessageBox.Show($"Response saved to {saveFileDialog.FileName}", "Save Successful", MessageBoxButton.OK,
                MessageBoxImage.Information);

            return saveFileDialog.FileName;

            // Return the original file path if save was canceled
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error saving response: {ex.Message}");
            ErrorLogger.LogError(ex, "Saving response to file");
            MessageBox.Show("An error occurred while saving the response.", "Save Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return currentFilePath;
        }
    }

    public void AutoSaveResponse(string responseText, int responseIndex)
    {
        try
        {
            // Create the AiOutput directory if it doesn't exist
            var outputDirectory = Path.Combine(AppContext.BaseDirectory, "AiOutput");
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                _loggingService.LogOperation($"Created AiOutput directory at {outputDirectory}");
            }

            // Generate a filename based on project name (if available) and timestamp
            var projectName = "unknown";
            if (!string.IsNullOrEmpty(SelectedFolder))
            {
                projectName = new DirectoryInfo(SelectedFolder).Name;

                // Sanitize the project name to remove invalid characters
                projectName = string.Join("_", projectName.Split(Path.GetInvalidFileNameChars()));
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
            var filename = $"{projectName}_response_{responseIndex + 1}_{timestamp}.md";
            var filePath = Path.Combine(outputDirectory, filename);

            // Save the response
            File.WriteAllText(filePath, responseText);
            _loggingService.LogOperation($"Auto-saved response #{responseIndex + 1} to {filename}");
        }
        catch (Exception ex)
        {
            // Log error but don't interrupt the user experience
            _loggingService.LogOperation($"Error auto-saving response: {ex.Message}");
        }
    }

    public async Task<string> LoadMarkdownFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                _loggingService.LogOperation($"Invalid file path: {filePath}");
                MessageBox.Show("The specified file does not exist.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return string.Empty;
            }

            // Read the file on background thread
            var fileContent = await Task.Run(() => File.ReadAllText(filePath));
            _loggingService.LogOperation($"Loaded markdown file: {filePath}");

            return fileContent;
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error loading markdown file: {ex.Message}");
            ErrorLogger.LogError(ex, $"Loading markdown file: {filePath}");
            MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return string.Empty;
        }
    }

    private async Task FindSourceFilesAsync(string folderPath)
    {
        try
        {
            // Convert SourceFileExtensions to a HashSet for O(1) lookups instead of O(n)
            var allowedExtensions = new HashSet<string>(_settingsManager.Settings.SourceFileExtensions,
                StringComparer.OrdinalIgnoreCase);
            var maxFileSizeKb = _settingsManager.Settings.MaxFileSizeKb;

            // Create a set of excluded directories for faster lookups
            var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "bin", "obj", "node_modules", "packages", ".git", ".vs" };

            // Use ConcurrentDictionary to avoid locks when adding files
            var filesByExtConcurrent =
                new System.Collections.Concurrent.ConcurrentDictionary<string, List<SourceFile>>();

            // Keep track of processed paths to avoid duplicates (much faster than searching the lists)
            var processedPaths =
                new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

            // Optimize UI updates by throttling them
            var lastUiUpdate = DateTime.MinValue;
            var fileProcessedCount = 0;
            var totalFileCount = 0;

            // Use ParallelOptions for better control of parallelism
            var parallelOptions = new ParallelOptions
            {
                // Adjust this based on your CPU - maybe set to Environment.ProcessorCount
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2)
            };

            // Process files and directories using a custom recursive method
            await ProcessDirectoryAsync(folderPath, 0);

            // Sync the concurrent dictionary with the regular one
            _filesByExtension.Clear();
            foreach (var kvp in filesByExtConcurrent)
            {
                _filesByExtension[kvp.Key] = kvp.Value;
            }

            async Task ProcessDirectoryAsync(string currentDir, int depth)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(currentDir);

                    // Skip excluded directories
                    if (excludedDirs.Contains(dirInfo.Name) ||
                        dirInfo.Attributes.HasFlag(FileAttributes.Hidden) ||
                        dirInfo.Attributes.HasFlag(FileAttributes.System) ||
                        dirInfo.Name.StartsWith('.'))
                    {
                        return;
                    }

                    // Process all matching files in this directory in parallel
                    var matchingFiles = dirInfo.EnumerateFiles()
                        .Where(f => allowedExtensions.Contains(f.Extension.ToLowerInvariant()) &&
                                    f.Length / 1024 <= maxFileSizeKb)
                        .ToList();

                    // Update total count atomically
                    Interlocked.Add(ref totalFileCount, matchingFiles.Count);

                    // Update UI only occasionally - not on every batch
                    var now = DateTime.Now;
                    if ((now - lastUiUpdate).TotalMilliseconds > 500) // Only update every 500ms
                    {
                        try
                        {
                            if ((now - lastUiUpdate).TotalMilliseconds > 500)
                            {
                                lastUiUpdate = now;
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    _loggingService.LogOperation(
                                        $"Scanning folder... (Found {fileProcessedCount}/{totalFileCount} files)");
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogOperation($"Error scanning directory {currentDir}: {ex.Message}");
                        }
                    }

                    // Process files in parallel with better batching
                    await Parallel.ForEachAsync(matchingFiles, parallelOptions, async (file, ct) =>
                    {
                        try
                        {
                            var ext = file.Extension.ToLowerInvariant();

                            // Skip if already processed (avoid duplicates)
                            if (!processedPaths.TryAdd(file.FullName, 0))
                                return;

                            // Calculate the relative path once
                            string relativePath;
                            if (file.FullName.StartsWith(SelectedFolder, StringComparison.OrdinalIgnoreCase))
                            {
                                relativePath = file.FullName[SelectedFolder.Length..].TrimStart('\\', '/');
                            }
                            else
                            {
                                relativePath = file.FullName;
                            }

                            // Read file content asynchronously
                            string content;
                            try
                            {
                                // Read file with optimized buffering
                                content = await File.ReadAllTextAsync(file.FullName, ct);
                            }
                            catch (Exception ex)
                            {
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                    _loggingService.LogOperation($"Error reading file {file.FullName}: {ex.Message}")
                                );
                                return;
                            }

                            // Create source file
                            var sourceFile = new SourceFile
                            {
                                Path = file.FullName,
                                RelativePath = relativePath,
                                Extension = ext,
                                Content = content
                            };

                            // Add to concurrent dictionary
                            filesByExtConcurrent.AddOrUpdate(
                                ext,
                                _ => new List<SourceFile> { sourceFile },
                                (_, list) =>
                                {
                                    lock (list) // Still need a lock for the list itself
                                    {
                                        list.Add(sourceFile);
                                    }

                                    return list;
                                }
                            );

                            // Increment processed count
                            Interlocked.Increment(ref fileProcessedCount);
                        }
                        catch (Exception ex)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                                _loggingService.LogOperation($"Error processing file {file.FullName}: {ex.Message}")
                            );
                        }
                    });

                    // Process subdirectories in parallel with depth throttling
                    var subDirs = dirInfo.GetDirectories();

                    // For deep directories, process sequentially to avoid thread explosion
                    if (depth > 3)
                    {
                        foreach (var dir in subDirs)
                        {
                            await ProcessDirectoryAsync(dir.FullName, depth + 1);
                        }
                    }
                    else
                    {
                        // Process directories in parallel for better performance at shallow depths
                        await Parallel.ForEachAsync(subDirs, parallelOptions,
                            async (dir, _) => { await ProcessDirectoryAsync(dir.FullName, depth + 1); });
                    }
                }
                catch (Exception ex)
                {
                    _loggingService.LogOperation($"Error scanning directory {currentDir}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error in folder scan: {ex.Message}");
        }
    }

    private async Task ProcessSelectedFilesAsync(IEnumerable<string> filePaths)
    {
        // Process files in parallel with a limit of 10 concurrent files
        var tasks = new List<Task>();
        var throttler = new SemaphoreSlim(10);

        foreach (var filePath in filePaths)
        {
            await throttler.WaitAsync();

            // Start a new task for each file
            tasks.Add(ProcessSingleFileAsync(filePath, throttler));
        }

        // Wait for all file processing tasks to complete
        await Task.WhenAll(tasks);
    }

    private async Task ProcessSingleFileAsync(string filePath, SemaphoreSlim throttler)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var ext = fileInfo.Extension.ToLowerInvariant();

            // Check file size limit only, don't filter by extension
            var fileSizeKb = (int)(fileInfo.Length / 1024);
            if (fileSizeKb <= _settingsManager.Settings.MaxFileSizeKb)
            {
                // Get a relative path
                string relativePath;
                if (filePath.StartsWith(SelectedFolder, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = filePath.Substring(SelectedFolder.Length).TrimStart('\\', '/');
                }
                else
                {
                    // If the file is outside the base folder, use the full path
                    relativePath = filePath;
                }

                // Read file asynchronously - OUTSIDE of any lock
                string content;
                try
                {
                    content = await File.ReadAllTextAsync(filePath);
                }
                catch (Exception ex)
                {
                    _loggingService.LogOperation($"Error reading file {filePath}: {ex.Message}");
                    return;
                }

                // Create the file object we'll add
                var sourceFile = new SourceFile
                {
                    Path = filePath,
                    RelativePath = relativePath,
                    Extension = ext,
                    Content = content
                };

                var fileAdded = false;

                // Use lock to safely update shared collection
                lock (_filesByExtension)
                {
                    // Initialize the extension group if needed
                    if (!_filesByExtension.TryGetValue(ext, out var value))
                    {
                        value = new List<SourceFile>();
                        _filesByExtension[ext] = value;
                    }

                    // Check if this file is already added
                    if (!value.Any(f => f.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        value.Add(sourceFile);
                        fileAdded = true;
                    }
                }

                // Log outside the lock
                if (fileAdded)
                {
                    _loggingService.LogOperation($"Added file: {relativePath}");
                }
                else
                {
                    _loggingService.LogOperation($"Skipped duplicate file: {relativePath}");
                }
            }
            else
            {
                _loggingService.LogOperation(
                    $"Skipped file due to size limit: {filePath} ({fileSizeKb} KB > {_settingsManager.Settings.MaxFileSizeKb} KB)");
            }
        }
        finally
        {
            // Release the throttler when done
            throttler.Release();
        }
    }

    /// <summary>
    /// Gets the language identifier for a file extension to be used in markdown code blocks
    /// </summary>
    public static string GetLanguageForExtension(string ext)
    {
        return ext switch
        {
            // C# and .NET
            ".cs" => "csharp",
            ".vb" => "vb",
            ".fs" => "fsharp",
            ".xaml" => "xml",
            ".csproj" => "xml",
            ".vbproj" => "xml",
            ".fsproj" => "xml",
            ".nuspec" => "xml",
            ".aspx" => "aspx",
            ".asp" => "asp",
            ".cshtml" => "cshtml",
            ".axaml" => "xml",

            // Web languages
            ".html" => "html",
            ".htm" => "html",
            ".css" => "css",
            ".js" => "javascript",
            ".jsx" => "jsx",
            ".ts" => "typescript",
            ".tsx" => "tsx",
            ".vue" => "vue",
            ".svelte" => "svelte",
            ".scss" => "scss",
            ".sass" => "sass",
            ".less" => "less",
            ".mjs" => "javascript",
            ".cjs" => "javascript",

            // JVM languages
            ".java" => "java",
            ".kt" => "kotlin",
            ".scala" => "scala",
            ".groovy" => "groovy",

            // Python
            ".py" => "python",

            // Ruby
            ".rb" => "ruby",
            ".erb" => "erb",

            // PHP
            ".php" => "php",

            // C/C++
            ".c" => "c",
            ".cpp" => "cpp",
            ".h" => "cpp", // C/C++ headers typically get cpp highlighting

            // Go
            ".go" => "go",

            // Rust
            ".rs" => "rust",

            // Swift/Objective-C
            ".swift" => "swift",
            ".m" => "objectivec",
            ".mm" => "objectivec",

            // Dart/Flutter
            ".dart" => "dart",

            // Markup and Data
            ".xml" => "xml",
            ".json" => "json",
            ".yaml" => "yaml",
            ".yml" => "yaml",
            ".md" => "markdown",
            ".txt" => "text",
            ".plist" => "xml",

            // Templates
            ".pug" => "pug",
            ".jade" => "jade",
            ".ejs" => "ejs",
            ".haml" => "haml",

            // Query Languages
            ".sql" => "sql",
            ".graphql" => "graphql",
            ".gql" => "graphql",

            // Shell/Scripts
            ".sh" => "bash",
            ".bash" => "bash",
            ".bat" => "batch",
            ".ps1" => "powershell",
            ".pl" => "perl",

            // Other Languages
            ".r" => "r",
            ".lua" => "lua",
            ".dockerfile" => "dockerfile",
            ".ex" => "elixir",
            ".exs" => "elixir",
            ".jl" => "julia",
            ".nim" => "nim",
            ".hs" => "haskell",
            ".clj" => "clojure",
            ".elm" => "elm",
            ".erl" => "erlang",
            ".asm" => "asm",
            ".s" => "asm",
            ".wasm" => "wasm",

            // Configuration/Infrastructure
            ".ini" => "ini",
            ".toml" => "toml",
            ".tf" => "hcl",
            ".tfvars" => "hcl",
            ".proto" => "proto",
            ".config" => "xml",

            // Default case
            _ => "text"
        };
    }

    protected virtual void OnFilesChanged()
    {
        FilesChanged?.Invoke(this, EventArgs.Empty);
    }
}