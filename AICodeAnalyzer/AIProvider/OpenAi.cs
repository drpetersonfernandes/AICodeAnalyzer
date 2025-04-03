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

public class OpenAi : IAiApiProvider
{
    private readonly HttpClient _httpClient = new();

    public string Name => "ChatGPT API";
    public string DefaultModel => "gpt-4o";

    /// <summary>
    /// Available OpenAI model identifiers
    /// </summary>
    private static class Models
    {
        // GPT-4.5 models
        public const string Gpt45Preview = "gpt-4.5-preview";

        // GPT-4o models
        public const string Gpt4O = "gpt-4o";

        // GPT-4o Mini models
        public const string Gpt4OMini = "gpt-4o-mini";

        // O1 models
        public const string O1 = "o1";

        // O1 Pro models
        public const string O1Pro = "o1-pro";

        // O3 Mini models
        public const string O3Mini = "o3-mini";

        // O1 Mini models
        public const string O1Mini = "o1-mini";
    }

    /// <summary>
    /// Gets all available OpenAI models with their descriptions
    /// </summary>
    /// <returns>List of model options</returns>
    public List<OpenAiModelInfo> GetAvailableModels()
    {
        return new List<OpenAiModelInfo>
        {
            // GPT-4.5 models
            new()
            {
                Id = Models.Gpt45Preview,
                Name = "GPT-4.5 Preview",
                Description = "Most advanced GPT model with enhanced reasoning capabilities (128K context)",
                ContextLength = 128000,
                MaxTokens = 4096,
                Category = "GPT-4.5 Models"
            },

            // GPT-4o models
            new()
            {
                Id = Models.Gpt4O,
                Name = "GPT-4o",
                Description = "GPT-4o model - points to latest version (128K context)",
                ContextLength = 128000,
                MaxTokens = 4096,
                Category = "GPT-4o Models"
            },

            // GPT-4o Mini models
            new()
            {
                Id = Models.Gpt4OMini,
                Name = "GPT-4o Mini",
                Description = "Smaller, faster, more affordable version of GPT-4o (128K context)",
                ContextLength = 128000,
                MaxTokens = 4096,
                Category = "GPT-4o Mini Models"
            },

            // O1 models
            new()
            {
                Id = Models.O1,
                Name = "o1",
                Description = "Next generation model with advanced reasoning capabilities (128K context)",
                ContextLength = 128000,
                MaxTokens = 4096,
                Category = "o1 Models"
            },

            // O1 Pro models
            new()
            {
                Id = Models.O1Pro,
                Name = "o1 Pro",
                Description = "Premium version of o1 with enhanced capabilities (128K context)",
                ContextLength = 128000,
                MaxTokens = 4096,
                Category = "o1 Pro Models"
            },

            // O3 Mini models
            new()
            {
                Id = Models.O3Mini,
                Name = "o3 Mini",
                Description = "Compact and efficient o3 model (128K context)",
                ContextLength = 128000,
                MaxTokens = 4096,
                Category = "o3 Models"
            },

            // O1 Mini models
            new()
            {
                Id = Models.O1Mini,
                Name = "o1 Mini",
                Description = "Compact version of o1 (128K context)",
                ContextLength = 128000,
                MaxTokens = 4096,
                Category = "o1 Mini Models"
            }
        };
    }

    /// <summary>
    /// Sends a prompt to the OpenAI API using the default model
    /// Implements the interface method
    /// </summary>
    public Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        // Call the overloaded method with the default model
        return SendPromptWithModelAsync(apiKey, prompt, conversationHistory, DefaultModel);
    }

    /// <summary>
    /// Sends a prompt to the OpenAI API using the specified model
    /// Extended method for model selection
    /// </summary>
    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        var model = modelId;
        var apiUrl = "https://api.openai.com/v1/chat/completions";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Properly format the message history for OpenAI API
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

        // Set max tokens based on the model
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

        JsonDocument? doc;
        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();

            // Try to parse the error for a more user-friendly message
            try
            {
                doc = JsonDocument.Parse(errorText);
                if (doc.RootElement.TryGetProperty("error", out var errorElement) &&
                    errorElement.TryGetProperty("message", out var messageElement))
                {
                    errorText = messageElement.GetString() ?? errorText;
                }
            }
            catch
            {
                // If parsing fails, use the original error text
            }

            throw new Exception($"API error ({response.StatusCode}): {errorText}");
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        doc = JsonDocument.Parse(responseJson);

        return doc.RootElement.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content").GetString() ?? "No response";
    }

    /// <summary>
    /// Gets the maximum output tokens for the specified model
    /// </summary>
    private int GetMaxTokensForModel(string model)
    {
        // Most OpenAI models support up to 4096 output tokens
        // This could be adjusted for specific models if needed
        return 4096;
    }
}

/// <summary>
/// Represents information about an OpenAI model
/// </summary>
public class OpenAiModelInfo
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
    public int MaxTokens { get; set; }

    /// <summary>
    /// Category for grouping models in the UI
    /// </summary>
    public string Category { get; set; } = string.Empty;
}