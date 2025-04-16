// File: Utilities/MarkdownHelper.cs

using System.Text;
using Markdig.Syntax;

namespace AICodeAnalyzer.Utilities;

public static class MarkdownHelper
{
    /// <summary>
    /// Extracts the raw text content from a CodeBlock or FencedCodeBlock.
    /// </summary>
    public static string GetCode(LeafBlock codeBlock)
    {
        if (codeBlock == null) return string.Empty;

        var builder = new StringBuilder();
        var lines = codeBlock.Lines.Lines; // Get the StringLineGroup
        if (lines == null) return string.Empty;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Slice;
            if (line.Text == null) continue; // Should not happen but safety check

            // Append the line text, preserving original line endings if possible
            // Although StringLineGroup usually normalizes them
            builder.Append(line.Text, line.Start, line.Length);
            if (i < lines.Length - 1) // Add newline except for the last line
            {
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }
}