using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;
using AICodeAnalyzer.Services;

namespace AICodeAnalyzer.AIProvider;

public class DeepSeek : IAProvider, IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(300) };

    public string Name => "DeepSeek";

    private static class Models
    {
        public const string DeepSeekChat = "deepseek-chat"; // DeepSeek-V3
        public const string DeepSeekReasoner = "deepseek-reasoner"; // DeepSeek-R1
    }

    public List<DeepSeekModelInfo> GetAvailableModels()
    {
        return
        [
            new DeepSeekModelInfo
            {
                Id = Models.DeepSeekChat,
                Name = "DeepSeek Chat (V3)",
                Description = "64K context - $0,27/M input tokens - $1,1/M output tokens.",
                ContextLength = 64000
            },

            new DeepSeekModelInfo
            {
                Id = Models.DeepSeekReasoner,
                Name = "DeepSeek Reasoner (R1)",
                Description = "64K context - $0,55/M input tokens - $2,19/M output tokens.",
                ContextLength = 64000
            }
        ];
    }

    public async Task<string> SendPromptWithModelAsync(string apiKey, string prompt, List<ChatMessage> conversationHistory, string modelId)
    {
        var model = modelId;
        const string apiUrl = "https://api.deepseek.com/v1/chat/completions";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        // Properly format the message history for DeepSeek API
        var messages = new List<object>();

        // Handle the DeepSeek Reasoner model which requires strict user/assistant alternation
        if (model == Models.DeepSeekReasoner)
        {
            // For DeepSeek Reasoner, ensure strict alternation between user and assistant
            var systemPrompt = GetSystemPromptForModel(model);

            // Always add a system message first
            messages.Add(new { role = "system", content = systemPrompt });

            if (conversationHistory.Count == 0)
            {
                // If this is the first message, add the user prompt
                messages.Add(new { role = "user", content = prompt });
            }
            else
            {
                // For existing conversations, ensure user/assistant alternation
                var expectedRole = "user";

                foreach (var msg in conversationHistory)
                {
                    if (msg.Role != expectedRole) continue;

                    messages.Add(new { role = msg.Role, content = msg.Content });
                    expectedRole = expectedRole == "user" ? "assistant" : "user";
                }

                // Add the current prompt only if the last message was from the assistant
                if (expectedRole == "user")
                {
                    messages.Add(new { role = "user", content = prompt });
                }
            }
        }
        else
        {
            // For standard DeepSeek Chat, use the original behavior
            if (conversationHistory.Count == 0)
            {
                var systemPrompt = GetSystemPromptForModel(model);
                messages.Add(new { role = "system", content = systemPrompt });
            }
            else
            {
                foreach (var msg in conversationHistory)
                {
                    messages.Add(new { role = msg.Role, content = msg.Content });
                }
            }

            messages.Add(new { role = "user", content = prompt });
        }

        var requestData = new
        {
            model,
            messages
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestData),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.PostAsync(apiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                throw new Exception($"API error ({response.StatusCode}): {errorText}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);

            // Robust JSON handling: Check for optional properties
            if (doc.RootElement.TryGetProperty("choices", out var choicesElement) &&
                choicesElement.ValueKind == JsonValueKind.Array && // Fixed: Check for array kind
                choicesElement.GetArrayLength() > 0 && // Now using GetArrayLength() after confirming it's an array
                choicesElement[0].TryGetProperty("message", out var messageElement) &&
                messageElement.TryGetProperty("content", out var contentElement))
            {
                return contentElement.GetString() ?? "No response";
            }

            return "No response found in the API output.";
        }
        catch (TaskCanceledException ex)
        {
            Logger.LogError(ex, $"Request timed out with model {modelId}");
            return "There was an error with your request.";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"An error occurred with model {modelId}");
            return "There was an error with your request.";
        }
    }

    private static string GetSystemPromptForModel(string model)
    {
        return model switch
        {
            _ => "You are a helpful assistant specializing in code review and analysis."
        };
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class DeepSeekModelInfo : ModelInfo
{
    // No additional properties needed
}
