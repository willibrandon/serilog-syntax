using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace SerilogSyntax.Classification;

/// <summary>
/// Defines the visual format for Serilog property names in message templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.PropertyName)]
[Name("Serilog Property Name")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogPropertyNameFormat : ClassificationFormatDefinition
{
    public SerilogPropertyNameFormat()
    {
        DisplayName = "Serilog Property Name";
        ForegroundColor = Color.FromRgb(0x00, 0x7A, 0xCC); // Accessible blue (#007ACC) - works in both themes
    }
}

/// <summary>
/// Defines the visual format for the destructure operator (@) in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.DestructureOperator)]
[Name("Serilog Destructure Operator")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogDestructureOperatorFormat : ClassificationFormatDefinition
{
    public SerilogDestructureOperatorFormat()
    {
        DisplayName = "Serilog Destructure Operator (@)";
        ForegroundColor = Color.FromRgb(0xB8, 0x86, 0x0B); // Dark goldenrod (#B8860B) - visible in both themes
    }
}

/// <summary>
/// Defines the visual format for the stringify operator ($) in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.StringifyOperator)]
[Name("Serilog Stringify Operator")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogStringifyOperatorFormat : ClassificationFormatDefinition
{
    public SerilogStringifyOperatorFormat()
    {
        DisplayName = "Serilog Stringify Operator ($)";
        ForegroundColor = Color.FromRgb(0xB8, 0x86, 0x0B); // Dark goldenrod (#B8860B) - visible in both themes
    }
}

/// <summary>
/// Defines the visual format for format specifiers in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.FormatSpecifier)]
[Name("Serilog Format Specifier")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogFormatSpecifierFormat : ClassificationFormatDefinition
{
    public SerilogFormatSpecifierFormat()
    {
        DisplayName = "Serilog Format Specifier";
        ForegroundColor = Color.FromRgb(0x00, 0x80, 0x80); // Teal (#008080) - good contrast in both themes
    }
}

/// <summary>
/// Defines the visual format for property braces in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.PropertyBrace)]
[Name("Serilog Property Brace")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogPropertyBraceFormat : ClassificationFormatDefinition
{
    public SerilogPropertyBraceFormat()
    {
        DisplayName = "Serilog Property Brace";
        ForegroundColor = Color.FromRgb(0x80, 0x00, 0x80); // Purple (#800080) - works in both themes
    }
}

/// <summary>
/// Defines the visual format for positional indices in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.PositionalIndex)]
[Name("Serilog Positional Index")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogPositionalIndexFormat : ClassificationFormatDefinition
{
    public SerilogPositionalIndexFormat()
    {
        DisplayName = "Serilog Positional Index";
        ForegroundColor = Color.FromRgb(0xAF, 0x00, 0xDB); // Dark violet (#AF00DB) - visible in both themes
    }
}

/// <summary>
/// Defines the visual format for alignment values in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.Alignment)]
[Name("Serilog Alignment")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogAlignmentFormat : ClassificationFormatDefinition
{
    public SerilogAlignmentFormat()
    {
        DisplayName = "Serilog Alignment";
        ForegroundColor = Color.FromRgb(0xDC, 0x26, 0x26); // Muted red (#DC2626) - 5.2:1 dark, 4.5:1 light contrast
    }
}