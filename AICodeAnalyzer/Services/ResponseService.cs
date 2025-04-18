using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.Services;

public sealed class ResponseService(LoggingService loggingService, FileService fileService)
{
    private readonly LoggingService _loggingService = loggingService;
    private readonly FileService _fileService = fileService;
    private int _currentResponseIndex = -1;
    private string _currentResponseText = string.Empty;
    private string _currentFilePath = string.Empty;
    private string _lastInputPrompt = string.Empty;
    private readonly List<SourceFile> _lastIncludedFiles = new();
    private string _previousMarkdownContent = string.Empty;
    private bool _isShowingInputQuery;

    public int CurrentResponseIndex => _currentResponseIndex;
    public string CurrentResponseText => _currentResponseText;
    public string CurrentFilePath => _currentFilePath;
    public bool IsShowingInputQuery => _isShowingInputQuery;
    public string PreviousMarkdownContent => _previousMarkdownContent;
    public List<ChatMessage> ConversationHistory { get; } = new();

    // Events
    public event EventHandler ResponseUpdated = static delegate { };
    public event EventHandler NavigationChanged = static delegate { };

    public void SetCurrentFilePath(string path)
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
            // For a new response, set the index to the last response
            _currentResponseIndex = ConversationHistory.Count(m => m.Role == "assistant") - 1;

            // Auto-save the response and store the path
            _currentFilePath = _fileService.AutoSaveResponse(responseText, _currentResponseIndex);
        }

        OnResponseUpdated();
        OnNavigationChanged();
    }

    public void AddToConversation(string content, string role)
    {
        ConversationHistory.Add(new ChatMessage { Role = role, Content = content });

        if (role == "user")
        {
            _lastInputPrompt = content;
        }

        OnNavigationChanged();
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
        _currentFilePath = string.Empty;

        OnResponseUpdated();
        OnNavigationChanged();

        _loggingService.LogOperation("Cleared conversation history and responses");
    }

    private void NavigateToResponse(int index)
    {
        // Get all assistant responses from the conversation history
        var assistantResponses = ConversationHistory
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

        // Update the current response text
        _currentResponseText = response;
        _previousMarkdownContent = response;

        // When navigating, clear the current file path as we are viewing history, not a loaded file
        _currentFilePath = string.Empty;


        OnResponseUpdated();
        OnNavigationChanged();

        _loggingService.LogOperation($"Navigated to response #{index + 1} of {assistantResponses.Count}");
    }

    public bool CanNavigatePrevious()
    {
        return _currentResponseIndex > 0;
    }

    public bool CanNavigateNext()
    {
        var totalResponses = ConversationHistory.Count(m => m.Role == "assistant");
        return _currentResponseIndex < totalResponses - 1;
    }

    public string GetNavigationCounterText()
    {
        var assistantResponses = ConversationHistory.Count(m => m.Role == "assistant");

        if (assistantResponses == 0)
        {
            return "No responses";
        }
        else
        {
            // Display as a 1-based index for the user (1 of 1 instead of 0 of 0)
            return $"Response {_currentResponseIndex + 1} of {assistantResponses}";
        }
    }

    public void NavigatePrevious()
    {
        if (CanNavigatePrevious())
        {
            NavigateToResponse(_currentResponseIndex - 1);
        }
    }

    public void NavigateNext()
    {
        if (CanNavigateNext())
        {
            NavigateToResponse(_currentResponseIndex + 1);
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
