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
    public string DefaultModel => "deepseek-coder";

    public async Task<string> SendPromptAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        var model = DefaultModel;
        var apiUrl = "https://api.deepseek.com/v1/chat/completions";
                
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            
        // Properly format the message history for DeepSeek API
        var messages = new List<object>();
            
        // First add system message if this is the first message
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
}