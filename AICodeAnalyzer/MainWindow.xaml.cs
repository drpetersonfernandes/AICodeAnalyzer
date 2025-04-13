using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AICodeAnalyzer.AIProvider;
using AICodeAnalyzer.Models;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

namespace AICodeAnalyzer;

public partial class MainWindow
{
    private readonly ApiKeyManager _keyManager = new();
    private readonly Dictionary<string, List<SourceFile>> _filesByExtension = new();
    private readonly ApiProviderFactory _apiProviderFactory = new();
    private readonly Dictionary<string, DateTime> _operationTimers = new();
    private readonly RecentFilesManager _recentFilesManager = new();
    private readonly List<ChatMessage> _conversationHistory = new();
    private readonly DispatcherTimer _statusUpdateTimer = new();
    private readonly SettingsManager _settingsManager = new();
    private readonly string[] _loadingDots = { ".", "..", "...", "....", "....." };
    private int _currentResponseIndex = -1;
    private string _selectedFolder = string.Empty;
    private bool _isMarkdownViewActive = true;
    private string _currentResponseText = string.Empty;
    private bool _isProcessing;
    private int _dotsIndex;
    private string _baseStatusMessage = string.Empty;
    private readonly double _textBoxDefaultFontSize;
    private string _currentFilePath = string.Empty;
    private bool _isShowingInputQuery; // Flag to track if the input query is shown
    private string _previousMarkdownContent = string.Empty; // Store Markdown content before showing input
    private string _lastInputPrompt = string.Empty; // Store the last prompt sent
    private readonly List<SourceFile> _lastIncludedFiles = new(); // Store files included in the last prompt
    private readonly TokenCounterService _tokenCounterService = new();
    private TokenCalculationResult _tokenCalculationResult = new();

    // Zoom variables
    private const double ZoomIncrement = 10.0; // Zoom step (10%)
    private const double MinZoom = 20.0; // Minimum zoom level (20%)
    private const double MaxZoom = 500.0; // Maximum zoom level (500%)
    private double _markdownZoomLevel = 100.0; // Current zoom level (default 100%)

    public MainWindow()
    {
        InitializeComponent();

        _textBoxDefaultFontSize = TxtResponse.FontSize;
        TxtFollowupQuestion.IsEnabled = true;
        IncludeSelectedFilesChecker.IsEnabled = true;

        // Populate API dropdown using provider names from the factory
        var providers = _apiProviderFactory.AllProviders
            .OrderBy(p => p.Name)
            .ToList();

        foreach (var provider in providers)
        {
            AiProvider.Items.Add(provider.Name);
        }

        AiProvider.SelectedIndex = -1; // Default to none

        UpdateRecentFilesMenu();

        // Check if a startup file path was passed from App.xaml.cs
        if (Application.Current.Properties["StartupFilePath"] is string filePath)
        {
            // Load the file asynchronously, but don't await here since we're in the constructor
            Dispatcher.InvokeAsync(async () => { await LoadMarkdownFileAsync(filePath); });
        }

        LogOperation("Application started");
        LoadPromptTemplates();
        UpdateZoomDisplay();
        SetupStatusUpdateTimer();
    }

    private async Task LoadMarkdownFileAsync(string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                LogOperation($"Invalid file path: {filePath}");
                MessageBox.Show("The specified file does not exist.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            // Show loading indicator
            SetProcessingState(true, $"Loading {Path.GetFileName(filePath)}");

            // Read the file on background thread
            var fileContent = await Task.Run(() => File.ReadAllText(filePath));

            // Update UI on UI thread
            await Dispatcher.InvokeAsync(() =>
            {
                _currentResponseText = fileContent;
                TxtResponse.Text = fileContent;
                MarkdownViewer.Markdown = PreprocessMarkdown(fileContent);
                TxtResponseCounter.Text = $"Viewing: {Path.GetFileName(filePath)}";
                BtnToggleMarkdown.IsEnabled = true;
                BtnSaveResponse.IsEnabled = true;
                BtnSaveEdits.IsEnabled = true; // Enable edit saving

                // Disable input query button when loading a file
                BtnShowInputQuery.IsEnabled = false;
                _isShowingInputQuery = false;
                BtnShowInputQuery.Content = "Show Input Query";

                // Store the current file path
                _currentFilePath = filePath;

                // Update UI to reflect we're viewing a standalone file
                TxtStatus.Text = $"Viewing file: {Path.GetFileName(filePath)}";

                // Add to recent files
                AddToRecentFiles(filePath);

                LogOperation($"Loaded markdown file: {filePath}");
            });
        }
        catch (Exception ex)
        {
            LogOperation($"Error loading markdown file: {ex.Message}");
            ErrorLogger.LogError(ex, $"Loading markdown file: {filePath}");
            MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetProcessingState(false);
        }
    }


    private void AddToRecentFiles(string filePath)
    {
        _recentFilesManager.AddRecentFile(filePath);
        UpdateRecentFilesMenu();
        LogOperation($"Added file to recent files: {Path.GetFileName(filePath)}");
    }

    private void UpdateRecentFilesMenu()
    {
        // Clear current recent files menu items
        MenuRecentFiles.Items.Clear();

        var recentFiles = _recentFilesManager.GetRecentFiles();

        if (recentFiles.Count == 0)
        {
            var noRecentFilesItem = new MenuItem { Header = "(No recent files)", IsEnabled = false };
            MenuRecentFiles.Items.Add(noRecentFilesItem);
        }
        else
        {
            // Add each recent file to the menu
            foreach (var filePath in recentFiles)
            {
                var menuItem = new MenuItem
                {
                    Header = Path.GetFileName(filePath),
                    ToolTip = filePath,
                    Tag = filePath,
                    // Add document icon to each menu item
                    Icon = new TextBlock { Text = "📄", FontSize = 14 }
                };

                menuItem.Click += RecentFileMenuItem_Click;
                MenuRecentFiles.Items.Add(menuItem);
            }

            // Add separator and "Clear Recent Files" option
            MenuRecentFiles.Items.Add(new Separator());

            var clearMenuItem = new MenuItem
            {
                Header = "Clear Recent Files",
                Icon = new TextBlock { Text = "🗑️", FontSize = 14 } // Trash icon for a clear option
            };
            clearMenuItem.Click += ClearRecentFiles_Click;
            MenuRecentFiles.Items.Add(clearMenuItem);
        }
    }

