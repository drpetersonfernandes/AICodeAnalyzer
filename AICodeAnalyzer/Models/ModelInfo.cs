namespace AICodeAnalyzer.Models;

/// <summary>
/// Base class for AI model information that can be used across different providers
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// The model identifier used when making API calls
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The display name of the model
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the model including pricing and capabilities
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Maximum context length in tokens
    /// </summary>
    public int ContextLength { get; set; }

    /// <summary>
    /// Category or type of model
    /// </summary>
    public string Category { get; set; } = string.Empty;
}