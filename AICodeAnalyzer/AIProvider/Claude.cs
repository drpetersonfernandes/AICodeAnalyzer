using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.AIProvider;

public class Claude : IAiApiProvider, IDisposable
{
    private static readonly HttpClient HttpClient = new();

    public string Name => "Claude API";
    private static string DefaultModel => "claude-3-7-sonnet-20250219";

    public Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        return SendPromptWithModelAsync(apiKey, prompt, conversationHistory, DefaultModel);
    }

    private static class Models
    {
        public const string Claude37Sonnet = "claude-3-7-sonnet-20250219";
        public const string Claude35Sonnet = "claude-3-5-sonnet-20240620";
        public const string Claude35Haiku = "claude-3-5-haiku-20240307";
        public const string Claude3Opus = "claude-3-opus-20240229";
    }

    public List<ClaudeModelInfo> GetAvailableModels()
    {
        return new List<ClaudeModelInfo>
        {
            // Claude 3.7
            new()
            {
                Id = Models.Claude37Sonnet,
                Name = "Claude 3.7 Sonnet",
                Description = "200K context - $3/M input tokens - $15/M output tokens.",
                ContextLength = 200000
            },

            // Claude 3.5
            new()
            {
                Id = Models.Claude35Sonnet,
                Name = "Claude 3.5 Sonnet",
                Description = "200K context - $3/M input tokens - $15/M output tokens.",
                ContextLength = 200000
            },
            new()
            {
                Id = Models.Claude35Haiku,
                Name = "Claude 3.5 Haiku",
                Description = "200K context - $0,8/M input tokens - $4/M output tokens.",
                ContextLength = 200000
            },

            // Claude 3
            new()
            {
                Id = Models.Claude3Opus,
                Name = "Claude 3 Opus",
                Description = "200K context - $15/M input tokens - $75/M output tokens.",
                ContextLength = 200000
            }
        };
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        var model = modelId;
        const string apiUrl = "https://api.anthropic.com/v1/messages";

        HttpClient.DefaultRequestHeaders.Clear();
        HttpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        HttpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

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

        var response = await HttpClient.PostAsync(apiUrl, content);

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

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

public class ClaudeModelInfo : ModelInfo
{
    // No need for additional properties as ModelInfo has everything we need
}