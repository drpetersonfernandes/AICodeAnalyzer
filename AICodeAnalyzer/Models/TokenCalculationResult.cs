using System.Collections.Generic;

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
    public int PromptTokens { get; set; }
    
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
    /// Detailed breakdown of tokens by file
    /// </summary>
    public Dictionary<string, int> TokensByFile { get; } = new Dictionary<string, int>();
    
    /// <summary>
    /// Gets a human-readable breakdown of the token allocation
    /// </summary>
    /// <returns>A string describing the token breakdown</returns>
    public string GetBreakdown()
    {
        return $"Total: {TotalTokens:N0} tokens\n" +
               $"- Prompt: {PromptTokens:N0} tokens\n" +
               $"- Files: {FileTokens:N0} tokens\n" +
               $"- Headers: {SectionHeadersTokens:N0} tokens\n" +
               $"- Buffer: {BufferTokens:N0} tokens";
    }
}