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
    }
}