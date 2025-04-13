using System.Collections.Generic;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer;

public interface IAiApiProvider
{
    string Name { get; }
    Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory);
}