    private async void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string filePath)
            {
                await LoadMarkdownFileAsync(filePath);

                // Ensure the input query button is disabled after loading
                BtnShowInputQuery.IsEnabled = false;
                _isShowingInputQuery = false;
                BtnShowInputQuery.Content = "Show Input Query";
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Error opening recent file");
        }
    }

    private void ClearRecentFiles_Click(object sender, RoutedEventArgs e)
    {
        _recentFilesManager.ClearRecentFiles();
        UpdateRecentFilesMenu();
        LogOperation("Cleared recent files list");
    }

    private void SetupStatusUpdateTimer()
    {
        _statusUpdateTimer.Interval = TimeSpan.FromMilliseconds(300);
        _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;
    }

    private void StatusUpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isProcessing) return;

        // Update the status text with animated dots
        _dotsIndex = (_dotsIndex + 1) % _loadingDots.Length;
        TxtStatus.Text = $"{_baseStatusMessage}{_loadingDots[_dotsIndex]}";
    }

    private void SetProcessingState(bool isProcessing, string statusMessage = "")
    {
        if (isProcessing == _isProcessing) return;

        _isProcessing = isProcessing;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (isProcessing)
            {
                // Store the base message for the animation
                _baseStatusMessage = statusMessage;
                _dotsIndex = 0;

                // Start the timer for the animation
                _statusUpdateTimer.Start();

                // Only show wait cursor to indicate processing, don't disable buttons
                // (buttons are disabled individually in each operation)
                Mouse.OverrideCursor = Cursors.Wait;

                // Show "Processing..." text in the Markdown viewer if it's empty
                if (string.IsNullOrWhiteSpace(MarkdownViewer.Markdown))
                {
                    TxtResponse.Text = "Processing...";
                    MarkdownViewer.Markdown =
                        "## Processing request...\n\nPlease wait while the operation completes. The UI will remain responsive.";
                }
            }
            else
            {
                // Stop the timer
                _statusUpdateTimer.Stop();

                // Clear the wait cursor
                Mouse.OverrideCursor = null;
            }
        }));
    }

    private void UpdateZoomDisplay()
    {
        // Clamp the zoom level within min/max bounds
        _markdownZoomLevel = Math.Max(MinZoom, Math.Min(MaxZoom, _markdownZoomLevel));

        if (MarkdownViewer != null)
        {
            // Apply zoom to Markdown view using ScaleTransform
            MarkdownViewer.LayoutTransform = new ScaleTransform(
                _markdownZoomLevel / 100.0, // X scale factor
                _markdownZoomLevel / 100.0 // Y scale factor
            );
        }

        if (TxtResponse != null)
        {
            // For raw text view, adjust font size based on zoom level
            TxtResponse.FontSize = _textBoxDefaultFontSize * (_markdownZoomLevel / 100.0);
        }

        // Update the TextBlock display
        TxtZoomLevel.Text = $"{_markdownZoomLevel:F0}%";

        // When zooming, we might want to update the page width as well
        UpdateMarkdownPageWidth();
    }

    private void TxtResponse_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Check if Ctrl key is pressed for zooming
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Delta > 0) // Wheel scrolled up (Zoom In)
            {
                ZoomIn();
            }
            else if (e.Delta < 0) // Wheel scrolled down (Zoom Out)
            {
                ZoomOut();
            }

            e.Handled = true; // Prevent default scroll behavior when zooming
        }
    }

    private void PreserveTextBoxScrollPosition(TextBox textBox, Action action)
    {
        if (textBox == null) return;

        // Store current caret position and scroll position
        var caretIndex = textBox.CaretIndex;
        double verticalOffset = 0;

        // Get the scroll viewer within the TextBox
        var scrollViewer = FindVisualChild<ScrollViewer>(textBox);
        if (scrollViewer != null)
        {
            verticalOffset = scrollViewer.VerticalOffset;
        }

        // Perform the action (e.g., changing zoom)
        action();

        // Restore caret position
        if (caretIndex >= 0 && caretIndex <= textBox.Text.Length)
        {
            textBox.CaretIndex = caretIndex;
        }

        // Schedule scroll position restore (needs to be delayed a bit)
        Dispatcher.BeginInvoke(new Action(() => { scrollViewer?.ScrollToVerticalOffset(verticalOffset); }),
            DispatcherPriority.Loaded);
    }

    private void ZoomIn()
    {
        if (_isMarkdownViewActive)
        {
            _markdownZoomLevel += ZoomIncrement;
            UpdateZoomDisplay();
        }
        else
        {
            // For text view, preserve scroll position
            PreserveTextBoxScrollPosition(TxtResponse, () =>
            {
                _markdownZoomLevel += ZoomIncrement;
                UpdateZoomDisplay();
            });
        }
    }

    private void ZoomOut()
    {
        if (_isMarkdownViewActive)
        {
            _markdownZoomLevel -= ZoomIncrement;
            UpdateZoomDisplay();
        }
        else
        {
            // For text view, preserve scroll position
            PreserveTextBoxScrollPosition(TxtResponse, () =>
            {
                _markdownZoomLevel -= ZoomIncrement;
                UpdateZoomDisplay();
            });
        }
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        ZoomIn();
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        ZoomOut();
    }

    private void BtnResetZoom_Click(object sender, RoutedEventArgs e)
    {
        if (_isMarkdownViewActive)
        {
            _markdownZoomLevel = 100.0; // Reset to default
            UpdateZoomDisplay();
        }
        else
        {
            // For text view, preserve scroll position
            PreserveTextBoxScrollPosition(TxtResponse, () =>
            {
                _markdownZoomLevel = 100.0; // Reset to default
                UpdateZoomDisplay();
            });
        }
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

    private void LogOperation(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var logEntry = $"[{timestamp}] {message}\r\n";

        // Add to log TextBox
        Dispatcher.Invoke(() =>
        {
            TxtLog.AppendText(logEntry);
            TxtLog.ScrollToEnd();

            // Limit log size (optional)
            if (TxtLog.Text.Length > 50000)
            {
                TxtLog.Text = TxtLog.Text[^40000..];
            }
        });
    }

    private void StartOperationTimer(string operationName)
    {
        _operationTimers[operationName] = DateTime.Now;
        LogOperation($"Started: {operationName}");
    }

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

    private void MarkdownScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Check if Ctrl key is pressed for zooming
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Delta > 0) // Wheel scrolled up (Zoom In)
            {
                ZoomIn();
            }
            else if (e.Delta < 0) // Wheel scrolled down (Zoom Out)
            {
                ZoomOut();
            }

            e.Handled = true; // Prevent default scroll behavior when zooming
        }
        else
        {
            // Example: Letting MarkdownViewer handle it (remove custom scroll)
            if (sender is ScrollViewer && MarkdownViewer != null)
            {
                // Let the event bubble down to the MarkdownViewer
            }

            e.Handled = false; // Allow normal scrolling
        }
    }

    private void UpdatePreviousKeys(string apiProvider)
    {
        AiProviderKeys.Items.Clear();
        AiProviderKeys.Items.Add("Select a key");

        var savedKeys = _keyManager.GetKeysForProvider(apiProvider);
        foreach (var key in savedKeys)
        {
            AiProviderKeys.Items.Add(MaskKey(key));
        }

        AiProviderKeys.SelectedIndex = 0;
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 8)
            return "*****";

        return string.Concat(key.AsSpan(0, 3), "*****", key.AsSpan(key.Length - 3));
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
                // Update UI immediately to show selected folder
                _selectedFolder = dialog.FileName;
                TxtSelectedFolder.Text = _selectedFolder;
                TxtStatus.Text = "Scanning folder for source files...";

                // Disable relevant buttons before starting the operation
                BtnSelectFolder.IsEnabled = false;
                BtnSelectFiles.IsEnabled = false;
                BtnClearFiles.IsEnabled = false;
                BtnSendQuery.IsEnabled = false;

                // Clear collections
                _filesByExtension.Clear();

                // Clear UI
                ListOfFiles.Items.Clear();
                LogOperation($"Starting folder scan: {_selectedFolder}");
                StartOperationTimer("FolderScan");

                // Scan files
                await FindSourceFilesAsync(_selectedFolder);

                // Update UI with results
                var totalFiles = _filesByExtension.Values.Sum(list => list.Count);
                TxtStatus.Text = $"Found {totalFiles} source files.";

                // Display files organized by folder
                DisplayFilesByFolder();
                EndOperationTimer("FolderScan");
                CalculateTotalTokens();
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Opening folder selection dialog");
            TxtStatus.Text = "Error selecting folder.";
        }
        finally
        {
            // Re-enable buttons
            BtnSelectFolder.IsEnabled = true;
            BtnSelectFiles.IsEnabled = true;
            BtnClearFiles.IsEnabled = true;
            BtnSendQuery.IsEnabled = true;
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
                if (!filesByFolder.TryGetValue(folderPath, out var value))
                {
                    value = new List<SourceFile>();
                    filesByFolder[folderPath] = value;
                }

                value.Add(file);
            }
        }

        // Clear the list before adding items
        ListOfFiles.Items.Clear();

        // Add mode indicator (folder scan or manual selection)
        ListOfFiles.Items.Add(new ListViewItem
        {
            Content = $"===== Files Summary ({_filesByExtension.Values.Sum(v => v.Count)} total) =====",
            FontWeight = FontWeights.Bold,
            Background = new SolidColorBrush(Colors.LightGray)
        });

        if (manuallyAddedFiles > 0)
        {
            ListOfFiles.Items.Add($"    {manuallyAddedFiles} manually selected files");
        }

        if (folderScannedFiles > 0)
        {
            ListOfFiles.Items.Add($"    {folderScannedFiles} files from folder scan");
        }

        // Now display stats by extension
        ListOfFiles.Items.Add(new ListViewItem
        {
            Content = "===== File Extensions Summary =====",
            FontWeight = FontWeights.Bold
        });

        foreach (var ext in _filesByExtension.Keys.OrderBy(k => k))
        {
            var count = _filesByExtension[ext].Count;
            ListOfFiles.Items.Add($"    {ext} - {count} files");
            LogOperation($"Found {count} {ext} files");
        }

        ListOfFiles.Items.Add(new ListViewItem
        {
            Content = "===== Files By Folder =====",
            FontWeight = FontWeights.Bold
        });

        // Then list files by folder
        foreach (var folderPath in filesByFolder.Keys.OrderBy(f => f))
        {
            // Add folder as a header
            ListOfFiles.Items.Add(new ListViewItem
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

                ListOfFiles.Items.Add($"{prefix} {Path.GetFileName(file.RelativePath)}");
            }
        }
    }

    private async void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Source Files",
                Filter = "All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                // Disable relevant buttons immediately
                BtnSelectFolder.IsEnabled = false;
                BtnSelectFiles.IsEnabled = false;
                BtnClearFiles.IsEnabled = false;
                BtnSendQuery.IsEnabled = false;

                // Initialize the file collection if this is the first selection
                if (string.IsNullOrEmpty(_selectedFolder))
                {
                    // Use the directory of the first selected file as the base folder
                    _selectedFolder = Path.GetDirectoryName(dialog.FileNames[0]) ?? string.Empty;
                    TxtSelectedFolder.Text = _selectedFolder;

                    // Clear any existing data
                    _filesByExtension.Clear();
                    await Dispatcher.InvokeAsync(() => ListOfFiles.Items.Clear());

                    LogOperation($"Base folder set to: {_selectedFolder}");
                }

                LogOperation($"Processing {dialog.FileNames.Length} selected files");
                StartOperationTimer("ProcessSelectedFiles");

                await ProcessSelectedFilesAsync(dialog.FileNames);

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
        finally
        {
            // Re-enable buttons
            BtnSelectFolder.IsEnabled = true;
            BtnSelectFiles.IsEnabled = true;
            BtnClearFiles.IsEnabled = true;
            BtnSendQuery.IsEnabled = true;
        }
    }

    private void BtnClearFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ask for confirmation
            var result = MessageBox.Show(
                "Are you sure you want to clear all currently selected files?",
                "Clear Files", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Clear the file collections
                _filesByExtension.Clear();
                ListOfFiles.Items.Clear();
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
        finally
        {
            UpdateTokenCountDisplay();
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

            // Track if we need to throttle UI updates
            var updateThrottleSemaphore = new SemaphoreSlim(1, 1);

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
                        if ((now - lastUiUpdate).TotalMilliseconds > 500)
                        {
                            lastUiUpdate = now;
                            await updateThrottleSemaphore.WaitAsync();
                            try
                            {
                                await Dispatcher.InvokeAsync(() =>
                                {
                                    TxtStatus.Text =
                                        $"Scanning folder... (Found {fileProcessedCount}/{totalFileCount} files)";
                                });
                            }
                            finally
                            {
                                updateThrottleSemaphore.Release();
                            }
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

                            // Calculate relative path once
                            string relativePath;
                            if (file.FullName.StartsWith(_selectedFolder, StringComparison.OrdinalIgnoreCase))
                            {
                                relativePath = file.FullName[_selectedFolder.Length..].TrimStart('\\', '/');
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
                                await Dispatcher.InvokeAsync(() =>
                                    LogOperation($"Error reading file {file.FullName}: {ex.Message}")
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
                            await Dispatcher.InvokeAsync(() =>
                                LogOperation($"Error processing file {file.FullName}: {ex.Message}")
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
                    LogOperation($"Error scanning directory {currentDir}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error in folder scan: {ex.Message}");
        }
    }

    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Check for API key
            if (AiProviderKeys.SelectedIndex == -1)
            {
                MessageBox.Show("Please enter an API key.", "Missing API Key", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                LogOperation("Analysis canceled: No API key provided");
                return;
            }

            // Get query text and API selection
            var queryText = TxtFollowupQuestion.Text.Trim();
            var apiSelection = AiProvider.SelectedItem?.ToString() ?? "Gemini API"; // Default to Claude

            // Setup processing UI
            TxtStatus.Text = $"Processing with {apiSelection}...";
            SetProcessingState(true, $"Processing with {apiSelection}");
            LogOperation($"Starting query with {apiSelection}");
            StartOperationTimer("QueryProcessing");

            try
            {
                // Initialize prompt variable
                string prompt;

                // Create a temporary list to track files for this specific query
                var currentQueryFiles = new List<SourceFile>();


                // Process as initial query or non-follow-up
                LogOperation("Handling as initial query or simple follow-up");

                // First determine if we need to include files
                if (IncludeSelectedFilesChecker.IsChecked == true)
                {
                    LogOperation("Include Files checkbox is checked - preparing files");
                    StartOperationTimer("PrepareFiles");

                    // Generate base prompt based on whether to include template
                    if (IncludePromptTemplate.IsChecked == true)
                    {
                        LogOperation("Including prompt template");
                        prompt = GetPromptTemplateText() + "\n\n";
                    }
                    else
                    {
                        LogOperation("Not using prompt template");
                        prompt = "Please analyze the following code files:\n\n";
                    }

                    // Now determine which files to include
                    if (ListOfFiles.SelectedItems.Count > 0)
                    {
                        // User has specifically selected files in the list
                        prompt = AppendSelectedFilesToPrompt(prompt, currentQueryFiles);
                        LogOperation($"Added {currentQueryFiles.Count} specifically selected files to the prompt");
                    }
                    else if (_filesByExtension.Count > 0)
                    {
                        // No specific selection, include all files
                        var consolidatedFiles = PrepareConsolidatedFiles(currentQueryFiles);

                        // Add files to the prompt
                        foreach (var ext in consolidatedFiles.Keys)
                        {
                            prompt += $"--- {ext.ToUpperInvariant()} FILES ---\n\n";

                            foreach (var fileContent in consolidatedFiles[ext])
                            {
                                prompt += fileContent + "\n\n";
                            }
                        }

                        LogOperation($"Added all {currentQueryFiles.Count} files to the prompt");
                    }
                    else
                    {
                        prompt += "--- NO FILES AVAILABLE TO INCLUDE ---\n\n";
                        LogOperation("No files available to include");
                    }

                    EndOperationTimer("PrepareFiles");

                    // Update the master list of included files
                    _lastIncludedFiles.Clear();
                    _lastIncludedFiles.AddRange(currentQueryFiles);
                    LogOperation($"Set master file list to {_lastIncludedFiles.Count} files");
                }
                else
                {
                    // Files not included - generate a basic prompt
                    if (IncludePromptTemplate.IsChecked == true)
                    {
                        LogOperation("Using prompt template only (no files)");
                        prompt = GetPromptTemplateText() + "\n\n";
                    }
                    else
                    {
                        LogOperation("Using minimal prompt (no template, no files)");
                        prompt = "Please respond to the following:\n\n";
                    }

                    // Clear the master list of included files
                    _lastIncludedFiles.Clear();
                    LogOperation("Cleared master file list as Include Files was unchecked");
                }

                // Add user query if provided
                if (!string.IsNullOrEmpty(queryText))
                {
                    prompt += "--- Additional Instructions/Question ---\n" + queryText;
                    LogOperation("Added query text to the prompt");
                }

                // Add to conversation history
                _conversationHistory.Add(new ChatMessage { Role = "user", Content = prompt });

                // Store final prompt and send to AI
                _lastInputPrompt = prompt;
                var response = await SendToAiApi(apiSelection, prompt);

                // Update conversation and UI
                _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = response });
                LogOperation(
                    $"Updated conversation history with response and file list ({_lastIncludedFiles.Count} files)");
                UpdateResponseDisplay(response, true);

                // Reset query input and update UI
                TxtFollowupQuestion.Text = string.Empty;
                BtnSaveResponse.IsEnabled = true;
                BtnShowInputQuery.IsEnabled = true;

                TxtStatus.Text = "Query processed successfully!";
                EndOperationTimer("QueryProcessing");
            }
            catch (Exception ex)
            {
                HandleQueryError(ex);
            }
            finally
            {
                SetProcessingState(false);
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, "Error processing query");
        }
    }

    private string GetPromptTemplateText()
    {
        // Get the selected prompt content from the dropdown or settings
        var selectedPromptTemplate = PromptTemplates.SelectedItem as CodePrompt;
        return selectedPromptTemplate?.Content ?? _settingsManager.Settings.InitialPrompt; // Fallback
    }

    private void HandleQueryError(Exception ex)
    {
        LogOperation($"Error processing query: {ex.Message}");

        // Don't show ErrorLogger dialog for token limits or self-thrown exceptions
        if (!ex.Message.StartsWith("Token limit exceeded:", StringComparison.Ordinal) && ex is not ApplicationException)
        {
            ErrorLogger.LogError(ex, "Processing query");
        }

        TxtStatus.Text = "Error processing query.";
        EndOperationTimer("QueryProcessing");
    }

    private Dictionary<string, List<string>> PrepareConsolidatedFiles(List<SourceFile> includedFiles)
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

    private static string GetLanguageForExtension(string ext)
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
            ".m" => "objectivec", // Fixed from "nim" to "objectivec"
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

    private string AppendSelectedFilesToPrompt(string originalPrompt, List<SourceFile> includedFiles)
    {
        var promptBuilder = new StringBuilder(originalPrompt);
        includedFiles.Clear(); // Start fresh for selected files

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("--- SELECTED FILES FOR FOCUSED REFERENCE ---");
        promptBuilder.AppendLine();

        var selectedFileNames = new List<string>();
        var selectedFilesContent = new StringBuilder();
        var fileCount = 0;

        // Ensure ListOfFiles.SelectedItems is accessed on the UI thread if necessary,
        // but since this method is likely called from BtnAnalyze_Click (UI thread), it should be fine.
        foreach (var item in ListOfFiles.SelectedItems)
        {
            // Check if the item is a string representing a file path/name
            if (item is string displayString)
            {
                // Basic check to filter out headers or summary lines
                if (!displayString.StartsWith("=====", StringComparison.Ordinal) && !displayString.Contains(" files") &&
                    !displayString.Contains(" - "))
                {
                    // Clean the display string (remove potential prefixes like '+' or '')
                    var cleanFileName = displayString.TrimStart(' ', '+');

                    // Find the matching SourceFile object across all extensions
                    SourceFile? matchingFile = null;
                    foreach (var extensionFiles in _filesByExtension.Values)
                    {
                        // Match against the file name part of the RelativePath
                        matchingFile = extensionFiles.FirstOrDefault(f =>
                            Path.GetFileName(f.RelativePath).Equals(cleanFileName, StringComparison.OrdinalIgnoreCase));

                        if (matchingFile != null)
                        {
                            break; // Found the file, exit inner loop
                        }
                    }

                    // If a matching file was found
                    if (matchingFile != null)
                    {
                        fileCount++;
                        selectedFileNames.Add(matchingFile.RelativePath); // Add a relative path to the summary list
                        includedFiles.Add(matchingFile); // Add the SourceFile object to the list

                        // Append file content in a code block with language identification
                        selectedFilesContent.AppendLine(CultureInfo.InvariantCulture,
                            $"File: {matchingFile.RelativePath}");
                        selectedFilesContent.AppendLine(CultureInfo.InvariantCulture,
                            $"```{GetLanguageForExtension(matchingFile.Extension)}");
                        selectedFilesContent.AppendLine(matchingFile.Content);
                        selectedFilesContent.AppendLine("```");
                        selectedFilesContent.AppendLine();
                    }
                    else
                    {
                        LogOperation($"Warning: Could not find source file data for selected item: '{cleanFileName}'");
                    }
                }
            }
        } // End foreach selected item

        // Add summary of selected files to the prompt
        if (fileCount > 0)
        {
            promptBuilder.AppendLine(CultureInfo.InvariantCulture,
                $"Specifically, I've selected the following {fileCount} file(s) for you to focus on or reference:");
            foreach (var fileName in selectedFileNames)
            {
                promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"- {fileName}");
            }

            promptBuilder.AppendLine();

            // Add the file contents
            promptBuilder.Append(selectedFilesContent);
        }
        else
        {
            promptBuilder.AppendLine("(No specific files were selected in the list for focused reference)");
            promptBuilder.AppendLine();
        }

        // Log the operation
        LogOperation($"Included {fileCount} specifically selected files in the prompt");

        return promptBuilder.ToString();
    }

    private async void BtnSaveResponse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // First, check if we have unsaved edits in the raw text view
            if (!_isMarkdownViewActive && TxtResponse.Text != _currentResponseText)
            {
                // Update the current response text with the edited content before saving
                _currentResponseText = TxtResponse.Text;
                LogOperation("Updated content from text editor before saving");
            }

            if (string.IsNullOrWhiteSpace(_currentResponseText))
            {
                MessageBox.Show("There is no response to save.", "No Response", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // If we have a current file path, and we want to overwrite it
            if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
            {
                // Ask the user if they want to overwrite or save as a new file
                var result = MessageBox.Show(
                    $"Do you want to overwrite the current file?\n{_currentFilePath}\n\nClick 'Yes' to overwrite, 'No' to save as a new file.",
                    "Save Options",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Show saving indicator
                    SetProcessingState(true, $"Saving to {Path.GetFileName(_currentFilePath)}");

                    // Save on background thread
                    await Task.Run(() => File.WriteAllText(_currentFilePath, _currentResponseText));

                    LogOperation($"Overwrote file: {_currentFilePath}");
                    TxtStatus.Text = $"File saved: {Path.GetFileName(_currentFilePath)}";
                    MessageBox.Show($"File saved: {Path.GetFileName(_currentFilePath)}", "Save Successful",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    SetProcessingState(false);
                    return;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    // User canceled the operation
                    return;
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
            if (!string.IsNullOrEmpty(_selectedFolder) && Directory.Exists(_selectedFolder))
            {
                saveFileDialog.InitialDirectory = _selectedFolder;

                // Suggest a filename based on the project folder name and timestamp
                var folderName = new DirectoryInfo(_selectedFolder).Name;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                saveFileDialog.FileName = $"{folderName}_analysis_{timestamp}.md";
            }
            else
            {
                // Default filename with timestamp if no folder is selected
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                saveFileDialog.FileName = $"ai_analysis_{timestamp}.md";
            }

            // Show the dialog and get the result
            var dialogResult = saveFileDialog.ShowDialog();

            // If the user clicked OK, save the file
            if (dialogResult == true)
            {
                // Show saving indicator
                SetProcessingState(true, $"Saving to {Path.GetFileName(saveFileDialog.FileName)}");

                // Save on background thread
                await Task.Run(() => File.WriteAllText(saveFileDialog.FileName, _currentResponseText));

                // Update the current file path
                _currentFilePath = saveFileDialog.FileName;

                // Add to recent files
                AddToRecentFiles(saveFileDialog.FileName);

                TxtResponseCounter.Text = $"Viewing: {Path.GetFileName(saveFileDialog.FileName)}";
                TxtStatus.Text = $"File saved: {Path.GetFileName(saveFileDialog.FileName)}";

                LogOperation($"Saved response to: {saveFileDialog.FileName}");
                MessageBox.Show($"Response saved to {saveFileDialog.FileName}", "Save Successful", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error saving response: {ex.Message}");
            ErrorLogger.LogError(ex, "Saving response to file");
            MessageBox.Show("An error occurred while saving the response.", "Save Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetProcessingState(false);
        }
    }

    private void ToggleMarkdownView_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isMarkdownViewActive = !_isMarkdownViewActive;

            if (_isMarkdownViewActive)
            {
                // Check if content was edited before switching
                var contentWasEdited = _currentResponseText != TxtResponse.Text;

                // If content was edited, ask user if they want to save
                if (contentWasEdited)
                {
                    var result = MessageBox.Show(
                        "You have made changes to the raw text. Do you want to apply these changes?",
                        "Save Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel)
                    {
                        // User canceled, stay in raw text view
                        return;
                    }
                    else if (result == MessageBoxResult.Yes)
                    {
                        // User wants to save changes, update the response text
                        _currentResponseText = TxtResponse.Text;
                        LogOperation("Saved edited markdown content when switching to rendered view");
                    }
                    else
                    {
                        // User doesn't want to save, revert the TextBox content
                        TxtResponse.Text = _currentResponseText;
                        LogOperation("Discarded edited markdown content when switching to rendered view");
                    }
                }

                // Switch to Markdown view
                TxtResponse.Visibility = Visibility.Collapsed;
                MarkdownScrollViewer.Visibility = Visibility.Visible;
                BtnToggleMarkdown.Content = "Show Raw Text";
                BtnSaveEdits.Visibility = Visibility.Visible;

                // Set the Markdown content with preprocessing and syntax highlighting
                var processedMarkdown = PreprocessMarkdown(_currentResponseText);
                MarkdownViewer.Markdown = processedMarkdown;

                // Update the page width
                UpdateMarkdownPageWidth();

                // Log the switch to Markdown view
                LogOperation("Switched to markdown view with preprocessing");
            }
            else
            {
                // Switch to raw text view
                TxtResponse.Visibility = Visibility.Visible;
                MarkdownScrollViewer.Visibility = Visibility.Collapsed;
                BtnToggleMarkdown.Content = "Show Markdown";
                BtnSaveEdits.Visibility = Visibility.Visible;
                BtnSaveEdits.IsEnabled = true;

                // Log the switch to raw text view
                LogOperation("Switched to raw text view (edit mode)");
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error toggling markdown view: {ex.Message}");
            ErrorLogger.LogError(ex, "Toggling markdown view");
            MessageBox.Show("An error occurred while toggling the markdown view.", "View Error", MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Revert to a raw text as a fallback
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
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error setting Markdown document properties: {ex.Message}");
            ErrorLogger.LogError(ex, $"Error setting Markdown document properties: {ex.Message}");
        }
    }

    private void PopulateModelDropdown()
    {
        AiModel.Items.Clear();

        if (AiProvider.SelectedItem == null)
            return;

        var providerName = AiProvider.SelectedItem.ToString();

        try
        {
            // Only populate models for providers that support model selection
            if (providerName == "DeepSeek API")
            {
                var provider = (DeepSeek)_apiProviderFactory.GetProvider(providerName);
                var models = provider.GetAvailableModels();

                foreach (var model in models)
                {
                    AiModel.Items.Add(new ModelDropdownItem
                    {
                        DisplayText = model.Name,
                        ModelId = model.Id,
                        Description = model.Description
                    });
                }

                AiModel.IsEnabled = true;
                // No automatic selection
            }
            else if (providerName == "Claude API")
            {
                var provider = (Claude)_apiProviderFactory.GetProvider(providerName);
                var models = provider.GetAvailableModels();

                foreach (var model in models)
                {
                    AiModel.Items.Add(new ModelDropdownItem
                    {
                        DisplayText = model.Name,
                        ModelId = model.Id,
                        Description = model.Description
                    });
                }

                AiModel.IsEnabled = true;
                // No automatic selection
            }
            else if (providerName == "Grok API")
            {
                var provider = (Grok)_apiProviderFactory.GetProvider(providerName);
                var models = provider.GetAvailableModels();

                foreach (var model in models)
                {
                    AiModel.Items.Add(new ModelDropdownItem
                    {
                        DisplayText = model.Name,
                        ModelId = model.Id,
                        Description = model.Description
                    });
                }

                AiModel.IsEnabled = true;
                // No automatic selection
            }
            else if (providerName == "Gemini API")
            {
                var provider = (Gemini)_apiProviderFactory.GetProvider(providerName);
                var models = provider.GetAvailableModels();

                foreach (var model in models)
                {
                    AiModel.Items.Add(new ModelDropdownItem
                    {
                        DisplayText = model.Name,
                        ModelId = model.Id,
                        Description = model.Description
                    });
                }

                AiModel.IsEnabled = true;
                // No automatic selection
            }
            else if (providerName == "ChatGPT API")
            {
                var provider = (OpenAi)_apiProviderFactory.GetProvider(providerName);
                var models = provider.GetAvailableModels();

                foreach (var model in models)
                {
                    AiModel.Items.Add(new ModelDropdownItem
                    {
                        DisplayText = model.Name,
                        ModelId = model.Id,
                        Description = model.Description
                    });
                }

                AiModel.IsEnabled = true;
                // No automatic selection
            }
            else
            {
                // For other providers, disable the model dropdown
                AiModel.IsEnabled = false;
                AiModel.Items.Add("Default model");
                AiModel.SelectedIndex = 0; // Only set this for unsupported providers
                AiModel.ToolTip = null;
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error populating model dropdown: {ex.Message}");
            ErrorLogger.LogError(ex, $"Error populating model dropdown: {ex.Message}");
        }
    }

    private void AiProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AiProvider.SelectedItem != null)
        {
            var apiSelection = AiProvider.SelectedItem.ToString() ?? string.Empty;
            UpdatePreviousKeys(apiSelection);
            PopulateModelDropdown();

            // Clear the model description initially
            TxtModelDescription.Text = string.Empty;

            // Add an instruction to select a model
            if (AiModel.IsEnabled)
            {
                TxtModelDescription.Text = "Please select a model from the dropdown above.";
            }
        }
    }

    private async Task ProcessSelectedFilesAsync(IEnumerable<string> filePaths)
    {
        // Process files in parallel with a limit of 10 concurrent files
        var tasks = new List<Task>();
        var throttler = new SemaphoreSlimSafe(10);

        foreach (var filePath in filePaths)
        {
            await throttler.WaitAsync();

            // Start a new task for each file
            tasks.Add(ProcessSingleFileAsync(filePath, throttler));
        }

        // Wait for all file processing tasks to complete
        await Task.WhenAll(tasks);

        // Recalculate tokens on UI thread
        await Dispatcher.InvokeAsync(CalculateTotalTokens);
    }

    private async Task ProcessSingleFileAsync(string filePath, SemaphoreSlimSafe throttler)
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
                if (filePath.StartsWith(_selectedFolder, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = filePath.Substring(_selectedFolder.Length).TrimStart('\\', '/');
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
                    await Dispatcher.InvokeAsync(() =>
                        LogOperation($"Error reading file {filePath}: {ex.Message}")
                    );
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
                    await Dispatcher.InvokeAsync(() =>
                        LogOperation($"Added file: {relativePath}")
                    );
                }
                else
                {
                    await Dispatcher.InvokeAsync(() =>
                        LogOperation($"Skipped duplicate file: {relativePath}")
                    );
                }
            }
            else
            {
                await Dispatcher.InvokeAsync(() =>
                    LogOperation(
                        $"Skipped file due to size limit: {filePath} ({fileSizeKb} KB > {_settingsManager.Settings.MaxFileSizeKb} KB)")
                );
            }
        }
        finally
        {
            // Release the throttler when done
            throttler.Release();
        }
    }


    private void AiModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AiModel.SelectedItem is ModelDropdownItem selectedModel)
        {
            // Update the model description TextBlock
            TxtModelDescription.Text = selectedModel.Description;
        }
        else
        {
            // Clear the description if no valid model is selected
            TxtModelDescription.Text = string.Empty;
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

            // Get selected model ID for providers that support model selection
            string? modelId = null;
            if (AiModel.IsEnabled && AiModel.SelectedItem is ModelDropdownItem selectedModel)
            {
                // Check if it's not the placeholder item (which has empty ModelId)
                if (!string.IsNullOrEmpty(selectedModel.ModelId))
                {
                    modelId = selectedModel.ModelId;
                    LogOperation($"Using model: {selectedModel.DisplayText} ({modelId})");
                }
                else
                {
                    // No model selected or placeholder selected
                    MessageBox.Show("Please select a model before sending your request.",
                        "Model Selection Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    throw new ApplicationException("No model selected");
                }
            }

            // Start timer for API request
            StartOperationTimer($"ApiRequest-{apiSelection}");
            LogOperation($"Sending prompt to {apiSelection} ({prompt.Length} characters)");

            // Check if a key is selected and get the actual API key
            string? key;
            if (AiProviderKeys.SelectedIndex <= 0)
            {
                MessageBox.Show("Please select an API key", "API Key Required", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                throw new ApplicationException("No API key selected");
            }
            else
            {
                // Get the actual API key from the key manager (not the masked one shown in the UI)
                // Subtract 1 from index because index 0 is "Select a key"
                var savedKeys = _keyManager.GetKeysForProvider(apiSelection);
                var keyIndex = AiProviderKeys.SelectedIndex - 1;

                if (keyIndex >= 0 && keyIndex < savedKeys.Count)
                {
                    key = savedKeys[keyIndex];
                }
                else
                {
                    MessageBox.Show("The selected API key is invalid", "Invalid API Key", MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    throw new ApplicationException("Invalid API key selection");
                }
            }

            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("The API key is empty", "Empty API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                throw new ApplicationException("Empty API key");
            }

            // Send the prompt and return the response
            string response;

            // Special handling for providers with model selection
            if (provider is DeepSeek deepSeekProvider && modelId != null)
            {
                response = await deepSeekProvider.SendPromptWithModelAsync(key, prompt, _conversationHistory, modelId);
            }
            else if (provider is Claude claudeProvider && modelId != null)
            {
                response = await claudeProvider.SendPromptWithModelAsync(key, prompt, _conversationHistory, modelId);
            }
            else if (provider is Grok grokProvider && modelId != null)
            {
                response = await grokProvider.SendPromptWithModelAsync(key, prompt, _conversationHistory, modelId);
            }
            else if (provider is Gemini geminiProvider && modelId != null)
            {
                response = await geminiProvider.SendPromptWithModelAsync(key, prompt, _conversationHistory, modelId);
            }
            else if (provider is OpenAi openAiProvider && modelId != null)
            {
                response = await openAiProvider.SendPromptWithModelAsync(key, prompt, _conversationHistory, modelId);
            }
            else
            {
                response = await provider.SendPromptWithModelAsync(key, prompt, _conversationHistory);
            }

            // Log response received
            EndOperationTimer($"ApiRequest-{apiSelection}");
            LogOperation($"Received response from {apiSelection} ({response.Length} characters)");

            return response;
        }
        catch (Exception ex)
        {
            LogOperation($"Error calling {apiSelection} API: {ex.Message}");
            ErrorLogger.LogError(ex, $"Error calling {apiSelection} API");
        }

        return "The response is null";
    }

    private void NavigateToResponse(int index)
    {
        // Get all assistant responses from the conversation history
        var assistantResponses = _conversationHistory
            .Where(m => m.Role == "assistant")
            .ToList();

        // Ensure the index is within bounds
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
        MarkdownViewer.Markdown = PreprocessMarkdown(response);

        // Update page width for markdown
        UpdateMarkdownPageWidth();

        // Update the message counter
        UpdateMessageCounter();

        // Update display without resetting zoom
        _currentResponseText = response;
        TxtResponse.Text = response;
        MarkdownViewer.Markdown = PreprocessMarkdown(response);
        UpdateZoomDisplay(); // Re-apply current zoom level
        UpdateMarkdownPageWidth();
        UpdateMessageCounter();

        LogOperation($"Navigated to response #{index + 1} of {assistantResponses.Count}");
    }

    private void UpdateMessageCounter()
    {
        var assistantResponses = _conversationHistory.Count(m => m.Role == "assistant");

        if (assistantResponses == 0)
        {
            TxtResponseCounter.Text = "No responses";
        }
        else
        {
            // Display as 1-based index for the user (1 of 1 instead of 0 of 0)
            TxtResponseCounter.Text = $"Response {_currentResponseIndex + 1} of {assistantResponses}";
        }
    }

    private void UpdateNavigationControls()
    {
        var totalResponses = _conversationHistory.Count(m => m.Role == "assistant");

        // Enable/disable previous button
        BtnPreviousResponse.IsEnabled = _currentResponseIndex > 0;

        // Enable/disable next button
        BtnNextResponse.IsEnabled = _currentResponseIndex < totalResponses - 1;
    }

    private void BtnPreviousResponse_Click(object sender, RoutedEventArgs e)
    {
        if (_currentResponseIndex > 0)
        {
            NavigateToResponse(_currentResponseIndex - 1);
        }
    }

    private void BtnNextResponse_Click(object sender, RoutedEventArgs e)
    {
        var totalResponses = _conversationHistory.Count(m => m.Role == "assistant");

        if (_currentResponseIndex < totalResponses - 1)
        {
            NavigateToResponse(_currentResponseIndex + 1);
        }
    }

    private void UpdateResponseDisplay(string responseText, bool isNewResponse = false)
    {
        // If we are currently showing the input query, switch back first
        if (_isShowingInputQuery)
        {
            _isShowingInputQuery = false; // Reset the flag
            // Restore the previous content (which should be the latest AI response)
            MarkdownViewer.Markdown = _previousMarkdownContent;
            BtnShowInputQuery.Content = "Show Input Query";
            // Re-enable buttons that were disabled
            UpdateNavigationControls(); // Handles Prev/Next based on _currentResponseIndex
            BtnSaveResponse.IsEnabled = !string.IsNullOrEmpty(_currentResponseText);
            BtnSaveEdits.IsEnabled = !string.IsNullOrEmpty(_currentResponseText);
            BtnToggleMarkdown.IsEnabled = !string.IsNullOrEmpty(_currentResponseText);
            LogOperation("Switched back from Input Query view to show new AI response.");
        }

        _currentResponseText = responseText; // Store the actual AI response
        TxtResponse.Text = responseText; // Update raw text view

        // Apply preprocessing to fix markdown rendering issues
        var processedMarkdown = PreprocessMarkdown(responseText);
        MarkdownViewer.Markdown = processedMarkdown; // Update rendered view
        _previousMarkdownContent = processedMarkdown; // Update the stored content for toggle

        if (isNewResponse)
        {
            // For a new response, set the index to the last response
            _currentResponseIndex = _conversationHistory.Count(m => m.Role == "assistant") - 1;

            // Clear the current file path when generating a new response
            _currentFilePath = string.Empty;
        }

        // Reset zoom when displaying a new response initially
        if (isNewResponse)
        {
            _markdownZoomLevel = 100.0; // Reset zoom for new content
        }

        UpdateZoomDisplay(); // Apply potentially reset zoom level
        UpdateMarkdownPageWidth(); // Keep this if you still need custom page width logic

        BtnToggleMarkdown.IsEnabled = true;
        BtnSaveResponse.IsEnabled = true;
        BtnSaveEdits.IsEnabled = true;
        // BtnShowInputQuery is enabled in BtnAnalyze_Click after success

        if (isNewResponse)
        {
            AutoSaveResponse(responseText, _currentResponseIndex);
        }

        UpdateNavigationControls();
        UpdateMessageCounter();
    }

    private void LoadPromptTemplates()
    {
        PromptTemplates.ItemsSource = null;
        PromptTemplates.ItemsSource = _settingsManager.Settings.CodePrompts;

        // Select the current prompt
        if (!string.IsNullOrEmpty(_settingsManager.Settings.SelectedPromptName))
        {
            var selectedPrompt = _settingsManager.Settings.CodePrompts.FirstOrDefault(p =>
                p.Name == _settingsManager.Settings.SelectedPromptName);

            if (selectedPrompt != null)
            {
                PromptTemplates.SelectedItem = selectedPrompt;
            }
            else if (_settingsManager.Settings.CodePrompts.Count > 0)
            {
                PromptTemplates.SelectedIndex = 0;
            }
        }
        else if (_settingsManager.Settings.CodePrompts.Count > 0)
        {
            PromptTemplates.SelectedIndex = 0;
        }
    }

    private void PromptTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PromptTemplates.SelectedItem is CodePrompt selectedPrompt)
        {
            _settingsManager.Settings.SelectedPromptName = selectedPrompt.Name;
            _settingsManager.SaveSettings();
            LogOperation($"Selected prompt template: {selectedPrompt.Name}");
        }
    }

    private void PromptTemplatesEdit_Click(object sender, RoutedEventArgs e)
    {
        OpenConfigurationWindow();
    }

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

                // Sanitize the project name to remove invalid characters
                projectName = string.Join("_", projectName.Split(Path.GetInvalidFileNameChars()));
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
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

    private void CalculateTotalTokens()
    {
        // Get all source files as a flat list
        var allFiles = _filesByExtension.Values
            .SelectMany(files => files)
            .ToList();

        // Get the prompt template text for token counting
        var promptTemplate = GetPromptTemplateText();

        // Use SharpToken to calculate tokens
        _tokenCalculationResult = _tokenCounterService.CalculateTotalTokens(allFiles, promptTemplate);

        // Update UI
        UpdateTokenCountDisplay();

        LogOperation($"Token calculation complete: {_tokenCalculationResult.TotalTokens:N0} tokens");
    }

    private void UpdateTokenCountDisplay()
    {
        Dispatcher.Invoke(() =>
        {
            // Get the total token count from the result
            var totalTokens = _tokenCalculationResult.TotalTokens;

            // Format the display text with exact token count
            TxtTokenCount.Text = $"Estimated tokens: {totalTokens:N0} (range {totalTokens * 0.8:N0} - {totalTokens * 1.2:N0})";

            // Detailed tooltip with model-specific information
            var tooltipBuilder = new StringBuilder();
            tooltipBuilder.AppendLine(_tokenCalculationResult.GetBreakdown());

            TxtTokenCount.ToolTip = tooltipBuilder.ToString();

            LogOperation($"Updated token count display: {totalTokens:N0} tokens");
        });
    }

    private string PreprocessMarkdown(string markdownContent)
    {
        if (string.IsNullOrEmpty(markdownContent))
            return markdownContent;

        // Fix for responses that start with ```Markdown by removing that line
        // This prevents the entire content from being treated as a code block
        if (markdownContent.StartsWith("```markdown", StringComparison.Ordinal) || markdownContent.StartsWith("```Markdown", StringComparison.Ordinal))
        {
            // Find the first line break after the ```Markdown
            var lineBreakIndex = markdownContent.IndexOf('\n');
            if (lineBreakIndex > 0)
            {
                // Remove the ```markdown line
                markdownContent = markdownContent.Substring(lineBreakIndex + 1);

                // Look for the closing ``` and remove it as well
                var closingBackticksIndex = markdownContent.LastIndexOf("```", StringComparison.Ordinal);
                if (closingBackticksIndex >= 0)
                {
                    markdownContent = markdownContent.Substring(0, closingBackticksIndex).TrimEnd();
                }

                LogOperation("Preprocessed markdown response to fix formatting issues");
            }
        }

        return markdownContent;
    }

    private static T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) return null;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);

            var result = child as T ?? FindVisualChild<T>(child);
            if (result != null) return result;
        }

        return null;
    }

    private void MenuStart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ask for confirmation
            var result = MessageBox.Show(
                "Are you sure you want to restart the application? This will clear all current analysis data.",
                "Restart Application",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                LogOperation("Restarting application...");

                // Clear MarkdownViewer and response display
                MarkdownViewer.Markdown = string.Empty;
                TxtResponse.Text = string.Empty;
                _currentResponseText = string.Empty;
                _previousMarkdownContent = string.Empty; // Clear stored markdown

                // Reset response counter and navigation
                _conversationHistory.Clear();
                _currentResponseIndex = -1;
                BtnPreviousResponse.IsEnabled = false;
                BtnNextResponse.IsEnabled = false;
                TxtResponseCounter.Text = "No responses";

                // Reset zoom level
                _markdownZoomLevel = 100.0;
                UpdateZoomDisplay();

                // Clear file list
                _filesByExtension.Clear();
                ListOfFiles.Items.Clear();
                _selectedFolder = string.Empty;
                TxtSelectedFolder.Text = string.Empty;
                TxtTokenCount.Text = string.Empty;

                // Clear last prompt/files state
                _lastInputPrompt = string.Empty;
                _lastIncludedFiles.Clear();
                _isShowingInputQuery = false;

                // Reset UI elements state
                BtnSendQuery.IsEnabled = true;
                TxtFollowupQuestion.IsEnabled = true;
                BtnSaveResponse.IsEnabled = false;
                BtnToggleMarkdown.IsEnabled = false;
                BtnShowInputQuery.IsEnabled = false; // Disable show input button
                BtnShowInputQuery.Content = "Show Input Query";
                BtnSaveEdits.IsEnabled = false;


                // Reset checkboxes to default state
                IncludePromptTemplate.IsChecked = true;
                IncludeSelectedFilesChecker.IsChecked = true;

                // Reset AI provider
                AiProvider.SelectedIndex = -1; // Default to none

                // Reset AI Model
                AiModel.SelectedIndex = -1; // Default to none

                // Reset API Key
                AiProviderKeys.SelectedIndex = -1; // Default to none

                // Reset Model description
                TxtModelDescription.Text = string.Empty;

                // Reset status
                TxtStatus.Text = "Ready";

                LogOperation("Application reset complete");

                // Clear the current file path when restarting
                _currentFilePath = string.Empty;
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error restarting application: {ex.Message}");
            ErrorLogger.LogError(ex, "Restarting application");
        }
    }

    private async void MenuOpenPastResponses_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Check if AiOutput directory exists
            var outputDirectory = Path.Combine(AppContext.BaseDirectory, "AiOutput");
            if (!Directory.Exists(outputDirectory))
            {
                MessageBox.Show(
                    "No past responses found. The AiOutput folder doesn't exist yet.",
                    "No Responses",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Create file selection dialog
            var dialog = new OpenFileDialog
            {
                Title = "Open Past Response",
                Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                InitialDirectory = outputDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                LogOperation($"Opening past response file: {Path.GetFileName(dialog.FileName)}");
                StartOperationTimer("LoadResponseFile");

                // Store the current file path
                _currentFilePath = dialog.FileName;

                // Read the file content
                var responseText = await Task.Run(() => File.ReadAllText(dialog.FileName)); // Read async

                // Display in the MarkdownViewer
                _currentResponseText = responseText;
                TxtResponse.Text = responseText;

                // Apply preprocessing to fix markdown rendering issues
                var processedMarkdown = PreprocessMarkdown(responseText);
                _previousMarkdownContent = processedMarkdown; // Store for toggling
                MarkdownViewer.Markdown = processedMarkdown;

                // Use the original file name in the response counter
                TxtResponseCounter.Text = $"Viewing: {Path.GetFileName(dialog.FileName)}";

                // Enable relevant buttons
                BtnSaveResponse.IsEnabled = true;
                BtnToggleMarkdown.IsEnabled = true;
                BtnSaveEdits.IsEnabled = true; // Enable edit saving

                // Disable input query button when loading a file
                BtnShowInputQuery.IsEnabled = false;
                _isShowingInputQuery = false;
                BtnShowInputQuery.Content = "Show Input Query";


                // Ensure we're in markdown view mode
                if (!_isMarkdownViewActive)
                {
                    // Directly update the view state
                    _isMarkdownViewActive = true;
                    TxtResponse.Visibility = Visibility.Collapsed;
                    MarkdownScrollViewer.Visibility = Visibility.Visible;
                    BtnToggleMarkdown.Content = "Show Raw Text";
                    LogOperation("Switched to markdown view for past response");
                }

                // Update the page width and zoom
                _markdownZoomLevel = 100.0; // Reset zoom for new content
                UpdateZoomDisplay();
                UpdateMarkdownPageWidth();

                // Add the file to the Recent Files list
                AddToRecentFiles(dialog.FileName);

                EndOperationTimer("LoadResponseFile");
                TxtStatus.Text = "Past response loaded successfully";
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error opening past response: {ex.Message}");
            ErrorLogger.LogError(ex, "Opening past response");
            TxtStatus.Text = "Error opening past response.";
        }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private async void SaveEdits_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ensure we are not showing the input query
            if (_isShowingInputQuery)
            {
                MessageBox.Show("Cannot save edits while viewing the input query. Please switch back to the AI response first.",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Update the current response text with the edited content from the raw text view
            if (!_isMarkdownViewActive) // Only update if in raw text view
            {
                _currentResponseText = TxtResponse.Text;
            }
            else
            {
                _previousMarkdownContent = PreprocessMarkdown(_currentResponseText);
            }

            // Set the Markdown content with preprocessing (updates the viewer if visible)
            var processedMarkdown = PreprocessMarkdown(_currentResponseText);
            MarkdownViewer.Markdown = processedMarkdown;
            _previousMarkdownContent = processedMarkdown; // Keep stored content in sync

            // If we have a file path, automatically save changes to that file
            if (!string.IsNullOrEmpty(_currentFilePath) && File.Exists(_currentFilePath))
            {
                // Save the changes to the file asynchronously
                await Task.Run(() => File.WriteAllText(_currentFilePath, _currentResponseText));
                LogOperation($"Automatically saved edits to file: {_currentFilePath}");
                TxtStatus.Text = $"Edits applied and saved to {Path.GetFileName(_currentFilePath)}";
                MessageBox.Show($"Edits applied and saved to {Path.GetFileName(_currentFilePath)}",
                    "Edits Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // No file path, so update the in-memory content
                LogOperation("Applied edits to the content (not saved to file)");
                TxtStatus.Text = "Edits applied (not saved to file)";
                MessageBox.Show("Edits applied to the content. Use 'Save Response' to save to a file.",
                    "Edits Applied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error saving edits: {ex.Message}");
            ErrorLogger.LogError(ex, "Saving edited markdown");
            MessageBox.Show("An error occurred while saving your edits.", "Edit Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnShowInputQuery_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _isShowingInputQuery = !_isShowingInputQuery; // Toggle the state

            if (_isShowingInputQuery)
            {
                // Generate File Summary
                var fileSummary = new StringBuilder();
                fileSummary.AppendLine("## Files Included in Query");
                fileSummary.AppendLine(CultureInfo.InvariantCulture, $"Total files: {_lastIncludedFiles.Count}");
                if (_lastIncludedFiles.Count != 0)
                {
                    fileSummary.AppendLine("```"); // Use a simple code block for the list
                    foreach (var file in _lastIncludedFiles.OrderBy(f => f.RelativePath))
                    {
                        fileSummary.AppendLine(CultureInfo.InvariantCulture, $"- {file.RelativePath} ({file.Extension})");
                    }

                    fileSummary.AppendLine("```");
                }
                else
                {
                    fileSummary.AppendLine("\n_(No files were included in this query)_");
                }

                fileSummary.AppendLine("\n---\n");

                // Combine Summary and Prompt
                var fullInputView = new StringBuilder();
                fullInputView.Append(fileSummary);
                fullInputView.AppendLine("## Full Input Prompt Sent to AI");
                fullInputView.AppendLine("```text"); // Display prompt as plain text
                fullInputView.AppendLine(_lastInputPrompt);
                fullInputView.AppendLine("```");

                // Update MarkdownViewer
                // Ensures we are in Markdown view visually
                if (!_isMarkdownViewActive)
                {
                    // Force switch to Mark down view UI elements
                    TxtResponse.Visibility = Visibility.Collapsed;
                    MarkdownScrollViewer.Visibility = Visibility.Visible;
                    BtnToggleMarkdown.Content = "Show Raw Text";
                    _isMarkdownViewActive = true; // Update state flag
                    LogOperation("Switched UI to markdown view to show input query");
                }

                MarkdownViewer.Markdown = fullInputView.ToString();
                UpdateMarkdownPageWidth(); // Adjust width for the new content

                // Update Button Text
                BtnShowInputQuery.Content = "Show AI Response";

                // Disable irrelevant buttons
                BtnPreviousResponse.IsEnabled = false;
                BtnNextResponse.IsEnabled = false;
                BtnSaveResponse.IsEnabled = false;
                BtnSaveEdits.IsEnabled = false;
                BtnToggleMarkdown.IsEnabled = false; // Disable toggling while showing input
            }
            else
            {
                // 1. Restore previous Markdown content
                MarkdownViewer.Markdown = _previousMarkdownContent; // Restore the saved AI response
                UpdateMarkdownPageWidth();

                // 2. Update Button Text
                BtnShowInputQuery.Content = "Show Input Query";

                // 3. Re-enable buttons based on actual state
                UpdateNavigationControls(); // Handles Prev/Next based on _currentResponseIndex
                BtnSaveResponse.IsEnabled = !string.IsNullOrEmpty(_currentResponseText);
                BtnSaveEdits.IsEnabled = !string.IsNullOrEmpty(_currentResponseText);
                BtnToggleMarkdown.IsEnabled = !string.IsNullOrEmpty(_currentResponseText);
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error toggling input query view: {ex.Message}");
            ErrorLogger.LogError(ex, "Toggling input query view");

            // Attempt to restore a sane state
            _isShowingInputQuery = false;
            MarkdownViewer.Markdown = _previousMarkdownContent;
            BtnShowInputQuery.Content = "Show Input Query";
            UpdateNavigationControls();
            BtnSaveResponse.IsEnabled = !string.IsNullOrEmpty(_currentResponseText);
            BtnSaveEdits.IsEnabled = !string.IsNullOrEmpty(_currentResponseText);
            BtnToggleMarkdown.IsEnabled = !string.IsNullOrEmpty(_currentResponseText);
        }
    }
}