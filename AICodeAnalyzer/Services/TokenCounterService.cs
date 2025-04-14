﻿using System;
using System.Collections.Generic;
using System.Linq;
using AICodeAnalyzer.Models;
using SharpToken;

namespace AICodeAnalyzer.Services;

/// <summary>
/// Service class to calculate token usage for LLM requests
/// </summary>
public class TokenCounterService
{
    // Dictionary of encoding instances, keyed by model name
    private readonly Dictionary<string, GptEncoding> _encodings = new();

    // Default encoding (cl100k_base for Claude/GPT4)
    private const string DefaultEncodingName = "cl100k_base";

    // Default token ratios for different models (tokens per character) - used as fallback
    private readonly Dictionary<string, double> _tokenRatios = new()
    {
        { "gpt-4", 0.25 }, // GPT-4 series
        { "gpt-3.5", 0.25 }, // GPT-3.5 series
        { "claude", 0.26 }, // Claude models
        { "gemini", 0.24 }, // Gemini models
        { "deepseek", 0.25 }, // DeepSeek models
        { "default", 0.25 } // Default fallback ratio
    };

    // Token limits for common models
    private readonly Dictionary<string, int> _modelLimits = new()
    {
        { "gpt-4", 8192 }, // GPT-4
        { "gpt-4-turbo", 128000 }, // GPT-4 Turbo
        { "gpt-3.5-turbo", 16385 }, // GPT-3.5 Turbo
        { "claude-3-opus", 200000 }, // Claude 3 Opus
        { "claude-3-sonnet", 200000 }, // Claude 3 Sonnet
        { "gemini-pro", 32768 }, // Gemini Pro
        { "deepseek-coder", 32768 }, // DeepSeek Coder
        { "default", 8000 } // Conservative default
    };

    private readonly LoggingService _loggingService;

    /// <summary>
    /// Initializes a new instance of the TokenCounterService class
    /// </summary>
    public TokenCounterService(LoggingService loggingService)
    {
        _loggingService = loggingService;
        // Initialize default encoder (try-catch in case SharpToken isn't available)
        try
        {
            _encodings[DefaultEncodingName] = GptEncoding.GetEncoding(DefaultEncodingName);
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Failed to initialize default encoding {DefaultEncodingName}: {ex.Message}");
            // Fallback to estimation if SharpToken is unavailable
            // No action needed here as we'll use the fallback method
        }
    }

    /// <summary>
    /// Calculates total tokens for a list of source files and a prompt template
    /// </summary>
    /// <param name="sourceFiles">The source files to analyze</param>
    /// <param name="promptTemplate">The prompt template text</param>
    /// <param name="encodingName">Optional encoding name to use</param>
    /// <returns>Detailed token calculation result</returns>
    public TokenCalculationResult CalculateTotalTokens(
        List<SourceFile> sourceFiles,
        string promptTemplate,
        string encodingName = DefaultEncodingName)
    {
        // Try to use SharpToken for precise counting, fall back to estimation if unavailable
        var useSharpToken = _encodings.ContainsKey(encodingName);

        if (useSharpToken)
        {
            return CalculateTokensWithSharpToken(sourceFiles, promptTemplate, encodingName);
        }
        else
        {
            return EstimateTokens(sourceFiles, promptTemplate);
        }
    }

    /// <summary>
    /// Calculates tokens accurately using the SharpToken library
    /// </summary>
    private TokenCalculationResult CalculateTokensWithSharpToken(
        List<SourceFile> sourceFiles,
        string promptTemplate,
        string encodingName = DefaultEncodingName)
    {
        var result = new TokenCalculationResult();

        if (!_encodings.TryGetValue(encodingName, out var encoding))
        {
            _loggingService.LogOperation($"Encoding {encodingName} not found. Falling back to estimation.");
            return EstimateTokens(sourceFiles, promptTemplate);
        }

        // Calculate prompt template tokens
        if (!string.IsNullOrEmpty(promptTemplate))
        {
            try
            {
                result.PromptTemplateTokens = encoding.Encode(promptTemplate).Count;
                result.TotalTokens += result.PromptTemplateTokens;
            }
            catch (Exception ex)
            {
                _loggingService.LogOperation($"Error encoding prompt template: {ex.Message}. Skipping SharpToken for prompt.");
                result.PromptTemplateTokens = EstimateTokenCount(promptTemplate.Length);
                result.TotalTokens += result.PromptTemplateTokens;
            }
        }

        // Create dictionaries for extension tracking
        var tokensByExtension = new Dictionary<string, int>();

        // Section headers tokens (extension groups)
        if (sourceFiles.Count > 0)
        {
            var extensionGroups = sourceFiles.GroupBy(f => f.Extension).ToList();
            foreach (var group in extensionGroups)
            {
                var sectionHeader = $"--- {group.Key.ToUpperInvariant()} FILES ---\n\n";
                try
                {
                    var sectionTokens = encoding.Encode(sectionHeader).Count;
                    result.SectionHeadersTokens += sectionTokens;
                    result.TotalTokens += sectionTokens;
                }
                catch (Exception ex)
                {
                    _loggingService.LogOperation($"Error encoding section header: {ex.Message}. Estimating tokens.");
                    result.SectionHeadersTokens += EstimateTokenCount(sectionHeader.Length);
                    result.TotalTokens += EstimateTokenCount(sectionHeader.Length);
                }
            }
        }

        // Individual file tokens
        foreach (var file in sourceFiles)
        {
            var fileTokens = CountFileTokens(file, encoding);
            result.FileTokens += fileTokens;
            result.TotalTokens += fileTokens;

            // Store detailed file token count
            result.TokensByFile[file.RelativePath] = fileTokens;

            // Add to extension grouping
            tokensByExtension.TryAdd(file.Extension, 0);
            tokensByExtension[file.Extension] += fileTokens;
        }

        // Store extension breakdown
        result.TokensByExtension = tokensByExtension;

        // Add a small buffer for safety (5%)
        var bufferTokens = (int)(result.TotalTokens * 0.05);
        result.BufferTokens = bufferTokens;
        result.TotalTokens += bufferTokens;

        // Add model compatibility info
        AddModelCompatibilityInfo(result);

        return result;
    }

