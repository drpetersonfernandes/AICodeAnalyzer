using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    private int _estimatedTokenCount;
    private string _selectedFolder = string.Empty;
    private bool _isMarkdownViewActive = true;
    private string _currentResponseText = string.Empty;
    private bool _isProcessing;
    private int _dotsIndex;
    private string _baseStatusMessage = string.Empty;
    private const double ZoomIncrement = 10.0; // Zoom step (10%)
    private const double MinZoom = 20.0; // Minimum zoom level (20%)
    private const double MaxZoom = 500.0; // Maximum zoom level (500%)
    private double _markdownZoomLevel = 100.0; // Current zoom level (default 100%)
    private readonly double _textBoxDefaultFontSize;
    private string _currentFilePath = string.Empty;
    private bool _isShowingInputQuery; // Flag to track if the input query is shown
    private string _previousMarkdownContent = string.Empty; // Store Markdown content before showing input
    private string _lastInputPrompt = string.Empty; // Store the last prompt sent
    private readonly List<SourceFile> _lastIncludedFiles = new(); // Store files included in the last prompt

    public MainWindow()
    {
        InitializeComponent();

        _textBoxDefaultFontSize = TxtResponse.FontSize;
        TxtFollowupQuestion.IsEnabled = true;
        ChkIncludeSelectedFiles.IsEnabled = true;
        
        // Populate API dropdown using provider names from the factory, sorted alphabetically
        var providers = _apiProviderFactory.AllProviders
            .OrderBy(p => p.Name)
            .ToList();

        foreach (var provider in providers)
        {
            CboAiApi.Items.Add(provider.Name);
        }

        CboAiApi.SelectedIndex = -1; // Default to none

        UpdateRecentFilesMenu();

        // Check if a startup file path was passed from App.xaml.cs
        if (Application.Current.Properties["StartupFilePath"] is string filePath)
        {
            // Load the file asynchronously, but don't await here since we're in the constructor
            Dispatcher.InvokeAsync(async () =>
            {
                await LoadMarkdownFileAsync(filePath);
            });
        }

        LogOperation("Application started");
        InitializePromptSelection();
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
                MessageBox.Show("The specified file does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    Tag = filePath
                };

                // Add document icon to each menu item
                menuItem.Icon = new TextBlock { Text = "📄", FontSize = 14 };

                menuItem.Click += RecentFileMenuItem_Click;
                MenuRecentFiles.Items.Add(menuItem);
            }

            // Add separator and "Clear Recent Files" option
            MenuRecentFiles.Items.Add(new Separator());

            var clearMenuItem = new MenuItem { Header = "Clear Recent Files" };
            clearMenuItem.Icon = new TextBlock { Text = "🗑️", FontSize = 14 }; // Trash icon for a clear option
            clearMenuItem.Click += ClearRecentFiles_Click;
            MenuRecentFiles.Items.Add(clearMenuItem);
        }
    }

    private async void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
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

    // Event handler for clearing recent files
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

    private async void MenuOpenFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Markdown File",
                Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "md"
            };

            if (dialog.ShowDialog() == true)
            {
                LogOperation($"Opening file: {Path.GetFileName(dialog.FileName)}");
                await LoadMarkdownFileAsync(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error opening file: {ex.Message}");
            ErrorLogger.LogError(ex, "Opening file dialog");
        }
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
                    MarkdownViewer.Markdown = "## Processing request...\n\nPlease wait while the operation completes. The UI will remain responsive.";
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

        LogOperation($"Zoom set to {_markdownZoomLevel:F0}%");

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
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToVerticalOffset(verticalOffset);
            }
        }), DispatcherPriority.Loaded);
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
                TxtLog.Text = TxtLog.Text.Substring(TxtLog.Text.Length - 40000);
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
        CboPreviousKeys.Items.Clear();
        CboPreviousKeys.Items.Add("Select a key");

        var savedKeys = _keyManager.GetKeysForProvider(apiProvider);
        foreach (var key in savedKeys)
        {
            CboPreviousKeys.Items.Add(MaskKey(key));
        }

        CboPreviousKeys.SelectedIndex = 0;

        TxtApiKey.Clear();
    }

    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 8)
            return "*****";

        return string.Concat(key.AsSpan(0, 3), "*****", key.AsSpan(key.Length - 3));
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
            
                // Show processing state without blocking UI
                // SetProcessingState(true, "Scanning folder");
            
                // Use Task.Run to move heavy processing to background thread
                await Task.Run(async () => 
                {
                    try 
                    {
                        // Clear collections on background thread
                        _filesByExtension.Clear();
                    
                        // Clear UI on UI thread
                        await Dispatcher.InvokeAsync(() => 
                        {
                            LvFiles.Items.Clear();
                            LogOperation($"Starting folder scan: {_selectedFolder}");
                            StartOperationTimer("FolderScan");
                        });
                    
                        // Scan files on background thread
                        await FindSourceFilesAsync(_selectedFolder);
                    
                        // Update UI with results on UI thread
                        await Dispatcher.InvokeAsync(() => 
                        {
                            var totalFiles = _filesByExtension.Values.Sum(list => list.Count);
                            TxtStatus.Text = $"Found {totalFiles} source files.";
                        
                            // Display files organized by folder
                            DisplayFilesByFolder();
                        
                            EndOperationTimer("FolderScan");
                            CalculateTotalTokens();
                            
                            SetProcessingState(false);
                        });
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() => 
                        {
                            LogOperation($"Error in background scanning: {ex.Message}");
                            SetProcessingState(false);
                        });
                    }
                });
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
        
            // Ensure processing state is reset
            SetProcessingState(false);
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
            // SetProcessingState(true, "Scanning folder");

            _filesByExtension.Clear();
            LvFiles.Items.Clear();

            // Make FindSourceFiles async and await it directly
            await FindSourceFilesAsync(_selectedFolder);

            // Update UI on UI thread
            await Dispatcher.InvokeAsync(() =>
            {
                var totalFiles = _filesByExtension.Values.Sum(list => list.Count);
                TxtStatus.Text = $"Found {totalFiles} source files.";

                // Display files organized by folder
                DisplayFilesByFolder();

                EndOperationTimer("FolderScan");
                CalculateTotalTokens();
            });
        }
        catch (Exception ex)
        {
            LogOperation($"Error scanning folder: {ex.Message}");
            ErrorLogger.LogError(ex, "Scanning folder");
            TxtStatus.Text = "Error scanning folder.";
        }
        finally
        {
            SetProcessingState(false);
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

    private static string GetLanguageGroupForExtension(string ext)
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
        // Return only the "All Files" filter as requested
        return "All Files (*.*)|*.*";
    }

    private async void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Create file selection dialog
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select Source Files",
                Filter = GenerateFileFilterFromExtensions()
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
                    await Dispatcher.InvokeAsync(() => LvFiles.Items.Clear());
                
                    LogOperation($"Base folder set to: {_selectedFolder}");
                }

                // SetProcessingState(true, "Processing selected files");
                LogOperation($"Processing {dialog.FileNames.Length} selected files");
                StartOperationTimer("ProcessSelectedFiles");

                // Process files in background without blocking UI
                await Task.Run(async () => 
                {
                    try 
                    {
                        // Process files without blocking UI thread
                        await ProcessSelectedFilesAsync(dialog.FileNames);
                        
                        // Update UI on UI thread
                        await Dispatcher.InvokeAsync(() => 
                        {
                            var totalFiles = _filesByExtension.Values.Sum(list => list.Count);
                            LogOperation($"Total files after selection: {totalFiles}");

                            // Update the files view
                            DisplayFilesByFolder();
                        
                            EndOperationTimer("ProcessSelectedFiles");
                            
                            SetProcessingState(false);
                        });
                    }
                    catch (Exception ex)
                    {
                        await Dispatcher.InvokeAsync(() => 
                        {
                            LogOperation($"Error in background processing: {ex.Message}");
                        });
                    }
                });
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
        
            // Ensure processing state is reset
            SetProcessingState(false);
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

                // Check file size limit only, don't filter by extension
                var fileSizeKb = (int)(fileInfo.Length / 1024);
                if (fileSizeKb <= _settingsManager.Settings.MaxFileSizeKb)
                {
                    // Get a relative path (if the file is not in the selected folder, it will use its full path)
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

                    // Initialize the extension group if needed
                    if (!_filesByExtension.TryGetValue(ext, out var value))
                    {
                        value = new List<SourceFile>();
                        _filesByExtension[ext] = value;
                    }

                    // Check if this file is already added
                    if (!value.Any(f => f.Path.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        value.Add(new SourceFile
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
            catch (Exception ex)
            {
                LogOperation($"Error processing file {filePath}: {ex.Message}");
            }
        }

        CalculateTotalTokens();
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
        finally
        {
            _estimatedTokenCount = 0;
            UpdateTokenCountDisplay();
        }
    }

    private async Task FindSourceFilesAsync(string folderPath)
    {
        try 
        {
            var dirInfo = new DirectoryInfo(folderPath);
        
            // Get all files with source extensions in this directory
            var files = dirInfo.EnumerateFiles()
                .Where(f => _settingsManager.Settings.SourceFileExtensions.Contains(f.Extension.ToLowerInvariant()))
                .Where(f => f.Length / 1024 <= _settingsManager.Settings.MaxFileSizeKb)
                .ToList();
            
            // Process files in batches
            const int batchSize = 50;
            for (var i = 0; i < files.Count; i += batchSize)
            {
                // Take a batch of files
                var batch = files.Skip(i).Take(batchSize).ToList();
            
                // Update UI with progress every few batches
                if (i % 100 == 0)
                {
                    await Dispatcher.InvokeAsync(() => 
                    {
                        TxtStatus.Text = $"Scanning folder... ({i}/{files.Count} files processed)";
                    });
                }
            
                // Process batch of files
                var fileTasks = new List<Task>();
                foreach (var file in batch)
                {
                    fileTasks.Add(ProcessFileInScanAsync(file, i));
                }
            
                // Wait for all files in batch to complete
                await Task.WhenAll(fileTasks);
            }
        
            // Process subdirectories with concurrency limit
            var subDirs = dirInfo.GetDirectories()
                .Where(d => !d.Attributes.HasFlag(FileAttributes.Hidden) &&
                            !d.Attributes.HasFlag(FileAttributes.System) &&
                            !d.Name.StartsWith('.') &&
                            !d.Name.Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                            !d.Name.Equals("obj", StringComparison.OrdinalIgnoreCase) &&
                            !d.Name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) &&
                            !d.Name.Equals("packages", StringComparison.OrdinalIgnoreCase))
                .ToList();
        
            // Process directories with throttling
            var dirThrottler = new SemaphoreSlimSafe(5); // Process up to 5 directories at once
            var dirTasks = new List<Task>();
        
            foreach (var dir in subDirs)
            {
                await dirThrottler.WaitAsync();
                dirTasks.Add(ProcessSubdirectoryAsync(dir, dirThrottler));
            }
        
            // Wait for all directory tasks to complete
            await Task.WhenAll(dirTasks);
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => 
                LogOperation($"Error scanning directory {folderPath}: {ex.Message}")
            );
        }
    }
    
    private async Task ProcessFileInScanAsync(FileInfo file, int batchIndex)
    {
        var ext = file.Extension.ToLowerInvariant();
        try
        {
            // Read file content asynchronously - OUTSIDE any lock
            var content = await File.ReadAllTextAsync(file.FullName);
        
            // Create the source file object
            var sourceFile = new SourceFile
            {
                Path = file.FullName,
                RelativePath = file.FullName.Replace(_selectedFolder, "").TrimStart('\\', '/'),
                Extension = ext,
                Content = content
            };
        
            var fileAdded = false;
        
            // Add to collection with thread safety
            lock (_filesByExtension)
            {
                if (!_filesByExtension.TryGetValue(ext, out var filesList))
                {
                    filesList = new List<SourceFile>();
                    _filesByExtension[ext] = filesList;
                }
            
                // Skip duplicates
                if (!filesList.Any(f => f.Path.Equals(file.FullName, StringComparison.OrdinalIgnoreCase)))
                {
                    filesList.Add(sourceFile);
                    fileAdded = true;
                }
            }
        
            // Only log if needed (and outside the lock)
            if (fileAdded && batchIndex % 100 == 0) // Only log every 100 files to avoid flood
            {
                await Dispatcher.InvokeAsync(() => 
                    LogOperation($"Added file: {sourceFile.RelativePath}")
                );
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => 
                LogOperation($"Error reading file {file.FullName}: {ex.Message}")
            );
        }
    }
    
    private async Task ProcessSubdirectoryAsync(DirectoryInfo dir, SemaphoreSlimSafe throttler)
    {
        try
        {
            await FindSourceFilesAsync(dir.FullName);
        }
        finally
        {
            throttler.Release();
        }
    }

    private async Task ProcessFileBatchAsync(List<FileInfo> files)
    {
        await Task.Run(() =>
        {
            foreach (var file in files)
            {
                var ext = file.Extension.ToLowerInvariant();

                string content;
                try
                {
                    // Using synchronous File.ReadAllText inside Task.Run
                    // This is a reasonable approach since we're already in a background thread
                    content = File.ReadAllText(file.FullName);
                }
                catch (Exception ex)
                {
                    // Log error but continue processing other files
                    Dispatcher.InvokeAsync(() =>
                        LogOperation($"Error reading file {file.FullName}: {ex.Message}")
                    );
                    continue;
                }

                lock (_filesByExtension)
                {
                    // Initialize the extension group if needed
                    if (!_filesByExtension.TryGetValue(ext, out var value))
                    {
                        value = new List<SourceFile>();
                        _filesByExtension[ext] = value;
                    }

                    // Check if this file is already added
                    if (!value.Any(f => f.Path.Equals(file.FullName, StringComparison.OrdinalIgnoreCase)))
                    {
                        value.Add(new SourceFile
                        {
                            Path = file.FullName,
                            RelativePath = file.FullName.Replace(_selectedFolder, "").TrimStart('\\', '/'),
                            Extension = ext,
                            Content = content
                        });

                        Dispatcher.InvokeAsync(() =>
                            LogOperation($"Added file: {file.FullName.Replace(_selectedFolder, "").TrimStart('\\', '/')}")
                        );
                    }
                    else
                    {
                        Dispatcher.InvokeAsync(() =>
                            LogOperation($"Skipped duplicate file: {file.FullName.Replace(_selectedFolder, "").TrimStart('\\', '/')}")
                        );
                    }
                }
            }
        });
    }

    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        // Check for API key
        if (string.IsNullOrEmpty(TxtApiKey.Password))
        {
            MessageBox.Show("Please enter an API key.", "Missing API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
            LogOperation("Analysis canceled: No API key provided");
            return;
        }

        // Get query text and API selection
        var queryText = TxtFollowupQuestion.Text.Trim();
        var apiSelection = CboAiApi.SelectedItem?.ToString() ?? "Claude API"; // Default to Claude

        // Setup processing UI
        TxtStatus.Text = $"Processing with {apiSelection}...";
        SetProcessingState(true, $"Processing with {apiSelection}");
        LogOperation($"Starting query with {apiSelection}");
        StartOperationTimer("QueryProcessing");

        try
        {
            // Determine context mode and generate appropriate prompt
            string prompt;
            var isFollowUp = _conversationHistory.Count >= 2 && SendfollowUpReminder.IsChecked == true;
        
            // Create a temporary list to track files for this specific query
            var currentQueryFiles = new List<SourceFile>();

            if (isFollowUp)
            {
                // Process as follow-up query - use existing file history
                LogOperation($"Handling as follow-up question with {_lastIncludedFiles.Count} previous files");
                prompt = GenerateContextualFollowupPrompt(queryText);

                // Add selected files if option is checked
                if (ChkIncludeSelectedFiles.IsChecked == true && LvFiles.SelectedItems.Count > 0)
                {
                    // Pass current list without clearing _lastIncludedFiles
                    prompt = AppendSelectedFilesToPrompt(prompt, currentQueryFiles);
                    LogOperation($"Added {currentQueryFiles.Count} selected files to follow-up prompt");
                
                    // Update master list with any newly selected files
                    if (currentQueryFiles.Count > 0)
                    {
                        // Add any new files that aren't already in the master list
                        foreach (var file in currentQueryFiles)
                        {
                            if (_lastIncludedFiles.All(f => f.Path != file.Path))
                            {
                                _lastIncludedFiles.Add(file);
                            }
                        }
                    }
                }

                // Add to conversation history
                _conversationHistory.Add(new ChatMessage { Role = "user", Content = queryText });
            }
            else
            {
                // Process as initial query or simple follow-up
                LogOperation("Handling as initial query or simple follow-up");
            
                // Prepare files if option is checked
                var consolidatedFiles = new Dictionary<string, List<string>>();
                if (ChkIncludeSelectedFiles.IsChecked == true)
                {
                    LogOperation("Preparing consolidated files for prompt");
                    StartOperationTimer("PrepareFiles");
                    consolidatedFiles = PrepareConsolidatedFiles(currentQueryFiles);
                    EndOperationTimer("PrepareFiles");
                    LogOperation($"Included {currentQueryFiles.Count} files");
                
                    // For new queries, replace the master list completely
                    _lastIncludedFiles.Clear();
                    _lastIncludedFiles.AddRange(currentQueryFiles);
                    LogOperation($"Updated master file list with {_lastIncludedFiles.Count} files");
                }
                else
                {
                    // If not including files in this query, clear the master list
                    _lastIncludedFiles.Clear();
                    LogOperation("Cleared master file list as no files were included");
                }

                // Clear history if not using follow-up reminder
                if (_conversationHistory.Count < 2 || SendfollowUpReminder.IsChecked == false)
                {
                    _conversationHistory.Clear();
                    LogOperation("Conversation history cleared for new query");
                }

                // Generate prompt based on template option
                StartOperationTimer("GeneratePrompt");
                if (ChkIncludeInitialPrompt.IsChecked == true)
                {
                    LogOperation("Using prompt template");
                    prompt = GenerateInitialPrompt(consolidatedFiles);
                }
                else
                {
                    LogOperation("Using minimal prompt (no template)");
                    prompt = GenerateMinimalPrompt(consolidatedFiles);
                }

                EndOperationTimer("GeneratePrompt");

                // Add user query if provided
                if (!string.IsNullOrEmpty(queryText))
                {
                    prompt += "\n\n--- Additional Instructions/Question ---\n" + queryText;
                    LogOperation("Added query text to the prompt");
                }

                // Add to conversation history
                _conversationHistory.Add(new ChatMessage { Role = "user", Content = prompt });
            }

            // Store final prompt and send to AI
            _lastInputPrompt = prompt;
            var response = await SendToAiApi(apiSelection, prompt);
        
            // Update conversation and UI
            _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = response });
            LogOperation($"Updated conversation history with response and file list ({_lastIncludedFiles.Count} files)");
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

    private string GenerateMinimalPrompt(Dictionary<string, List<string>> consolidatedFiles)
    {
        var prompt = new StringBuilder();

        // Skip the template, add a minimal instruction if files are included
        if (consolidatedFiles.Count != 0)
        {
            prompt.AppendLine("Please analyze the following code files:");
            prompt.AppendLine();

            // Include files, grouped by extension
            foreach (var ext in consolidatedFiles.Keys)
            {
                prompt.AppendLine(CultureInfo.InvariantCulture, $"--- {ext.ToUpperInvariant()} FILES ---");
                prompt.AppendLine();

                foreach (var fileContent in consolidatedFiles[ext])
                {
                    prompt.AppendLine(fileContent);
                    prompt.AppendLine();
                }
            }
        }
        else
        {
            prompt.AppendLine("Please respond to the following:"); // Base instruction if no files
            prompt.AppendLine();
        }


        return prompt.ToString();
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
            ".txt" => "text",
            ".sql" => "sql",
            ".sh" or ".bash" => "bash",
            ".ps1" => "powershell",
            ".r" => "r",
            ".vb" => "vb",
            ".fs" => "fsharp",
            ".lua" => "lua",
            ".pl" => "perl",
            ".groovy" => "groovy",
            ".dockerfile" => "dockerfile",
            ".ini" => "ini",
            ".toml" => "toml",
            _ => "text" // Default to "text" for unknown extensions
        };
    }

    private string GenerateInitialPrompt(Dictionary<string, List<string>> consolidatedFiles)
    {
        var prompt = new StringBuilder();

        // Get the selected prompt content from the dropdown or settings
        var selectedPromptTemplate = CboPromptTemplate.SelectedItem as CodePrompt;
        var promptTemplate = selectedPromptTemplate?.Content ?? _settingsManager.Settings.InitialPrompt; // Fallback

        // Use the template as the basis for the prompt
        prompt.AppendLine(promptTemplate);
        prompt.AppendLine();

        // Include files, grouped by extension, if any
        if (consolidatedFiles.Count != 0)
        {
            foreach (var ext in consolidatedFiles.Keys)
            {
                prompt.AppendLine(CultureInfo.InvariantCulture, $"--- {ext.ToUpperInvariant()} FILES ---");
                prompt.AppendLine();

                foreach (var fileContent in consolidatedFiles[ext])
                {
                    prompt.AppendLine(fileContent);
                    prompt.AppendLine();
                }
            }
        }
        else
        {
            prompt.AppendLine("--- NO FILES INCLUDED ---");
            prompt.AppendLine();
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

        // Update status and show processing indicators
        var statusMessage = $"Sending follow-up question to {apiSelection}";
        TxtStatus.Text = $"{statusMessage}...";
        SetProcessingState(true, statusMessage);

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
                enhancedPrompt = AppendSelectedFilesToPrompt(enhancedPrompt, _lastIncludedFiles);
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
            UpdateResponseDisplay(response, true);

            // Clear the follow-up question text box
            TxtFollowupQuestion.Text = "";

            TxtStatus.Text = "Follow-up response received!";
            EndOperationTimer("FollowupQuestion");
            LogOperation("Follow-up question workflow completed");
        }
        catch (Exception ex)
        {
            LogOperation($"Error sending follow-up question: {ex.Message}");

            // Only show the error dialog if it's not a token limit error
            // that we've already handled
            if (!ex.Message.StartsWith("Token limit exceeded:", StringComparison.Ordinal))
            {
                ErrorLogger.LogError(ex, "Sending follow-up question");
            }

            TxtStatus.Text = "Error sending follow-up question.";
            EndOperationTimer("FollowupQuestion");
        }
        finally
        {
            // Always reset the processing state
            SetProcessingState(false);
        }
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

        // Ensure LvFiles.SelectedItems is accessed on the UI thread if necessary,
        // but since this method is likely called from BtnAnalyze_Click (UI thread), it should be fine.
        foreach (var item in LvFiles.SelectedItems)
        {
            // Check if the item is a string representing a file path/name
            if (item is string displayString)
            {
                // Basic check to filter out headers or summary lines
                if (!displayString.StartsWith("=====", StringComparison.Ordinal) && !displayString.Contains(" files") && !displayString.Contains(" - "))
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
                        selectedFilesContent.AppendLine(CultureInfo.InvariantCulture, $"File: {matchingFile.RelativePath}");
                        selectedFilesContent.AppendLine(CultureInfo.InvariantCulture, $"```{GetLanguageForExtension(matchingFile.Extension)}");
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
            promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"Specifically, I've selected the following {fileCount} file(s) for you to focus on or reference:");
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

    private string GenerateContextualFollowupPrompt(string followupQuestion)
    {
        // Check if the reminder/context should be added
        if (SendfollowUpReminder.IsChecked == true)
        {
            // Check if there are previous messages to provide context from
            if (_conversationHistory.Count < 2)
            {
                // If no previous conversation, just return the follow-up question as is
                LogOperation("Sending follow-up question without context (no prior conversation)");
                return followupQuestion;
            }

            var promptBuilder = new StringBuilder();

            // Add context about this being a follow-up question about previously analyzed code
            promptBuilder.AppendLine("This is a follow-up question regarding the source code I previously shared with you. Please reference the code files and our earlier discussion when responding.");
            promptBuilder.AppendLine();

            // List a few files analyzed to provide better context (using the *actual* files from the last analysis)
            promptBuilder.AppendLine("The previous analysis likely covered files including:");

            // Get up to 5 representative files from the *last included* files
            var filesList = _lastIncludedFiles.Take(5).Select(f => f.RelativePath).ToList();

            // Add file names to the prompt
            if (filesList.Count != 0)
            {
                foreach (var file in filesList)
                {
                    promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"- {file}");
                }

                // Add the total file count for context
                var totalFiles = _lastIncludedFiles.Count;
                if (totalFiles > filesList.Count)
                {
                    promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"And {totalFiles - filesList.Count} more files.");
                }
            }
            else
            {
                promptBuilder.AppendLine("(No specific files were recorded from the previous interaction)");
            }

            promptBuilder.AppendLine();

            // Finally, add the actual follow-up question
            promptBuilder.AppendLine("My follow-up question is:");
            promptBuilder.AppendLine(followupQuestion);

            LogOperation("Generated contextual follow-up prompt");
            return promptBuilder.ToString();
        }
        else
        {
            // If the reminder checkbox is unchecked, send the question directly
            LogOperation("Sending follow-up question without context (reminder unchecked)");
            return followupQuestion;
        }
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
                MessageBox.Show("There is no response to save.", "No Response", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    MessageBox.Show($"File saved: {Path.GetFileName(_currentFilePath)}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
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
                MessageBox.Show($"Response saved to {saveFileDialog.FileName}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error saving response: {ex.Message}");
            ErrorLogger.LogError(ex, "Saving response to file");
            MessageBox.Show("An error occurred while saving the response.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetProcessingState(false);
        }
    }

    private void BtnToggleMarkdown_Click(object sender, RoutedEventArgs e)
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
                        "Save Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

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
            MessageBox.Show("An error occurred while toggling the markdown view.", "View Error", MessageBoxButton.OK, MessageBoxImage.Error);

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

                LogOperation($"Updated Markdown page width to {contentWidth:F0}px (90% of {containerWidth:F0}px)");
            }
        }
        catch (Exception ex)
        {
            // Log the error but don't crash the application
            LogOperation($"Error setting Markdown document properties: {ex.Message}");
        }
    }

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
                // No automatic selection
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
                // No automatic selection
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
                // No automatic selection
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
                // No automatic selection
            }
            else if (providerName == "ChatGPT API")
            {
                var provider = (OpenAi)_apiProviderFactory.GetProvider(providerName);
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
                // No automatic selection
            }
            else
            {
                // For other providers, disable the model dropdown
                CboModel.IsEnabled = false;
                CboModel.Items.Add("Default model");
                CboModel.SelectedIndex = 0; // Only set this for unsupported providers
                CboModel.ToolTip = null;
            }
        }
        catch (Exception ex)
        {
            LogOperation($"Error populating model dropdown: {ex.Message}");
        }
    }

    private void CboAiApi_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboAiApi.SelectedItem != null)
        {
            var apiSelection = CboAiApi.SelectedItem.ToString() ?? string.Empty;
            UpdatePreviousKeys(apiSelection);
            PopulateModelDropdown();

            // Clear the model description initially
            TxtModelDescription.Text = string.Empty;

            // Add an instruction to select a model
            if (CboModel.IsEnabled)
            {
                TxtModelDescription.Text = "Please select a model from the dropdown above.";
            }
        }
    }

    private async Task ProcessSelectedFilesAsync(string[] filePaths)
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
                    if (!_filesByExtension.TryGetValue(ext, out List<SourceFile>? value))
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
                    LogOperation($"Skipped file due to size limit: {filePath} ({fileSizeKb} KB > {_settingsManager.Settings.MaxFileSizeKb} KB)")
                );
            }
        }
        finally
        {
            // Release the throttler when done
            throttler.Release();
        }
    }


    private void CboModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboModel.SelectedItem is ModelDropdownItem selectedModel)
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
            if (CboModel.IsEnabled && CboModel.SelectedItem is ModelDropdownItem selectedModel)
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
            else if (provider is OpenAi openAiProvider && modelId != null)
            {
                response = await openAiProvider.SendPromptWithModelAsync(TxtApiKey.Password, prompt, _conversationHistory, modelId);
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

            // Check if it's a token limit error
            if (ex.Message.Contains("maximum context length") || ex.Message.Contains("token limit exceeded"))
            {
                // Log error silently (no dialog)
                ErrorLogger.LogErrorSilently(ex, $"Sending prompt to {apiSelection}");

                // Parse error message to extract token information
                var actualTokens = 0;
                var modelLimit = 0;

                try
                {
                    // Try to parse the token information from error message
                    // Typical message: "...maximum context length is 65536 tokens. However, you requested 97153 tokens (88961 in the messages..."
                    var message = ex.Message;

                    // Extract model limit
                    var limitMatch = MyRegex().Match(message);
                    if (limitMatch.Success && limitMatch.Groups.Count > 1)
                    {
                        modelLimit = int.Parse(limitMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    }

                    // Extract actual tokens
                    var tokensMatch = MyRegex1().Match(message);
                    if (tokensMatch.Success && tokensMatch.Groups.Count > 1)
                    {
                        actualTokens = int.Parse(tokensMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    }

                    // Handle the token limit error with our specialized method
                    if (actualTokens > 0 && modelLimit > 0)
                    {
                        HandleTokenLimitError(actualTokens, modelLimit);

                        // Throw a custom exception so the calling method knows what happened
                        throw new ApplicationException($"Token limit exceeded: {actualTokens} tokens exceeds model limit of {modelLimit}");
                    }
                }
                catch (FormatException)
                {
                    // If parsing fails, just continue with normal error handling
                }
            }

            // Don't log the "No model selected" exception that we throw ourselves
            if (ex.Message != "No model selected")
            {
                // For non-token limit errors, log as normal
                ErrorLogger.LogError(ex, $"Sending prompt to {apiSelection}");
            }

            throw; // Re-throw to let the caller handle it
        }
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

    private void CboPromptTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboPromptTemplate.SelectedItem is CodePrompt selectedPrompt)
        {
            _settingsManager.Settings.SelectedPromptName = selectedPrompt.Name;
            _settingsManager.SaveSettings();
            LogOperation($"Selected prompt template: {selectedPrompt.Name}");
        }
    }

    private void BtnConfigurePrompts_Click(object sender, RoutedEventArgs e)
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

    private static int EstimateTokenCount(string text, string fileExtension = "")
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        // Base tokenization ratio - approximately 2.5 characters per token
        var charsPerToken = 2.5;

        // Adjust based on file type - code files generally use more tokens per character
        if (!string.IsNullOrEmpty(fileExtension))
        {
            charsPerToken = fileExtension switch
            {
                // Code files often have more tokens due to special characters and syntax
                ".cs" => 2.2, // C# has many operators and punctuation
                ".java" => 2.2, // Similar to C#
                ".xaml" => 2.0, // XML-based files have many tags and attributes
                ".xml" => 2.0, // XML has many brackets and quotes
                ".json" => 2.0, // JSON has many quotes and syntax characters
                ".cpp" => 2.2, // C++ similar to C#
                ".js" => 2.3, // JavaScript
                ".py" => 2.5, // Python is somewhat more compact
                ".html" => 2.0, // HTML has many tags
                ".css" => 2.3, // CSS has special characters
                ".md" => 3.0, // Markdown is closer to natural language
                ".txt" => 3.5, // Plain text is closest to natural language
                _ => 2.5 // Default conservative estimate
            };
        }

        // Token calculation with more conservative rounding
        return (int)Math.Ceiling(text.Length / charsPerToken);
    }

    private void CalculateTotalTokens()
    {
        _estimatedTokenCount = 0;

        // Calculate tokens for each file with extension-specific adjustments
        foreach (var extensionGroup in _filesByExtension)
        {
            var extension = extensionGroup.Key;
            foreach (var file in extensionGroup.Value)
            {
                _estimatedTokenCount += EstimateTokenCount(file.Content, extension);
            }
        }

        // Add tokens for the initial prompt
        var promptTemplate = _settingsManager.Settings.InitialPrompt;
        _estimatedTokenCount += EstimateTokenCount(promptTemplate);

        // Add tokens for file headers and formatting (increased estimate)
        // Each file gets a header like "File: path.ext" and code block markers
        var fileCount = _filesByExtension.Values.Sum(list => list.Count);
        _estimatedTokenCount += fileCount * EstimateTokenCount("File: filename.ext\n```language\n\n```\n");

        // Add tokens for section headers and structural overhead
        // This accounts for extension headers like "--- .CS FILES ---" and extra formatting
        var extensionCount = _filesByExtension.Keys.Count;
        _estimatedTokenCount += extensionCount * EstimateTokenCount("--- .EXTENSION FILES ---\n\n");

        // Add additional overhead for organizing and structuring the content (15% buffer)
        _estimatedTokenCount = (int)(_estimatedTokenCount * 1.15);

        // Update the UI with the new estimate
        UpdateTokenCountDisplay();
    }

    private void UpdateTokenCountDisplay()
    {
        Dispatcher.Invoke(() =>
        {
            // Calculate lower and upper bounds with wider range (±30%)
            var lowerBound = (int)(_estimatedTokenCount * 0.80);
            var upperBound = (int)(_estimatedTokenCount * 1.50); // Wider upper bound for safety

            // Format the display text with the range
            TxtTokenCount.Text = $"Estimated Input Tokens: {_estimatedTokenCount:N0} (range {lowerBound:N0} - {upperBound:N0})";

            // Detailed tooltip with model-specific information
            var tooltipText = $"Base estimate: {_estimatedTokenCount:N0} tokens\n" +
                              $"Range: {lowerBound:N0} - {upperBound:N0} tokens\n\n" +
                              "⚠️ Token estimates are approximate and may vary by model\n";

            TxtTokenCount.ToolTip = tooltipText;

            LogOperation($"Updated token estimate range: {lowerBound:N0} - {upperBound:N0} tokens (base: {_estimatedTokenCount:N0})");
        });
    }

    private void HandleTokenLimitError(int actualTokens, int modelLimit)
    {
        // Update our estimation algorithm based on actual token usage
        var observedRatio = (double)_estimatedTokenCount / actualTokens;
        LogOperation($"Token estimation accuracy: {observedRatio:P2} (estimated: {_estimatedTokenCount:N0}, actual: {actualTokens:N0})");

        // Show error dialog with helpful guidance
        MessageBox.Show(
            $"Error: Token limit exceeded\n\n" +
            $"Your code contains approximately {actualTokens:N0} tokens, but the model's limit is {modelLimit:N0} tokens.\n\n" +
            "To fix this issue, try one of these approaches:\n" +
            "• Remove non-essential files from your selection\n" +
            "• Focus on specific parts of your codebase\n" +
            "• Try a model with a larger context window, if available\n\n" +
            "The token count display has been updated to reflect this information.",
            "Token Limit Exceeded",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Update the estimation display with actual token count for future reference
        _estimatedTokenCount = actualTokens;
        UpdateTokenCountDisplay();
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

    private T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) return null;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);

            var result = (child as T) ?? FindVisualChild<T>(child);
            if (result != null) return result;
        }

        return null;
    }

    private Button? FindButtonByContent(string content)
    {
        // Find all buttons in the window
        var buttons = FindVisualChildren<Button>(this);

        // Look for a button with matching content
        foreach (var button in buttons)
        {
            if (button.Content is string buttonContent && buttonContent == content)
            {
                return button;
            }
        }

        return null;
    }

    private FrameworkElement? FindNameInWindow(string name)
    {
        return FindName(name) as FrameworkElement;
    }

    private IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);

            if (child is T typedChild)
                yield return typedChild;

            foreach (var childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
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
                LvFiles.Items.Clear();
                _selectedFolder = string.Empty;
                TxtSelectedFolder.Text = string.Empty;
                _estimatedTokenCount = 0;
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
                ChkIncludeInitialPrompt.IsChecked = true;
                ChkIncludeSelectedFiles.IsChecked = true;
                SendfollowUpReminder.IsChecked = false; // Reset reminder checkbox


                // Reset AI provider
                CboAiApi.SelectedIndex = -1; // Default to none

                // Reset AI Model
                CboModel.SelectedIndex = -1; // Default to none

                // Reset API Key
                TxtApiKey.Clear();
                CboPreviousKeys.SelectedIndex = -1; // Default to none

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

    private async void BtnSaveEdits_Click(object sender, RoutedEventArgs e)
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
            else // If in Markdown view, the content is conceptually read-only here, but we sync anyway
            {
                // Potentially get content from MarkdownViewer if it were editable,
                // but Markdig.Wpf viewer isn't directly editable like TextBox.
                // We assume edits happen in TxtResponse. So, if _isMarkdownViewActive is true,
                // we might need to decide if saving edits is allowed or if the user should switch first.
                // For simplicity, let's assume edits are primarily done in TxtResponse.
                // If the user somehow edited the underlying document (unlikely with current setup),
                // those changes wouldn't be captured here.
                // Let's update _previousMarkdownContent just in case.
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
                // --- Show Input Query ---
                LogOperation("Showing input query view");

                // 1. Save current Markdown view content
                // We assume _previousMarkdownContent holds the latest AI response Markdown

                // 2. Generate File Summary
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

                // 3. Combine Summary and Prompt
                var fullInputView = new StringBuilder();
                fullInputView.Append(fileSummary);
                fullInputView.AppendLine("## Full Input Prompt Sent to AI");
                fullInputView.AppendLine("```text"); // Display prompt as plain text
                fullInputView.AppendLine(_lastInputPrompt);
                fullInputView.AppendLine("```");

                // 4. Update MarkdownViewer
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

                // 5. Update Button Text
                BtnShowInputQuery.Content = "Show AI Response";

                // 6. Disable irrelevant buttons
                BtnPreviousResponse.IsEnabled = false;
                BtnNextResponse.IsEnabled = false;
                BtnSaveResponse.IsEnabled = false;
                BtnSaveEdits.IsEnabled = false;
                BtnToggleMarkdown.IsEnabled = false; // Disable toggling while showing input
            }
            else
            {
                // --- Show AI Response ---
                LogOperation("Showing AI response view");

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

    [GeneratedRegex(@"maximum context length is (\d+)")]
    private static partial Regex MyRegex();

    [GeneratedRegex(@"you requested (\d+) tokens")]
    private static partial Regex MyRegex1();
}