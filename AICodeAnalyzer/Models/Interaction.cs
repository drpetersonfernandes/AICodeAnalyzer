using System.Collections.Generic;

namespace AICodeAnalyzer.Models;

/// <summary>
/// Represents a single turn of conversation, linking the user's input,
/// included files, and the AI's response, along with their saved file paths.
/// </summary>
public class Interaction
{
    /// <summary>
    /// The full text of the user's prompt sent to the AI.
    /// </summary>
    public string UserPrompt { get; set; } = string.Empty;

    /// <summary>
    /// The list of source files included with this specific user prompt.
    /// </summary>
    public List<SourceFile> IncludedFiles { get; set; } = new();

    /// <summary>
    /// The full text of the AI's response.
    /// </summary>
    public string AssistantResponse { get; set; } = string.Empty;

    /// <summary>
    /// The file path where the UserPrompt was auto-saved.
    /// </summary>
    public string? UserPromptFilePath { get; set; }

    /// <summary>
    /// The file path where the AssistantResponse was auto-saved.
    /// </summary>
    public string? AssistantResponseFilePath { get; set; }
}