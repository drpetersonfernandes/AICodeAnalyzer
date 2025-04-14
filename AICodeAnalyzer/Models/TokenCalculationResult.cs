using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AICodeAnalyzer.Models;

/// <summary>
/// Contains detailed information about token calculations
/// </summary>
public class TokenCalculationResult
{
    /// <summary>
    /// Total number of tokens including all components
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Tokens in the initial prompt or system message
    /// </summary>
    public int PromptTemplateTokens { get; set; }

    /// <summary>
    /// Total tokens in file contents
    /// </summary>
    public int FileTokens { get; set; }

    /// <summary>
    /// Tokens from section headers and formatting
    /// </summary>
    public int SectionHeadersTokens { get; set; }

    /// <summary>
    /// Buffer tokens added for safety
    /// </summary>
    public int BufferTokens { get; set; }

    /// <summary>
    /// Alias for backward compatibility
    /// </summary>
    public int PromptTokens
    {
        get => PromptTemplateTokens;
        set => PromptTemplateTokens = value;
    }

    /// <summary>
    /// Alias for backward compatibility
    /// </summary>
    public int OverheadTokens
    {
        get => SectionHeadersTokens + BufferTokens;
        set => SectionHeadersTokens = value; // Simple assignment for compatibility
    }

    /// <summary>
    /// Detailed breakdown of tokens by file
    /// </summary>
    public Dictionary<string, int> TokensByFile { get; } = new();

    /// <summary>
    /// Detailed breakdown of tokens by file extension
    /// </summary>
    public Dictionary<string, int> TokensByExtension { get; set; } = new();

    /// <summary>
    /// Compatibility information for different models
    /// </summary>
    public Dictionary<string, string> ModelCompatibility { get; set; } = new();

    /// <summary>
    /// Gets a human-readable breakdown of the token allocation
    /// </summary>
    /// <returns>A string describing the token breakdown</returns>
    public string GetBreakdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total Token Estimate: {TotalTokens:N0}");
        sb.AppendLine();

        sb.AppendLine("Token Breakdown:");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Prompt Template: {PromptTemplateTokens:N0} tokens");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Source Files: {FileTokens:N0} tokens");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Section Headers: {SectionHeadersTokens:N0} tokens");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Buffer/Overhead: {BufferTokens:N0} tokens");
        sb.AppendLine();

        if (TokensByExtension.Count > 0)
        {
            sb.AppendLine("File Extension Breakdown:");
            foreach (var ext in TokensByExtension.OrderByDescending(e => e.Value))
            {
                var percentage = (double)ext.Value / TotalTokens * 100;
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {ext.Key}: {ext.Value:N0} tokens ({percentage:F1}%)");
            }

            sb.AppendLine();
        }

        if (ModelCompatibility.Count > 0)
        {
            sb.AppendLine("Model Compatibility:");
            foreach (var model in ModelCompatibility)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"- {model.Key}: {model.Value}");
            }
        }

        return sb.ToString();
    }
}