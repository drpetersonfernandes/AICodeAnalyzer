using System;
using System.Collections.Generic;
using System.Linq;

namespace AICodeAnalyzer.Models;

[Serializable]
public class ApplicationSettings
{
    public List<string> SourceFileExtensions { get; set; }
    public int MaxFileSizeKb { get; set; } = 1024; // Default 1MB
    public bool RegisterAsDefaultMdHandler { get; set; }

    public string InitialPrompt
    {
        get => SelectedPromptName != null && CodePrompts.Count > 0
            ? CodePrompts.FirstOrDefault(p => p.Name == SelectedPromptName)?.Content ?? DefaultPrompt
            : DefaultPrompt;
        set
        {
            // If we're setting this directly, update the default prompt or create it if needed
            if (CodePrompts.Count == 0)
            {
                CodePrompts.Add(new CodePrompt("Analyze Source Code", value));
                SelectedPromptName = "Analyze Source Code";
            }
            else if (SelectedPromptName != null)
            {
                var prompt = CodePrompts.FirstOrDefault(p => p.Name == SelectedPromptName);
                if (prompt != null)
                {
                    prompt.Content = value;
                }
                else
                {
                    CodePrompts.Add(new CodePrompt(SelectedPromptName, value));
                }
            }
            else
            {
                CodePrompts.Add(new CodePrompt("Analyze Source Code", value));
                SelectedPromptName = "Analyze Source Code";
            }
        }
    }

    public List<CodePrompt> CodePrompts { get; set; } = new();

    public string? SelectedPromptName { get; set; }

    // Default prompt text
    private static string DefaultPrompt =>
        "Please analyze the following source code files from my project. I would like you to:" + Environment.NewLine +
        "1. Understand the overall structure and purpose of the codebase" + Environment.NewLine +
        "2. Identify any bugs, errors, or inconsistencies" + Environment.NewLine +
        "3. Highlight potential security vulnerabilities" + Environment.NewLine +
        "4. Suggest improvements for code quality and maintainability" + Environment.NewLine +
        "5. Provide specific recommendations for the most critical issues" + Environment.NewLine +
        Environment.NewLine +
        "Here are all the files from my project:";

    public ApplicationSettings()
    {
        SourceFileExtensions =
        [
            ".cs", ".vb", ".fs", ".xaml", ".csproj", ".vbproj", ".fsproj",
            ".nuspec", ".aspx", ".asp", ".cshtml", ".axaml",

            // Web languages
            ".html", ".htm", ".css", ".js", ".jsx", ".ts", ".tsx",
            ".vue", ".svelte", ".scss", ".sass", ".less", ".mjs", ".cjs",

            // JVM languages
            ".java", ".kt", ".scala", ".groovy",

            // Python
            ".py",

            // Ruby
            ".rb", ".erb",

            // PHP
            ".php",

            // C/C++
            ".c", ".cpp", ".h",

            // Go
            ".go",

            // Rust
            ".rs",

            // Swift/Objective-C
            ".swift", ".m", ".mm",

            // Dart/Flutter
            ".dart",

            // Markup and Data
            ".xml", ".json", ".yaml", ".yml", ".md", ".txt", ".plist",

            // Templates
            ".pug", ".jade", ".ejs", ".haml",

            // Query Languages
            ".sql", ".graphql", ".gql",

            // Shell/Scripts
            ".sh", ".bash", ".bat", ".ps1", ".pl",

            // Other Languages
            ".r", ".lua", ".dockerfile", ".ex", ".exs", ".jl", ".nim",
            ".hs", ".clj", ".elm", ".erl", ".asm", ".s", ".wasm",

            // Configuration/Infrastructure
            ".ini", ".toml", ".tf", ".tfvars", ".proto", ".config"
        ];

        CodePrompts.Add(new CodePrompt("Analyze Source Code", DefaultPrompt));
        SelectedPromptName = "Analyze Source Code";
    }
}