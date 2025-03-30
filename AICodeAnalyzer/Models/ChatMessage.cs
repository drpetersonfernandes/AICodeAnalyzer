namespace AICodeAnalyzer.Models;

/// <summary>
/// Represents a single message in a chat conversation.
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Specifies the role of a chat participant in a conversation, such as "user", "assistant", or "system".
    /// </summary>
    public required string Role { get; set; }

    /// <summary>
    /// Gets or sets the content of the chat message. This property represents the
    /// main body of the message text that is sent or received during interaction.
    /// </summary>
    public required string Content { get; set; }
}