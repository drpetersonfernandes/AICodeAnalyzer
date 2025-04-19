using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.AIProvider;

public class XAi : IAProvider, IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(300) };

    public string Name => "xAI";

    public List<GrokModelInfo> GetAvailableModels()
    {
        return
        [
            new GrokModelInfo
            {
                Name = "Grok 3",
                Id = "grok-3-beta",
                Description = "131K context - $3/M input tokens - $15/M output tokens."
            },

            new GrokModelInfo
            {
                Name = "Grok 3 Mini",
                Id = "grok-3-mini-beta",
                Description = "131K context - $0,3/M input tokens - $0,5/M output tokens."
            },

            new GrokModelInfo
            {
                Name = "Grok 2",
                Id = "grok-2-1212",
                Description = "131K context - $2/M input tokens - $10/M output tokens."
            }
        ];
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        try
        {
            var model = modelId;
            const string apiUrl = "https://api.x.ai/v1/chat/completions";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Properly format the message history for xAI API
            var messages = new List<object>();

            // First, add a system message if this is the first message
            if (conversationHistory.Count == 0)
            {
                messages.Add(new { role = "system", content = "You are a helpful assistant specializing in code review and analysis." });
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

            var requestData = new
            {
                model,
                messages
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
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, $"Error with model {modelId}.");
            return "There was an error with your request.";
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class GrokModelInfo : ModelInfo
{
    // No additional properties needed
}