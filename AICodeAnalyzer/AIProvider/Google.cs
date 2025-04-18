using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.AIProvider;

public class Google : IAProvider, IDisposable
{
    private readonly HttpClient _httpClient = new();

    public string Name => "Google API";

    private static class Models
    {
        public const string Gemini25FlashPreview = "gemini-2.5-flash-preview-04-17";
        public const string Gemini25ProPreview = "gemini-2.5-pro-preview-03-25";
        public const string Gemini20Flash = "gemini-2.0-flash";
        public const string Gemini20FlashLite = "gemini-2.0-flash-lite";
    }

    public List<GeminiModelInfo> GetAvailableModels()
    {
        return
        [
            new GeminiModelInfo
            {
                Id = Models.Gemini25FlashPreview,
                Name = "Gemini 2.5 Flash Preview",
                Description = "1M context - $0.15/M input tokens - $3.5/M output tokens.",
                ContextLength = 1000000,
                ApiVersion = "v1beta"
            },

            new GeminiModelInfo
            {
                Id = Models.Gemini25ProPreview,
                Name = "Gemini 2.5 Pro Preview",
                Description = "1M context - $2.5/M input tokens - $15.0/M output tokens.",
                ContextLength = 1000000,
                ApiVersion = "v1beta"
            },

            new GeminiModelInfo
            {
                Id = Models.Gemini20Flash,
                Name = "Gemini 2.0 Flash",
                Description = "1M context - $0.1/M input tokens - $0.4/M output tokens.",
                ContextLength = 1000000,
                ApiVersion = "v1beta"
            },

            new GeminiModelInfo
            {
                Id = Models.Gemini20FlashLite,
                Name = "Gemini 2.0 Flash-Lite",
                Description = "1M context - $0.075/M input tokens - $0.3/M output tokens.",
                ContextLength = 1000000,
                ApiVersion = "v1beta"
            }
        ];
    }

    private string GetApiVersionForModel(string modelId)
    {
        var models = GetAvailableModels();
        var model = models.Find(m => m.Id == modelId);

        // Return the API version or default to v1beta
        return model?.ApiVersion ?? "v1beta";
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        try
        {
            // Get the appropriate API version for this model
            var apiVersion = GetApiVersionForModel(modelId);

            // Build the API URL with the correct version
            var apiUrl = $"https://generativelanguage.googleapis.com/{apiVersion}/models/{modelId}:generateContent?key={apiKey}";

            _httpClient.DefaultRequestHeaders.Clear();

            // Build message content based on conversation history
            var contents = new List<object>();

            // Add previous messages from history if any
            if (conversationHistory.Count > 0)
            {
                foreach (var msg in conversationHistory)
                {
                    // Map "assistant" role to "model" for Google API
                    var role = msg.Role == "assistant" ? "model" : msg.Role;

                    contents.Add(new
                    {
                        role,
                        parts = new[] { new { text = msg.Content } }
                    });
                }
            }

            // Add the current prompt
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = prompt } }
            });

            var requestData = new
            {
                contents = contents.ToArray(),
                generationConfig = new
                {
                    temperature = 0.3
                }
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

                // Try to extract a user-friendly error message
                string userFriendlyError;
                try
                {
                    doc = JsonDocument.Parse(errorText);
                    userFriendlyError = doc.RootElement
                        .GetProperty("error")
                        .GetProperty("message")
                        .GetString() ?? errorText;
                }
                catch
                {
                    userFriendlyError = errorText;
                }

                throw new Exception($"API error ({response.StatusCode}): {userFriendlyError}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            doc = JsonDocument.Parse(responseJson);

            return doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString() ?? "No response";
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

public class GeminiModelInfo : ModelInfo
{
    public string ApiVersion { get; set; } = "v1beta";
}