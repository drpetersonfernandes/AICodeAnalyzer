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
        .OrderBy(static p => p.Name)
        .Select(static p => p.Name);

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
                case "Anthropic":
                    var claudeProvider = (Anthropic)_apiProviderFactory.GetProviderName(providerName);
                    return claudeProvider.GetAvailableModels().Cast<ModelInfo>().ToList();

                case "DeepSeek":
                    var deepSeekProvider = (DeepSeek)_apiProviderFactory.GetProviderName(providerName);
                    return deepSeekProvider.GetAvailableModels().Cast<ModelInfo>().ToList();

                case "Google":
                    var geminiProvider = (Google)_apiProviderFactory.GetProviderName(providerName);
                    return geminiProvider.GetAvailableModels().Cast<ModelInfo>().ToList();

                case "OpenAI":
                    var openAiProvider = (OpenAi)_apiProviderFactory.GetProviderName(providerName);
                    return openAiProvider.GetAvailableModels().Cast<ModelInfo>().ToList();

                case "xAI":
                    var grokProvider = (XAi)_apiProviderFactory.GetProviderName(providerName);
                    return grokProvider.GetAvailableModels().Cast<ModelInfo>().ToList();

                default:
                    return []; // Return an empty list for providers without model selection
            }
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error getting models for provider {providerName}: {ex.Message}");
            return []; // Return an empty list for providers without model selection
        }
    }

    public static bool SupportsModelSelection(string providerName)
    {
        return providerName is "Anthropic" or "DeepSeek" or "Google" or "OpenAI" or "xAI";
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

            _loggingService.LogOperation($"Preparing request to {providerName}");

            var provider = _apiProviderFactory.GetProviderName(providerName);

            // Start timer for API request
            _loggingService.StartOperationTimer($"ApiRequest-{providerName}");
            _loggingService.LogOperation($"Sending prompt to {providerName} ({prompt.Length} characters)");

            string response;

            switch (provider)
            {
                case DeepSeek deepSeekProvider when modelId != null:
                    response = await deepSeekProvider.SendPromptWithModelAsync(key, prompt, conversationHistory, modelId);
                    break;
                case Anthropic claudeProvider when modelId != null:
                    response = await claudeProvider.SendPromptWithModelAsync(key, prompt, conversationHistory, modelId);
                    break;
                case XAi grokProvider when modelId != null:
                    response = await grokProvider.SendPromptWithModelAsync(key, prompt, conversationHistory, modelId);
                    break;
                case Google geminiProvider when modelId != null:
                    response = await geminiProvider.SendPromptWithModelAsync(key, prompt, conversationHistory, modelId);
                    break;
                case OpenAi openAiProvider when modelId != null:
                    response = await openAiProvider.SendPromptWithModelAsync(key, prompt, conversationHistory, modelId);
                    break;
                default:
                    response = "Please select the AI Model";
                    break;
            }

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