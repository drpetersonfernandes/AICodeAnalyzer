using System;
using System.Collections.Generic;
using AICodeAnalyzer.AIProvider;

namespace AICodeAnalyzer;

/// <summary>
/// Factory class to create and manage API provider instances
/// </summary>
public class ApiProviderFactory
{
    private readonly Dictionary<string, IAiApiProvider> _providers = new();

    /// <summary>
    /// Gets all available API providers
    /// </summary>
    public IEnumerable<IAiApiProvider> AllProviders => new List<IAiApiProvider>(_providers.Values).AsReadOnly();

    public ApiProviderFactory()
    {
        // Register all available providers
        RegisterProvider(new Anthropic());
        RegisterProvider(new OpenAi());
        RegisterProvider(new DeepSeek());
        RegisterProvider(new XAi());
        RegisterProvider(new Google());
    }

    /// <summary>
    /// Registers a new API provider
    /// </summary>
    /// <param name="provider">The provider to register</param>
    private void RegisterProvider(IAiApiProvider provider)
    {
        _providers[provider.Name] = provider;
    }

    /// <summary>
    /// Gets a provider by name
    /// </summary>
    /// <param name="name">The name of the provider</param>
    /// <returns>The provider instance</returns>
    public IAiApiProvider GetProvider(string name)
    {
        if (_providers.TryGetValue(name, out var provider))
        {
            return provider;
        }

        throw new ArgumentException($"Provider '{name}' is not registered.", nameof(name));
    }
}