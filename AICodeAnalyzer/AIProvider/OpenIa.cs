using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.AIProvider;

public class OpenIa : IAProvider, IDisposable
{
    private readonly HttpClient _httpClient = new();

    public string Name => "OpenAI API";

    private static class Models
    {
        public const string Gpt41 = "gpt-4.1";
        public const string Gpt41Mini = "gpt-4.1-mini";
        public const string Gpt41Nano = "gpt-4.1-nano";
        public const string Gpt4O = "gpt-4o";
        public const string Gpt4OMini = "gpt-4o-mini";
        public const string O1 = "o1";
        public const string O1Mini = "o1-mini";
        public const string O1Pro = "o1-pro";
        public const string O3Mini = "o3-mini";
    }

    public List<OpenAiModelInfo> GetAvailableModels()
    {
        return
        [
            new OpenAiModelInfo
            {
                Id = Models.Gpt41,
                Name = "GPT-4.1",
                Description = "1M context - $2/M input tokens - $8/M output tokens.",
                ContextLength = 1000000
            },

            new OpenAiModelInfo
            {
                Id = Models.Gpt41Mini,
                Name = "GPT-4.1 Mini",
                Description = "1M context - $0,4/M input tokens - $1,6/M output tokens.",
                ContextLength = 1000000
            },

            new OpenAiModelInfo
            {
                Id = Models.Gpt41Nano,
                Name = "GPT-4.1 Nano",
                Description = "1M context - $0,1/M input tokens - $0,4/M output tokens.",
                ContextLength = 1000000
            },

            new OpenAiModelInfo
            {
                Id = Models.Gpt4O,
                Name = "GPT-4o",
                Description = "128K context - $2,5/M input tokens - $10/M output tokens.",
                ContextLength = 128000
            },

            new OpenAiModelInfo
            {
                Id = Models.Gpt4OMini,
                Name = "GPT-4o Mini",
                Description = "128K context - $0,15/M input tokens - $0,6/M output tokens.",
                ContextLength = 128000
            },

            new OpenAiModelInfo
            {
                Id = Models.O1,
                Name = "o1",
                Description = "200K context - $15/M input tokens - $60/M output tokens.",
                ContextLength = 200000
            },

            new OpenAiModelInfo
            {
                Id = Models.O1Mini,
                Name = "o1 Mini",
                Description = "131K context - $3/M input tokens - $12/M output tokens.",
                ContextLength = 131000
            },

            new OpenAiModelInfo
            {
                Id = Models.O1Pro,
                Name = "o1 Pro",
                Description = "200K context - $150/M input tokens - $600/M output tokens.",
                ContextLength = 200000
            },

            new OpenAiModelInfo
            {
                Id = Models.O3Mini,
                Name = "o3 Mini",
                Description = "200K context - $1,1/M input tokens - $4,4/M output tokens.",
                ContextLength = 200000
            }
        ];
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        try
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
        catch (Exception ex)
        {
            ErrorLogger.LogError(ex, $"There was an error with model {modelId}");
            return "There was an error with your request.";
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class OpenAiModelInfo : ModelInfo
{
    // No additional properties needed
}