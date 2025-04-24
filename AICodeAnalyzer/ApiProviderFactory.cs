using System;
using System.Collections.Generic;
using AICodeAnalyzer.AIProvider;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer;

public class ApiProviderFactory
{
    private readonly Dictionary<string, IAProvider> _providers = new();

    public IEnumerable<IAProvider> AllProviders => new List<IAProvider>(_providers.Values).AsReadOnly();

    public ApiProviderFactory()
    {
        // Register all available providers
        RegisterProviderName(new Anthropic());
        RegisterProviderName(new DeepSeek());
        RegisterProviderName(new Google());
        RegisterProviderName(new OpenAi());
        RegisterProviderName(new XAi());
    }

    private void RegisterProviderName(IAProvider provider)
    {
        _providers[provider.Name] = provider;
    }

    public IAProvider GetProviderName(string name)
    {
        if (_providers.TryGetValue(name, out var provider))
        {
            return provider;
        }

        throw new ArgumentException($"Provider '{name}' is not registered.", nameof(name));
    }
}