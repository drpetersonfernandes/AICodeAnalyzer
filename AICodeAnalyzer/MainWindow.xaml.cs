using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AICodeAnalyzer.AIProvider;
using AICodeAnalyzer.Models;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace AICodeAnalyzer;

public partial class MainWindow
{
    private readonly ApiKeyManager _keyManager;
    private string _selectedFolder = string.Empty;
    private readonly Dictionary<string, List<SourceFile>> _filesByExtension;
    private readonly List<ChatMessage> _conversationHistory = new();
    private bool _isMarkdownViewActive = true; // Set Markdown as default
    private string _currentResponseText = string.Empty;
    private readonly ApiProviderFactory _apiProviderFactory;
    private readonly Dictionary<string, DateTime> _operationTimers = new();
    private readonly SettingsManager _settingsManager;
    private int _currentResponseIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
        _filesByExtension = new Dictionary<string, List<SourceFile>>();
        _keyManager = new ApiKeyManager();
        _apiProviderFactory = new ApiProviderFactory();
        _settingsManager = new SettingsManager();

        TxtFollowupQuestion.IsEnabled = false;
        BtnSendFollowup.IsEnabled = false;
        ChkIncludeSelectedFiles.IsEnabled = false;

        // Populate API dropdown using provider names from the factory
        foreach (var provider in _apiProviderFactory.AllProviders)
        {
            CboAiApi.Items.Add(provider.Name);
        }

        CboAiApi.SelectedIndex = 0; // Default to first provider (Claude)

        // Initialize log
        LogOperation("Application started");