    /// <summary>
    /// Counts tokens in a source file, including tokens for formatting
    /// </summary>
    private int CountFileTokens(SourceFile file, GptEncoding encoding)
    {
        if (file == null || string.IsNullOrEmpty(file.Content))
            return 0;

        var fileHeaderLanguage = GetLanguageForExtension(file.Extension);
        var fileHeader = $"File: {file.RelativePath}\n```{fileHeaderLanguage}\n";
        const string fileFooter = "\n```\n";

        int headerTokens, contentTokens, footerTokens;

        try
        {
            headerTokens = encoding.Encode(fileHeader).Count;
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error encoding file header: {ex.Message}. Estimating tokens.");
            headerTokens = EstimateTokenCount(fileHeader.Length);
        }

        try
        {
            contentTokens = encoding.Encode(file.Content).Count;
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error encoding file content: {ex.Message}. Estimating tokens.");
            contentTokens = EstimateTokenCount(file.Content.Length);
        }

        try
        {
            footerTokens = encoding.Encode(fileFooter).Count;
        }
        catch (Exception ex)
        {
            _loggingService.LogOperation($"Error encoding file footer: {ex.Message}. Estimating tokens.");
            footerTokens = EstimateTokenCount(fileFooter.Length);
        }

        return headerTokens + contentTokens + footerTokens;
    }

    /// <summary>
    /// Estimates tokens using character counts when SharpToken is unavailable
    /// </summary>
    private TokenCalculationResult EstimateTokens(List<SourceFile> sourceFiles, string promptTemplate)
    {
        var result = new TokenCalculationResult();

        // Calculate tokens from the prompt template
        var promptTemplateLength = promptTemplate?.Length ?? 0;
        var promptTemplateTokens = EstimateTokenCount(promptTemplateLength);
        result.PromptTemplateTokens = promptTemplateTokens;

        // Calculate tokens from files
        var totalFileChars = 0;
        var filesByExtension = new Dictionary<string, int>();

        foreach (var file in sourceFiles)
        {
            var fileLength = file.Content?.Length ?? 0;
            totalFileChars += fileLength;

            // Track token count by extension
            var ext = file.Extension;
            filesByExtension.TryAdd(ext, 0);

            // Estimate tokens for this file
            var fileTokens = EstimateTokenCount(fileLength);
            filesByExtension[ext] += fileTokens;

            // Also track by individual file
            result.TokensByFile[file.RelativePath] = fileTokens;
        }

        var totalFileTokens = EstimateTokenCount(totalFileChars);
        result.FileTokens = totalFileTokens;

        // Store extension breakdown
        result.TokensByExtension = filesByExtension;

        // Additional tokens for section headers and formatting
        var sectionHeadersEstimate = sourceFiles
            .Select(f => f.Extension)
            .Distinct()
            .Count() * 10; // Estimate 10 tokens per section header

        result.SectionHeadersTokens = sectionHeadersEstimate;

        // Buffer tokens for safety
        var bufferTokens = EstimateOverheadTokens(sourceFiles.Count);
        result.BufferTokens = bufferTokens;

        // Compute the total
        result.TotalTokens = promptTemplateTokens + totalFileTokens + sectionHeadersEstimate + bufferTokens;

        // Add model compatibility info
        AddModelCompatibilityInfo(result);

        return result;
    }

