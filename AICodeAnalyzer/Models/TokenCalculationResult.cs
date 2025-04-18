using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AICodeAnalyzer.Models;

public class TokenCalculationResult
{
    public int TotalTokens { get; set; }
    public int PromptTemplateTokens { get; set; }
    public int FileTokens { get; set; }
    public int SectionHeadersTokens { get; set; }
    public int BufferTokens { get; set; }

    public int PromptTokens
    {
        get => PromptTemplateTokens;
        set => PromptTemplateTokens = value;
    }

    public int OverheadTokens
    {
        get => SectionHeadersTokens + BufferTokens;
        set => SectionHeadersTokens = value; // Simple assignment for compatibility
    }

    public Dictionary<string, int> TokensByFile { get; } = new();
    public Dictionary<string, int> TokensByExtension { get; set; } = new();
    public Dictionary<string, string> ModelCompatibility { get; set; } = new();

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

        if (ModelCompatibility.Count <= 0) return sb.ToString();

        sb.AppendLine("Model Compatibility:");
        foreach (var model in ModelCompatibility)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- {model.Key}: {model.Value}");
        }

        return sb.ToString();
    }
}