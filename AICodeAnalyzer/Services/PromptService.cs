using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AICodeAnalyzer.Models;

namespace AICodeAnalyzer.Services;

public class PromptService(LoggingService loggingService, SettingsManager settingsManager)
{
    private readonly LoggingService _loggingService = loggingService;
    private readonly SettingsManager _settingsManager = settingsManager;

    public string BuildPrompt(
        bool includeTemplate,
        bool includeFiles,
        List<SourceFile> files,
        string additionalInstructions)
    {
        var promptBuilder = new StringBuilder();

        // Add template if requested
        if (includeTemplate)
        {
            var templateText = GetPromptTemplateText();
            promptBuilder.AppendLine(templateText);
            promptBuilder.AppendLine();
            _loggingService.LogOperation("Including prompt template in query");
        }
        else
        {
            promptBuilder.AppendLine("Please analyze the following code files:");
            promptBuilder.AppendLine();
            _loggingService.LogOperation("Not using prompt template");
        }

        // Add files if requested
        if (includeFiles && files.Count > 0)
        {
            promptBuilder.AppendLine("--- SOURCE FILES ---");
            promptBuilder.AppendLine();

            // Group files by extension for better organization
            var filesByExtension = files
                .GroupBy(f => f.Extension)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var ext in filesByExtension.Keys.OrderBy(k => k))
            {
                promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"--- {ext.ToUpperInvariant()} FILES ---");
                promptBuilder.AppendLine();

                foreach (var file in filesByExtension[ext])
                {
                    promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"File: {file.RelativePath}");
                    promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"```{GetLanguageForExtension(ext)}");
                    promptBuilder.AppendLine(file.Content);
                    promptBuilder.AppendLine("```");
                    promptBuilder.AppendLine();
                }
            }

            _loggingService.LogOperation($"Added {files.Count} files to the prompt");
        }
        else if (includeFiles)
        {
            promptBuilder.AppendLine("--- NO FILES AVAILABLE TO INCLUDE ---");
            promptBuilder.AppendLine();
            _loggingService.LogOperation("No files available to include");
        }

        // Add additional instructions if provided
        if (!string.IsNullOrEmpty(additionalInstructions))
        {
            promptBuilder.AppendLine("--- Additional Instructions/Question ---");
            promptBuilder.AppendLine(additionalInstructions);
            _loggingService.LogOperation("Added additional instructions to the prompt");
        }

        return promptBuilder.ToString();
    }

    public string BuildPromptWithSelectedFiles(
        string originalPrompt,
        List<SourceFile> selectedFiles)
    {
        var promptBuilder = new StringBuilder(originalPrompt);

        promptBuilder.AppendLine();
        promptBuilder.AppendLine("--- SELECTED FILES FOR FOCUSED REFERENCE ---");
        promptBuilder.AppendLine();

        // First show a summary of the selected files
        if (selectedFiles.Count > 0)
        {
            promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"Specifically, I've selected the following {selectedFiles.Count} file(s) for you to focus on or reference:");
            foreach (var file in selectedFiles.OrderBy(f => f.RelativePath))
            {
                promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"- {file.RelativePath}");
            }

            promptBuilder.AppendLine();

            // Then include the file contents
            foreach (var file in selectedFiles)
            {
                promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"File: {file.RelativePath}");
                promptBuilder.AppendLine(CultureInfo.InvariantCulture, $"```{GetLanguageForExtension(file.Extension)}");
                promptBuilder.AppendLine(file.Content);
                promptBuilder.AppendLine("```");
                promptBuilder.AppendLine();
            }
        }
        else
        {
            promptBuilder.AppendLine("(No specific files were selected in the list for focused reference)");
            promptBuilder.AppendLine();
        }

        _loggingService.LogOperation($"Included {selectedFiles.Count} specifically selected files in the prompt");

        return promptBuilder.ToString();
    }

    private string GetPromptTemplateText()
    {
        // Get the active prompt template from settings
        var selectedPrompt = _settingsManager.Settings.CodePrompts
            .FirstOrDefault(p => p.Name == _settingsManager.Settings.SelectedPromptName);

        return selectedPrompt?.Content ?? _settingsManager.Settings.InitialPrompt; // Fallback
    }

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