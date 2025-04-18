using System;

namespace AICodeAnalyzer.Models;

[Serializable]
public class CodePrompt : IEquatable<CodePrompt>
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    public CodePrompt()
    {
    }

    public CodePrompt(string name, string content)
    {
        Name = name;
        Content = content;
    }

    public override string ToString()
    {
        return Name;
    }

    public bool Equals(CodePrompt? other)
    {
        if (other is null)
            return false;

        return Name == other.Name;
    }

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