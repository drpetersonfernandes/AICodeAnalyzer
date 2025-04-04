using System.Collections.Generic;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.Interfaces;

public interface IAiApiProvider
{
    string Name { get; }
    string DefaultModel { get; }
    Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory);
}