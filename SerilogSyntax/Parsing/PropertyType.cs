namespace SerilogSyntax.Parsing;

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
