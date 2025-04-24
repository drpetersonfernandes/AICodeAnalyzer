using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AICodeAnalyzer.Models;
using AICodeAnalyzer.Services;
using Microsoft.Win32;
using System.Windows.Media;
using System.Windows.Input;
using ControlzEx.Theming;

namespace AICodeAnalyzer;

public partial class MainWindow
{
    // Services
    private readonly LoggingService _loggingService;
    private readonly FileService _fileService;
    private readonly AiProviderService _aiProviderService;
    private readonly ResponseService _responseService;
    private readonly UiStateManager _uiStateManager;
    private readonly TokenCounterService _tokenCounterService;

    // Additional managers
    private readonly RecentFilesManager _recentFilesManager = new();
    private readonly SettingsManager _settingsManager = new(); // SettingsManager is already here

    private readonly HtmlService _htmlService;

    // Zoom variables for WebView2
    private double _webViewZoomFactor = 1.0;
    private const double WebViewZoomStep = 0.1;
    private const double MinWebViewZoom = 0.2;
    private const double MaxWebViewZoom = 5.0;

    // Zoom variables for Raw Text Box
    private double _rawTextFontSize = 12.0; // Default font size from XAML
    private const double RawZoomStep = 2.0;
    private const double MinRawFontSize = 8.0;
    private const double MaxRawFontSize = 48.0;
    private bool _isShowingRawText;

    // State tracking
    private TokenCalculationResult _tokenCalculationResult = new();

    public MainWindow()
    {
        // SettingsManager is initialized here, loading settings from file
        var settings = _settingsManager.Settings;

        // Apply the loaded theme and accent *before* InitializeComponent
        try
        {
            ThemeManager.Current.ChangeTheme(Application.Current, settings.BaseTheme, settings.AccentColor);
        }
        catch (Exception ex)
        {
            Logger.LogErrorSilently(ex, $"Error applying theme from settings before InitializeComponent: {settings.BaseTheme}/{settings.AccentColor}");

            // Fallback to default theme if the saved one is invalid
            try
            {
                ThemeManager.Current.ChangeTheme(Application.Current, "Light", "Blue");
            }
            catch (Exception innerEx)
            {
                Logger.LogErrorSilently(innerEx, "Error applying default theme before InitializeComponent");
            }
        }

        InitializeComponent();

        // Initialize LoggingService after InitializeComponent
        _loggingService = new LoggingService(TxtLog);

        _loggingService.LogOperation($"Applied theme from settings: {settings.BaseTheme}/{settings.AccentColor}");

        // Initialize other services
        _tokenCounterService = new TokenCounterService(_loggingService);
        _fileService = new FileService(_settingsManager, _loggingService);
        _aiProviderService = new AiProviderService(_loggingService);
        _responseService = new ResponseService(_loggingService, _fileService);
        _uiStateManager = new UiStateManager(TxtStatus, _loggingService);
        _htmlService = new HtmlService(_loggingService);

        SetupEventHandlers();
        InitializeUi();
        InitializeThemeMenu();
    }

    private void SetupEventHandlers()
    {
        // File service events
        _fileService.FilesChanged += (_, _) => UpdateFileListDisplay();

        // Response service events
        _responseService.ResponseUpdated += (_, _) => UpdateResponseDisplay();
        _responseService.NavigationChanged += (_, _) => UpdateNavigationControls();

        // Wire up UI events to handlers
        // Unsubscribe the handler first before adding it
        BtnSelectFolder.Click -= BtnSelectFolder_Click;
        BtnSelectFolder.Click += BtnSelectFolder_Click;

        BtnSelectFiles.Click -= BtnSelectFiles_Click;
        BtnSelectFiles.Click += BtnSelectFiles_Click;

        BtnClearFiles.Click -= BtnClearFiles_Click;
        BtnClearFiles.Click += BtnClearFiles_Click;

        BtnSendQuery.Click -= BtnAnalyze_Click;
        BtnSendQuery.Click += BtnAnalyze_Click;

        BtnPreviousResponse.Click -= BtnPreviousResponse_Click;
        BtnPreviousResponse.Click += BtnPreviousResponse_Click;

        BtnNextResponse.Click -= BtnNextResponse_Click;
        BtnNextResponse.Click += BtnNextResponse_Click;

        BtnShowInputQuery.Click -= BtnShowInputQuery_Click;
        BtnShowInputQuery.Click += BtnShowInputQuery_Click;

        BtnSaveResponse.Click -= BtnSaveResponse_Click;
        BtnSaveResponse.Click += BtnSaveResponse_Click;

        BtnSaveEdits.Click -= SaveEdits_Click;
        BtnSaveEdits.Click += SaveEdits_Click;

        AiProvider.SelectionChanged -= AiProvider_SelectionChanged;
        AiProvider.SelectionChanged += AiProvider_SelectionChanged;

        AiModel.SelectionChanged -= AiModel_SelectionChanged;
        AiModel.SelectionChanged += AiModel_SelectionChanged;

        PromptTemplates.SelectionChanged -= PromptTemplates_SelectionChanged;
        PromptTemplates.SelectionChanged += PromptTemplates_SelectionChanged;

        BtnContinue.Click -= BtnContinue_Click;
        BtnContinue.Click += BtnContinue_Click;

        BtnToggleHtml.Click -= BtnToggleHtml_Click;
        BtnToggleHtml.Click += BtnToggleHtml_Click;

        BtnZoomIn.Click -= BtnZoomIn_Click;
        BtnZoomIn.Click += BtnZoomIn_Click;

        BtnZoomOut.Click -= BtnZoomOut_Click;
        BtnZoomOut.Click += BtnZoomOut_Click;

        BtnResetZoom.Click -= BtnResetZoom_Click;
        BtnResetZoom.Click += BtnResetZoom_Click;

        // Theme Menu Event Handlers - Access MenuItems directly by their x:Name
        if (MenuConfigure != null)
        {
            MenuConfigure.Click -= MenuConfigure_Click;
            MenuConfigure.Click += MenuConfigure_Click;
        }

        if (MenuStart != null)
        {
            MenuStart.Click -= MenuStart_Click;
            MenuStart.Click += MenuStart_Click;
        }

        if (MenuAbout != null)
        {
            MenuAbout.Click -= MenuAbout_Click;
            MenuAbout.Click += MenuAbout_Click;
        }

        if (MenuOpenPastResponses != null)
        {
            MenuOpenPastResponses.Click -= MenuOpenPastResponses_Click;
            MenuOpenPastResponses.Click += MenuOpenPastResponses_Click;
        }

        if (MenuExit != null)
        {
            MenuExit.Click -= MenuExit_Click;
            MenuExit.Click += MenuExit_Click;
        }

        // Theme Menu Event Handlers - Access MenuItems directly by their x:Name
        if (Light != null)
        {
            Light.Click -= ChangeBaseTheme_Click;
            Light.Click += ChangeBaseTheme_Click;
        }

        if (Dark != null)
        {
            Dark.Click -= ChangeBaseTheme_Click;
            Dark.Click += ChangeBaseTheme_Click;
        }

        if (Red != null)
        {
            Red.Click -= ChangeAccentColor_Click;
            Red.Click += ChangeAccentColor_Click;
        }

        if (Green != null)
        {
            Green.Click -= ChangeAccentColor_Click;
            Green.Click += ChangeAccentColor_Click;
        }

        if (Blue != null)
        {
            Blue.Click -= ChangeAccentColor_Click;
            Blue.Click += ChangeAccentColor_Click;
        }

        if (Purple != null)
        {
            Purple.Click -= ChangeAccentColor_Click;
            Purple.Click += ChangeAccentColor_Click;
        }

        if (Orange != null)
        {
            Orange.Click -= ChangeAccentColor_Click;
            Orange.Click += ChangeAccentColor_Click;
        }

        if (Lime != null)
        {
            Lime.Click -= ChangeAccentColor_Click;
            Lime.Click += ChangeAccentColor_Click;
        }

        if (Emerald != null)
        {
            Emerald.Click -= ChangeAccentColor_Click;
            Emerald.Click += ChangeAccentColor_Click;
        }

        if (Teal != null)
        {
            Teal.Click -= ChangeAccentColor_Click;
            Teal.Click += ChangeAccentColor_Click;
        }

        if (Cyan != null)
        {
            Cyan.Click -= ChangeAccentColor_Click;
            Cyan.Click += ChangeAccentColor_Click;
        }

        if (Cobalt != null)
        {
            Cobalt.Click -= ChangeAccentColor_Click;
            Cobalt.Click += ChangeAccentColor_Click;
        }

        if (Indigo != null)
        {
            Indigo.Click -= ChangeAccentColor_Click;
            Indigo.Click += ChangeAccentColor_Click;
        }

        if (Violet != null)
        {
            Violet.Click -= ChangeAccentColor_Click;
            Violet.Click += ChangeAccentColor_Click;
        }

        if (Pink != null)
        {
            Pink.Click -= ChangeAccentColor_Click;
            Pink.Click += ChangeAccentColor_Click;
        }

        if (Magenta != null)
        {
            Magenta.Click -= ChangeAccentColor_Click;
            Magenta.Click += ChangeAccentColor_Click;
        }

        if (Crimson != null)
        {
            Crimson.Click -= ChangeAccentColor_Click;
            Crimson.Click += ChangeAccentColor_Click;
        }

        if (Amber != null)
        {
            Amber.Click -= ChangeAccentColor_Click;
            Amber.Click += ChangeAccentColor_Click;
        }

        if (Yellow != null)
        {
            Yellow.Click -= ChangeAccentColor_Click;
            Yellow.Click += ChangeAccentColor_Click;
        }

        if (Brown != null)
        {
            Brown.Click -= ChangeAccentColor_Click;
            Brown.Click += ChangeAccentColor_Click;
        }

        if (Olive != null)
        {
            Olive.Click -= ChangeAccentColor_Click;
            Olive.Click += ChangeAccentColor_Click;
        }

        if (Steel != null)
        {
            Steel.Click -= ChangeAccentColor_Click;
            Steel.Click += ChangeAccentColor_Click;
        }

        if (Mauve != null)
        {
            Mauve.Click -= ChangeAccentColor_Click;
            Mauve.Click += ChangeAccentColor_Click;
        }

        if (Taupe != null)
        {
            Taupe.Click -= ChangeAccentColor_Click;
            Taupe.Click += ChangeAccentColor_Click;
        }

        if (Sienna == null) return;

        Sienna.Click -= ChangeAccentColor_Click;
        Sienna.Click += ChangeAccentColor_Click;
    }

