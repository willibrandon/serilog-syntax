namespace SerilogSyntax.Parsing;

/// <summary>
/// Represents a property within a Serilog message template.
/// </summary>
internal class TemplateProperty
{
    /// <summary>
    /// Gets or sets the name of the property (without operators or braces).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the starting index of the property name within the template.
    /// </summary>
    public int StartIndex { get; set; }

    /// <summary>
    /// Gets or sets the length of the property name.
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Gets or sets the type of the property.
    /// </summary>
    public PropertyType Type { get; set; }

    /// <summary>
    /// Gets or sets the format specifier (e.g., "yyyy-MM-dd" in {Date:yyyy-MM-dd}).
    /// </summary>
    public string FormatSpecifier { get; set; }

    /// <summary>
    /// Gets or sets the starting index of the format specifier including the colon.
    /// </summary>
    public int FormatStartIndex { get; set; }

    /// <summary>
    /// Gets or sets the index of the opening brace.
    /// </summary>
    public int BraceStartIndex { get; set; }

    /// <summary>
    /// Gets or sets the index of the closing brace.
    /// </summary>
    public int BraceEndIndex { get; set; }

    /// <summary>
    /// Gets or sets the index of the operator (@ or $) if present.
    /// </summary>
    public int OperatorIndex { get; set; }

    /// <summary>
    /// Gets or sets the alignment value (e.g., "10" in {Property,10}).
    /// </summary>
    public string Alignment { get; set; }

    /// <summary>
    /// Gets or sets the starting index of the alignment including the comma.
    /// </summary>
    public int AlignmentStartIndex { get; set; }
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