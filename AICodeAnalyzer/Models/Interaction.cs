using System.Collections.Generic;

namespace AICodeAnalyzer.Models;

public class Interaction
{
    public string UserPrompt { get; set; } = string.Empty;
    public string? UserPromptFilePath { get; set; }
    public List<SourceFile> IncludedFiles { get; set; } = new();

    public string AssistantResponse { get; set; } = string.Empty;
    public string? AssistantResponseFilePath { get; set; }
}