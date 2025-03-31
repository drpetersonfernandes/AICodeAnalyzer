using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Interfaces;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.AIProvider;

public class Gemini : IAiApiProvider
{
    private readonly HttpClient _httpClient = new();
        
    public string Name => "Gemini API";
    public string DefaultModel => "gemini-pro";

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        var model = DefaultModel;
        var apiUrl = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={apiKey}";
                
        _httpClient.DefaultRequestHeaders.Clear();
            
        // Gemini has a different API structure, so we need to handle it differently
        // It doesn't support full conversation history in the same way
        // For simplicity, we'll just include the latest prompt
        // In a production app, you might want to implement a more complex solution that
        // combines the conversation history into the prompt
            
        var requestData = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                maxOutputTokens = 4096,
                temperature = 0.7
            }
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
                
        return doc.RootElement.GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text").GetString() ?? "No response";
    }
}