    /// <summary>
    /// Estimates token count from character count
    /// </summary>
    private int EstimateTokenCount(int characterCount)
    {
        // Use default ratio (0.25 tokens per character is a reasonable approximation)
        return (int)Math.Ceiling(characterCount * _tokenRatios["default"]);
    }

    /// <summary>
    /// Estimates overhead tokens based on file count
    /// </summary>
    private int EstimateOverheadTokens(int fileCount)
    {
        // Estimate overhead for file separators, formatting, etc.
        return 200 + fileCount * 20; // Base overhead + per-file overhead
    }

    /// <summary>
    /// Adds model compatibility information to the result
    /// </summary>
    private void AddModelCompatibilityInfo(TokenCalculationResult result)
    {
        const double warningThreshold = 0.9; // 90% of model's capacity

        foreach (var modelEntry in _modelLimits)
        {
            var modelName = modelEntry.Key;
            var limit = modelEntry.Value;
            var withinLimit = result.TotalTokens <= limit;
            var approaching = result.TotalTokens >= limit * warningThreshold && result.TotalTokens <= limit;

            // Create descriptive status string
            var status = withinLimit
                ? approaching
                    ? "⚠️ Approaching limit"
                    : "✅ Within limit"
                : "❌ Exceeds limit";

            // Add percentages
            var percentage = Math.Min(100, (double)result.TotalTokens / limit * 100);
            result.ModelCompatibility[modelName] = $"{status} ({percentage:F1}% - {result.TotalTokens:N0}/{limit:N0})";
        }
    }

    /// <summary>
    /// Gets the language identifier for a file extension to be used in markdown code blocks
    /// </summary>
    private static string GetLanguageForExtension(string ext)
    {
        return ext switch
        {
            // C# and .NET
            ".cs" => "csharp",
            ".vb" => "vb",
            ".fs" => "fsharp",
            ".xaml" => "xml",
            ".csproj" => "xml",
            ".vbproj" => "xml",
            ".fsproj" => "xml",
            ".nuspec" => "xml",
            ".aspx" => "aspx",
            ".asp" => "asp",
            ".cshtml" => "cshtml",
            ".axaml" => "xml",

            // Web languages
            ".html" => "html",
            ".htm" => "html",
            ".css" => "css",
            ".js" => "javascript",
            ".jsx" => "jsx",
            ".ts" => "typescript",
            ".tsx" => "tsx",
            ".vue" => "vue",
            ".svelte" => "svelte",
            ".scss" => "scss",
            ".sass" => "sass",
            ".less" => "less",
            ".mjs" => "javascript",
            ".cjs" => "javascript",

            // JVM languages
            ".java" => "java",
            ".kt" => "kotlin",
            ".scala" => "scala",
            ".groovy" => "groovy",

            // Python
            ".py" => "python",

            // Ruby
            ".rb" => "ruby",
            ".erb" => "erb",

            // PHP
            ".php" => "php",

            // C/C++
            ".c" => "c",
            ".cpp" => "cpp",
            ".h" => "cpp", // C/C++ headers typically get cpp highlighting

            // Go
            ".go" => "go",

            // Rust
            ".rs" => "rust",

            // Swift/Objective-C
            ".swift" => "swift",
            ".m" => "objectivec",
            ".mm" => "objectivec",

            // Dart/Flutter
            ".dart" => "dart",

            // Markup and Data
            ".xml" => "xml",
            ".json" => "json",
            ".yaml" => "yaml",
            ".yml" => "yaml",
            ".md" => "markdown",
            ".txt" => "text",
            ".plist" => "xml",

            // Templates
            ".pug" => "pug",
            ".jade" => "jade",
            ".ejs" => "ejs",
            ".haml" => "haml",

            // Query Languages
            ".sql" => "sql",
            ".graphql" => "graphql",
            ".gql" => "graphql",

            // Shell/Scripts
            ".sh" => "bash",
            ".bash" => "bash",
            ".bat" => "batch",
            ".ps1" => "powershell",
            ".pl" => "perl",

            // Other Languages
            ".r" => "r",
            ".lua" => "lua",
            ".dockerfile" => "dockerfile",
            ".ex" => "elixir",
            ".exs" => "elixir",
            ".jl" => "julia",
            ".nim" => "nim",
            ".hs" => "haskell",
            ".clj" => "clojure",
            ".elm" => "elm",
            ".erl" => "erlang",
            ".asm" => "asm",
            ".s" => "asm",
            ".wasm" => "wasm",

            // Configuration/Infrastructure
            ".ini" => "ini",
            ".toml" => "toml",
            ".tf" => "hcl",
            ".tfvars" => "hcl",
            ".proto" => "proto",
            ".config" => "xml",

            // Default case
            _ => "text"
        };
    }
}