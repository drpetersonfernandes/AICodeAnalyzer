using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;
using AICodeAnalyzer.Services;

namespace AICodeAnalyzer.AIProvider;

public class Anthropic : IAProvider, IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(300) };

    public string Name => "Anthropic";

    private static class Models
    {
        public const string Claude37Sonnet = "claude-3-7-sonnet-20250219";
        public const string Claude35Sonnet = "claude-3-5-sonnet-20240620";
        public const string Claude35Haiku = "claude-3-5-haiku-20240307";
        public const string Claude3Opus = "claude-3-opus-20240229";
    }

    public List<ClaudeModelInfo> GetAvailableModels()
    {
        return
        [
            new ClaudeModelInfo
            {
                Id = Models.Claude37Sonnet,
                Name = "Claude 3.7 Sonnet",
                Description = "200K context - $3/M input tokens - $15/M output tokens.",
                ContextLength = 200000,
                MaxOutputTokens = 64000
            },

            new ClaudeModelInfo
            {
                Id = Models.Claude35Sonnet,
                Name = "Claude 3.5 Sonnet",
                Description = "200K context - $3/M input tokens - $15/M output tokens.",
                ContextLength = 200000,
                MaxOutputTokens = 8192
            },

            new ClaudeModelInfo
            {
                Id = Models.Claude35Haiku,
                Name = "Claude 3.5 Haiku",
                Description = "200K context - $0,8/M input tokens - $4/M output tokens.",
                ContextLength = 200000,
                MaxOutputTokens = 4096
            },

            new ClaudeModelInfo
            {
                Id = Models.Claude3Opus,
                Name = "Claude 3 Opus",
                Description = "200K context - $15/M input tokens - $75/M output tokens.",
                ContextLength = 200000,
                MaxOutputTokens = 4096
            }
        ];
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        try
        {
            var model = modelId;
            const string apiUrl = "https://api.anthropic.com/v1/messages";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            // Properly format the message history for Anthropic API
            var messages = new List<object>();

            // Add each message from history with the proper format
            foreach (var msg in conversationHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }

            // Add the current prompt
            messages.Add(new { role = "user", content = prompt });

            // --- Retrieve Max Output Tokens for the selected model ---
            var selectedModelInfo = GetAvailableModels().FirstOrDefault(m => m.Id == modelId);
            // Use the model's specific max_tokens or a fallback (e.g., 4096)
            var maxTokens = selectedModelInfo?.MaxOutputTokens > 0 ? selectedModelInfo.MaxOutputTokens : 4096;
            // ---------------------------------------------------------

            var requestData = new
            {
                model,
                messages,
                max_tokens = maxTokens // Use the dynamic maxTokens value
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

            // Try-catch for JSON structure (Fix for issue 11)
            try
            {
                return doc.RootElement.GetProperty("content").EnumerateArray()
                    .First(static x => x.GetProperty("type").GetString() == "text")
                    .GetProperty("text").GetString() ?? "No response";
            }
            catch (JsonException jsonEx)
            {
                Logger.LogError(jsonEx, "Error parsing Anthropic response JSON structure");
                return "Error processing response: Unexpected JSON format. Please try again.";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"There was an error in the method SendPromptWithModelAsync with model {modelId}");
            return "There was an error with your request.";
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class ClaudeModelInfo : ModelInfo;
