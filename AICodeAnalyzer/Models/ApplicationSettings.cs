using System;
using System.Collections.Generic;

namespace AICodeAnalyzer.Models;

[Serializable]
public class ApplicationSettings
{
    // File extensions settings
    public List<string> SourceFileExtensions { get; set; }
    
    // File size limitations
    public int MaxFileSizeKb { get; set; } = 1024; // Default 1MB
    
    // Initial prompt for code analysis
    public string InitialPrompt { get; set; }
    
    // Other settings can be added here in the future
    
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
        
        // Set default initial prompt
        InitialPrompt = "Please analyze the following source code files from my project. I would like you to:" + Environment.NewLine +
                        "1. Understand the overall structure and purpose of the codebase" + Environment.NewLine +
                        "2. Identify any bugs, errors, or inconsistencies" + Environment.NewLine +
                        "3. Highlight potential security vulnerabilities" + Environment.NewLine +
                        "4. Suggest improvements for code quality and maintainability" + Environment.NewLine +
                        "5. Provide specific recommendations for the most critical issues" + Environment.NewLine + 
                        Environment.NewLine +
                        "Here are all the files from my project:";
    }
}