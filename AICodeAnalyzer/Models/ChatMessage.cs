namespace AICodeAnalyzer.Models;

public class ChatMessage
{
    public required string Role { get; set; }
    public required string Content { get; set; }

    public string? FilePath { get; set; }
}