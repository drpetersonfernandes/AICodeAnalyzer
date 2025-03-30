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

    public async Task<string> SendPromptAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        var model = DefaultModel;
        var apiUrl = "https://api.anthropic.com/v1/messages";
            
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            
        // Properly format the message history for Claude API
        var messages = new List<object>();
            
        // Claude API doesn't use system messages in the same way - they go in the messages array
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