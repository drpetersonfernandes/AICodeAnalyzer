using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AICodeAnalyzer.Models;
using AICodeAnalyzer.Services;
using Microsoft.Win32;
using System.Windows.Media;

namespace AICodeAnalyzer;

public partial class MainWindow
{
    // Services
    private readonly LoggingService _loggingService;
    private readonly FileService _fileService;
    private readonly AiProviderService _aiProviderService;
    private readonly ResponseService _responseService;
    private readonly MarkdownService _markdownService;
    private readonly UiStateManager _uiStateManager;
    private readonly TokenCounterService _tokenCounterService;

    // Additional managers
    private readonly RecentFilesManager _recentFilesManager = new();
    private readonly SettingsManager _settingsManager = new();

    // State tracking
    private TokenCalculationResult _tokenCalculationResult = new();

    private MenuItem? MenuConfigure => FindName("MenuConfigure") as MenuItem;
    private MenuItem? MenuStart => FindName("MenuStart") as MenuItem;
    private MenuItem? MenuAbout => FindName("MenuAbout") as MenuItem;
    private MenuItem? MenuOpenPastResponses => FindName("MenuOpenPastResponses") as MenuItem;
    private MenuItem? MenuExit => FindName("MenuExit") as MenuItem;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize services in the correct order (with dependencies)
        _loggingService = new LoggingService(TxtLog);
        _tokenCounterService = new TokenCounterService(_loggingService);
        _fileService = new FileService(_settingsManager, _loggingService);
        _aiProviderService = new AiProviderService(_loggingService);
        _responseService = new ResponseService(_loggingService, _fileService);
        _markdownService = new MarkdownService(
            _loggingService,
            TxtResponse,
            MarkdownViewer,
            MarkdownScrollViewer,
            TxtZoomLevel);
        _uiStateManager = new UiStateManager(TxtStatus, _loggingService);

        // Setup event handlers
        SetupEventHandlers();

        // Initialize UI
        InitializeUi();

        _loggingService.LogOperation("Application started");
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

        BtnToggleMarkdown.Click -= ToggleMarkdownView_Click;
        BtnToggleMarkdown.Click += ToggleMarkdownView_Click;

        BtnSaveEdits.Click -= SaveEdits_Click;
        BtnSaveEdits.Click += SaveEdits_Click;

        BtnZoomIn.Click -= BtnZoomIn_Click;
        BtnZoomIn.Click += BtnZoomIn_Click;

        BtnZoomOut.Click -= BtnZoomOut_Click;
        BtnZoomOut.Click += BtnZoomOut_Click;

        BtnResetZoom.Click -= BtnResetZoom_Click;
        BtnResetZoom.Click += BtnResetZoom_Click;

        AiProvider.SelectionChanged -= AiProvider_SelectionChanged;
        AiProvider.SelectionChanged += AiProvider_SelectionChanged;

        AiModel.SelectionChanged -= AiModel_SelectionChanged;
        AiModel.SelectionChanged += AiModel_SelectionChanged;

        PromptTemplates.SelectionChanged -= PromptTemplates_SelectionChanged;
        PromptTemplates.SelectionChanged += PromptTemplates_SelectionChanged;

        BtnContinue.Click -= BtnContinue_Click;
        BtnContinue.Click += BtnContinue_Click;

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

        // Scroll wheel event handlers
        TxtResponse.PreviewMouseWheel -= TxtResponse_PreviewMouseWheel;
        TxtResponse.PreviewMouseWheel += TxtResponse_PreviewMouseWheel;

        MarkdownScrollViewer.PreviewMouseWheel -= MarkdownScrollViewer_PreviewMouseWheel;
        MarkdownScrollViewer.PreviewMouseWheel += MarkdownScrollViewer_PreviewMouseWheel;

