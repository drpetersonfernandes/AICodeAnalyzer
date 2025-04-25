namespace AICodeAnalyzer.Models;

public class ModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ContextLength { get; set; }
    public string Category { get; set; } = string.Empty;
    public int MaxOutputTokens { get; set; }
}