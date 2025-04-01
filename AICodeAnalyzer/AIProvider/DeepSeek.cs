using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Interfaces;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.AIProvider;

public class DeepSeek : IAiApiProvider
{
    private readonly HttpClient _httpClient = new();

    public string Name => "DeepSeek API";
    public string DefaultModel => "deepseek-coder"; // Keep deepseek-coder as default for backward compatibility

    public Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Available DeepSeek models
    /// </summary>
    private static class Models
    {
        // General purpose models
        public const string DeepSeekChat = "deepseek-chat"; // DeepSeek-V3
        public const string DeepSeekReasoner = "deepseek-reasoner"; // DeepSeek-R1
    }

    /// <summary>
    /// Gets all available DeepSeek models with their descriptions
    /// </summary>
    /// <returns>List of model options</returns>
    public List<DeepSeekModelInfo> GetAvailableModels()
    {
        return new List<DeepSeekModelInfo>
        {
            new()
            {
                Id = Models.DeepSeekChat,
                Name = "DeepSeek Chat (V3)",
                Description = "General purpose chat model with 64K context window",
                ContextLength = 65536,
                MaxOutputTokens = 8192
            },
            new()
            {
                Id = Models.DeepSeekReasoner,
                Name = "DeepSeek Reasoner (R1)",
                Description = "Reasoning model with chain-of-thought capabilities, 64K context",
                ContextLength = 65536,
                MaxOutputTokens = 8192
            }
        };
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        // Use specified model or fall back to default
        var model = modelId; // Fix: Use null-coalescing operator to handle null modelId
        var apiUrl = "https://api.deepseek.com/v1/chat/completions";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Properly format the message history for DeepSeek API
        var messages = new List<object>();

        // First add system message if this is the first message
        if (conversationHistory.Count == 0)
        {
            var systemPrompt = GetSystemPromptForModel(model);
            messages.Add(new { role = "system", content = systemPrompt });
        }
        else
        {
            // Add each message from history with the proper format
            foreach (var msg in conversationHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }
        }

        // Add the current prompt
        messages.Add(new { role = "user", content = prompt });

        // Determine max tokens based on model
        var maxTokens = GetMaxTokensForModel(model);

        var requestData = new
        {
            model,
            messages,
            max_tokens = maxTokens
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestData),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(apiUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new Exception($"API error ({response.StatusCode}): {errorText}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        return doc.RootElement.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content").GetString() ?? "No response";
    }

    /// <summary>
    /// Gets the appropriate system prompt based on the model
    /// </summary>
    private string GetSystemPromptForModel(string model)
    {
        return model switch
        {
            Models.DeepSeekReasoner => "You are a helpful assistant specializing in code review and analysis.",
            _ => "You are a helpful assistant specializing in code review and analysis." // Default for deepseek-chat and others
        };
    }

    /// <summary>
    /// Gets the maximum output tokens for the specified model
    /// </summary>
    private int GetMaxTokensForModel(string model)
    {
        return model switch
        {
            Models.DeepSeekChat => 8192, // 8K for DeepSeek-V3
            Models.DeepSeekReasoner => 8192, // 8K for DeepSeek-R1
            _ => 4096 // 4K for DeepSeek-Coder and others
        };
    }
}

/// <summary>
/// Represents information about an AI model
/// </summary>
public class DeepSeekModelInfo
{
    /// <summary>
    /// The model identifier used in API calls
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Display name for the model
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Description of the model's capabilities
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Maximum context length in tokens
    /// </summary>
    public int ContextLength { get; set; }

    /// <summary>
    /// Maximum number of output tokens
    /// </summary>
    public int MaxOutputTokens { get; set; } // Removed redundant initialization
}