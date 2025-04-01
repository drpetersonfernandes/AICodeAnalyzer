using System;

namespace AICodeAnalyzer.Models;

/// <summary>
/// Represents a named code analysis prompt template.
/// </summary>
[Serializable]
public class CodePrompt : IEquatable<CodePrompt>
{
    /// <summary>
    /// Gets or sets the name of the prompt template.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the prompt template.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Creates a new instance of the CodePrompt class.
    /// </summary>
    public CodePrompt()
    {
    }

    /// <summary>
    /// Creates a new instance of the CodePrompt class with the specified name and content.
    /// </summary>
    /// <param name="name">The name of the prompt template.</param>
    /// <param name="content">The content of the prompt template.</param>
    public CodePrompt(string name, string content)
    {
        Name = name;
        Content = content;
    }

    /// <summary>
    /// Returns a string representation of the prompt template.
    /// </summary>
    public override string ToString()
    {
        return Name;
    }

    /// <summary>
    /// Determines whether this prompt template is equal to another.
    /// </summary>
    public bool Equals(CodePrompt? other)
    {
        if (other is null)
            return false;

        return Name == other.Name;
    }

    /// <summary>
    /// Determines whether this prompt template is equal to another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is null)
            return false;

        if (ReferenceEquals(this, obj))
            return true;

        if (obj is CodePrompt other)
            return Equals(other);

        return false;
    }

    /// <summary>
    /// Returns a hash code for this prompt template.
    /// Note: Changing the Name property after adding this object to a hash-based
    /// collection (Dictionary, HashSet) could lead to unexpected behavior.
    /// </summary>
    public override int GetHashCode()
    {
        // We're using a constant value to avoid the warning while maintaining serialization compatibility.
        // This means we rely on Equals for correct Dictionary/HashSet behavior.
        return 0;
    }
}