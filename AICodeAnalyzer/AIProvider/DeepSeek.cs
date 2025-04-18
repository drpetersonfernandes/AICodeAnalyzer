using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.AIProvider;

public class DeepSeek : IAiApiProvider, IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(300) };

    public string Name => "DeepSeek API";

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
            // For DeepSeek Reasoner, we need to ensure strict alternation between user and assistant
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
                // For existing conversations, we need to ensure user/assistant alternation
                // Start with determining what the first role should be
                var expectedRole = "user";

                // Process history and force proper alternation
                foreach (var msg in conversationHistory)
                {
                    // Skip messages that don't fit the expected alternating pattern
                    if (msg.Role != expectedRole)
                    {
                        continue;
                    }

                    // Add the message and flip the expected role
                    messages.Add(new { role = msg.Role, content = msg.Content });
                    expectedRole = expectedRole == "user" ? "assistant" : "user";
                }

                // Add the current prompt only if the last message was from the assistant
                if (expectedRole == "user")
                {
                    messages.Add(new { role = "user", content = prompt });
                }
                else
                {
                    // If the last message was from a user, we need to combine the prompts
                    // to maintain the alternating pattern
                    var lastMessage = messages[^1];
                    var lastContent = ((dynamic)lastMessage).content;

                    // Remove the last message
                    messages.RemoveAt(messages.Count - 1);

                    // Add a combined message
                    messages.Add(new
                    {
                        role = "user",
                        content = $"{lastContent}\n\nFollow-up question: {prompt}"
                    });
                }
            }
        }
        else
        {
            // For standard DeepSeek Chat, use the original behavior
            // First add system message if this is the first message
            if (conversationHistory.Count == 0)
            {
                var systemPrompt = GetSystemPromptForModel(model);
                messages.Add(new { role = "system", content = systemPrompt });
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

            return doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content").GetString() ?? "No response";
        }
        catch (TaskCanceledException ex)
        {
            // Log the exception or handle it appropriately.
            Console.WriteLine($"Request timed out: {ex.Message}");
            throw; // Re-throw the exception to be handled upstream.
        }
        catch (Exception ex)
        {
            // Handle other exceptions as needed.
            Console.WriteLine($"An error occurred: {ex.Message}");
            throw;
        }
    }


    private static string GetSystemPromptForModel(string model)
    {
        return model switch
        {
            Models.DeepSeekReasoner => "You are a helpful assistant specializing in code review and analysis.",
            _ => "You are a helpful assistant specializing in code review and analysis." // Default for deepseek-chat and others
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