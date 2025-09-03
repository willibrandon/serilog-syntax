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

/// <summary>
/// Defines the visual format for brace matching highlight in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[Name("bracehighlight")]
[UserVisible(true)]
internal sealed class SerilogBraceHighlightFormat : MarkerFormatDefinition
{
    public SerilogBraceHighlightFormat()
    {
        DisplayName = "Serilog Brace Highlight";
        // Don't use a solid background - just use a border for subtle highlighting
        // This follows VS's built-in brace matching approach
        ForegroundColor = Color.FromRgb(0x66, 0x66, 0x66); // Dark gray border
        BackgroundCustomizable = true;
        ForegroundCustomizable = true;
        ZOrder = 5;
    }
}

// Expression syntax format definitions

/// <summary>
/// Defines the visual format for expression properties.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionProperty)]
[Name("Serilog Expression Property")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogExpressionPropertyFormat : ClassificationFormatDefinition
{
    public SerilogExpressionPropertyFormat()
    {
        DisplayName = "Serilog Expression Property";
        ForegroundColor = Color.FromRgb(0x09, 0x69, 0xDA); // Blue (#0969DA) - 4.5:1 contrast
    }
}

/// <summary>
/// Defines the visual format for expression operators.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionOperator)]
[Name("Serilog Expression Operator")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogExpressionOperatorFormat : ClassificationFormatDefinition
{
    public SerilogExpressionOperatorFormat()
    {
        DisplayName = "Serilog Expression Operator";
        ForegroundColor = Color.FromRgb(0xCF, 0x22, 0x2E); // Red (#CF222E) - 4.8:1 contrast
    }
}

/// <summary>
/// Defines the visual format for expression functions.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionFunction)]
[Name("Serilog Expression Function")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogExpressionFunctionFormat : ClassificationFormatDefinition
{
    public SerilogExpressionFunctionFormat()
    {
        DisplayName = "Serilog Expression Function";
        ForegroundColor = Color.FromRgb(0x82, 0x50, 0xDF); // Purple (#8250DF) - 4.6:1 contrast
    }
}

/// <summary>
/// Defines the visual format for expression keywords.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionKeyword)]
[Name("Serilog Expression Keyword")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogExpressionKeywordFormat : ClassificationFormatDefinition
{
    public SerilogExpressionKeywordFormat()
    {
        DisplayName = "Serilog Expression Keyword";
        ForegroundColor = Color.FromRgb(0x05, 0x50, 0xAE); // Dark blue (#0550AE) - 7.5:1 contrast
    }
}

/// <summary>
/// Defines the visual format for expression literals.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionLiteral)]
[Name("Serilog Expression Literal")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogExpressionLiteralFormat : ClassificationFormatDefinition
{
    public SerilogExpressionLiteralFormat()
    {
        DisplayName = "Serilog Expression Literal";
        ForegroundColor = Color.FromRgb(0x4A, 0x8B, 0xC2); // Medium Blue (#4A8BC2) - 4.2:1 contrast on both light and dark themes
    }
}

/// <summary>
/// Defines the visual format for expression template directives.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionDirective)]
[Name("Serilog Expression Directive")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogExpressionDirectiveFormat : ClassificationFormatDefinition
{
    public SerilogExpressionDirectiveFormat()
    {
        DisplayName = "Serilog Expression Directive";
        ForegroundColor = Color.FromRgb(0x8B, 0x00, 0x8B); // Magenta (#8B008B) - 4.9:1 contrast
    }
}

/// <summary>
/// Defines the visual format for expression built-in properties.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionBuiltin)]
[Name("Serilog Expression Builtin")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
internal sealed class SerilogExpressionBuiltinFormat : ClassificationFormatDefinition
{
    public SerilogExpressionBuiltinFormat()
    {
        DisplayName = "Serilog Expression Built-in";
        ForegroundColor = Color.FromRgb(0x1F, 0x7A, 0x8C); // Teal (#1F7A8C) - 4.5:1 contrast
    }
}