using System.Collections.Generic;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.Interfaces;

/// <summary>
/// Interface for all AI API providers
/// </summary>
public interface IAiApiProvider
{
    /// <summary>
    /// Gets the display name of the API provider
    /// </summary>
    string Name { get; }
        
    /// <summary>
    /// Gets the default model name for this provider
    /// </summary>
    string DefaultModel { get; }
        
    /// <summary>
    /// Sends a prompt to the AI API and returns the response
    /// </summary>
    /// <param name="apiKey">The API key to use for authentication</param>
    /// <param name="prompt">The prompt to send to the API</param>
    /// <param name="conversationHistory">Previous messages in the conversation</param>
    /// <returns>The AI's response text</returns>
    Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory);
}