using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO; // Added for File.Exists
using System.Linq;
using System.Text;
using System.Threading.Tasks; // Added for async
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.Services;

public sealed class ResponseService(LoggingService loggingService, FileService fileService)
{
    private readonly LoggingService _loggingService = loggingService;
    private readonly FileService _fileService = fileService;
    private int _currentResponseIndex = -1;
    private string _currentResponseText = string.Empty;
    private string? _currentFilePath = string.Empty; // Made nullable
    private string _lastInputPrompt = string.Empty;
    private readonly List<SourceFile> _lastIncludedFiles = new();
    private string _previousMarkdownContent = string.Empty;
    private bool _isShowingInputQuery;

    public int CurrentResponseIndex => _currentResponseIndex;
    public string CurrentResponseText => _currentResponseText;
    public string? CurrentFilePath => _currentFilePath; // Made nullable
    public bool IsShowingInputQuery => _isShowingInputQuery;
    public string PreviousMarkdownContent => _previousMarkdownContent;
    public List<ChatMessage> ConversationHistory { get; } = new();

    // Events
    public event EventHandler ResponseUpdated = static delegate { };
    public event EventHandler NavigationChanged = static delegate { };

    public void SetCurrentFilePath(string? path) // Made nullable
    {
        _currentFilePath = path;
    }

    /// <summary>
    /// Updates the current response text and optionally triggers auto-save and file path update.
    /// </summary>
    /// <param name="responseText">The new response text.</param>
    /// <param name="isNewResponse">True if this is a newly generated AI response, false otherwise (e.g., loading from file, applying edits).</param>
    public void UpdateCurrentResponse(string responseText, bool isNewResponse = false)
    {
        _currentResponseText = responseText;
        _previousMarkdownContent = responseText;

        if (isNewResponse)
        {
            // For a new response, create a new ChatMessage and add it
            var newAssistantMessage = new ChatMessage
            {
                Role = "assistant",
                Content = responseText,
                FilePath = null // FilePath will be set after auto-save
            };
            ConversationHistory.Add(newAssistantMessage);

            // Auto-save the response and get the path
            // Pass the 0-based index of the newly added message
            var filePath = _fileService.AutoSaveResponse(responseText, ConversationHistory.Count - 1);

            // Update the newly added message with the file path
            newAssistantMessage.FilePath = filePath;

            // Set the current state
            _currentResponseIndex = ConversationHistory.Count - 1;
            _currentFilePath = filePath;
        }
        // If !isNewResponse, it means we are updating the *current* response (e.g., from editing or loading a file).
        // The _currentFilePath should already be set in these cases.
        // We just update the in-memory text (_currentResponseText) and keep the existing _currentFilePath.
        // The history entry's content is NOT updated here, as navigation will reload from the file if FilePath exists.

        OnResponseUpdated();
        OnNavigationChanged();
    }

    public void AddToConversation(string content, string role)
    {
        // Only add user messages here. Assistant messages are added via UpdateCurrentResponse
        if (role != "user") return;

        ConversationHistory.Add(new ChatMessage { Role = role, Content = content, FilePath = null });
        _lastInputPrompt = content;
        OnNavigationChanged();
        // Ignore assistant role here to ensure UpdateCurrentResponse is the single source for assistant messages
    }

    public void StoreIncludedFiles(List<SourceFile> includedFiles)
    {
        _lastIncludedFiles.Clear();
        _lastIncludedFiles.AddRange(includedFiles);
        _loggingService.LogOperation($"Stored {_lastIncludedFiles.Count} included files for reference");
    }

    public void ClearHistory()
    {
        ConversationHistory.Clear();
        _currentResponseIndex = -1;
        _currentResponseText = string.Empty;
        _previousMarkdownContent = string.Empty;
        _lastInputPrompt = string.Empty;
        _lastIncludedFiles.Clear();
        _isShowingInputQuery = false;
        _currentFilePath = string.Empty; // Set to empty string or null

        OnResponseUpdated();
        OnNavigationChanged();

        _loggingService.LogOperation("Cleared conversation history and responses");
    }