    private void InitializeUi()
    {
        // Populate UI elements
        PopulateAiProviders();
        LoadPromptTemplates();
        UpdateRecentFilesMenu();

        // Set the initial UI state
        TxtFollowupQuestion.IsEnabled = true;
        IncludeSelectedFilesChecker.IsEnabled = true;
        UpdateNavigationControls();

        // Ensure WebView2 is visible and TextBox is hidden initially
        if (HtmlViewer != null)
        {
            HtmlViewer.Visibility = Visibility.Visible;
        }

        if (RawResponseTextBox != null)
        {
            RawResponseTextBox.Visibility = Visibility.Collapsed;
            _rawTextFontSize = RawResponseTextBox.FontSize;
        }

        _isShowingRawText = false;
    }

    private void InitializeThemeMenu()
    {
        try
        {
            var currentTheme = ThemeManager.Current.DetectTheme(this);
            if (currentTheme == null) return;

            // Check Base Theme - Access MenuItems directly by their x:Name
            if (Light != null)
            {
                Light.IsChecked = currentTheme.BaseColorScheme == "Light";
            }

            if (Dark != null)
            {
                Dark.IsChecked = currentTheme.BaseColorScheme == "Dark";
            }

            // Check Accent Color - Access MenuItems directly by their x:Name
            var accentMenuItems = new List<MenuItem?>
            {
                Red, Green, Blue, Purple, Orange, Lime, Emerald, Teal, Cyan, Cobalt,
                Indigo, Violet, Pink, Magenta, Crimson, Amber, Yellow, Brown, Olive,
                Steel, Mauve, Taupe, Sienna
            };

            foreach (var menuItem in accentMenuItems)
            {
                if (menuItem == null) continue;
                // MahApps accent names match the MenuItem Headers (DynamicResource names)
                // Use the actual Header string for comparison
                var accentName = menuItem.Header.ToString();
                menuItem.IsChecked = currentTheme.ColorScheme == accentName;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation("Error initializing theme menu");
            Logger.LogError(ex, "Error initializing theme menu");
        }
    }

    private void ChangeBaseTheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem clickedMenuItem) return;

        try
        {
            var baseThemeName = clickedMenuItem.Header.ToString();
            if (baseThemeName == null) return;

            // Change theme
            ThemeManager.Current.ChangeThemeBaseColor(Application.Current, baseThemeName);
            _loggingService.LogOperation($"Changed base theme to: {baseThemeName}");

            // Update settings and save
            _settingsManager.Settings.BaseTheme = baseThemeName;
            _settingsManager.SaveSettings();
            _loggingService.LogOperation("Theme settings saved.");

            // Update check marks - Access MenuItems directly by their x:Name
            if (Light != null)
            {
                Light.IsChecked = clickedMenuItem == Light;
            }

            if (Dark != null)
            {
                Dark.IsChecked = clickedMenuItem == Dark;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation("Error changing base theme");
            Logger.LogError(ex, "Error changing base theme");
        }
    }

