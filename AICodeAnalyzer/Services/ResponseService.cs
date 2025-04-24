using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.Services;

public sealed class ResponseService(LoggingService loggingService, FileService fileService)
{
    private readonly LoggingService _loggingService = loggingService;
    private readonly FileService _fileService = fileService;

    // Use a list of Interactions to store the history
    private readonly List<Interaction> _interactions = new();
    private int _currentInteractionIndex = -1; // Index of the currently viewed interaction

    private bool _isShowingInputQuery; // State for toggling between input/output view

    // Events
    public event EventHandler ResponseUpdated = static delegate { };
    public event EventHandler NavigationChanged = static delegate { };

    // Expose the current state based on the selected interaction
    public string CurrentResponseText { get; private set; } = string.Empty;
    public string? CurrentFilePath { get; private set; } // File path of the *displayed* content (response file if viewing response, query file if viewing query)
    public bool IsShowingInputQuery => _isShowingInputQuery;

    // Publicly expose the current index (using the existing property name)
    public int CurrentResponseIndex => _currentInteractionIndex;

    // Expose the list of interactions for methods like LoadMarkdownFile
    public IReadOnlyList<Interaction> Interactions => _interactions.AsReadOnly();

    /// <summary>
    /// Starts a new interaction by saving the user prompt and included files.
    /// </summary>
    /// <param name="userPrompt">The full text of the user's prompt.</param>
    /// <param name="includedFiles">The list of files included with this prompt.</param>
    public async Task<Interaction> StartNewInteractionAsync(string userPrompt, List<SourceFile> includedFiles)
    {
        try
        {
            var newInteraction = new Interaction
            {
                UserPrompt = userPrompt,
                IncludedFiles = includedFiles,
                UserPromptFilePath = await _fileService.AutoSaveInputQuery(userPrompt, _interactions.Count)
            };

            _interactions.Add(newInteraction);
            _currentInteractionIndex = _interactions.Count - 1;
            _isShowingInputQuery = true;

            _loggingService.LogOperation($"Started new interaction #{_currentInteractionIndex + 1}.");

            return newInteraction;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error starting new interaction.");
            return new Interaction();
        }
    }

    /// <summary>
    /// Updates the current interaction with the AI's response.
    /// </summary>
    /// <param name="assistantResponse">The text of the AI's response.</param>
    public async Task CompleteCurrentInteraction(string assistantResponse)
    {
        if (_currentInteractionIndex < 0 || _currentInteractionIndex >= _interactions.Count)
        {
            _loggingService.LogOperation("Attempted to complete interaction but no current interaction found.");
            // Handle error or create a new interaction if necessary
            return;
        }

        var currentInteraction = _interactions[_currentInteractionIndex];

        // Update the interaction with the response
        currentInteraction.AssistantResponse = assistantResponse;

        // Auto-save the assistant response
        currentInteraction.AssistantResponseFilePath = _fileService.AutoSaveAiResponse(assistantResponse, _currentInteractionIndex);

        // Switch to showing the response view
        _isShowingInputQuery = false;

        // Update UI (will show the response)
        await DisplayCurrentInteraction();

        _loggingService.LogOperation($"Completed interaction #{_currentInteractionIndex + 1} with AI response.");
    }

    public void ClearCurrentResponse()
    {
        CurrentResponseText = string.Empty;
        CurrentFilePath = string.Empty;
        _isShowingInputQuery = false;
        OnResponseUpdated();
        OnNavigationChanged();
    }

    /// <summary>
    /// Adds a pre-built interaction (like a loaded file) to the history and sets it as the current one.
    /// </summary>
    /// <param name="interaction">The interaction to add.</param>
    public async Task AddInteractionAndSetCurrent(Interaction interaction)
    {
        try
        {
            _interactions.Add(interaction);
            _currentInteractionIndex = _interactions.Count - 1;
            // Default to showing the response for loaded files
            _isShowingInputQuery = false;
            await DisplayCurrentInteraction();
            _loggingService.LogOperation($"Added interaction #{_currentInteractionIndex + 1} and set as current.");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding interaction and setting as current.");
        }
    }

