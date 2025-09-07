using System.Windows.Media;

namespace SerilogSyntax.Classification;

/// <summary>
/// Represents the visual properties for text formatting in Serilog syntax highlighting.
/// </summary>
/// <remarks>
/// Initializes a new instance of the SerilogTextProperties class.
/// </remarks>
/// <param name="foreground">The foreground color, or null for default.</param>
/// <param name="background">The background color, or null for default.</param>
/// <param name="isBold">Whether the text should be bold.</param>
/// <param name="isItalic">Whether the text should be italic.</param>
public class SerilogTextProperties(Color? foreground, Color? background, bool isBold, bool isItalic)
{
    /// <summary>
    /// Gets the foreground color for the text, or null to use default.
    /// </summary>
    public Color? Foreground { get; } = foreground;

    /// <summary>
    /// Gets the background color for the text, or null to use default.
    /// </summary>
    public Color? Background { get; } = background;

    /// <summary>
    /// Gets whether the text should be rendered in bold.
    /// </summary>
    public bool IsBold { get; } = isBold;

    /// <summary>
    /// Gets whether the text should be rendered in italic.
    /// </summary>
    public bool IsItalic { get; } = isItalic;

    /// <summary>
    /// Creates text properties with only foreground color specified.
    /// </summary>
    /// <param name="foreground">The foreground color.</param>
    /// <returns>A new SerilogTextProperties instance.</returns>
    public static SerilogTextProperties Create(Color foreground)
        => new(foreground, null, false, false);

    /// <summary>
    /// Creates text properties with foreground color and bold formatting.
    /// </summary>
    /// <param name="foreground">The foreground color.</param>
    /// <param name="isBold">Whether the text should be bold.</param>
    /// <returns>A new SerilogTextProperties instance.</returns>
    public static SerilogTextProperties Create(Color foreground, bool isBold)
        => new(foreground, null, isBold, false);

    /// <summary>
    /// Creates text properties with all formatting options.
    /// </summary>
    /// <param name="foreground">The foreground color.</param>
    /// <param name="isBold">Whether the text should be bold.</param>
    /// <param name="isItalic">Whether the text should be italic.</param>
    /// <returns>A new SerilogTextProperties instance.</returns>
    public static SerilogTextProperties Create(Color foreground, bool isBold, bool isItalic)
        => new(foreground, null, isBold, isItalic);
}