    private async Task NavigateToResponse(int index) // Made async
    {
        // Get all assistant responses from the conversation history
        var assistantMessages = ConversationHistory
            .Where(m => m.Role == "assistant")
            .ToList();

        // Ensure the index is within bounds
        if (assistantMessages.Count == 0 || index < 0 || index >= assistantMessages.Count)
        {
            // This should not happen if CanNavigatePrevious/Next are checked, but as a safeguard
            _loggingService.LogOperation($"Navigation failed: Index {index} out of bounds for {assistantMessages.Count} assistant messages.");
            return;
        }

        // Find the original ChatMessage object in the full history list
        // We need the original object to access its FilePath property
        var targetMessage = ConversationHistory
            .Where(m => m.Role == "assistant")
            .ElementAt(index); // Use ElementAt to get the message at the correct assistant index

        if (targetMessage == null)
        {
            _loggingService.LogOperation($"Navigation failed: Could not find message at assistant index {index}.");
            return;
        }

        string contentToDisplay;
        string? filePathToSet;

        // Check if the message has an associated file path and if the file exists
        if (!string.IsNullOrEmpty(targetMessage.FilePath) && File.Exists(targetMessage.FilePath))
        {
            _loggingService.LogOperation($"Navigating to file-backed response: {Path.GetFileName(targetMessage.FilePath)}");
            // Load the content from the file
            contentToDisplay = await _fileService.LoadMarkdownFileAsync(targetMessage.FilePath);
            filePathToSet = targetMessage.FilePath;
        }
        else
        {
            _loggingService.LogOperation($"Navigating to in-memory response #{index + 1}.");
            // Use the content stored in the history
            contentToDisplay = targetMessage.Content;
            filePathToSet = string.Empty; // No file path associated with this view
        }

        // Update the current state
        _currentResponseIndex = index;
        _currentResponseText = contentToDisplay;
        _previousMarkdownContent = contentToDisplay; // Also update previous content for potential edits
        _currentFilePath = filePathToSet;

        OnResponseUpdated();
        OnNavigationChanged();

        _loggingService.LogOperation($"Navigated to response #{index + 1} of {assistantMessages.Count}");
    }

    public bool CanNavigatePrevious()
    {
        return _currentResponseIndex > 0;
    }

    public bool CanNavigateNext()
    {
        var totalAssistantResponses = ConversationHistory.Count(m => m.Role == "assistant");
        return _currentResponseIndex < totalAssistantResponses - 1;
    }

    public string GetNavigationCounterText()
    {
        var totalAssistantResponses = ConversationHistory.Count(m => m.Role == "assistant");

        if (totalAssistantResponses == 0)
        {
            return "No responses";
        }
        else
        {
            // Display as a 1-based index for the user (1 of 1 instead of 0 of 0)
            return $"Response {_currentResponseIndex + 1} of {totalAssistantResponses}";
        }
    }

    public async Task NavigatePrevious() // Made async
    {
        if (CanNavigatePrevious())
        {
            await NavigateToResponse(_currentResponseIndex - 1);
        }
    }

    public async Task NavigateNext() // Made async
    {
        if (CanNavigateNext())
        {
            await NavigateToResponse(_currentResponseIndex + 1);
        }
    }

    public string GetInputQueryMarkdown()
    {
        // Generate File Summary
        var fileSummary = new StringBuilder();
        fileSummary.AppendLine("## Files Included in Query");
        fileSummary.AppendLine(CultureInfo.InvariantCulture, $"Total files: {_lastIncludedFiles.Count}");

        if (_lastIncludedFiles.Count != 0)
        {
            fileSummary.AppendLine("```");
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
        fullInputView.AppendLine("```text");
        fullInputView.AppendLine(_lastInputPrompt);
        fullInputView.AppendLine("```");

        return fullInputView.ToString();
    }

    public void ToggleInputQueryView()
    {
        _isShowingInputQuery = !_isShowingInputQuery;
        OnResponseUpdated();
        OnNavigationChanged();
    }

    // Event invokers
    private void OnResponseUpdated()
    {
        ResponseUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void OnNavigationChanged()
    {
        NavigationChanged?.Invoke(this, EventArgs.Empty);
    }
}