    /// <summary>
    /// Sets the current interaction index.
    /// </summary>
    /// <param name="index">The 0-based index of the interaction to display.</param>
    public async void SetCurrentInteractionIndex(int index)
    {
        try
        {
            if (index >= 0 && index < _interactions.Count)
            {
                _currentInteractionIndex = index;
                // Default to showing the response when navigating
                _isShowingInputQuery = false;
                await DisplayCurrentInteraction();
                _loggingService.LogOperation($"Set current interaction index to {index}.");
            }
            else
            {
                _loggingService.LogOperation($"Attempted to set invalid interaction index: {index}.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error setting current interaction index.");
        }
    }

    /// <summary>
    /// Gets the conversation history formatted for the AI API.
    /// </summary>
    /// <returns>A list of ChatMessage objects representing the history.</returns>
    public List<ChatMessage> GetConversationHistoryForApi()
    {
        var history = new List<ChatMessage>();

        // Reconstruct history from interactions up to the current one
        for (var i = 0; i <= _currentInteractionIndex; i++)
        {
            var interaction = _interactions[i];

            // Add user prompt (use in-memory content)
            // The content should already be loaded into UserPrompt by StartNewInteraction
            history.Add(new ChatMessage { Role = "user", Content = interaction.UserPrompt });

            // Add assistant response if it exists (use in-memory content)
            // The content should already be loaded into AssistantResponse by CompleteCurrentInteraction
            if (!string.IsNullOrEmpty(interaction.AssistantResponse))
            {
                history.Add(new ChatMessage { Role = "assistant", Content = interaction.AssistantResponse });
            }
        }

        return history;
    }

    /// <summary>
    /// Clears all interaction history.
    /// </summary>
    public void ClearHistory()
    {
        _interactions.Clear();
        _currentInteractionIndex = -1;
        CurrentResponseText = string.Empty;
        CurrentFilePath = string.Empty;
        _isShowingInputQuery = false;

        OnResponseUpdated();
        OnNavigationChanged();

        _loggingService.LogOperation("Cleared all interaction history.");
    }

    /// <summary>
    /// Displays the interaction at the current index.
    /// Loads content from files if paths exist and in-memory content is empty.
    /// </summary>
    public Task DisplayCurrentInteraction() // Changed to return Task
    {
        try
        {
            if (_currentInteractionIndex < 0 || _currentInteractionIndex >= _interactions.Count)
            {
                CurrentResponseText = string.Empty;
                CurrentFilePath = string.Empty;
                _isShowingInputQuery = false; // Should default to not showing input if no interaction
                OnResponseUpdated();
                OnNavigationChanged();
                return Task.CompletedTask;
            }

            var interaction = _interactions[_currentInteractionIndex];

            string contentToDisplay;
            string? filePathToSet;

            if (_isShowingInputQuery)
            {
                // Display the user prompt
                // Prioritize in-memory content, fallback to file if path exists
                contentToDisplay = interaction.UserPrompt;
                filePathToSet = interaction.UserPromptFilePath;

                // Always attempt to load from file if path exists, making the file the source of truth
                if (!string.IsNullOrEmpty(filePathToSet) && File.Exists(filePathToSet))
                {
                    contentToDisplay = _fileService.LoadMarkdownFile(filePathToSet);
                    // Optionally update the in-memory property if loaded from file
                    interaction.UserPrompt = contentToDisplay;
                }

                _loggingService.LogOperation($"Displaying input query for interaction #{_currentInteractionIndex + 1}.");
            }
            else
            {
                // Display the assistant response
                // Prioritize in-memory content, fallback to file if path exists
                contentToDisplay = interaction.AssistantResponse;
                filePathToSet = interaction.AssistantResponseFilePath;

                // Always attempt to load from file if path exists, making the file the source of truth
                if (!string.IsNullOrEmpty(filePathToSet) && File.Exists(filePathToSet))
                {
                    contentToDisplay = _fileService.LoadMarkdownFile(filePathToSet);
                    // Optionally update the in-memory property if loaded from file
                    interaction.AssistantResponse = contentToDisplay;
                }

                _loggingService.LogOperation($"Displaying assistant response for interaction #{_currentInteractionIndex + 1}.");
            }

            CurrentResponseText = contentToDisplay;
            CurrentFilePath = filePathToSet;

            OnResponseUpdated();
            OnNavigationChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error displaying current interaction.");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the content of the currently displayed item (either prompt or response).
    /// This is used for applying edits from the raw text view.
    /// </summary>
    /// <param name="editedContent">The edited text.</param>
    public void UpdateCurrentResponse(string editedContent)
    {
        if (_currentInteractionIndex < 0 || _currentInteractionIndex >= _interactions.Count)
        {
            _loggingService.LogOperation("Attempted to update content but no current interaction found.");
            return;
        }

        var interaction = _interactions[_currentInteractionIndex];

        if (_isShowingInputQuery)
        {
            // Update the user prompt content
            interaction.UserPrompt = editedContent;
            // Note: We don't auto-save edits to the query file here.
            // The user can use "Save Response" to save the whole interaction if needed.
            _loggingService.LogOperation($"Updated in-memory input query for interaction #{_currentInteractionIndex + 1}.");
        }
        else
        {
            // Update the assistant response content
            interaction.AssistantResponse = editedContent;
            // Note: Auto-saving to the response file happens in SaveEdits_Click in MainWindow
            _loggingService.LogOperation($"Updated in-memory assistant response for interaction #{_currentInteractionIndex + 1}.");
        }

        // Update the displayed text immediately
        CurrentResponseText = editedContent;
        // Do NOT call DisplayCurrentInteraction here, as it would reload from file/memory
        // and overwrite the edits just applied to the in-memory object.
        // The UI update is handled by the TextChanged event or the SaveEdits_Click logic.
    }


    public bool CanNavigatePrevious()
    {
        return _currentInteractionIndex > 0;
    }

    public bool CanNavigateNext()
    {
        return _currentInteractionIndex < _interactions.Count - 1;
    }

    public string GetNavigationCounterText()
    {
        var totalInteractions = _interactions.Count;

        if (totalInteractions == 0)
        {
            return "No interactions";
        }
        else
        {
            // Display as a 1-based index for the user
            return $"Interaction {_currentInteractionIndex + 1} of {totalInteractions}";
        }
    }

    public async Task NavigatePrevious() // Changed to async Task
    {
        if (CanNavigatePrevious())
        {
            _currentInteractionIndex--;
            await DisplayCurrentInteraction(); // Await the async display method
        }
    }

    public async Task NavigateNext() // Changed to async Task
    {
        if (CanNavigateNext())
        {
            _currentInteractionIndex++;
            await DisplayCurrentInteraction(); // Await the async display method
        }
    }

    /// <summary>
    /// Gets the markdown content for the input query of the currently viewed interaction.
    /// </summary>
    /// <returns>Markdown formatted string of the input query and included files.</returns>
    // Change signature to async Task<string>
    public Task<string> GetInputQueryMarkdownAsync()
    {
        if (_currentInteractionIndex < 0 || _currentInteractionIndex >= _interactions.Count)
        {
            return Task.FromResult("No input query available for this interaction.");
        }

        var interaction = _interactions[_currentInteractionIndex];

        // Generate File Summary
        var fileSummary = new StringBuilder();
        fileSummary.AppendLine("## Files Included in Query");
        fileSummary.AppendLine(CultureInfo.InvariantCulture, $"Total files: {interaction.IncludedFiles.Count}");

        if (interaction.IncludedFiles.Count != 0)
        {
            fileSummary.AppendLine("```");
            foreach (var file in interaction.IncludedFiles.OrderBy(f => f.RelativePath))
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

        string promptContent;

        if (!string.IsNullOrEmpty(interaction.UserPromptFilePath) && File.Exists(interaction.UserPromptFilePath))
        {
            promptContent = _fileService.LoadMarkdownFile(interaction.UserPromptFilePath);
        }
        else
        {
            promptContent = interaction.UserPrompt;
        }

        fullInputView.AppendLine(promptContent);
        fullInputView.AppendLine("```");

        return Task.FromResult(fullInputView.ToString());
    }

    /// <summary>
    /// Toggles between displaying the input query and the AI response for the current interaction.
    /// </summary>
    public async void ToggleInputQueryView()
    {
        try
        {
            _isShowingInputQuery = !_isShowingInputQuery;
            // Display the current interaction again to update the view
            await DisplayCurrentInteraction();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling input query view.");
        }
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