        MarkdownScrollViewer.SizeChanged -= MarkdownScrollViewer_SizeChanged;
        MarkdownScrollViewer.SizeChanged += MarkdownScrollViewer_SizeChanged;
    }

    private void InitializeUi()
    {
        // Populate UI elements
        PopulateAiProviders();
        LoadPromptTemplates();
        UpdateRecentFilesMenu();

        // Set initial UI state
        TxtFollowupQuestion.IsEnabled = true;
        IncludeSelectedFilesChecker.IsEnabled = true;
        UpdateNavigationControls();

        // Update token display
        UpdateTokenCountDisplay();

        // Check if a startup file path was passed
        if (Application.Current.Properties["StartupFilePath"] is string filePath)
        {
            // Load the file asynchronously, but don't await here since we're in the constructor
            Dispatcher.InvokeAsync(async () => { await LoadMarkdownFileAsync(filePath); });
        }
    }

    private void PopulateAiProviders()
    {
        // Clear and repopulate providers
        AiProvider.Items.Clear();

        foreach (var providerName in _aiProviderService.ProviderNames)
        {
            AiProvider.Items.Add(providerName);
        }

        AiProvider.SelectedIndex = -1; // Default to none
    }

    private void UpdateFileListDisplay()
    {
        ListOfFiles.Items.Clear();

        // Display files organized by folder
        DisplayFilesByFolder();

        // Recalculate tokens
        CalculateTotalTokens();

        // Update folder text box
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

        // Add mode indicator (folder scan or manual selection)
        ListOfFiles.Items.Add(new ListViewItem
        {
            Content = $"===== Files Summary ({_fileService.FilesByExtension.Values.Sum(v => v.Count)} total) =====",
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

        foreach (var ext in _fileService.FilesByExtension.Keys.OrderBy(k => k))
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

    private void UpdateTokenCountDisplay()
    {
        // Get the total token count from the result
        var totalTokens = _tokenCalculationResult.TotalTokens;

        // Format the display text with exact token count
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
            .SelectMany(files => files)
            .ToList();

        // Get the prompt template text for token counting
        var promptTemplate = GetPromptTemplateText();

        // Use SharpToken to calculate tokens
        _tokenCalculationResult = _tokenCounterService.CalculateTotalTokens(allFiles, promptTemplate);

        // Update UI
        UpdateTokenCountDisplay();

        _loggingService.LogOperation($"Token calculation complete: {_tokenCalculationResult.TotalTokens:N0} tokens");
    }

    private string GetPromptTemplateText()
    {
        // Get the selected prompt content from the dropdown or settings
        var selectedPromptTemplate = PromptTemplates.SelectedItem as CodePrompt;
        return selectedPromptTemplate?.Content ?? _settingsManager.Settings.InitialPrompt; // Fallback
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
            ErrorLogger.LogError(ex, "Opening folder selection dialog");
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
            ErrorLogger.LogError(ex, "Selecting files");
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
            // Ask for confirmation
            var result = MessageBox.Show(
                "Are you sure you want to clear all currently selected files?",
                "Clear Files", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _fileService.ClearFiles();
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error clearing files: {ex.Message}");
            ErrorLogger.LogError(ex, "Clearing files");
        }
    }

    private async void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Check for API key
            if (AiProviderKeys.SelectedIndex <= 0)
            {
                MessageBox.Show("Please enter an API key.", "Missing API Key", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                _loggingService.LogOperation("Analysis canceled: No API key provided");
                return;
            }

            // Get query text and API selection
            var queryText = TxtFollowupQuestion.Text.Trim();
            var apiSelection = AiProvider.SelectedItem?.ToString() ?? "Gemini API"; // Default to Gemini

            // Setup processing UI
            _uiStateManager.SetStatusMessage($"Processing with {apiSelection}...");
            _uiStateManager.SetProcessingState(true, $"Processing with {apiSelection}");
            _loggingService.LogOperation($"Starting query with {apiSelection}");
            _loggingService.StartOperationTimer("QueryProcessing");

            try
            {
                // Initialize prompt variable
                string prompt;

                // Create a temporary list to track files for this specific query
                var currentQueryFiles = new List<SourceFile>();

                // Process as initial query or non-follow-up
                _loggingService.LogOperation("Handling as initial query or simple follow-up");

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
                        prompt = "Please analyze the following code files:\n\n";
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

                    // Store the included files
                    _responseService.StoreIncludedFiles(currentQueryFiles);
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

                    // Clear any previously stored files
                    _responseService.StoreIncludedFiles(new List<SourceFile>());
                }

                // Add user query if provided
                if (!string.IsNullOrEmpty(queryText))
                {
                    prompt += "--- Additional Instructions/Question ---\n" + queryText;
                    _loggingService.LogOperation("Added query text to the prompt");
                }

                // Get selected API key
                var savedKeys = _aiProviderService.GetKeysForProvider(apiSelection);
                var keyIndex = AiProviderKeys.SelectedIndex - 1;
                var apiKey = keyIndex >= 0 && keyIndex < savedKeys.Count ? savedKeys[keyIndex] : string.Empty;

                // Get selected model if applicable
                string? modelId = null;
                if (AiModel.IsEnabled && AiModel.SelectedItem is ModelDropdownItem selectedModel)
                {
                    modelId = selectedModel.ModelId;
                }

                // Add to conversation history and store prompt
                _responseService.AddToConversation(prompt, "user");

                // Send to AI API and get response
                var response = await _aiProviderService.SendPromptAsync(
                    apiSelection, apiKey, prompt, _responseService.ConversationHistory, modelId);

                // Add response to conversation history
                _responseService.AddToConversation(response, "assistant");

                // Update display with the response
                _responseService.UpdateCurrentResponse(response, true);

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
            ErrorLogger.LogError(ex, "Error processing query");
        }
    }

    private void HandleQueryError(Exception ex)
    {
        _loggingService.LogOperation($"Error processing query: {ex.Message}");

        // Don't show ErrorLogger dialog for token limits or self-thrown exceptions
        if (!ex.Message.StartsWith("Token limit exceeded:", StringComparison.Ordinal) && ex is not ApplicationException)
        {
            ErrorLogger.LogError(ex, "Processing query");
        }

        _uiStateManager.SetStatusMessage("Error processing query.");
        _loggingService.EndOperationTimer("QueryProcessing");

        // Disable Continue Button in case of an error
        BtnContinue.IsEnabled = false;
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
                selectedFilesContent.AppendLine(CultureInfo.InvariantCulture, $"```{FileService.GetLanguageForExtension(matchingFile.Extension)}");
                selectedFilesContent.AppendLine(matchingFile.Content);
                selectedFilesContent.AppendLine("```");
                selectedFilesContent.AppendLine();
            }
            else
            {
                _loggingService.LogOperation($"Warning: Could not find source file data for selected item: '{cleanFileName}'");
            }
        }

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
            ErrorLogger.LogError(ex, $"Error populating model dropdown: {ex.Message}");
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

    private void UpdateResponseDisplay()
    {
        // Get current response data
        var responseText = _responseService.CurrentResponseText;
        var isShowingInputQuery = _responseService.IsShowingInputQuery;

        // Update UI elements based on state
        if (isShowingInputQuery)
        {
            // Show the input query
            _markdownService.SetContent(_responseService.GetInputQueryMarkdown());
            BtnShowInputQuery.Content = "Show AI Response";

            // Disable navigation and editing buttons
            BtnPreviousResponse.IsEnabled = false;
            BtnNextResponse.IsEnabled = false;
            BtnSaveResponse.IsEnabled = false;
            BtnSaveEdits.IsEnabled = false;
            BtnToggleMarkdown.IsEnabled = false;
        }
        else
        {
            // Show the actual response content
            _markdownService.SetContent(responseText);
            BtnShowInputQuery.Content = "Show Input Query";

            // Enable buttons
            BtnToggleMarkdown.IsEnabled = !string.IsNullOrEmpty(responseText);
            BtnSaveResponse.IsEnabled = !string.IsNullOrEmpty(responseText);
            BtnSaveEdits.IsEnabled = !string.IsNullOrEmpty(responseText);
            BtnShowInputQuery.IsEnabled = true; // Make sure the button is enabled when showing the AI response
        }
    }

    private void UpdateNavigationControls()
    {
        // Update navigation buttons based on current position
        BtnPreviousResponse.IsEnabled = _responseService.CanNavigatePrevious();
        BtnNextResponse.IsEnabled = _responseService.CanNavigateNext();

        // Update counter text
        TxtResponseCounter.Text = _responseService.GetNavigationCounterText();
    }

    private async void BtnSaveResponse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var responseText = _responseService.CurrentResponseText;

            if (string.IsNullOrWhiteSpace(responseText))
            {
                MessageBox.Show("There is no response to save.", "No Response", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Show saving indicator
            _uiStateManager.SetProcessingState(true, "Saving response");

            // Save the response
            var newFilePath = await _fileService.SaveResponseAsync(responseText, _responseService.CurrentFilePath);

            // Update the file path if it changed
            if (newFilePath != _responseService.CurrentFilePath)
            {
                _responseService.SetCurrentFilePath(newFilePath);
                AddToRecentFiles(newFilePath);
                TxtResponseCounter.Text = $"Viewing: {Path.GetFileName(newFilePath)}";
            }

            _uiStateManager.SetStatusMessage($"File saved: {Path.GetFileName(newFilePath)}");
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error saving response: {ex.Message}");
            ErrorLogger.LogError(ex, "Saving response to file");
            MessageBox.Show("An error occurred while saving the response.", "Save Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _uiStateManager.SetProcessingState(false);
        }
    }

    private void ToggleMarkdownView_Click(object sender, RoutedEventArgs e)
    {
        _markdownService.ToggleView();
        BtnToggleMarkdown.Content = _markdownService.IsMarkdownViewActive ? "Show Raw Text" : "Show Markdown";
    }

    private void BtnPreviousResponse_Click(object sender, RoutedEventArgs e)
    {
        _responseService.NavigatePrevious();
    }

    private void BtnNextResponse_Click(object sender, RoutedEventArgs e)
    {
        _responseService.NavigateNext();
    }

    private void BtnShowInputQuery_Click(object sender, RoutedEventArgs e)
    {
        _responseService.ToggleInputQueryView();

        // Update button content based on _isShowingInputQuery
        if (_responseService.IsShowingInputQuery)
        {
            BtnShowInputQuery.Content = "Show AI Response";
            BtnSaveResponse.IsEnabled = false;
            BtnSaveEdits.IsEnabled = false;
            BtnToggleMarkdown.IsEnabled = false;
        }
        else
        {
            BtnShowInputQuery.Content = "Show Input Query";
            BtnSaveResponse.IsEnabled = true;
            BtnSaveEdits.IsEnabled = true;
            BtnToggleMarkdown.IsEnabled = true;
        }
    }

    private void SaveEdits_Click(object sender, RoutedEventArgs e)
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

            // Update the current response text with the edited content from the raw text view
            if (!_markdownService.IsMarkdownViewActive)
            {
                _responseService.UpdateCurrentResponse(TxtResponse.Text);
            }

            // If we have a file path, automatically save changes to that file
            if (!string.IsNullOrEmpty(_responseService.CurrentFilePath) && File.Exists(_responseService.CurrentFilePath))
            {
                _uiStateManager.SetProcessingState(true, "Saving edits");

                // Save the changes to the file asynchronously
                File.WriteAllText(_responseService.CurrentFilePath, _responseService.CurrentResponseText);

                _loggingService.LogOperation($"Automatically saved edits to file: {_responseService.CurrentFilePath}");
                _uiStateManager.SetStatusMessage($"Edits applied and saved to {Path.GetFileName(_responseService.CurrentFilePath)}");

                MessageBox.Show($"Edits applied and saved to {Path.GetFileName(_responseService.CurrentFilePath)}",
                    "Edits Saved", MessageBoxButton.OK, MessageBoxImage.Information);

                _uiStateManager.SetProcessingState(false);
            }
            else
            {
                // No file path, so update the in-memory content
                _loggingService.LogOperation("Applied edits to the content (not saved to file)");
                _uiStateManager.SetStatusMessage("Edits applied (not saved to file)");

                MessageBox.Show("Edits applied to the content. Use 'Save Response' to save to a file.",
                    "Edits Applied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error saving edits: {ex.Message}");
            ErrorLogger.LogError(ex, "Saving edited markdown");
            MessageBox.Show("An error occurred while saving your edits.", "Edit Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _markdownService.ZoomIn();
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _markdownService.ZoomOut();
    }

    private void BtnResetZoom_Click(object sender, RoutedEventArgs e)
    {
        _markdownService.ResetZoom();
    }

    private void TxtResponse_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Check if Ctrl key is pressed for zooming
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        switch (e.Delta)
        {
            // Wheel scrolled up (Zoom In)
            case > 0:
                _markdownService.ZoomIn();
                break;
            // Wheel scrolled down (Zoom Out)
            case < 0:
                _markdownService.ZoomOut();
                break;
        }

        e.Handled = true; // Prevent default scroll behavior when zooming
    }

    private void MarkdownScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Check if Ctrl key is pressed for zooming
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        switch (e.Delta)
        {
            // Wheel scrolled up (Zoom In)
            case > 0:
                _markdownService.ZoomIn();
                break;
            // Wheel scrolled down (Zoom Out)
            case < 0:
                _markdownService.ZoomOut();
                break;
        }

        e.Handled = true; // Prevent default scroll behavior when zooming
    }

    private async void RecentFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not MenuItem { Tag: string filePath }) return;

            await LoadMarkdownFileAsync(filePath);

            // Ensure the input query button is disabled after loading
            BtnShowInputQuery.IsEnabled = false;
            _responseService.ToggleInputQueryView(); // Reset to false
            BtnShowInputQuery.Content = "Show Input Query";
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
        _loggingService.LogOperation("Cleared recent files list");
    }

    private async Task LoadMarkdownFileAsync(string filePath)
    {
        try
        {
            // Show loading indicator
            _uiStateManager.SetProcessingState(true, $"Loading {Path.GetFileName(filePath)}");

            // Read the file content
            var fileContent = await _fileService.LoadMarkdownFileAsync(filePath);

            if (string.IsNullOrEmpty(fileContent))
            {
                _uiStateManager.SetProcessingState(false);
                return;
            }

            // Update UI
            _responseService.SetCurrentFilePath(filePath);
            _responseService.UpdateCurrentResponse(fileContent);
            TxtResponseCounter.Text = $"Viewing: {Path.GetFileName(filePath)}";

            // Disable input query button when loading a file
            BtnShowInputQuery.IsEnabled = false;

            // Add to recent files
            AddToRecentFiles(filePath);

            _uiStateManager.SetStatusMessage($"Viewing file: {Path.GetFileName(filePath)}");
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
            // Settings were saved, update any necessary UI
            _loggingService.LogOperation("Settings updated");
            LoadPromptTemplates();
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error opening configuration window: {ex.Message}");
            ErrorLogger.LogError(ex, "Opening configuration window");
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

            // Reset zoom level
            _markdownService.ResetZoom();

            // Clear file list
            _fileService.ClearFiles();

            // Reset UI elements state
            BtnSendQuery.IsEnabled = true;
            TxtFollowupQuestion.IsEnabled = true;
            BtnSaveResponse.IsEnabled = false;
            BtnToggleMarkdown.IsEnabled = false;
            BtnShowInputQuery.IsEnabled = false;
            BtnShowInputQuery.Content = "Show Input Query";
            BtnSaveEdits.IsEnabled = false;

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

            _loggingService.LogOperation("Application reset complete");
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error restarting application: {ex.Message}");
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
                await LoadMarkdownFileAsync(dialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error opening past response: {ex.Message}");
            ErrorLogger.LogError(ex, "Opening past response");
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
        // This method opens the configuration window focused on the prompt templates tab
        OpenConfigurationWindow();
    }

    private void MarkdownScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // When the ScrollViewer size changes, update the page width
        _markdownService.UpdateMarkdownPageWidth();
    }

    private async void BtnContinue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Check for API key
            if (AiProviderKeys.SelectedIndex <= 0)
            {
                MessageBox.Show("Please enter an API key.", "Missing API Key", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                _loggingService.LogOperation("Analysis canceled: No API key provided");
                return;
            }

            // Get query text and API selection
            const string queryText = "continue";
            var apiSelection = AiProvider.SelectedItem?.ToString() ?? "Gemini API"; // Default to Gemini

            // Setup processing UI
            _uiStateManager.SetStatusMessage($"Processing with {apiSelection} (Continue)...");
            _uiStateManager.SetProcessingState(true, $"Processing with {apiSelection} (Continue)");
            _loggingService.LogOperation($"Sending 'Continue' with {apiSelection}");
            _loggingService.StartOperationTimer("ContinueProcessing");

            try
            {
                // Get selected API key
                var savedKeys = _aiProviderService.GetKeysForProvider(apiSelection);
                var keyIndex = AiProviderKeys.SelectedIndex - 1;
                var apiKey = keyIndex >= 0 && keyIndex < savedKeys.Count ? savedKeys[keyIndex] : string.Empty;

                // Get selected model if applicable
                string? modelId = null;
                if (AiModel.IsEnabled && AiModel.SelectedItem is ModelDropdownItem selectedModel)
                {
                    modelId = selectedModel.ModelId;
                }

                // Add to conversation history and store prompt
                _responseService.AddToConversation(queryText, "user");

                // Send to AI API and get response
                var response = await _aiProviderService.SendPromptAsync(
                    apiSelection, apiKey, queryText, _responseService.ConversationHistory, modelId);

                // Add response to conversation history
                _responseService.AddToConversation(response, "assistant");

                // Append the new response to the existing one
                _responseService.UpdateCurrentResponse(_responseService.CurrentResponseText + "\n" + response);

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
            ErrorLogger.LogError(ex, "Error processing 'Continue' query");
        }
    }
}