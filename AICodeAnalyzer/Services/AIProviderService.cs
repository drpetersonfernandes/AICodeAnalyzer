using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AICodeAnalyzer.AIProvider;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.Services;

public class AiProviderService(LoggingService loggingService)
{
    private readonly ApiKeyManager _keyManager = new();
    private readonly ApiProviderFactory _apiProviderFactory = new();
    private readonly LoggingService _loggingService = loggingService;

    public IEnumerable<string> ProviderNames => _apiProviderFactory.AllProviders
        .OrderBy(p => p.Name)
        .Select(p => p.Name);

    public List<string> GetKeysForProvider(string providerName)
    {
        return _keyManager.GetKeysForProvider(providerName);
    }

    public static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length < 8)
            return "*****";

        return string.Concat(key.AsSpan(0, 3), "*****", key.AsSpan(key.Length - 3));
    }

    public List<ModelInfo> GetModelsForProvider(string providerName)
    {
        try
        {
            switch (providerName)
            {
                case "DeepSeek API":
                    var deepSeekProvider = (DeepSeek)_apiProviderFactory.GetProvider(providerName);
                    return deepSeekProvider.GetAvailableModels().Cast<ModelInfo>().ToList();

                case "Claude API":
                    var claudeProvider = (Claude)_apiProviderFactory.GetProvider(providerName);
                    return claudeProvider.GetAvailableModels().Cast<ModelInfo>().ToList();

                case "Grok API":
                    var grokProvider = (Grok)_apiProviderFactory.GetProvider(providerName);
                    return grokProvider.GetAvailableModels().Cast<ModelInfo>().ToList();

                case "Gemini API":
                    var geminiProvider = (Gemini)_apiProviderFactory.GetProvider(providerName);
                    return geminiProvider.GetAvailableModels().Cast<ModelInfo>().ToList();

                case "ChatGPT API":
                    var openAiProvider = (OpenAi)_apiProviderFactory.GetProvider(providerName);
                    return openAiProvider.GetAvailableModels().Cast<ModelInfo>().ToList();

                default:
                    return new List<ModelInfo>(); // Return empty list for providers without model selection
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error getting models for provider {providerName}: {ex.Message}");
            return new List<ModelInfo>();
        }
    }

    public static bool SupportsModelSelection(string providerName)
    {
        return providerName is "DeepSeek API" or "Claude API" or "Grok API" or "Gemini API" or "ChatGPT API";
    }

    public async Task<string> SendPromptAsync(
        string providerName,
        string key,
        string prompt,
        List<ChatMessage> conversationHistory,
        string? modelId = null)
    {
        try
        {
            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("Please select an API key", "API Key Required", MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                throw new ApplicationException("No API key selected");
            }

            // Log the request
            _loggingService.LogOperation($"Preparing request to {providerName}");

            // Get the selected provider
            var provider = _apiProviderFactory.GetProvider(providerName);

            // Start timer for API request
            _loggingService.StartOperationTimer($"ApiRequest-{providerName}");
            _loggingService.LogOperation($"Sending prompt to {providerName} ({prompt.Length} characters)");

            // Send the prompt and return the response
            string response;

            // Special handling for providers with model selection
            if (provider is DeepSeek deepSeekProvider && modelId != null)
            {
                response = await deepSeekProvider.SendPromptWithModelAsync(key, prompt, conversationHistory, modelId);
            }
            else if (provider is Claude claudeProvider && modelId != null)
            {
                response = await claudeProvider.SendPromptWithModelAsync(key, prompt, conversationHistory, modelId);
            }
            else if (provider is Grok grokProvider && modelId != null)
            {
                response = await grokProvider.SendPromptWithModelAsync(key, prompt, conversationHistory, modelId);
            }
            else if (provider is Gemini geminiProvider && modelId != null)
            {
                response = await geminiProvider.SendPromptWithModelAsync(key, prompt, conversationHistory, modelId);
            }
            else if (provider is OpenAi openAiProvider && modelId != null)
            {
                response = await openAiProvider.SendPromptWithModelAsync(key, prompt, conversationHistory, modelId);
            }
            else
            {
                response = await provider.SendPromptWithModelAsync(key, prompt, conversationHistory);
            }

            // Log response received
            _loggingService.EndOperationTimer($"ApiRequest-{providerName}");
            _loggingService.LogOperation($"Received response from {providerName} ({response.Length} characters)");

            return response;
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error calling {providerName} API: {ex.Message}");
            ErrorLogger.LogError(ex, $"Error calling {providerName} API");
            throw; // Re-throw to let the caller handle it
        }
    }
}