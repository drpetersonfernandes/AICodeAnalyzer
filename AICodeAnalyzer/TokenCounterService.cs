using System.Collections.Generic;
using System.Linq;
using AICodeAnalyzer.Models;
using SharpToken;

namespace AICodeAnalyzer;

/// <summary>
/// Service class to accurately count tokens using the SharpToken library
/// </summary>
public class TokenCounterService
{
    // Dictionary of encoding instances, keyed by model name
    private readonly Dictionary<string, GptEncoding> _encodings = new();

    // Default encoding (cl100k_base for Claude/GPT4)
    private const string DefaultEncodingName = "cl100k_base";

    /// <summary>
    /// Initializes a new instance of the TokenCounterService class
    /// </summary>
    public TokenCounterService()
    {
        // Initialize default encoder
        _encodings[DefaultEncodingName] = GptEncoding.GetEncoding(DefaultEncodingName);

        // Optional: Initialize other encodings if needed
        // _encodings["p50k_base"] = GptEncoding.GetEncoding("p50k_base"); // For older models
    }

    /// <summary>
    /// Counts tokens in a source file, including tokens for formatting
    /// </summary>
    /// <param name="file">The source file</param>
    /// <param name="encodingName">The encoding name (default is cl100k_base)</param>
    /// <returns>The total number of tokens for the file</returns>
    private int CountFileTokens(SourceFile file, string encodingName = DefaultEncodingName)
    {
        if (file == null || string.IsNullOrEmpty(file.Content))
            return 0;

        // Get the encoding
        if (!_encodings.TryGetValue(encodingName, out var encoding))
        {
            encoding = GptEncoding.GetEncoding(encodingName);
            _encodings[encodingName] = encoding;
        }

        // Calculate tokens for the file content
        var contentTokens = encoding.Encode(file.Content).Count;

        // Calculate tokens for the file metadata and formatting
        // Format: "File: {file.RelativePath}\n```{language}\n{content}\n```\n"
        var fileHeaderLanguage = GetLanguageForExtension(file.Extension);
        var fileHeader = $"File: {file.RelativePath}\n```{fileHeaderLanguage}\n";
        const string fileFooter = "\n```\n";

        var headerTokens = encoding.Encode(fileHeader).Count;
        var footerTokens = encoding.Encode(fileFooter).Count;

        return contentTokens + headerTokens + footerTokens;
    }

    /// <summary>
    /// Counts tokens for a list of files and options
    /// </summary>
    /// <param name="files">The source files</param>
    /// <param name="includePrompt">The prompt to include</param>
    /// <param name="encodingName">The encoding name (default is cl100k_base)</param>
    /// <returns>Token calculation result with details</returns>
    public TokenCalculationResult CalculateTotalTokens(
        IEnumerable<SourceFile> files,
        string includePrompt = "",
        string encodingName = DefaultEncodingName)
    {
        var result = new TokenCalculationResult();
        var filesList = files.ToList();

        // Get encoding
        if (!_encodings.TryGetValue(encodingName, out var encoding))
        {
            encoding = GptEncoding.GetEncoding(encodingName);
            _encodings[encodingName] = encoding;
        }

        // Count prompt tokens if provided
        if (!string.IsNullOrEmpty(includePrompt))
        {
            result.PromptTokens = encoding.Encode(includePrompt).Count;
            result.TotalTokens += result.PromptTokens;
        }

        // Section headers tokens (extension groups)
        if (filesList.Count > 0)
        {
            var extensionGroups = filesList.GroupBy(f => f.Extension).ToList();
            foreach (var group in extensionGroups)
            {
                var sectionHeader = $"--- {group.Key.ToUpperInvariant()} FILES ---\n\n";
                var sectionTokens = encoding.Encode(sectionHeader).Count;
                result.SectionHeadersTokens += sectionTokens;
                result.TotalTokens += sectionTokens;
            }
        }

        // Individual file tokens
        foreach (var file in filesList)
        {
            var fileTokens = CountFileTokens(file, encodingName);
            result.FileTokens += fileTokens;
            result.TotalTokens += fileTokens;

            // Store detailed file token count for analysis
            result.TokensByFile[file.RelativePath] = fileTokens;
        }

        // Add a small buffer for other formatting not captured (5%)
        // This is much lower than the previous 15% because our token counting is now more accurate
        var bufferTokens = (int)(result.TotalTokens * 0.05);
        result.BufferTokens = bufferTokens;
        result.TotalTokens += bufferTokens;

        return result;
    }

    /// <summary>
    /// Gets the language identifier for a file extension to be used in markdown code blocks
    /// </summary>
    private static string GetLanguageForExtension(string ext)
    {
        return ext switch
        {
            ".cs" => "csharp",
            ".py" => "python",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".java" => "java",
            ".html" => "html",
            ".css" => "css",
            ".cpp" or ".h" or ".c" => "cpp",
            ".go" => "go",
            ".rb" => "ruby",
            ".php" => "php",
            ".swift" => "swift",
            ".kt" => "kotlin",
            ".rs" => "rust",
            ".dart" => "dart",
            ".xaml" => "xml",
            ".xml" => "xml",
            ".json" => "json",
            ".yaml" or ".yml" => "yaml",
            ".md" => "markdown",
            ".txt" => "text",
            ".sql" => "sql",
            ".sh" or ".bash" => "bash",
            ".ps1" => "powershell",
            ".r" => "r",
            ".vb" => "vb",
            ".fs" => "fsharp",
            ".lua" => "lua",
            ".pl" => "perl",
            ".groovy" => "groovy",
            ".dockerfile" => "dockerfile",
            ".ini" => "ini",
            ".toml" => "toml",
            ".asp" or ".aspx" => "asp",
            ".cshtml" => "cshtml",
            ".axaml" => "axml",
            ".jsx" => "jsx",
            ".tsx" => "tsx",
            ".vue" => "vue",
            ".svelte" => "svelte",
            ".scss" => "scss",
            ".sass" => "sass",
            ".less" => "less",
            ".mjs" => "mjs",
            ".cjs" => "cjs",
            ".graphql" => "graphql",
            ".gql" => "gql",
            ".pug" => "pug",
            ".jade" => "jade",
            ".ejs" => "ejs",
            ".haml" => "haml",
            ".erb" => "erb",
            ".ex" => "ex",
            ".exs" => "exs",
            ".jl" => "jl",
            ".nim" => "nim",
            ".hs" => "hs",
            ".clj" => "clj",
            ".elm" => "elm",
            ".erl" => "erl",
            ".m" => "objc",
            ".mm" => "objc",
            ".asm" => "asm",
            ".s" => "asm",
            ".tf" => "tf",
            ".tfvars" => "tfvars",
            ".proto" => "proto",
            ".plist" => "plist",
            ".config" => "config",
            ".csproj" => "xml",
            ".vbproj" => "xml",
            ".fsproj" => "xml",
            ".nuspec" => "xml",
            ".wasm" => "wasm",
            _ => "text" // Default to "text" for unknown extensions
        };
    }
}