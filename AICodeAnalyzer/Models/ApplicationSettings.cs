using System;
using System.Collections.Generic;
using System.Linq;

namespace AICodeAnalyzer.Models;

[Serializable]
public class ApplicationSettings
{
    // File extensions settings
    public List<string> SourceFileExtensions { get; set; }

    // File size limitations
    public int MaxFileSizeKb { get; set; } = 1024; // Default 1MB

    public bool RegisterAsDefaultMdHandler { get; set; } = false;

    // Initial prompt for code analysis - kept for backward compatibility
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
                CodePrompts.Add(new CodePrompt("Default", value));
                SelectedPromptName = "Default";
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
                CodePrompts.Add(new CodePrompt("Default", value));
                SelectedPromptName = "Default";
            }
        }
    }

    // Collection of code prompts
    public List<CodePrompt> CodePrompts { get; set; } = new List<CodePrompt>();

    // Currently selected prompt name
    public string? SelectedPromptName { get; set; }

    // Other settings can be added here in the future

    // Default prompt text
    private string DefaultPrompt =>
        "Please analyze the following source code files from my project. I would like you to:" + Environment.NewLine +
        "1. Understand the overall structure and purpose of the codebase" + Environment.NewLine +
        "2. Identify any bugs, errors, or inconsistencies" + Environment.NewLine +
        "3. Highlight potential security vulnerabilities" + Environment.NewLine +
        "4. Suggest improvements for code quality and maintainability" + Environment.NewLine +
        "5. Provide specific recommendations for the most critical issues" + Environment.NewLine +
        Environment.NewLine +
        "Here are all the files from my project:";

    // Constructor with default values
    public ApplicationSettings()
    {
        // Set default extensions
        SourceFileExtensions = new List<string>
        {
            ".cs", ".xaml", ".java", ".js", ".ts", ".py", ".html", ".css",
            ".cpp", ".h", ".c", ".go", ".rb", ".php", ".swift", ".kt",
            ".rs", ".dart", ".scala", ".groovy", ".pl", ".sh", ".bat",
            ".ps1", ".xml", ".json", ".yaml", ".yml", ".md", ".txt"
        };

        // Set up default prompt
        CodePrompts.Add(new CodePrompt("Default", DefaultPrompt));
        SelectedPromptName = "Default";
    }
}