    private void ChangeAccentColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem clickedMenuItem) return;

        try
        {
            var accentName = clickedMenuItem.Header.ToString();
            if (accentName == null) return;

            ThemeManager.Current.ChangeThemeColorScheme(Application.Current, accentName);
            _loggingService.LogOperation($"Changed accent color to: {accentName}");

            // Update settings and save
            _settingsManager.Settings.AccentColor = accentName;
            _settingsManager.SaveSettings();
            _loggingService.LogOperation("Theme settings saved.");

            // Update check marks - Access MenuItems directly by their x:Name
            var accentMenuItems = new List<MenuItem?>
            {
                Red, Green, Blue, Purple, Orange, Lime, Emerald, Teal, Cyan, Cobalt,
                Indigo, Violet, Pink, Magenta, Crimson, Amber, Yellow, Brown, Olive,
                Steel, Mauve, Taupe, Sienna
            };

            foreach (var menuItem in accentMenuItems)
            {
                if (menuItem == null) continue;

                menuItem.IsChecked = clickedMenuItem == menuItem;
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation("Error changing accent color");
            Logger.LogError(ex, "Error changing accent color");
        }
    }

    private void PopulateAiProviders()
    {
        AiProvider.Items.Clear();

        foreach (var providerName in _aiProviderService.ProviderNames)
        {
            AiProvider.Items.Add(providerName);
        }

        AiProvider.SelectedIndex = -1;
    }

    private void UpdateFileListDisplay()
    {
        ListOfFiles.Items.Clear();

        DisplayFilesByFolder();
        CalculateTotalTokens();

        TxtSelectedFolder.Text = _fileService.SelectedFolder;
    }

    private void DisplayFilesByFolder()
    {
        // First, organize files by their folder structure
        var filesByFolder = new Dictionary<string, List<SourceFile>>();

        // Track how many files were manually added vs. found through folder scan
        var manuallyAddedFiles = 0;
        var folderScannedFiles = 0;

        // Group all files by their parent folder
        foreach (var extensionFiles in _fileService.FilesByExtension.Values)
        {
            foreach (var file in extensionFiles)
            {
                // Check if this file is inside or outside the base folder
                var isOutsideBaseFolder = !file.Path.StartsWith(_fileService.SelectedFolder, StringComparison.OrdinalIgnoreCase);

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

        // Add a mode indicator (folder scan or manual selection)
        ListOfFiles.Items.Add(new ListViewItem
        {
            Content = $"===== Files Summary ({_fileService.FilesByExtension.Values.Sum(static v => v.Count)} total) =====",
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

        foreach (var ext in _fileService.FilesByExtension.Keys.OrderBy(static k => k))
        {
            var count = _fileService.FilesByExtension[ext].Count;
            ListOfFiles.Items.Add($"    {ext} - {count} files");
            _loggingService.LogOperation($"Found {count} {ext} files");
        }

        ListOfFiles.Items.Add(new ListViewItem
        {
            Content = "===== Files By Folder =====",
            FontWeight = FontWeights.Bold
        });

        // Then list files by folder
        foreach (var folderPath in filesByFolder.Keys.OrderBy(static f => f))
        {
            // Add a folder as a header
            ListOfFiles.Items.Add(new ListViewItem
            {
                Content = folderPath,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Colors.LightGray)
            });

            // Add each file in this folder (ordered alphabetically)
            foreach (var file in filesByFolder[folderPath].OrderBy(static f => Path.GetFileName(f.RelativePath)))
            {
                // Show files that are manually added with a different indicator
                var isOutsideBaseFolder = !file.Path.StartsWith(_fileService.SelectedFolder, StringComparison.OrdinalIgnoreCase);
                var prefix = isOutsideBaseFolder ? "+" : "    ";

                ListOfFiles.Items.Add($"{prefix} {Path.GetFileName(file.RelativePath)}");
            }
        }
    }

    private void LoadPromptTemplates()
    {
        PromptTemplates.ItemsSource = null;
        PromptTemplates.ItemsSource = _settingsManager.Settings.CodePrompts;
        PromptTemplates.SelectedIndex = -1;
    }

    private void UpdateTokenCountDisplay()
    {
        // Get the total token count from the result
        var totalTokens = _tokenCalculationResult.TotalTokens;

        // Format the display text with an exact token count
        TxtTokenCount.Text =
            $"Estimated tokens: {totalTokens:N0} (range {(long)(totalTokens * 0.8):N0} - {(long)(totalTokens * 1.2):N0})";

        // Detailed tooltip with model-specific information
        var tooltipBuilder = new StringBuilder();
        tooltipBuilder.AppendLine(_tokenCalculationResult.GetBreakdown());

        TxtTokenCount.ToolTip = tooltipBuilder.ToString();

        _loggingService.LogOperation($"Updated token count display: {totalTokens:N0} tokens");
    }

    private void CalculateTotalTokens()
    {
        // Get all source files as a flat list
        var allFiles = _fileService.FilesByExtension.Values
            .SelectMany(static files => files)
            .ToList();

        var promptTemplate = GetPromptTemplateText();

        // Use SharpToken to calculate tokens
        _tokenCalculationResult = _tokenCounterService.CalculateTotalTokens(allFiles, promptTemplate);

        // Update UI
        UpdateTokenCountDisplay();

        _loggingService.LogOperation($"Token calculation complete: {_tokenCalculationResult.TotalTokens:N0} tokens");
    }

    private string GetPromptTemplateText()
    {
        var selectedPromptTemplate = PromptTemplates.SelectedItem as CodePrompt;
        return selectedPromptTemplate?.Content ?? _settingsManager.Settings.InitialPrompt;
    }

    private void UpdateRecentFilesMenu()
    {
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

            // Add a separator and "Clear Recent Files" option
            MenuRecentFiles.Items.Add(new Separator());

            var clearMenuItem = new MenuItem
            {
                Header = "Clear Recent Files",
                Icon = new TextBlock { Text = "🗑️", FontSize = 14 }
            };
            clearMenuItem.Click += ClearRecentFiles_Click;
            MenuRecentFiles.Items.Add(clearMenuItem);
        }
    }

    private void AddToRecentFiles(string filePath)
    {
        _recentFilesManager.AddRecentFile(filePath);
        UpdateRecentFilesMenu();
        _loggingService.LogOperation($"Added file to recent files: {Path.GetFileName(filePath)}");
    }

    private async void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Disable relevant buttons before starting the operation
            BtnSelectFolder.IsEnabled = false;
            BtnSelectFiles.IsEnabled = false;
            BtnClearFiles.IsEnabled = false;
            BtnSendQuery.IsEnabled = false;

            _uiStateManager.SetStatusMessage("Scanning folder for source files...");
            _uiStateManager.SetProcessingState(true, "Scanning folder");

            await _fileService.SelectFolderAsync();

            _uiStateManager.SetStatusMessage($"Found {_fileService.FilesByExtension.Values.Sum(list => list.Count)} source files.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Opening folder selection dialog");
            _uiStateManager.SetStatusMessage("Error selecting folder.");
        }
        finally
        {
            // Re-enable buttons
            BtnSelectFolder.IsEnabled = true;
            BtnSelectFiles.IsEnabled = true;
            BtnClearFiles.IsEnabled = true;
            BtnSendQuery.IsEnabled = true;
            _uiStateManager.SetProcessingState(false);
        }
    }

    private async void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Disable relevant buttons immediately
            BtnSelectFolder.IsEnabled = false;
            BtnSelectFiles.IsEnabled = false;
            BtnClearFiles.IsEnabled = false;
            BtnSendQuery.IsEnabled = false;

            _uiStateManager.SetProcessingState(true, "Processing selected files");

            await _fileService.SelectFilesAsync();
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error selecting files: {ex.Message}");
            Logger.LogError(ex, "Error selecting files");
        }
        finally
        {
            // Re-enable buttons
            BtnSelectFolder.IsEnabled = true;
            BtnSelectFiles.IsEnabled = true;
            BtnClearFiles.IsEnabled = true;
            BtnSendQuery.IsEnabled = true;
            _uiStateManager.SetProcessingState(false);
        }
    }

    private void BtnClearFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ListOfFiles.Items.Clear();
            TxtTokenCount.Text = "";
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error clearing files: {ex.Message}");
            Logger.LogError(ex, "Clearing files");
        }
    }

    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Check for the API key
            if (AiProviderKeys.SelectedIndex <= 0)
            {
                MessageBox.Show("Please enter an API key.", "Missing API Key", MessageBoxButton.OK, MessageBoxImage.Warning);
                _loggingService.LogOperation("Analysis canceled: No API key provided");

                return;
            }

            // Get query text and API selection
            var queryText = TxtFollowupQuestion.Text.Trim();
            var apiSelection = AiProvider.SelectedItem?.ToString();

            // Check AI provider
            if (string.IsNullOrEmpty(apiSelection))
            {
                MessageBox.Show("Please select an AI provider.", "Missing AI Provider", MessageBoxButton.OK, MessageBoxImage.Warning);
                _loggingService.LogOperation("Analysis canceled: No AI provider selected");

                return;
            }

            // Setup processing UI
            _uiStateManager.SetStatusMessage($"Processing with {apiSelection}...");
            _uiStateManager.SetProcessingState(true, $"Processing with {apiSelection}");
            _loggingService.LogOperation($"Starting query with {apiSelection}");
            _loggingService.StartOperationTimer("QueryProcessing");

            try
            {
                string prompt;
                var currentQueryFiles = new List<SourceFile>();

                // First determine if we need to include files
                if (IncludeSelectedFilesChecker.IsChecked == true)
                {
                    _loggingService.LogOperation("Include Files checkbox is checked - preparing files");
                    _loggingService.StartOperationTimer("PrepareFiles");

                    // Generate base prompt based on whether to include template
                    if (IncludePromptTemplate.IsChecked == true)
                    {
                        _loggingService.LogOperation("Including prompt template");
                        prompt = GetPromptTemplateText() + "\n\n";
                    }
                    else
                    {
                        _loggingService.LogOperation("Not using prompt template");
                        prompt = ""; // Start with an empty prompt if no template
                    }

                    // Now determine which files to include
                    if (ListOfFiles.SelectedItems.Count > 0)
                    {
                        // User has specifically selected files in the list
                        prompt = AppendSelectedFilesToPrompt(prompt, currentQueryFiles);
                        _loggingService.LogOperation($"Added {currentQueryFiles.Count} specifically selected files to the prompt");
                    }
                    else if (_fileService.FilesByExtension.Count > 0)
                    {
                        // No specific selection, include all files
                        var consolidatedFiles = _fileService.PrepareConsolidatedFiles(currentQueryFiles);

                        // Add files to the prompt
                        foreach (var ext in consolidatedFiles.Keys)
                        {
                            prompt += $"--- {ext.ToUpperInvariant()} FILES ---\n\n";

                            foreach (var fileContent in consolidatedFiles[ext])
                            {
                                prompt += fileContent + "\n\n";
                            }
                        }

                        _loggingService.LogOperation($"Added all {currentQueryFiles.Count} files to the prompt");
                    }
                    else
                    {
                        prompt += "--- NO FILES AVAILABLE TO INCLUDE ---\n\n";
                        _loggingService.LogOperation("No files available to include");
                    }

                    _loggingService.EndOperationTimer("PrepareFiles");
                }
                else
                {
                    // Files not included - generate a basic prompt
                    if (IncludePromptTemplate.IsChecked == true)
                    {
                        _loggingService.LogOperation("Using prompt template only (no files)");
                        prompt = GetPromptTemplateText() + "\n\n";
                    }
                    else
                    {
                        _loggingService.LogOperation("Using minimal prompt (no template, no files)");
                        prompt = "Please respond to the following:\n\n";
                    }
                }

                // Add user query if provided
                if (!string.IsNullOrEmpty(queryText))
                {
                    prompt += "--- Additional Instructions/Question ---\n" + queryText;
                    _loggingService.LogOperation("Added query text to the prompt");
                }

                // Start a new interaction in the ResponseService
                await _responseService.StartNewInteractionAsync(prompt, currentQueryFiles);

                // Get the conversation history for the API call (uses in-memory content now)
                var conversationHistory = _responseService.GetConversationHistoryForApi();

                // Get the selected API key
                var savedKeys = _aiProviderService.GetKeysForProvider(apiSelection);
                var keyIndex = AiProviderKeys.SelectedIndex - 1;
                var apiKey = keyIndex >= 0 && keyIndex < savedKeys.Count ? savedKeys[keyIndex] : string.Empty;

                // Get the selected model if applicable
                string? modelId = null;
                if (AiModel.IsEnabled && AiModel.SelectedItem is ModelDropdownItem selectedModel)
                {
                    modelId = selectedModel.ModelId;
                }

                // Send to AI API and get response
                var response = await _aiProviderService.SendPromptAsync(apiSelection, apiKey, prompt, conversationHistory, modelId);

                // Check for null or empty response
                if (string.IsNullOrEmpty(response))
                {
                    throw new Exception("Received an empty response from the AI provider.");
                }

                // Complete the current interaction with the AI response
                await _responseService.CompleteCurrentInteraction(response);

                // Reset query input
                TxtFollowupQuestion.Text = string.Empty;

                // Enable buttons
                BtnSaveResponse.IsEnabled = true;
                BtnShowInputQuery.IsEnabled = true;
                BtnContinue.IsEnabled = true;

                // Disable Checkbox
                IncludePromptTemplate.IsChecked = false;
                IncludeSelectedFilesChecker.IsChecked = false;

                _uiStateManager.SetStatusMessage("Query processed successfully!");
                _loggingService.EndOperationTimer("QueryProcessing");
            }
            catch (Exception ex)
            {
                HandleQueryError(ex);
            }
            finally
            {
                _uiStateManager.SetProcessingState(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing query");
        }
    }

    private void HandleQueryError(Exception ex)
    {
        _loggingService.LogOperation($"Error processing query: {ex.Message}");

        // Disable the Continue Button in case of an error
        BtnContinue.IsEnabled = false;
        BtnSaveResponse.IsEnabled = false;
        BtnShowInputQuery.IsEnabled = false;
        BtnToggleHtml.IsEnabled = false;

        // Clear the response area
        _responseService.ClearCurrentResponse();

        // Update status message, don't log again
        _uiStateManager.SetStatusMessage("Error processing query.");
        _loggingService.EndOperationTimer("QueryProcessing");
    }

    private string AppendSelectedFilesToPrompt(string originalPrompt, List<SourceFile> includedFiles)
    {
        var promptBuilder = new StringBuilder(originalPrompt);
        includedFiles.Clear();

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("--- SELECTED FILES FOR FOCUSED REFERENCE ---");
        promptBuilder.AppendLine();

        var selectedFileNames = new List<string>();
        var selectedFilesContent = new StringBuilder();
        var fileCount = 0;

        foreach (var item in ListOfFiles.SelectedItems)
        {
            // Check if the item is a string representing a file path/name
            if (item is not string displayString) continue;

            // Basic check to filter out headers or summary lines
            if (displayString.StartsWith("=====", StringComparison.Ordinal) || displayString.Contains(" files") ||
                displayString.Contains(" - ")) continue;

            // Clean the display string (remove potential prefixes like '+' or '')
            var cleanFileName = displayString.TrimStart(' ', '+');

            // Find the matching SourceFile object across all extensions
            SourceFile? matchingFile = null;
            foreach (var extensionFiles in _fileService.FilesByExtension.Values)
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
                selectedFilesContent.AppendLine(CultureInfo.InvariantCulture, $"```{GetCodeLanguage.GetLanguageForExtension(matchingFile.Extension)}");
                selectedFilesContent.AppendLine(matchingFile.Content);
                selectedFilesContent.AppendLine("```");
                selectedFilesContent.AppendLine();
            }
            else
            {
                _loggingService.LogOperation($"Warning: Could not find source file data for selected item: '{cleanFileName}'");
            }
        }

        // Add a summary of selected files to the prompt
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
        _loggingService.LogOperation($"Included {fileCount} specifically selected files in the prompt");

        return promptBuilder.ToString();
    }

    private void AiProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AiProvider.SelectedItem == null) return;

        var apiSelection = AiProvider.SelectedItem.ToString() ?? string.Empty;
        UpdateProviderKeys(apiSelection);
        PopulateModelDropdown(apiSelection);

        // Clear the model description initially
        TxtModelDescription.Text = string.Empty;

        // Add an instruction to select a model
        if (AiModel.IsEnabled)
        {
            TxtModelDescription.Text = "Please select a model from the dropdown above.";
        }
    }

    private void UpdateProviderKeys(string apiProvider)
    {
        AiProviderKeys.Items.Clear();
        AiProviderKeys.Items.Add("Select a key");

        var savedKeys = _aiProviderService.GetKeysForProvider(apiProvider);
        foreach (var key in savedKeys)
        {
            AiProviderKeys.Items.Add(AiProviderService.MaskKey(key));
        }

        AiProviderKeys.SelectedIndex = 0;
    }

    private void PopulateModelDropdown(string providerName)
    {
        AiModel.Items.Clear();

        if (string.IsNullOrEmpty(providerName))
            return;

        try
        {
            if (AiProviderService.SupportsModelSelection(providerName))
            {
                var models = _aiProviderService.GetModelsForProvider(providerName);

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
            _loggingService.LogOperation($"Error populating model dropdown: {ex.Message}");
            Logger.LogError(ex, $"Error populating model dropdown: {ex.Message}");
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

    private void PromptTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PromptTemplates.SelectedItem is not CodePrompt selectedPrompt) return;

        _settingsManager.Settings.SelectedPromptName = selectedPrompt.Name;
        _settingsManager.SaveSettings();
        _loggingService.LogOperation($"Selected prompt template: {selectedPrompt.Name}");
    }

    private async void UpdateResponseDisplay()
    {
        try
        {
            var responseText = _responseService.CurrentResponseText;
            var isShowingInputQuery = _responseService.IsShowingInputQuery;

            // Ensure WebView2 is initialized before loading content
            if (HtmlViewer.CoreWebView2 == null)
            {
                try
                {
                    await HtmlViewer.EnsureCoreWebView2Async();
                }
                catch (Exception ex)
                {
                    _loggingService.LogOperation($"Error initializing WebView2: {ex.Message}");
                    Logger.LogError(ex, "WebView2 initialization");

                    return;
                }
            }

            // Manage visibility of WebView2 and RawResponseTextBox
            if (isShowingInputQuery) // Showing Input Query
            {
                HtmlViewer.Visibility = Visibility.Visible;
                RawResponseTextBox.Visibility = Visibility.Collapsed;

                _isShowingRawText = false; // Cannot be in raw text mode while showing input query

                // Convert Input Query to Html and display it
                var inputQueryMd = await _responseService.GetInputQueryMarkdownAsync();
                var html = _htmlService.ConvertMarkdownToHtml(inputQueryMd);
                HtmlViewer.NavigateToString(html);

                // Switch the button
                BtnShowInputQuery.Content = "Show AI Response";

                // Navigation buttons are handled by UpdateNavigationControls
                BtnSaveResponse.IsEnabled = false; // Cannot save input query using Save Response
                BtnSaveEdits.IsEnabled = false; // Cannot save edits to input query file this way
                BtnToggleHtml.IsEnabled = false; // Cannot toggle raw/html for input query
                BtnToggleHtml.Content = "Show Raw Text"; // Reset button text
            }
            else // Showing AI Response or Loaded File Content
            {
                HtmlViewer.Visibility = Visibility.Visible;
                RawResponseTextBox.Visibility = Visibility.Collapsed;

                // Switch the button
                BtnShowInputQuery.Content = "Show Input Query";

                // Navigation buttons are handled by UpdateNavigationControls
                BtnSaveResponse.IsEnabled = !string.IsNullOrEmpty(responseText);
                BtnSaveEdits.IsEnabled = !string.IsNullOrEmpty(responseText);
                BtnShowInputQuery.IsEnabled = _responseService.CurrentResponseIndex >= 0; // Only allow toggling back if there's an interaction

                if (_isShowingRawText) // Raw mode
                {
                    HtmlViewer.Visibility = Visibility.Collapsed;
                    RawResponseTextBox.Visibility = Visibility.Visible;

                    if (RawResponseTextBox != null)
                    {
                        // Try loading from file first
                        if (!string.IsNullOrEmpty(_responseService.CurrentFilePath) && File.Exists(_responseService.CurrentFilePath))
                        {
                            try
                            {
                                var rawContent = await File.ReadAllTextAsync(_responseService.CurrentFilePath);
                                RawResponseTextBox.Text = rawContent;
                                _responseService.UpdateCurrentResponse(rawContent);
                            }
                            catch (Exception ex)
                            {
                                _loggingService.LogOperation($"Error loading file for raw view: {ex.Message}");

                                // Fall back to in-memory content
                                RawResponseTextBox.Text = _responseService.CurrentResponseText;
                            }
                        }
                        else // No file path or file doesn't exist
                        {
                            // Use in-memory content instead of showing an error
                            RawResponseTextBox.Text = _responseService.CurrentResponseText;
                        }

                        RawResponseTextBox.FontSize = _rawTextFontSize;
                    }

                    BtnToggleHtml.Content = "Show HTML";
                    BtnToggleHtml.IsEnabled = true;
                }

                else // HTML mode
                {
                    HtmlViewer.Visibility = Visibility.Visible;
                    RawResponseTextBox.Visibility = Visibility.Collapsed;

                    var contentToRender = _responseService.CurrentResponseText;

                    // Try to load from a file when CurrentFilePath exists
                    if (!string.IsNullOrEmpty(_responseService.CurrentFilePath) && File.Exists(_responseService.CurrentFilePath))
                    {
                        try
                        {
                            contentToRender = _fileService.LoadMarkdownFile(_responseService.CurrentFilePath);

                            // Update in-memory content so the model stays consistent
                            _responseService.UpdateCurrentResponse(contentToRender);
                        }
                        catch (Exception ex)
                        {
                            _loggingService.LogOperation($"Failed loading md file for HTML view: {ex.Message}");
                            // Fallback will use in-memory content
                        }
                    }

                    // Convert md to HTML
                    var html = _htmlService.ConvertMarkdownToHtml(contentToRender);

                    // Set the WebViewer
                    HtmlViewer.NavigateToString(html);

                    // Set button
                    BtnToggleHtml.Content = "Show Raw Text";
                    BtnToggleHtml.IsEnabled = !string.IsNullOrEmpty(contentToRender); // Enable if content exists
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in method UpdateResponseDisplay");
        }
    }

    private void UpdateNavigationControls()
    {
        // Update navigation buttons based on the current position
        BtnPreviousResponse.IsEnabled = _responseService.CanNavigatePrevious();
        BtnNextResponse.IsEnabled = _responseService.CanNavigateNext();

        // Update counter text
        TxtResponseCounter.Text = _responseService.GetNavigationCounterText();

        // Update the [Show Input Query] button state based on whether there is an interaction to show input for
        BtnShowInputQuery.IsEnabled = _responseService.CurrentResponseIndex >= 0; // Use CurrentResponseIndex

        // Ensure [Continue button] is only enabled for the *latest* AI response
        BtnContinue.IsEnabled = _responseService.CurrentResponseIndex >= 0 &&
                                _responseService.CurrentResponseIndex == _responseService.Interactions.Count - 1 &&
                                !_responseService.IsShowingInputQuery; // Cannot continue from input view
    }

    private async void BtnSaveResponse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ensure the current response text is up to date if in raw editing mode
            if (_isShowingRawText && RawResponseTextBox != null)
            {
                _responseService.UpdateCurrentResponse(RawResponseTextBox.Text); // This updates the service's internal text
            }

            var responseText = _responseService.CurrentResponseText;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                MessageBox.Show("There is no response to save.", "No Response", MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }

            // Show saving indicator
            _uiStateManager.SetProcessingState(true, "Saving response");

            // Always show SaveFileDialog to save a copy to a new location
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "md",
                Title = "Save AI Response"
            };

            // If the user has selected a project folder, suggest that as the initial directory
            if (!string.IsNullOrEmpty(_fileService.SelectedFolder) && Directory.Exists(_fileService.SelectedFolder))
            {
                saveFileDialog.InitialDirectory = _fileService.SelectedFolder;

                // Suggest a filename based on the project folder name and timestamp
                var folderName = new DirectoryInfo(_fileService.SelectedFolder).Name;
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                saveFileDialog.FileName = $"{folderName}_analysis_{timestamp}.md";
            }
            else
            {
                // Default filename with timestamp if no folder is selected
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                saveFileDialog.FileName = $"ai_analysis_{timestamp}.md";
            }

            var dialogResult = saveFileDialog.ShowDialog();

            // If the user clicked OK, save the file
            if (dialogResult != true)
            {
                _uiStateManager.SetStatusMessage("Save canceled.");

                return; // User canceled the operation
            }

            // Save on background thread
            await Task.Run(() => File.WriteAllText(saveFileDialog.FileName, responseText));

            _loggingService.LogOperation($"Saved response copy to: {saveFileDialog.FileName}");

            MessageBox.Show($"Response copy saved to {saveFileDialog.FileName}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);

            _uiStateManager.SetStatusMessage($"Response copy saved: {Path.GetFileName(saveFileDialog.FileName)}");

            // Do NOT update CurrentFilePath or RecentFiles with this copy
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error saving response copy: {ex.Message}");
            Logger.LogError(ex, "Saving response copy to file");

            MessageBox.Show("An error occurred while saving the response copy.", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _uiStateManager.SetProcessingState(false);
        }
    }

    private async void BtnPreviousResponse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // If currently in raw editing mode, save edits before navigating
            if (_isShowingRawText && RawResponseTextBox != null)
            {
                _responseService.UpdateCurrentResponse(RawResponseTextBox.Text);
                _isShowingRawText = false; // Switch back to [HTML mode] after saving edits
            }

            await _responseService.NavigatePrevious(); // Await the async navigation
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in method BtnPreviousResponse_Click");
        }
    }

    private async void BtnNextResponse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // If currently in raw editing mode, save edits before navigating
            if (_isShowingRawText && RawResponseTextBox != null)
            {
                _responseService.UpdateCurrentResponse(RawResponseTextBox.Text);
                _isShowingRawText = false; // Switch back to [HTML mode] after saving edits
            }

            await _responseService.NavigateNext();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in method BtnNextResponse_Click");
        }
    }

    private void BtnShowInputQuery_Click(object sender, RoutedEventArgs e)
    {
        // If currently in raw editing mode, save edits before switching view
        if (_isShowingRawText && RawResponseTextBox != null)
        {
            _responseService.UpdateCurrentResponse(RawResponseTextBox.Text);
        }

        // Ensure we are not in raw text mode when showing input query
        _isShowingRawText = false;

        _responseService.ToggleInputQueryView();

        // Button states are updated by UpdateNavigationControls and UpdateResponseDisplay
    }

    private async void SaveEdits_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Ensure we are not showing the input query
            if (_responseService.IsShowingInputQuery)
            {
                MessageBox.Show("Cannot save edits while viewing the input query. Please switch back to the AI response first.",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            // Get the text from the appropriate control based on the current view mode
            string editedText;
            if (_isShowingRawText && RawResponseTextBox != null)
            {
                editedText = RawResponseTextBox.Text;
            }
            else
            {
                // If not in raw mode, we can't get edits directly from WebView2.
                // The user should be in raw mode to edit.
                MessageBox.Show("Please switch to 'Show Raw Text' mode to edit the response.",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            if (string.IsNullOrEmpty(editedText))
            {
                MessageBox.Show("Cannot save empty content.",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            // Check if the current response is associated with a file path that exists
            var currentFilePath = _responseService.CurrentFilePath;

            // Ensure we are saving the AI response, not the input query
            if (_responseService.IsShowingInputQuery || string.IsNullOrEmpty(currentFilePath))
            {
                MessageBox.Show("Cannot save edits. This action is only available for AI responses that have been saved to a file.",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            if (string.IsNullOrEmpty(currentFilePath) || !File.Exists(currentFilePath)) return;

            _uiStateManager.SetProcessingState(true, "Saving edits");

            try
            {
                // Save the changes to the file
                await _fileService.OverwriteAiResponseAsync(currentFilePath, editedText);

                // After a successful file save, update the in-memory content
                // and refresh the display from the now-updated in-memory object (or file).
                _responseService.UpdateCurrentResponse(editedText);

                _loggingService.LogOperation($"Automatically saved edits to file: {currentFilePath}");
                _uiStateManager.SetStatusMessage($"Edits applied and saved to {Path.GetFileName(currentFilePath)}");

                MessageBox.Show($"Edits applied and saved to {Path.GetFileName(currentFilePath)}",
                    "Edits Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _loggingService.LogOperation($"Error overwriting file {currentFilePath}: {ex.Message}");
                Logger.LogError(ex, $"Error overwriting file: {currentFilePath}");
                MessageBox.Show($"An error occurred while saving your edits to {Path.GetFileName(currentFilePath)}.", "Edit Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Refresh the display to show the saved content (either raw or HTML)
                await _responseService.DisplayCurrentInteraction();
                _uiStateManager.SetProcessingState(false);
            }

            MessageBox.Show("Edits applied to the content. Use 'Save Response' to save a copy to a new file.",
                "Edits Applied", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error in SaveEdits_Click: {ex.Message}");
            Logger.LogError(ex, "Error in SaveEdits_Click");

            MessageBox.Show("Could not save the edited file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Tag: string filePath }) return;

            await LoadMarkdownFileAsync(filePath);

            // Ensure the input query button is disabled after loading
            BtnShowInputQuery.IsEnabled = false;
            BtnShowInputQuery.Content = "Show Input Query";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error opening recent file");
        }
    }

    private void ClearRecentFiles_Click(object sender, RoutedEventArgs e)
    {
        _recentFilesManager.ClearRecentFiles();
        UpdateRecentFilesMenu();
        _loggingService.LogOperation("Cleared recent files list");
    }

    public async Task LoadMarkdownFileAsync(string filePath)
    {
        try
        {
            _uiStateManager.SetProcessingState(true, $"Loading {Path.GetFileName(filePath)}");
            _uiStateManager.SetStatusMessage($"Loading {Path.GetFileName(filePath)} - please wait");

            // Read the file content asynchronously
            var fileContent = _fileService.LoadMarkdownFile(filePath);

            if (string.IsNullOrEmpty(fileContent))
            {
                _uiStateManager.SetStatusMessage("No content to display");
                _uiStateManager.SetProcessingState(false);

                MessageBox.Show("MD file is empty", "Info", MessageBoxButton.OK, MessageBoxImage.Information);

                // No content to return, exit the method
                return;
            }

            // Create a new interaction representing this loaded file
            var loadedInteraction = new Interaction
            {
                // When loading a file, the content comes from the file, not a prompt/response turn
                // So we set the AssistantResponse and its path directly
                AssistantResponse = fileContent,
                AssistantResponseFilePath = filePath,
                UserPrompt = "Content loaded from file.", // Placeholder prompt
                IncludedFiles = new List<SourceFile>() // No files included *with* this "prompt"
            };

            // Add this interaction to the list and set it as current
            await _responseService.AddInteractionAndSetCurrent(loadedInteraction); // Await the async method

            // Ensure UI is updated to show the loaded file content (response view)
            _isShowingRawText = false; // Ensure HTML view
            // DisplayCurrentInteraction is called by AddInteractionAndSetCurrent

            // Disable input query view for loaded files (as there's no meaningful input query)
            BtnShowInputQuery.IsEnabled = false;

            AddToRecentFiles(filePath);

            _uiStateManager.SetStatusMessage($"Loaded {Path.GetFileName(filePath)}");

            // Content was loaded and processed, no need to return it
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading markdown file");
            _uiStateManager.SetStatusMessage("Error loading file.");

            MessageBox.Show("Error loading the MD file content", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            // Error occurred, no content to return
        }
        finally
        {
            _uiStateManager.SetProcessingState(false);
        }
    }

    private void MenuConfigure_Click(object sender, RoutedEventArgs e)
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

            if (result != true) return;

            // Settings were saved, update the necessary parts of the MainWindow
            _loggingService.LogOperation("Settings updated. Refreshing UI and services.");

            // 1. Reload API Keys and refresh the API dropdowns
            _aiProviderService.ReloadApiKeys();

            // Repopulate providers (less likely to change, but safe)
            PopulateAiProviders();

            // UpdateProviderKeys is called by AiProvider_SelectionChanged when the provider is selected
            // Ensure the currently selected provider's keys are updated if a provider is already selected
            if (AiProvider.SelectedItem is string selectedProvider)
            {
                UpdateProviderKeys(selectedProvider);
            }

            // 2. Reload Prompt Templates and refresh the prompt dropdown
            LoadPromptTemplates();

            // 3. Recalculate token count based on potentially new file extensions/size limits
            // This will also update the token count display
            CalculateTotalTokens();

            // 4. File association change is handled directly in the ConfigurationWindow, no action needed here.

            _loggingService.LogOperation("Settings update complete.");
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error opening configuration window: {ex.Message}");
            Logger.LogError(ex, "Opening configuration window");
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

            if (result != MessageBoxResult.Yes) return;

            _loggingService.LogOperation("Restarting application...");

            // Clear response data
            _responseService.ClearHistory();

            // Clear file list
            _fileService.ClearFiles();

            // Reset UI elements state
            BtnSendQuery.IsEnabled = true;
            TxtFollowupQuestion.IsEnabled = true;
            BtnSaveResponse.IsEnabled = false;
            BtnToggleHtml.IsEnabled = false;
            BtnShowInputQuery.IsEnabled = false;
            BtnShowInputQuery.Content = "Show Input Query";
            BtnSaveEdits.IsEnabled = false;
            BtnContinue.IsEnabled = false;
            TxtFollowupQuestion.Clear();
            ListOfFiles.Items.Clear();

            // Reset checkboxes to default state
            IncludePromptTemplate.IsChecked = true;
            IncludeSelectedFilesChecker.IsChecked = true;

            // Reset AI provider
            AiProvider.SelectedIndex = -1;
            AiModel.SelectedIndex = -1;
            AiProviderKeys.SelectedIndex = -1;
            TxtModelDescription.Text = string.Empty;

            // Reset status
            _uiStateManager.SetStatusMessage("Ready");

            // Ensure UI is in default HTML view
            _isShowingRawText = false;
            UpdateResponseDisplay();

            _loggingService.LogOperation("Application reset complete");
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error restarting application: {ex.Message}");
            Logger.LogError(ex, "Restarting application");
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
                    MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }

            var dialog = new OpenFileDialog
            {
                Title = "Open Past Response",
                Filter = "Markdown files (*.md)|*.md|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                InitialDirectory = outputDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                await LoadMarkdownFileAsync(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error opening past response: {ex.Message}");
            Logger.LogError(ex, "Opening past response");

            _uiStateManager.SetStatusMessage("Error opening past response.");
        }
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow
        {
            Owner = this
        };
        aboutWindow.ShowDialog();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void PromptTemplatesEdit_Click(object sender, RoutedEventArgs e)
    {
        OpenConfigurationWindow();
    }

    private async void BtnContinue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Check for the API key
            if (AiProviderKeys.SelectedIndex <= 0)
            {
                MessageBox.Show("Please enter an API key.", "Missing API Key", MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _loggingService.LogOperation("Analysis canceled: No API key provided");

                return;
            }

            // Get query text and API selection
            const string queryText = "continue";
            var apiSelection = AiProvider.SelectedItem?.ToString();

            if (apiSelection == null)
            {
                MessageBox.Show("Please enter an AI provider.", "Missing AI Provider", MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _loggingService.LogOperation("Analysis canceled: No AI provider selected");

                return;
            }

            // Setup processing UI
            _uiStateManager.SetStatusMessage($"Processing with {apiSelection} (Continue)...");
            _uiStateManager.SetProcessingState(true, $"Processing with {apiSelection} (Continue)");
            _loggingService.LogOperation($"Sending 'Continue' with {apiSelection}");
            _loggingService.StartOperationTimer("ContinueProcessing");

            try
            {
                // Get the conversation history for the API call (uses in-memory content now)
                var conversationHistory = _responseService.GetConversationHistoryForApi();

                // Get the selected API key
                var savedKeys = _aiProviderService.GetKeysForProvider(apiSelection);
                var keyIndex = AiProviderKeys.SelectedIndex - 1;
                var apiKey = keyIndex >= 0 && keyIndex < savedKeys.Count ? savedKeys[keyIndex] : string.Empty;

                // Get the selected model if applicable
                string? modelId = null;
                if (AiModel.IsEnabled && AiModel.SelectedItem is ModelDropdownItem selectedModel)
                {
                    modelId = selectedModel.ModelId;
                }

                // Send to AI API and get response
                var response = await _aiProviderService.SendPromptAsync(
                    apiSelection, apiKey, queryText, conversationHistory, modelId);

                // Complete the current interaction with the AI response
                // Await CompleteCurrentInteraction if it were Task, but it's void, so fire and forget
                await _responseService.CompleteCurrentInteraction(response);

                // Reset query input (although 'continue' doesn't use TxtFollowupQuestion)
                TxtFollowupQuestion.Text = string.Empty;

                // Enable buttons (already done in CompleteCurrentInteraction?)
                BtnSaveResponse.IsEnabled = true;
                BtnShowInputQuery.IsEnabled = true;
                BtnContinue.IsEnabled = true;

                // Disable Checkbox
                IncludePromptTemplate.IsChecked = false;
                IncludeSelectedFilesChecker.IsChecked = false;

                _uiStateManager.SetStatusMessage("Query processed successfully!");
                _loggingService.EndOperationTimer("ContinueProcessing");
            }
            catch (Exception ex)
            {
                HandleQueryError(ex);
            }
            finally
            {
                _uiStateManager.SetProcessingState(false);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing 'Continue' query");
        }
    }

    private void BtnToggleHtml_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_responseService.IsShowingInputQuery)
            {
                MessageBox.Show("Cannot toggle view while viewing the input query. Please switch back to the AI response first.",
                    "Toggle View Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;
            }

            if (_isShowingRawText) // Raw mode
            {
                _isShowingRawText = false;
                BtnToggleHtml.Content = "Show Raw Text";

                UpdateResponseDisplay();

                BtnSaveResponse.IsEnabled = true;
                BtnSaveEdits.IsEnabled = false; // Editing is only for raw mode
            }
            else // HTML mode
            {
                _isShowingRawText = true;
                BtnToggleHtml.Content = "Show HTML";

                UpdateResponseDisplay();

                BtnSaveResponse.IsEnabled = true;
                BtnSaveEdits.IsEnabled = true; // Enable editing in raw mode
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error toggling raw/HTML view: {ex.Message}");
            Logger.LogError(ex, "Toggling raw/HTML view");

            MessageBox.Show("An error occurred while toggling the view mode.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isShowingRawText && RawResponseTextBox != null)
            {
                // Zoom Raw Text Box
                _rawTextFontSize = Math.Min(_rawTextFontSize + RawZoomStep, MaxRawFontSize);
                RawResponseTextBox.FontSize = _rawTextFontSize;
                _loggingService.LogOperation($"Zoomed In Raw Text to {_rawTextFontSize:F0}pt");
            }
            else if (HtmlViewer != null)
            {
                // Zoom WebView2
                _webViewZoomFactor = Math.Min(_webViewZoomFactor + WebViewZoomStep, MaxWebViewZoom);
                HtmlViewer.ZoomFactor = _webViewZoomFactor;
                _loggingService.LogOperation($"Zoomed In WebView2 to {_webViewZoomFactor:P0}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Zoom In failed");
            MessageBox.Show("Failed to zoom in.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isShowingRawText && RawResponseTextBox != null)
            {
                // Zoom Raw Text Box
                _rawTextFontSize = Math.Max(_rawTextFontSize - RawZoomStep, MinRawFontSize);
                RawResponseTextBox.FontSize = _rawTextFontSize;
                _loggingService.LogOperation($"Zoomed Out Raw Text to {_rawTextFontSize:F0}pt via mouse wheel");
            }
            else if (HtmlViewer != null)
            {
                // Zoom WebView2
                _webViewZoomFactor = Math.Max(_webViewZoomFactor - WebViewZoomStep, MinWebViewZoom);
                HtmlViewer.ZoomFactor = _webViewZoomFactor;
                _loggingService.LogOperation($"Zoomed Out WebView2 to {_webViewZoomFactor:P0}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Zoom Out failed");
            MessageBox.Show("Failed to zoom out.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnResetZoom_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_isShowingRawText && RawResponseTextBox != null)
            {
                // Reset Raw Text Box Zoom
                _rawTextFontSize = 12.0; // Reset to default XAML size
                RawResponseTextBox.FontSize = _rawTextFontSize;
                _loggingService.LogOperation("Raw Text Zoom Reset to 12pt");
            }
            else if (HtmlViewer != null)
            {
                // Reset WebView2 Zoom
                _webViewZoomFactor = 1.0;
                HtmlViewer.ZoomFactor = _webViewZoomFactor;
                _loggingService.LogOperation("WebView2 Zoom Reset to 100%");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Zoom Reset failed");
            MessageBox.Show("Failed to reset zoom.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async void HandleCommandLineArguments(string[] args)
    {
        try
        {
            // Bring the window to the foreground
            // Check if it's minimized, if so, restore it first
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            Topmost = true; // Bring to front
            Topmost = false; // Remove Topmost property

            _loggingService.LogOperation($"Received arguments from another instance: {string.Join(" ", args)}");

            // Process the arguments (e.g., open a file)
            if (args.Length <= 0) return;

            var filePath = args[0];
            if (File.Exists(filePath))
            {
                // Clear current files and load the new one
                _fileService.ClearFiles(); // Clear previous context
                await LoadMarkdownFileAsync(filePath);
            }
            else
            {
                _loggingService.LogOperation($"Received file path does not exist: {filePath}");
                _uiStateManager.SetStatusMessage($"Received file path does not exist: {Path.GetFileName(filePath)}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error handling command line arguments from another instance.");
        }
    }

    private void RawResponseTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e) // Renamed method
    {
        // Check if Ctrl key is pressed
        if (Keyboard.Modifiers != ModifierKeys.Control) return; // Check specifically for Ctrl
        if (RawResponseTextBox == null) return;

        switch (e.Delta)
        {
            case > 0:
                // Mouse wheel up - Zoom In
                _rawTextFontSize = Math.Min(_rawTextFontSize + RawZoomStep, MaxRawFontSize);
                RawResponseTextBox.FontSize = _rawTextFontSize;
                _loggingService.LogOperation($"Zoomed In Raw Text to {_rawTextFontSize:F0}pt via mouse wheel");
                break;
            case < 0:
                // Mouse wheel down - Zoom Out
                _rawTextFontSize = Math.Max(_rawTextFontSize - RawZoomStep, MinRawFontSize);
                RawResponseTextBox.FontSize = _rawTextFontSize;
                _loggingService.LogOperation($"Zoomed Out Raw Text to {_rawTextFontSize:F0}pt via mouse wheel");
                break;
        }

        // Mark the event as handled to prevent default scrolling
        e.Handled = true; // THIS IS CRUCIAL WHEN CTRL IS PRESSED
        // If Ctrl is NOT pressed, do nothing. The event will NOT be handled here,
        // and the default TextBox scrolling will occur.
    }
}