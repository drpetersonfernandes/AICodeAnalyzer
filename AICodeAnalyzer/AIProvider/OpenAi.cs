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

    private static class Models
    {
        public const string Gpt45Preview = "gpt-4.5-preview";
        public const string Gpt4O = "gpt-4o";
        public const string Gpt4OMini = "gpt-4o-mini";
        public const string O1 = "o1";
        public const string O1Pro = "o1-pro";
        public const string O3Mini = "o3-mini";
        public const string O1Mini = "o1-mini";
    }

    public List<OpenAiModelInfo> GetAvailableModels()
    {
        return new List<OpenAiModelInfo>
        {
            // GPT-4.5 models
            new()
            {
                Id = Models.Gpt45Preview,
                Name = "GPT-4.5 Preview",
                Description = "Context window 128,000 tokens. Input $75,00. Output $150,00.",
                ContextLength = 128000,
                Category = "GPT-4.5 Models"
            },

            // GPT-4o models
            new()
            {
                Id = Models.Gpt4O,
                Name = "GPT-4o",
                Description = "Context window 128,000 tokens. Input $2,50. Output $10,00.",
                ContextLength = 128000,
                Category = "GPT-4o Models"
            },

            // GPT-4o Mini models
            new()
            {
                Id = Models.Gpt4OMini,
                Name = "GPT-4o Mini",
                Description = "Context window 128,000 tokens. Input $0,15. Output $0,60.",
                ContextLength = 128000,
                Category = "GPT-4o Mini Models"
            },

            // O1 models
            new()
            {
                Id = Models.O1,
                Name = "o1",
                Description = "Context window 200,000 tokens. Input $15,00. Output $60,00.",
                ContextLength = 200000,
                Category = "o1 Models"
            },

            // O1 Pro models
            new()
            {
                Id = Models.O1Pro,
                Name = "o1 Pro",
                Description = "Context window 200,000 tokens. Input $150,00. Output $600,00.",
                ContextLength = 200000,
                Category = "o1 Pro Models"
            },

            // O3 Mini models
            new()
            {
                Id = Models.O3Mini,
                Name = "o3 Mini",
                Description = "Context window 200,000 tokens. Input $1,10. Output $4,40.",
                ContextLength = 200000,
                Category = "o3 Models"
            },

            // O1 Mini models
            new()
            {
                Id = Models.O1Mini,
                Name = "o1 Mini",
                Description = "Context window 131,000 tokens. Input $3,00. Output $12,00.",
                ContextLength = 131000,
                Category = "o1 Mini Models"
            }
        };
    }

    public Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        // Call the overloaded method with the default model
        return SendPromptWithModelAsync(apiKey, prompt, conversationHistory, DefaultModel);
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        var model = modelId;
        const string apiUrl = "https://api.openai.com/v1/chat/completions";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Properly format the message history for OpenAI API
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

    private static int GetMaxTokensForModel(string model)
    {
        // Most OpenAI models support up to 4096 output tokens
        // This could be adjusted for specific models if needed
        return 4096;
    }
}

public class OpenAiModelInfo
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public int ContextLength { get; set; }
    public string Category { get; set; } = string.Empty;
}