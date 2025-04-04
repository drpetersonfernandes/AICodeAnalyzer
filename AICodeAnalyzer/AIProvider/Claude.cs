using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Interfaces;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.AIProvider;

public class Claude : IAiApiProvider
{
    private readonly HttpClient _httpClient = new();

    public string Name => "Claude API";
    public string DefaultModel => "claude-3-sonnet-20240229";

    public Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        return SendPromptWithModelAsync(apiKey, prompt, conversationHistory, DefaultModel);
    }

    /// <summary>
    /// Available Claude models
    /// </summary>
    private static class Models
    {
        // Claude 3.7 models
        public const string Claude37Sonnet = "claude-3-7-sonnet-20250219";

        // Claude 3.5 models
        public const string Claude35Sonnet = "claude-3-5-sonnet-20240620";
        public const string Claude35Haiku = "claude-3-5-haiku-20240307";

        // Claude 3 models
        public const string Claude3Opus = "claude-3-opus-20240229";
    }

    /// <summary>
    /// Gets all available Claude models with their descriptions
    /// </summary>
    /// <returns>List of model options</returns>
    public List<ClaudeModelInfo> GetAvailableModels()
    {
        return new List<ClaudeModelInfo>
        {
            // Claude 3.7
            new()
            {
                Id = Models.Claude37Sonnet,
                Name = "Claude 3.7 Sonnet",
                Description = "Context window 200,000 tokens. Input $3,00. Output $15,00.",
                ContextLength = 200000,
                MaxOutputTokens = 4096
            },

            // Claude 3.5
            new()
            {
                Id = Models.Claude35Sonnet,
                Name = "Claude 3.5 Sonnet",
                Description = "Context window 200,000 tokens. Input $3,00. Output $15,00.",
                ContextLength = 200000,
                MaxOutputTokens = 4096
            },
            new()
            {
                Id = Models.Claude35Haiku,
                Name = "Claude 3.5 Haiku",
                Description = "Context window 200,000 tokens. Input $0,8. Output $4,00.",
                ContextLength = 200000,
                MaxOutputTokens = 4096
            },

            // Claude 3
            new()
            {
                Id = Models.Claude3Opus,
                Name = "Claude 3 Opus",
                Description = "Context window 200,000 tokens. Input $15,00. Output $75,00.",
                ContextLength = 200000,
                MaxOutputTokens = 4096
            }
        };
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        var model = modelId;
        var apiUrl = "https://api.anthropic.com/v1/messages";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        // Properly format the message history for Claude API
        var messages = new List<object>();

        // Add each message from history with the proper format
        foreach (var msg in conversationHistory)
        {
            messages.Add(new { role = msg.Role, content = msg.Content });
        }

        // Add the current prompt
        messages.Add(new { role = "user", content = prompt });

        var requestData = new
        {
            model,
            messages,
            max_tokens = 4096
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

        return doc.RootElement.GetProperty("content").EnumerateArray()
            .First(x => x.GetProperty("type").GetString() == "text")
            .GetProperty("text").GetString() ?? "No response";
    }
}

/// <summary>
/// Represents information about a Claude model
/// </summary>
public class ClaudeModelInfo
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
    public int MaxOutputTokens { get; set; }
}