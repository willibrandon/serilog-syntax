namespace SerilogSyntax.Classification;

/// <summary>
/// Represents a classified region in an expression.
/// </summary>
public class ClassifiedRegion(string type, int start, int length, string text)
{
    public string ClassificationType { get; set; } = type;

    public int Start { get; set; } = start;

    public int Length { get; set; } = length;

    public string Text { get; set; } = text;
}
