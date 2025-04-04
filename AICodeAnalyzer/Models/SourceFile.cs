namespace AICodeAnalyzer.Models;

public class SourceFile
{
    public required string Path { get; set; }
    public required string RelativePath { get; set; }
    public required string Extension { get; set; }
    public required string Content { get; set; }
}