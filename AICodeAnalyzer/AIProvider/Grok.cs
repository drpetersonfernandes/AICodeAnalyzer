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

public class Grok : IAiApiProvider
{
    private readonly HttpClient _httpClient = new();
        
    public string Name => "Grok API";
    public string DefaultModel => "grok-2-1212";

    /// <summary>
    /// Gets the list of available models for this provider
    /// </summary>
    /// <returns>A list of available models</returns>
    public List<GrokModelInfo> GetAvailableModels()
    {
        return new List<GrokModelInfo>
        {
            new()
            {
                Name = "Grok 2",
                Id = "grok-2-1212",
                Description = "Grok 2 model supporting function calling and structured outputs (131K context)"
            }
        };
    }

    /// <summary>
    /// Sends a prompt to the Grok API using the default model
    /// Implements the interface method
    /// </summary>
    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        // Call the overloaded method with the default model
        return await SendPromptWithModelAsync(apiKey, prompt, conversationHistory, DefaultModel);
    }

    /// <summary>
    /// Sends a prompt to the Grok API using the specified model
    /// Extended method for model selection
    /// </summary>
    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        var model = modelId;
        var apiUrl = "https://api.grok.x/v1/chat/completions";
                
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            
        // Properly format the message history for Grok API
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

/// <summary>
/// Represents an AI model that can be selected in the UI
/// </summary>
public class GrokModelInfo
{
    /// <summary>
    /// Gets or sets the display name of the model
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Gets or sets the model identifier used in API calls
    /// </summary>
    public required string Id { get; set; }
    
    /// <summary>
    /// Gets or sets a description of the model
    /// </summary>
    public required string Description { get; set; }
}