using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.AIProvider;

public class Gemini : IAiApiProvider, IDisposable
{
    private static readonly HttpClient HttpClient = new();

    public string Name => "Gemini API";
    public string DefaultModel => "gemini-2.0-flash";

    private static class Models
    {
        public const string Gemini25ProExp = "gemini-2.5-pro-exp-03-25";
        public const string Gemini20Flash = "gemini-2.0-flash";
        public const string Gemini20FlashLite = "gemini-2.0-flash-lite";
        public const string Gemini15Flash = "gemini-1.5-flash";
        public const string Gemini15Pro = "gemini-1.5-pro";
    }

    public List<GeminiModelInfo> GetAvailableModels()
    {
        return new List<GeminiModelInfo>
        {
            new()
            {
                Id = Models.Gemini25ProExp,
                Name = "Gemini 2.5 Pro Experimental",
                Description = "1M context - $TBA/M input tokens - $TBA/M output tokens.",
                ContextLength = 1000000,
                ApiVersion = "v1beta"
            },
            new()
            {
                Id = Models.Gemini20Flash,
                Name = "Gemini 2.0 Flash",
                Description = "1M context - $0,1/M input tokens - $0,4/M output tokens.",
                ContextLength = 1000000,
                ApiVersion = "v1beta"
            },
            new()
            {
                Id = Models.Gemini20FlashLite,
                Name = "Gemini 2.0 Flash-Lite",
                Description = "1M context - $0,075/M input tokens - $0,3/M output tokens.",
                ContextLength = 1000000,
                ApiVersion = "v1beta"
            },
            new()
            {
                Id = Models.Gemini15Flash,
                Name = "Gemini 1.5 Flash",
                Description = "1M context - $0,075/M input tokens - $0,3/M output tokens.",
                ContextLength = 1000000,
                ApiVersion = "v1"
            },
            new()
            {
                Id = Models.Gemini15Pro,
                Name = "Gemini 1.5 Pro",
                Description = "2M context - $2,5/M input tokens - $10/M output tokens.",
                ContextLength = 2000000,
                ApiVersion = "v1"
            }
        };
    }

    private string GetApiVersionForModel(string modelId)
    {
        // Find model in the available models list
        var models = GetAvailableModels();
        var model = models.Find(m => m.Id == modelId);

        // Return the API version or default to v1
        return model?.ApiVersion ?? "v1";
    }

    public Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory)
    {
        // Call the overloaded method with the default model
        return SendPromptWithModelAsync(apiKey, prompt, conversationHistory, DefaultModel);
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        try
        {
            var model = modelId;

            // Get the appropriate API version for this model
            var apiVersion = GetApiVersionForModel(model);

            // Build the API URL with the correct version
            var apiUrl = $"https://generativelanguage.googleapis.com/{apiVersion}/models/{model}:generateContent?key={apiKey}";

            HttpClient.DefaultRequestHeaders.Clear();

            // Build message content based on conversation history
            var contents = new List<object>();

            // Add previous messages from history if any
            if (conversationHistory.Count > 0)
            {
                foreach (var msg in conversationHistory)
                {
                    // Map "assistant" role to "model" for Gemini API
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

            // Get the appropriate output token limit based on model
            var maxOutputTokens = GetMaxTokensForModel(model);

            var requestData = new
            {
                contents = contents.ToArray(),
                generationConfig = new
                {
                    maxOutputTokens,
                    temperature = 0.7
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestData),
                Encoding.UTF8,
                "application/json");

            var response = await HttpClient.PostAsync(apiUrl, content);

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

                // If the model was not found or the other error, try falling back to the default model
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound && model != DefaultModel)
                {
                    Console.WriteLine($"Model {model} not found, falling back to {DefaultModel}");
                    return await SendPromptWithModelAsync(apiKey, prompt, conversationHistory, DefaultModel);
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
            // If we get a model not found error, try the default model
            if ((ex.Message.Contains("NOT_FOUND") || ex.Message.Contains("not found"))
                && modelId != DefaultModel)
            {
                Console.WriteLine($"Error with model {modelId}, falling back to {DefaultModel}: {ex.Message}");
                return await SendPromptWithModelAsync(apiKey, prompt, conversationHistory, DefaultModel);
            }

            throw;
        }
    }

    private static int GetMaxTokensForModel(string model)
    {
        return model switch
        {
            Models.Gemini25ProExp => 8192,
            Models.Gemini20Flash => 8192,
            Models.Gemini20FlashLite => 8192,
            Models.Gemini15Flash => 8192,
            Models.Gemini15Pro => 8192,
            _ => 4096 // Default for gemini-pro and others
        };
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}

public class GeminiModelInfo : ModelInfo
{
    // Keep the ApiVersion property which is specific to Gemini
    public string ApiVersion { get; set; } = "v1";
}