namespace AICodeAnalyzer.Models;

/// <summary>
/// Represents an API key item for display in the UI
/// </summary>
public class ApiKeyItem
{
    /// <summary>
    /// The masked key value for display
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The actual API key value (not displayed)
    /// </summary>
    public string ActualKey { get; set; } = string.Empty;
}