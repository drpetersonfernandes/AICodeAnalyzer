namespace AICodeAnalyzer.Models;

/// <summary>
/// Represents a source file with its associated metadata and content.
/// </summary>
public class SourceFile
{
    /// <summary>
    /// Gets or sets the full path of the source file.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Gets or sets the relative path of the source file from a selected base folder.
    /// </summary>
    /// <remarks>
    /// The RelativePath represents the file's path relative to a predefined root directory,
    /// typically stripping the root folder path and preserving only the subdirectory structure and file name.
    /// </remarks>
    public required string RelativePath { get; set; }

    /// <summary>
    /// Gets or sets the file extension of the source file.
    /// </summary>
    /// <remarks>
    /// The <see cref="Extension"/> property represents the file extension, including the leading dot (e.g., ".cs", ".txt").
    /// This value is used to classify and organize source files by their respective types or formats.
    /// </remarks>
    public required string Extension { get; set; }

    /// <summary>
    /// Gets or sets the content of the source file as a string.
    /// </summary>
    /// <remarks>
    /// This property contains the entire textual content of the source file.
    /// It can be used for operations such as content analysis, formatting, or searching within the file.
    /// The property value is populated by reading the file from disk.
    /// </remarks>
    public required string Content { get; set; }
}