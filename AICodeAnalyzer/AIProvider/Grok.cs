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

public class Grok : IAiApiProvider, IDisposable
{
    private readonly HttpClient _httpClient = new();

    public string Name => "Grok API";
    public string DefaultModel => "grok-2-1212";

    public List<GrokModelInfo> GetAvailableModels()
    {
        return new List<GrokModelInfo>
        {
            new()
            {
                Name = "Grok 2",
                Id = "grok-2-1212",
                Description = "Context window 131,072 tokens. Input price $2,00. Output price $10,00."
            }
        };
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        // Call the overloaded method with the default model
        return await SendPromptWithModelAsync(apiKey, prompt, conversationHistory, DefaultModel);
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        var model = modelId;
        const string apiUrl = "https://api.grok.x/v1/chat/completions";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Properly format the message history for Grok API
        var messages = new List<object>();

        // First add a system message if this is the first message
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

        return doc.RootElement.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content").GetString() ?? "No response";
    }

    public void Dispose()
    {
        // Dispose the HttpClient
        _httpClient.Dispose();

        // Suppress finalization since we've already cleaned up resources
        GC.SuppressFinalize(this);
    }
}

public class GrokModelInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
}