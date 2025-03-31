namespace AICodeAnalyzer.Models;

internal class ModelDropdownItem
{
    public required string DisplayText { get; set; }
    public required string ModelId { get; set; }
    public required string Description { get; set; }
    
    public override string ToString()
    {
        return DisplayText;
    }
}