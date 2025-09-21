using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace SerilogSyntax.Classification;

/// <summary>
/// Defines the theme-aware visual format for Serilog property names in message templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.PropertyName)]
[Name("Serilog Property Name")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogPropertyNameFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.PropertyName, "Serilog Property Name")
{
}

/// <summary>
/// Defines the theme-aware visual format for the destructure operator (@) in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.DestructureOperator)]
[Name("Serilog Destructure Operator")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogDestructureOperatorFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.DestructureOperator, "Serilog Destructure Operator (@)")
{
}

/// <summary>
/// Defines the theme-aware visual format for the stringify operator ($) in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.StringifyOperator)]
[Name("Serilog Stringify Operator")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogStringifyOperatorFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.StringifyOperator, "Serilog Stringify Operator ($)")
{
}

/// <summary>
/// Defines the theme-aware visual format for format specifiers in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.FormatSpecifier)]
[Name("Serilog Format Specifier")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogFormatSpecifierFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.FormatSpecifier, "Serilog Format Specifier")
{
}

/// <summary>
/// Defines the theme-aware visual format for property braces in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.PropertyBrace)]
[Name("Serilog Property Brace")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogPropertyBraceFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.PropertyBrace, "Serilog Property Brace")
{
}

/// <summary>
/// Defines the theme-aware visual format for positional indices in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.PositionalIndex)]
[Name("Serilog Positional Index")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogPositionalIndexFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.PositionalIndex, "Serilog Positional Index")
{
}

/// <summary>
/// Defines the theme-aware visual format for alignment values in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.Alignment)]
[Name("Serilog Alignment")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogAlignmentFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.Alignment, "Serilog Alignment")
{
}

/// <summary>
/// Defines the visual format for property-argument highlighting in Serilog templates.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[Name("PropertyArgumentHighlight")]
[UserVisible(true)]
internal sealed class PropertyArgumentHighlightFormat : MarkerFormatDefinition
{
    public PropertyArgumentHighlightFormat()
    {
        DisplayName = "Serilog Property-Argument Highlight";
        // Match VS Code's editor.wordHighlightBackground style
        // In VS dark theme, word highlights typically use a subtle blue-gray
        BackgroundColor = Color.FromArgb(40, 90, 90, 90); // Subtle gray background similar to word highlight
        Border = null; // No border, using background fill like VS Code
        BackgroundCustomizable = true;
        ForegroundCustomizable = false;
        ZOrder = 5;
    }
}

// Expression syntax format definitions

/// <summary>
/// Defines the theme-aware visual format for expression properties.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionProperty)]
[Name("Serilog Expression Property")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogExpressionPropertyFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.ExpressionProperty, "Serilog Expression Property")
{
}

/// <summary>
/// Defines the theme-aware visual format for expression operators.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionOperator)]
[Name("Serilog Expression Operator")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogExpressionOperatorFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.ExpressionOperator, "Serilog Expression Operator")
{
}

/// <summary>
/// Defines the theme-aware visual format for expression functions.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionFunction)]
[Name("Serilog Expression Function")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogExpressionFunctionFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.ExpressionFunction, "Serilog Expression Function")
{
}

/// <summary>
/// Defines the theme-aware visual format for expression keywords.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionKeyword)]
[Name("Serilog Expression Keyword")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogExpressionKeywordFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.ExpressionKeyword, "Serilog Expression Keyword")
{
}

/// <summary>
/// Defines the theme-aware visual format for expression literals.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionLiteral)]
[Name("Serilog Expression Literal")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogExpressionLiteralFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.ExpressionLiteral, "Serilog Expression Literal")
{
}

/// <summary>
/// Defines the theme-aware visual format for expression template directives.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionDirective)]
[Name("Serilog Expression Directive")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogExpressionDirectiveFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.ExpressionDirective, "Serilog Expression Directive")
{
}

/// <summary>
/// Defines the theme-aware visual format for expression built-in properties.
/// </summary>
[Export(typeof(EditorFormatDefinition))]
[ClassificationType(ClassificationTypeNames = SerilogClassificationTypes.ExpressionBuiltin)]
[Name("Serilog Expression Builtin")]
[UserVisible(true)]
[Order(Before = Priority.Default)]
[method: ImportingConstructor]
internal sealed class SerilogExpressionBuiltinFormat(SerilogThemeColors themeColors)
    : SerilogClassificationFormatBase(themeColors, SerilogClassificationTypes.ExpressionBuiltin, "Serilog Expression Built-in")
{
}