        InitializePromptSelection();
    }

    private void InitializePromptSelection()
    {
        // Initialize prompt templates
        LoadPromptTemplates();
        LogOperation("Loaded prompt templates");
    }

    private void MenuConfigure_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var configWindow = new ConfigurationWindow(_settingsManager)
            {
                Owner = this
            };

            var result = configWindow.ShowDialog();

            if (result == true)
            {
                // Settings were saved, update any necessary UI or behavior
                LogOperation("Settings updated");

                // Refresh the prompt templates dropdown
                LoadPromptTemplates();
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error opening configuration window: {ex.Message}");
            ErrorLogger.LogError(ex, "Opening configuration window");
        }
    }

    /// <summary>
    /// Logs an operation with a timestamp to the log panel
    /// </summary>
    /// <param name="message">The message to log</param>
    private void LogOperation(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var logEntry = $"[{timestamp}] {message}\r\n";

        // Add to log TextBox
        Dispatcher.Invoke(() =>
        {
            TxtLog.AppendText(logEntry);
            TxtLog.ScrollToEnd();

            // Limit log size (optional)
            if (TxtLog.Text.Length > 50000)
            {
                TxtLog.Text = TxtLog.Text.Substring(TxtLog.Text.Length - 40000);
            }
        });
    }

    /// <summary>
    /// Starts timing an operation
    /// </summary>
    /// <param name="operationName">Unique name for the operation</param>
    private void StartOperationTimer(string operationName)
    {
        _operationTimers[operationName] = DateTime.Now;
        LogOperation($"Started: {operationName}");
    }

    /// <summary>
    /// Ends timing an operation and logs the elapsed time
    /// </summary>
    /// <param name="operationName">Name of the operation to end</param>
    private void EndOperationTimer(string operationName)
    {
        if (_operationTimers.TryGetValue(operationName, out var startTime))
        {
            var elapsed = DateTime.Now - startTime;
            LogOperation($"Completed: {operationName} (Elapsed: {elapsed.TotalSeconds:F2} seconds)");
            _operationTimers.Remove(operationName);
        }
        else
        {
            LogOperation($"Completed: {operationName} (Timer not found)");
        }
    }

    // Add this new method to handle mouse wheel scrolling in the MarkdownScrollViewer
    private void MarkdownScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var scrollViewer = (ScrollViewer)sender;

        if (e.Delta < 0)
        {
            scrollViewer.LineDown();
            scrollViewer.LineDown();
        }
        else
        {
            scrollViewer.LineUp();
            scrollViewer.LineUp();
        }

        e.Handled = true;
    }

    private void UpdatePreviousKeys(string apiProvider)
    {
        CboPreviousKeys.Items.Clear();
        CboPreviousKeys.Items.Add("Select a key");

        var savedKeys = _keyManager.GetKeysForProvider(apiProvider);
        foreach (var key in savedKeys)
        {
            CboPreviousKeys.Items.Add(MaskKey(key));
        }

        CboPreviousKeys.SelectedIndex = 0;
    }

    private string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 8)
            return "*****";

        return key.Substring(0, 3) + "*****" + key.Substring(key.Length - 3);
    }

    private void CboPreviousKeys_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboPreviousKeys.SelectedIndex <= 0)
            return;

        var apiSelection = CboAiApi.SelectedItem?.ToString() ?? string.Empty;
        var savedKeys = _keyManager.GetKeysForProvider(apiSelection);

        if (CboPreviousKeys.SelectedIndex - 1 < savedKeys.Count)
        {
            TxtApiKey.Password = savedKeys[CboPreviousKeys.SelectedIndex - 1];
        }
    }

    private void BtnSaveKey_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TxtApiKey.Password))
        {
            MessageBox.Show("Please enter an API key before saving.", "No Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var apiSelection = CboAiApi.SelectedItem?.ToString() ?? string.Empty;
        _keyManager.SaveKey(apiSelection, TxtApiKey.Password);
        UpdatePreviousKeys(apiSelection);

        MessageBox.Show("API key saved successfully.", "Key Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    private async void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select Project Folder"
            };

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                _selectedFolder = dialog.FileName;
                TxtSelectedFolder.Text = _selectedFolder;
                await ScanFolder();
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Opening folder selection dialog");
            TxtStatus.Text = "Error selecting folder.";
        }
    }

    private async Task ScanFolder()
    {
        if (string.IsNullOrEmpty(_selectedFolder) || !Directory.Exists(_selectedFolder))
            return;

        try
        {
            TxtStatus.Text = "Scanning folder for source files...";
            LogOperation($"Starting folder scan: {_selectedFolder}");
            StartOperationTimer("FolderScan");

            _filesByExtension.Clear();
            LvFiles.Items.Clear();

            // Run the scan in a background task
            await Task.Run(() => FindSourceFiles(_selectedFolder));

            // Display results
            var totalFiles = _filesByExtension.Values.Sum(list => list.Count);
            TxtStatus.Text = $"Found {totalFiles} source files.";

            // Display files organized by folder
            DisplayFilesByFolder();

            EndOperationTimer("FolderScan");
        }
        catch (Exception ex)
        {
            LogOperation($"Error scanning folder: {ex.Message}");
            ErrorLogger.LogError(ex, "Scanning folder");
            TxtStatus.Text = "Error scanning folder.";
        }
    }

    private void DisplayFilesByFolder()
    {
        // First, organize files by their folder structure
        var filesByFolder = new Dictionary<string, List<SourceFile>>();

        // Track how many files were manually added vs. found through folder scan
        var manuallyAddedFiles = 0;
        var folderScannedFiles = 0;

        // Group all files by their parent folder
        foreach (var extensionFiles in _filesByExtension.Values)
        {
            foreach (var file in extensionFiles)
            {
                // Check if this file is inside or outside the base folder
                var isOutsideBaseFolder = !file.Path.StartsWith(_selectedFolder, StringComparison.OrdinalIgnoreCase);

                if (isOutsideBaseFolder)
                {
                    manuallyAddedFiles++;
                }
                else
                {
                    folderScannedFiles++;
                }

                // Extract the folder path from the relative path
                var folderPath = Path.GetDirectoryName(file.RelativePath) ?? string.Empty;

                // For files outside the base folder, prefix with "(External)"
                if (isOutsideBaseFolder && string.IsNullOrEmpty(folderPath))
                {
                    folderPath = "(External Files)";
                }
                else if (isOutsideBaseFolder)
                {
                    folderPath = $"(External) {folderPath}";
                }
                else if (string.IsNullOrEmpty(folderPath))
                {
                    folderPath = "(Root)";
                }

                // Add to folder dictionary
                if (!filesByFolder.ContainsKey(folderPath))
                    filesByFolder[folderPath] = new List<SourceFile>();

                filesByFolder[folderPath].Add(file);
            }
        }

        // Clear the list before adding items
        LvFiles.Items.Clear();

        // Add mode indicator (folder scan or manual selection)
        LvFiles.Items.Add(new ListViewItem
        {
            Content = $"===== Files Summary ({_filesByExtension.Values.Sum(v => v.Count)} total) =====",
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Colors.LightGray)
        });

        if (manuallyAddedFiles > 0)
        {
            LvFiles.Items.Add($"    {manuallyAddedFiles} manually selected files");
        }

        if (folderScannedFiles > 0)
        {
            LvFiles.Items.Add($"    {folderScannedFiles} files from folder scan");
        }

        // Now display stats by extension
        LvFiles.Items.Add(new ListViewItem
        {
            Content = "===== File Extensions Summary =====",
            FontWeight = FontWeights.Bold
        });

        foreach (var ext in _filesByExtension.Keys.OrderBy(k => k))
        {
            var count = _filesByExtension[ext].Count;
            LvFiles.Items.Add($"    {ext} - {count} files");
            LogOperation($"Found {count} {ext} files");
        }

        LvFiles.Items.Add(new ListViewItem
        {
            Content = "===== Files By Folder =====",
            FontWeight = FontWeights.Bold
        });

        // Then list files by folder
        foreach (var folderPath in filesByFolder.Keys.OrderBy(f => f))
        {
            // Add folder as a header
            LvFiles.Items.Add(new ListViewItem
            {
                Content = folderPath,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Colors.LightGray)
            });

            // Add each file in this folder (ordered alphabetically)
            foreach (var file in filesByFolder[folderPath].OrderBy(f => Path.GetFileName(f.RelativePath)))
            {
                // Show files that are manually added with a different indicator
                var isOutsideBaseFolder = !file.Path.StartsWith(_selectedFolder, StringComparison.OrdinalIgnoreCase);
                var prefix = isOutsideBaseFolder ? "+" : "    ";

                LvFiles.Items.Add($"{prefix} {Path.GetFileName(file.RelativePath)}");
            }
        }
    }

    private string GetLanguageGroupForExtension(string ext)
    {
        return ext switch
        {
            ".cs" => "C#",
            ".xaml" => "XAML",
            ".java" => "Java",
            ".js" => "JavaScript",
            ".ts" => "TypeScript",
            ".py" => "Python",
            ".html" or ".htm" => "HTML",
            ".css" => "CSS",
            ".cpp" or ".h" or ".c" => "C/C++",
            ".go" => "Go",
            ".rb" => "Ruby",
            ".php" => "PHP",
            ".swift" => "Swift",
            ".kt" => "Kotlin",
            ".rs" => "Rust",
            ".dart" => "Dart",
            ".xml" => "XML",
            ".json" => "JSON",
            ".yaml" or ".yml" => "YAML",
            ".md" => "Markdown",
            ".txt" => "Text",
            _ => "Other"
        };
    }

    private string GenerateFileFilterFromExtensions()
    {
        var filterBuilder = new StringBuilder();

        // Add an "All Supported Files" filter first
        filterBuilder.Append("All Supported Files|");

        // Add all supported extensions
        foreach (var ext in _settingsManager.Settings.SourceFileExtensions)
        {
            filterBuilder.Append($"*{ext};");
        }

        // Remove the last semicolon
        filterBuilder.Length--;

        // Add specific filters for each type
        var groupedExtensions = _settingsManager.Settings.SourceFileExtensions
            .GroupBy(GetLanguageGroupForExtension)
            .OrderBy(g => g.Key);

        foreach (var group in groupedExtensions)
        {
            filterBuilder.Append($"|{group.Key} Files|");
            foreach (var ext in group)
            {
                filterBuilder.Append($"*{ext};");
            }

            // Remove the last semicolon
            filterBuilder.Length--;
        }

        // Add "All Files" filter at the end
        filterBuilder.Append("|All Files|*.*");

        return filterBuilder.ToString();
    }

    private async void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Create file selection dialog
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Source Files",
                Filter = GenerateFileFilterFromExtensions()
            };

            if (dialog.ShowDialog() == true)
            {
                // Initialize the file collection if this is the first selection
                if (string.IsNullOrEmpty(_selectedFolder))
                {
                    // Use the directory of the first selected file as the base folder
                    _selectedFolder = Path.GetDirectoryName(dialog.FileNames[0]) ?? string.Empty;
                    TxtSelectedFolder.Text = _selectedFolder;

                    // Clear any existing data
                    _filesByExtension.Clear();
                    LvFiles.Items.Clear();

                    LogOperation($"Base folder set to: {_selectedFolder}");
                }

                LogOperation($"Processing {dialog.FileNames.Length} selected files");
                StartOperationTimer("ProcessSelectedFiles");

                // Process files in background
                await Task.Run(() => ProcessSelectedFiles(dialog.FileNames));

                // Display results
                var totalFiles = _filesByExtension.Values.Sum(list => list.Count);
                LogOperation($"Total files after selection: {totalFiles}");

                // Update the files view
                DisplayFilesByFolder();

                EndOperationTimer("ProcessSelectedFiles");
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error selecting files: {ex.Message}");
            ErrorLogger.LogError(ex, "Selecting files");
        }
    }

    private void ProcessSelectedFiles(string[] filePaths)
    {
        foreach (var filePath in filePaths)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var ext = fileInfo.Extension.ToLowerInvariant();

                // Check if this extension is supported
                if (_settingsManager.Settings.SourceFileExtensions.Contains(ext))
                {
                    // Check file size limit
                    var fileSizeKb = (int)(fileInfo.Length / 1024);
                    if (fileSizeKb <= _settingsManager.Settings.MaxFileSizeKb)
                    {
                        // Get relative path (if file is not in the selected folder, it will use its full path)
                        string relativePath;
                        if (filePath.StartsWith(_selectedFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            relativePath = filePath.Substring(_selectedFolder.Length).TrimStart('\\', '/');
                        }
                        else
                        {
                            // If file is outside the base folder, use the full path
                            relativePath = filePath;
                        }

                        // Initialize the extension group if needed
                        if (!_filesByExtension.ContainsKey(ext))
                        {
                            _filesByExtension[ext] = new List<SourceFile>();
                        }

                        // Check if this file is already added
                        if (!_filesByExtension[ext].Any(f => f.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                        {
                            // Add the file
                            _filesByExtension[ext].Add(new SourceFile
                            {
                                Path = filePath,
                                RelativePath = relativePath,
                                Extension = ext,
                                Content = File.ReadAllText(filePath)
                            });

                            LogOperation($"Added file: {relativePath}");
                        }
                        else
                        {
                            LogOperation($"Skipped duplicate file: {relativePath}");
                        }
                    }
                    else
                    {
                        LogOperation($"Skipped file due to size limit: {filePath} ({fileSizeKb} KB > {_settingsManager.Settings.MaxFileSizeKb} KB)");
                    }
                }
                else
                {
                    LogOperation($"Skipped unsupported file type: {filePath}");
                }
            }
            catch (Exception ex)
            {
                LogOperation($"Error processing file {filePath}: {ex.Message}");
            }
        }
    }

    private void BtnClearFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ask for confirmation
            var result = MessageBox.Show(
                "Are you sure you want to clear all currently selected files?",
                "Clear Files",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Clear the file collections
                _filesByExtension.Clear();
                LvFiles.Items.Clear();
                _selectedFolder = string.Empty;
                TxtSelectedFolder.Text = string.Empty;

                LogOperation("File selection cleared");
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error clearing files: {ex.Message}");
            ErrorLogger.LogError(ex, "Clearing files");
        }
    }

    private void FindSourceFiles(string folderPath)
    {
        var dirInfo = new DirectoryInfo(folderPath);

        // Get all files with source extensions in this directory
        foreach (var file in dirInfo.GetFiles())
        {
            var ext = file.Extension.ToLowerInvariant();
            if (_settingsManager.Settings.SourceFileExtensions.Contains(ext))
            {
                // Check file size limit
                var fileSizeKb = (int)(file.Length / 1024);
                if (fileSizeKb <= _settingsManager.Settings.MaxFileSizeKb)
                {
                    if (!_filesByExtension.ContainsKey(ext))
                        _filesByExtension[ext] = new List<SourceFile>();

                    _filesByExtension[ext].Add(new SourceFile
                    {
                        Path = file.FullName,
                        RelativePath = file.FullName.Replace(_selectedFolder, "").TrimStart('\\', '/'),
                        Extension = ext,
                        Content = File.ReadAllText(file.FullName)
                    });
                }
                else
                {
                    var relativePath = file.FullName.Replace(_selectedFolder, "").TrimStart('\\', '/');
                    LogOperation($"Skipped file due to size limit: {relativePath} ({fileSizeKb} KB > {_settingsManager.Settings.MaxFileSizeKb} KB)");
                }
            }
        }

        // Recursively process subdirectories (except hidden and system folders)
        foreach (var dir in dirInfo.GetDirectories())
        {
            // Skip hidden directories, bin, obj, node_modules, etc.
            if (dir.Attributes.HasFlag(FileAttributes.Hidden) ||
                dir.Attributes.HasFlag(FileAttributes.System) ||
                dir.Name.StartsWith(".") ||
                dir.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                dir.Name.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                dir.Name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                dir.Name.Equals("packages", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            FindSourceFiles(dir.FullName);
        }
    }

    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        if (_filesByExtension.Count == 0)
        {
            MessageBox.Show("Please select a folder and scan for files first.", "No Files", MessageBoxButton.OK, MessageBoxImage.Warning);
            LogOperation("Analysis canceled: No files selected");
            return;
        }

        if (string.IsNullOrEmpty(TxtApiKey.Password))
        {
            MessageBox.Show("Please enter an API key.", "Missing API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            LogOperation("Analysis canceled: No API key provided");
            return;
        }

        var apiSelection = CboAiApi.SelectedItem?.ToString() ?? "Claude API";
        TxtStatus.Text = $"Analyzing with {apiSelection}...";
        LogOperation($"Starting code analysis with {apiSelection}");
        StartOperationTimer("CodeAnalysis");

        // Prepare consolidated code files
        LogOperation("Preparing consolidated code files");
        StartOperationTimer("PrepareFiles");
        var consolidatedFiles = PrepareConsolidatedFiles();
        EndOperationTimer("PrepareFiles");

        // Begin a new conversation
        _conversationHistory.Clear();
        LogOperation("Conversation history cleared");

        // Reset navigation controls
        _currentResponseIndex = -1;
        BtnPreviousResponse.IsEnabled = false;
        BtnNextResponse.IsEnabled = false;
        TxtResponseCounter.Text = "No responses";

        try
        {
            // Prepare the initial prompt to ask for code analysis
            LogOperation("Generating initial prompt");
            StartOperationTimer("GeneratePrompt");
            var initialPrompt = GenerateInitialPrompt(consolidatedFiles);
            EndOperationTimer("GeneratePrompt");
            LogOperation($"Initial prompt generated ({initialPrompt.Length} characters)");

            // Add user message to conversation history first
            _conversationHistory.Add(new ChatMessage { Role = "user", Content = initialPrompt });

            // Send it to selected API
            var response = await SendToAiApi(apiSelection, initialPrompt);

            // Add assistant response to conversation history
            _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = response });
            LogOperation("Conversation history updated");

            // Display the response
            LogOperation("Updating UI with response");
            UpdateResponseDisplay(response);

            // Enable follow-up questions
            TxtFollowupQuestion.IsEnabled = true;
            BtnSendFollowup.IsEnabled = true;
            ChkIncludeSelectedFiles.IsEnabled = true;
            BtnSaveResponse.IsEnabled = true;

            TxtStatus.Text = "Analysis complete!";
            EndOperationTimer("CodeAnalysis");
            LogOperation("Analysis workflow completed successfully");
        }
        catch (Exception ex)
        {
            LogOperation($"Error during analysis: {ex.Message}");
            ErrorLogger.LogError(ex, "Analyzing code");
            TxtStatus.Text = "Error during analysis.";
            EndOperationTimer("CodeAnalysis");
        }
    }

    private Dictionary<string, List<string>> PrepareConsolidatedFiles()
    {
        // This dictionary will hold file content grouped by extension
        var consolidatedFiles = new Dictionary<string, List<string>>();

        foreach (var ext in _filesByExtension.Keys)
        {
            var files = _filesByExtension[ext];

            if (!consolidatedFiles.ContainsKey(ext))
            {
                consolidatedFiles[ext] = new List<string>();
            }

            foreach (var file in files)
            {
                // Format each file with a header showing a relative path
                consolidatedFiles[ext].Add($"File: {file.RelativePath}\n```{GetLanguageForExtension(ext)}\n{file.Content}\n```\n");
            }
        }

        return consolidatedFiles;
    }

    private string GetLanguageForExtension(string ext)
    {
        return ext switch
        {
            ".cs" => "csharp",
            ".py" => "python",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".java" => "java",
            ".html" => "html",
            ".css" => "css",
            ".cpp" or ".h" or ".c" => "cpp",
            ".go" => "go",
            ".rb" => "ruby",
            ".php" => "php",
            ".swift" => "swift",
            ".kt" => "kotlin",
            ".rs" => "rust",
            ".dart" => "dart",
            ".xaml" => "xml",
            ".xml" => "xml",
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            ".md" => "markdown",
            _ => ""
        };
    }

    private string GenerateInitialPrompt(Dictionary<string, List<string>> consolidatedFiles)
    {
        var prompt = new StringBuilder();

        // Get the selected prompt content
        var promptTemplate = _settingsManager.Settings.InitialPrompt;

        // Use the template as the basis for the prompt
        prompt.AppendLine(promptTemplate);
        prompt.AppendLine();

        // Include files, grouped by extension
        foreach (var ext in consolidatedFiles.Keys)
        {
            prompt.AppendLine($"--- {ext.ToUpperInvariant()} FILES ---");
            prompt.AppendLine();

            foreach (var fileContent in consolidatedFiles[ext])
            {
                prompt.AppendLine(fileContent);
                prompt.AppendLine();
            }
        }

        return prompt.ToString();
    }

    private async void BtnSendFollowup_Click(object sender, RoutedEventArgs e)
    {
        var followupQuestion = TxtFollowupQuestion.Text;

        if (string.IsNullOrWhiteSpace(followupQuestion))
        {
            MessageBox.Show("Please enter a follow-up question.", "Empty Question", MessageBoxButton.OK, MessageBoxImage.Warning);
            LogOperation("Follow-up canceled: Empty question");
            return;
        }

        var apiSelection = CboAiApi.SelectedItem?.ToString() ?? "Claude API";
        TxtStatus.Text = $"Sending follow-up question to {apiSelection}...";
        LogOperation($"Sending follow-up question to {apiSelection}");
        StartOperationTimer("FollowupQuestion");

        try
        {
            // Create an enhanced follow-up prompt with context
            var enhancedPrompt = GenerateContextualFollowupPrompt(followupQuestion);

            // Check if we should include selected files
            if (ChkIncludeSelectedFiles.IsChecked == true && LvFiles.SelectedItems.Count > 0)
            {
                // Add information about selected files to the prompt
                enhancedPrompt = AppendSelectedFilesToPrompt(enhancedPrompt);
            }

            // Add the user question to the conversation history first
            _conversationHistory.Add(new ChatMessage { Role = "user", Content = followupQuestion });
            LogOperation("Added follow-up question to conversation history");

            // Send the enhanced prompt to selected API
            var response = await SendToAiApi(apiSelection, enhancedPrompt);

            // Add the assistant response to conversation history
            _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = response });
            LogOperation("Added assistant response to conversation history");

            // Display the response (this will also auto-save it)
            LogOperation("Updating UI with follow-up response");
            UpdateResponseDisplay(response);

            // Clear the follow-up question text box
            TxtFollowupQuestion.Text = "";

            TxtStatus.Text = "Follow-up response received!";
            EndOperationTimer("FollowupQuestion");
            LogOperation("Follow-up question workflow completed");
        }
        catch (Exception ex)
        {
            LogOperation($"Error sending follow-up question: {ex.Message}");
            ErrorLogger.LogError(ex, "Sending follow-up question");
            TxtStatus.Text = "Error sending follow-up question.";
            EndOperationTimer("FollowupQuestion");
        }
    }

    /// <summary>
    /// Appends content of the selected files to the prompt
    /// </summary>
    /// <param name="originalPrompt">The original prompt text</param>
    /// <returns>Enhanced prompt with selected file contents</returns>
    private string AppendSelectedFilesToPrompt(string originalPrompt)
    {
        var promptBuilder = new StringBuilder(originalPrompt);

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("--- SELECTED FILES FOR REFERENCE ---");
        promptBuilder.AppendLine();

        var selectedFileNames = new List<string>();
        var selectedFilesContent = new StringBuilder();
        var fileCount = 0;

        foreach (var item in LvFiles.SelectedItems)
        {
            if (item is string fileName && !fileName.StartsWith("=====") && !fileName.TrimStart().StartsWith("("))
            {
                // Handle file entries (not folder or summary headers)
                // Trim any leading spaces or + signs used for displaying in the list
                var cleanFileName = fileName.TrimStart(' ', '+');

                // Find the matching file in our file collection
                foreach (var extensionFiles in _filesByExtension.Values)
                {
                    var matchingFile = extensionFiles.FirstOrDefault(f =>
                        Path.GetFileName(f.RelativePath).Equals(cleanFileName, StringComparison.OrdinalIgnoreCase));

                    if (matchingFile != null)
                    {
                        fileCount++;
                        selectedFileNames.Add(matchingFile.RelativePath);

                        // Append file content in a code block with language identification
                        selectedFilesContent.AppendLine($"File: {matchingFile.RelativePath}");
                        selectedFilesContent.AppendLine($"```{GetLanguageForExtension(matchingFile.Extension)}");
                        selectedFilesContent.AppendLine(matchingFile.Content);
                        selectedFilesContent.AppendLine("```");
                        selectedFilesContent.AppendLine();

                        // Break out of the inner loop once file is found
                        break;
                    }
                }
            }
        }

        // Add summary of selected files
        promptBuilder.AppendLine($"I've included {fileCount} selected files for your reference:");
        foreach (var fileName in selectedFileNames)
        {
            promptBuilder.AppendLine($"- {fileName}");
        }

        promptBuilder.AppendLine();

        // Add the file contents
        promptBuilder.Append(selectedFilesContent);

        // Log the operation
        LogOperation($"Included {fileCount} selected files in follow-up question");

        return promptBuilder.ToString();
    }

    private string GenerateContextualFollowupPrompt(string followupQuestion)
    {
        // Check if there are previous messages
        if (_conversationHistory.Count < 2)
        {
            // If no previous conversation, just return the follow-up question as is
            return followupQuestion;
        }

        var promptBuilder = new StringBuilder();

        // Add context about this being a follow-up question about previously analyzed code
        promptBuilder.AppendLine("This is a follow-up question regarding the source code I previously shared with you. Please reference the code files from our earlier discussion when responding.");
        promptBuilder.AppendLine();

        // List a few files analyzed to provide better context
        promptBuilder.AppendLine("The previous analysis covered files including:");

        // Get up to 5 representative files from the analyzed code
        var filesList = new List<string>();
        foreach (var ext in _filesByExtension.Keys)
        {
            foreach (var file in _filesByExtension[ext].Take(2)) // Take up to 2 files per extension
            {
                filesList.Add(file.RelativePath);
                if (filesList.Count >= 5) break; // Limit to 5 total files
            }

            if (filesList.Count >= 5) break;
        }

        // Add file names to the prompt
        foreach (var file in filesList)
        {
            promptBuilder.AppendLine($"- {file}");
        }

        // Add the total file count for context
        var totalFiles = _filesByExtension.Values.Sum(list => list.Count);
        promptBuilder.AppendLine($"And {totalFiles - filesList.Count} more files.");
        promptBuilder.AppendLine();

        // Finally, add the actual follow-up question
        promptBuilder.AppendLine("My follow-up question is:");
        promptBuilder.AppendLine(followupQuestion);

        return promptBuilder.ToString();
    }

    private void BtnSaveResponse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_currentResponseText))
            {
                MessageBox.Show("There is no response to save.", "No Response", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Create a save file dialog
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "md", // Default to markdown since the responses are markdown formatted
                Title = "Save AI Analysis Response"
            };

            // If the user has selected a project folder, suggest that as the initial directory
            if (!string.IsNullOrEmpty(_selectedFolder) && Directory.Exists(_selectedFolder))
            {
                saveFileDialog.InitialDirectory = _selectedFolder;

                // Suggest a filename based on the project folder name and timestamp
                var folderName = new DirectoryInfo(_selectedFolder).Name;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                saveFileDialog.FileName = $"{folderName}_analysis_{timestamp}.md";
            }
            else
            {
                // Default filename with timestamp if no folder is selected
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                saveFileDialog.FileName = $"ai_analysis_{timestamp}.md";
            }

            // Show the dialog and get result
            var result = saveFileDialog.ShowDialog();

            // If the user clicked OK, save the file
            if (result == true)
            {
                // Save the response text to the selected file
                File.WriteAllText(saveFileDialog.FileName, _currentResponseText);
                MessageBox.Show($"Response saved to {saveFileDialog.FileName}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Saving response to file");
            MessageBox.Show("An error occurred while saving the response.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnToggleMarkdown_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isMarkdownViewActive = !_isMarkdownViewActive;

            if (_isMarkdownViewActive)
            {
                // Switch to Markdown view
                TxtResponse.Visibility = Visibility.Collapsed;
                MarkdownScrollViewer.Visibility = Visibility.Visible;
                BtnToggleMarkdown.Content = "Show Raw Text";

                // Set the markdown content
                MarkdownViewer.Markdown = _currentResponseText;

                // Update the page width
                UpdateMarkdownPageWidth();
            }
            else
            {
                // Switch to raw text view
                TxtResponse.Visibility = Visibility.Visible;
                MarkdownScrollViewer.Visibility = Visibility.Collapsed;
                BtnToggleMarkdown.Content = "Show Markdown";
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error toggling markdown view: {ex.Message}");
            ErrorLogger.LogError(ex, "Toggling markdown view");
            MessageBox.Show("An error occurred while toggling the markdown view.", "View Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Revert to raw text as a fallback
            TxtResponse.Visibility = Visibility.Visible;
            MarkdownScrollViewer.Visibility = Visibility.Collapsed;
            _isMarkdownViewActive = false;
        }
    }

    private void MarkdownScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // When the ScrollViewer size changes, update the page width
        UpdateMarkdownPageWidth();
    }

    private void UpdateMarkdownPageWidth()
    {
        try
        {
            // If we can access the internal FlowDocument, set its properties
            if (MarkdownViewer.Document != null)
            {
                // Calculate 90% of the available width
                var containerWidth = MarkdownScrollViewer.ActualWidth;
                var contentWidth = Math.Max(800, containerWidth * 0.9); // Use at least 800 px or 90% of container

                MarkdownViewer.Document.PageWidth = contentWidth;
                MarkdownViewer.Document.PagePadding = new Thickness(0);
                MarkdownViewer.Document.TextAlignment = TextAlignment.Left;

                LogOperation($"Updated Markdown page width to {contentWidth:F0}px (90% of {containerWidth:F0}px)");
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't crash the application
            LogOperation($"Error setting Markdown document properties: {ex.Message}");
        }
    }

    /// <summary>
    /// Populate the model dropdown based on the selected provider
    /// </summary>
    private void PopulateModelDropdown()
    {
        CboModel.Items.Clear();

        if (CboAiApi.SelectedItem == null)
            return;

        var providerName = CboAiApi.SelectedItem.ToString();

        try
        {
            // Only populate models for providers that support model selection
            if (providerName == "DeepSeek API")
            {
                var provider = (DeepSeek)_apiProviderFactory.GetProvider(providerName);
                var models = provider.GetAvailableModels();

                foreach (var model in models)
                {
                    CboModel.Items.Add(new ModelDropdownItem
                    {
                        DisplayText = model.Name,
                        ModelId = model.Id,
                        Description = model.Description
                    });
                }

                CboModel.IsEnabled = true;
                CboModel.SelectedIndex = 0;

                // Show tooltip with model description
                CboModel.ToolTip = ((ModelDropdownItem)CboModel.SelectedItem).Description;
            }
            else if (providerName == "Claude API")
            {
                var provider = (Claude)_apiProviderFactory.GetProvider(providerName);
                var models = provider.GetAvailableModels();

                foreach (var model in models)
                {
                    CboModel.Items.Add(new ModelDropdownItem
                    {
                        DisplayText = model.Name,
                        ModelId = model.Id,
                        Description = model.Description
                    });
                }

                CboModel.IsEnabled = true;
                CboModel.SelectedIndex = 0;

                // Show tooltip with model description
                CboModel.ToolTip = ((ModelDropdownItem)CboModel.SelectedItem).Description;
            }
            else if (providerName == "Grok API")
            {
                var provider = (Grok)_apiProviderFactory.GetProvider(providerName);
                var models = provider.GetAvailableModels();

                foreach (var model in models)
                {
                    CboModel.Items.Add(new ModelDropdownItem
                    {
                        DisplayText = model.Name,
                        ModelId = model.Id,
                        Description = model.Description
                    });
                }

                CboModel.IsEnabled = true;
                CboModel.SelectedIndex = 0;

                // Show tooltip with model description
                CboModel.ToolTip = ((ModelDropdownItem)CboModel.SelectedItem).Description;
            }
            else if (providerName == "Gemini API")
            {
                var provider = (Gemini)_apiProviderFactory.GetProvider(providerName);
                var models = provider.GetAvailableModels();

                foreach (var model in models)
                {
                    CboModel.Items.Add(new ModelDropdownItem
                    {
                        DisplayText = model.Name,
                        ModelId = model.Id,
                        Description = model.Description
                    });
                }

                CboModel.IsEnabled = true;
                CboModel.SelectedIndex = 0;

                // Show tooltip with model description
                CboModel.ToolTip = ((ModelDropdownItem)CboModel.SelectedItem).Description;
            }
            else
            {
                // For other providers, disable the model dropdown
                CboModel.IsEnabled = false;
                CboModel.Items.Add("Default model");
                CboModel.SelectedIndex = 0;
                CboModel.ToolTip = null;
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error populating model dropdown: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle selection change in the API provider dropdown
    /// </summary>
    private void CboAiApi_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboAiApi.SelectedItem != null)
        {
            var apiSelection = CboAiApi.SelectedItem.ToString() ?? string.Empty;
            UpdatePreviousKeys(apiSelection);
            PopulateModelDropdown(); // Add this line to existing method
        }
    }

    /// <summary>
    /// Handle selection change in the model dropdown
    /// </summary>
    private void CboModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboModel.SelectedItem is ModelDropdownItem selectedModel)
        {
            // Update tooltip with model description
            CboModel.ToolTip = selectedModel.Description;
        }
    }

    private async Task<string> SendToAiApi(string apiSelection, string prompt)
    {
        try
        {
            // Log the request
            LogOperation($"Preparing request to {apiSelection}");

            // Get the selected provider
            var provider = _apiProviderFactory.GetProvider(apiSelection);

            // Start timer for API request
            StartOperationTimer($"ApiRequest-{apiSelection}");
            LogOperation($"Sending prompt to {apiSelection} ({prompt.Length} characters)");

            // Get selected model ID for providers that support model selection
            string? modelId = null;
            if (CboModel.IsEnabled && CboModel.SelectedItem is ModelDropdownItem selectedModel)
            {
                modelId = selectedModel.ModelId;
                LogOperation($"Using model: {selectedModel.DisplayText} ({modelId})");
            }

            // Send the prompt and return the response
            string response;

            // Special handling for providers with model selection
            if (provider is DeepSeek deepSeekProvider && modelId != null)
            {
                response = await deepSeekProvider.SendPromptWithModelAsync(TxtApiKey.Password, prompt, _conversationHistory, modelId);
            }
            else if (provider is Claude claudeProvider && modelId != null)
            {
                response = await claudeProvider.SendPromptWithModelAsync(TxtApiKey.Password, prompt, _conversationHistory, modelId);
            }
            else if (provider is Grok grokProvider && modelId != null)
            {
                response = await grokProvider.SendPromptWithModelAsync(TxtApiKey.Password, prompt, _conversationHistory, modelId);
            }
            else if (provider is Gemini geminiProvider && modelId != null)
            {
                response = await geminiProvider.SendPromptWithModelAsync(TxtApiKey.Password, prompt, _conversationHistory, modelId);
            }
            else
            {
                response = await provider.SendPromptWithModelAsync(TxtApiKey.Password, prompt, _conversationHistory);
            }

            // Log response received
            EndOperationTimer($"ApiRequest-{apiSelection}");
            LogOperation($"Received response from {apiSelection} ({response.Length} characters)");

            return response;
        }
        catch (Exception ex)
        {
            LogOperation($"Error calling {apiSelection} API: {ex.Message}");
            ErrorLogger.LogError(ex, $"Sending prompt to {apiSelection}");
            throw; // Re-throw to let the caller handle it
        }
    }

    /// <summary>
    /// Navigates to a specific response in the conversation history
    /// </summary>
    private void NavigateToResponse(int index)
    {
        // Get all assistant responses from the conversation history
        var assistantResponses = _conversationHistory
            .Where(m => m.Role == "assistant")
            .ToList();

        // Ensure index is within bounds
        if (assistantResponses.Count == 0 || index < 0 || index >= assistantResponses.Count)
        {
            return;
        }

        // Get the response at the specified index
        var response = assistantResponses[index].Content;

        // Update the current index
        _currentResponseIndex = index;

        // Update navigation buttons
        UpdateNavigationControls();

        // Display the selected response
        _currentResponseText = response;

        // Update both text controls without adding to history
        TxtResponse.Text = response;
        MarkdownViewer.Markdown = response;

        // Update page width for markdown
        UpdateMarkdownPageWidth();

        // Update the message counter
        UpdateMessageCounter();

        LogOperation($"Navigated to response #{index + 1} of {assistantResponses.Count}");
    }

    /// <summary>
    /// Updates the message counter display
    /// </summary>
    private void UpdateMessageCounter()
    {
        var totalResponses = _conversationHistory.Count(m => m.Role == "assistant");

        if (totalResponses == 0)
        {
            TxtResponseCounter.Text = "No responses";
        }
        else
        {
            // Display as 1-based index for the user (1 of 3 instead of 0 of 2)
            TxtResponseCounter.Text = $"Response {_currentResponseIndex + 1} of {totalResponses}";
        }
    }

    /// <summary>
    /// Updates the enabled state of navigation buttons
    /// </summary>
    private void UpdateNavigationControls()
    {
        var totalResponses = _conversationHistory.Count(m => m.Role == "assistant");

        // Enable/disable previous button
        BtnPreviousResponse.IsEnabled = _currentResponseIndex > 0;

        // Enable/disable next button
        BtnNextResponse.IsEnabled = _currentResponseIndex < totalResponses - 1;
    }

    /// <summary>
    /// Handler for the Previous Response button
    /// </summary>
    private void BtnPreviousResponse_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResponseIndex > 0)
        {
            NavigateToResponse(_currentResponseIndex - 1);
        }
    }

    /// <summary>
    /// Handler for the Next Response button
    /// </summary>
    private void BtnNextResponse_Click(object sender, RoutedEventArgs e)
    {
        var totalResponses = _conversationHistory.Count(m => m.Role == "assistant");

        if (_currentResponseIndex < totalResponses - 1)
        {
            NavigateToResponse(_currentResponseIndex + 1);
        }
    }

    /// <summary>
    /// Updates the response display and configures navigation
    /// </summary>
    private void UpdateResponseDisplay(string responseText)
    {
        // Store the response text
        _currentResponseText = responseText;

        // Update the text display
        TxtResponse.Text = responseText;

        // Update markdown view (now the default)
        MarkdownViewer.Markdown = responseText;

        // Update the page width based on the current container size
        UpdateMarkdownPageWidth();

        // Enable the markdown toggle button
        BtnToggleMarkdown.IsEnabled = true;
        BtnSaveResponse.IsEnabled = true;

        // Calculate the current response index (count of assistant messages - 1)
        var assistantResponses = _conversationHistory.Count(m => m.Role == "assistant");

        // This will be the index of the response we're about to add
        _currentResponseIndex = assistantResponses;

        // Auto-save the response
        AutoSaveResponse(responseText, _currentResponseIndex);

        // Update navigation controls
        UpdateNavigationControls();

        // Update message counter
        UpdateMessageCounter();
    }

    /// <summary>
    /// Loads available prompt templates into the dropdown
    /// </summary>
    private void LoadPromptTemplates()
    {
        CboPromptTemplate.ItemsSource = null;
        CboPromptTemplate.ItemsSource = _settingsManager.Settings.CodePrompts;

        // Select the current prompt
        if (!string.IsNullOrEmpty(_settingsManager.Settings.SelectedPromptName))
        {
            var selectedPrompt = _settingsManager.Settings.CodePrompts.FirstOrDefault(p =>
                p.Name == _settingsManager.Settings.SelectedPromptName);

            if (selectedPrompt != null)
            {
                CboPromptTemplate.SelectedItem = selectedPrompt;
            }
            else if (_settingsManager.Settings.CodePrompts.Count > 0)
            {
                CboPromptTemplate.SelectedIndex = 0;
            }
        }
        else if (_settingsManager.Settings.CodePrompts.Count > 0)
        {
            CboPromptTemplate.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Event handler for prompt template selection change
    /// </summary>
    private void CboPromptTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboPromptTemplate.SelectedItem is CodePrompt selectedPrompt)
        {
            _settingsManager.Settings.SelectedPromptName = selectedPrompt.Name;
            _settingsManager.SaveSettings();
            LogOperation($"Selected prompt template: {selectedPrompt.Name}");
        }
    }

    /// <summary>
    /// Event handler for Configure Prompts button
    /// </summary>
    private void BtnConfigurePrompts_Click(object sender, RoutedEventArgs e)
    {
        OpenConfigurationWindow();
    }

    /// <summary>
    /// Opens the configuration window with focus on prompt settings
    /// </summary>
    private void OpenConfigurationWindow()
    {
        try
        {
            var configWindow = new ConfigurationWindow(_settingsManager)
            {
                Owner = this
            };

            var result = configWindow.ShowDialog();

            if (result == true)
            {
                // Settings were saved, update any necessary UI
                LogOperation("Settings updated");
                LoadPromptTemplates();
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error opening configuration window: {ex.Message}");
            ErrorLogger.LogError(ex, "Opening configuration window");
        }
    }

    /// <summary>
    /// Automatically saves an AI response to the AiOutput folder
    /// </summary>
    /// <param name="responseText">The AI response text to save</param>
    /// <param name="responseIndex">The index of this response in the conversation</param>
    private void AutoSaveResponse(string responseText, int responseIndex)
    {
        try
        {
            // Create the AiOutput directory if it doesn't exist
            var outputDirectory = Path.Combine(AppContext.BaseDirectory, "AiOutput");
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                LogOperation($"Created AiOutput directory at {outputDirectory}");
            }

            // Generate a filename based on project name (if available) and timestamp
            var projectName = "unknown";
            if (!string.IsNullOrEmpty(_selectedFolder))
            {
                projectName = new DirectoryInfo(_selectedFolder).Name;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"{projectName}_response_{responseIndex + 1}_{timestamp}.md";
            var filePath = Path.Combine(outputDirectory, filename);

            // Save the response
            File.WriteAllText(filePath, responseText);
            LogOperation($"Auto-saved response #{responseIndex + 1} to {filename}");
        }
        catch (Exception ex)
        {
            // Log error but don't interrupt the user experience
            LogOperation($"Error auto-saving response: {ex.Message}");
        }
    }
}