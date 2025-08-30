namespace SerilogSyntax.Parsing;

/// <summary>
/// Represents a property within a Serilog message template.
/// </summary>
/// <remarks>
/// Creates a new template property with all parameters.
/// </remarks>
internal class TemplateProperty(
    string name,
    int startIndex,
    int length,
    PropertyType type,
    int braceStartIndex,
    int braceEndIndex,
    string formatSpecifier = null,
    int formatStartIndex = -1,
    int operatorIndex = -1,
    string alignment = null,
    int alignmentStartIndex = -1)
{
    /// <summary>
    /// Creates a standard property {PropertyName}.
    /// </summary>
    public static TemplateProperty CreateStandard(string name, int startIndex, int length, int braceStartIndex, int braceEndIndex)
    {
        return new TemplateProperty(name, startIndex, length, PropertyType.Standard, braceStartIndex, braceEndIndex);
    }

    /// <summary>
    /// Creates a destructured property {@PropertyName}.
    /// </summary>
    public static TemplateProperty CreateDestructured(string name, int startIndex, int length, int braceStartIndex, int braceEndIndex, int operatorIndex)
    {
        return new TemplateProperty(name, startIndex, length, PropertyType.Destructured, braceStartIndex, braceEndIndex, 
            operatorIndex: operatorIndex);
    }

    /// <summary>
    /// Creates a stringified property {$PropertyName}.
    /// </summary>
    public static TemplateProperty CreateStringified(string name, int startIndex, int length, int braceStartIndex, int braceEndIndex, int operatorIndex)
    {
        return new TemplateProperty(name, startIndex, length, PropertyType.Stringified, braceStartIndex, braceEndIndex,
            operatorIndex: operatorIndex);
    }

    /// <summary>
    /// Creates a positional property {0}.
    /// </summary>
    public static TemplateProperty CreatePositional(string name, int startIndex, int length, int braceStartIndex, int braceEndIndex)
    {
        return new TemplateProperty(name, startIndex, length, PropertyType.Positional, braceStartIndex, braceEndIndex);
    }
    /// <summary>
    /// Gets the name of the property (without operators or braces).
    /// </summary>
    public string Name { get; } = name ?? string.Empty;

    /// <summary>
    /// Gets the starting index of the property name within the template.
    /// </summary>
    public int StartIndex { get; } = startIndex;

    /// <summary>
    /// Gets the length of the property name.
    /// </summary>
    public int Length { get; } = length;

    /// <summary>
    /// Gets the type of the property.
    /// </summary>
    public PropertyType Type { get; } = type;

    /// <summary>
    /// Gets the format specifier (e.g., "yyyy-MM-dd" in {Date:yyyy-MM-dd}).
    /// </summary>
    public string FormatSpecifier { get; } = formatSpecifier;

    /// <summary>
    /// Gets the starting index of the format specifier including the colon.
    /// </summary>
    public int FormatStartIndex { get; } = formatStartIndex;

    /// <summary>
    /// Gets the index of the opening brace.
    /// </summary>
    public int BraceStartIndex { get; } = braceStartIndex;

    /// <summary>
    /// Gets the index of the closing brace.
    /// </summary>
    public int BraceEndIndex { get; } = braceEndIndex;

    /// <summary>
    /// Gets the index of the operator (@ or $) if present.
    /// </summary>
    public int OperatorIndex { get; } = operatorIndex;

    /// <summary>
    /// Gets the alignment value (e.g., "10" in {Property,10}).
    /// </summary>
    public string Alignment { get; } = alignment;

    /// <summary>
    /// Gets the starting index of the alignment including the comma.
    /// </summary>
    public int AlignmentStartIndex { get; } = alignmentStartIndex;
}

/// <summary>
/// Specifies the type of a Serilog template property.
/// </summary>
internal enum PropertyType
{
    /// <summary>
    /// Standard property rendered using ToString() (e.g., {Property}).
    /// </summary>
    Standard,

    /// <summary>
    /// Destructured property that serializes object structure (e.g., {@Property}).
    /// </summary>
    Destructured,

    /// <summary>
    /// Stringified property forced to render as a string (e.g., {$Property}).
    /// </summary>
    Stringified,

    /// <summary>
    /// Positional property referenced by index (e.g., {0}, {1}).
    /// </summary>
    Positional
}