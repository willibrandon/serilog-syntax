using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace SerilogSyntax.Classification;

/// <summary>
/// Defines the classification type names and definitions for Serilog template elements.
/// </summary>
internal static class SerilogClassificationTypes
{
    /// <summary>
    /// Classification type name for property names in templates.
    /// </summary>
    public const string PropertyName = "serilog.property.name";

    /// <summary>
    /// Classification type name for the destructure operator (@).
    /// </summary>
    public const string DestructureOperator = "serilog.operator.destructure";

    /// <summary>
    /// Classification type name for the stringify operator ($).
    /// </summary>
    public const string StringifyOperator = "serilog.operator.stringify";

    /// <summary>
    /// Classification type name for format specifiers.
    /// </summary>
    public const string FormatSpecifier = "serilog.format";

    /// <summary>
    /// Classification type name for property braces.
    /// </summary>
    public const string PropertyBrace = "serilog.brace";

    /// <summary>
    /// Classification type name for positional indices.
    /// </summary>
    public const string PositionalIndex = "serilog.index";

    /// <summary>
    /// Classification type name for alignment values.
    /// </summary>
    public const string Alignment = "serilog.alignment";

    // Expression syntax classification types
    /// <summary>
    /// Classification type name for expression properties.
    /// </summary>
    public const string ExpressionProperty = "serilog.expression.property";

    /// <summary>
    /// Classification type name for expression operators.
    /// </summary>
    public const string ExpressionOperator = "serilog.expression.operator";

    /// <summary>
    /// Classification type name for expression functions.
    /// </summary>
    public const string ExpressionFunction = "serilog.expression.function";

    /// <summary>
    /// Classification type name for expression keywords.
    /// </summary>
    public const string ExpressionKeyword = "serilog.expression.keyword";

    /// <summary>
    /// Classification type name for expression literals.
    /// </summary>
    public const string ExpressionLiteral = "serilog.expression.literal";

    /// <summary>
    /// Classification type name for expression template directives.
    /// </summary>
    public const string ExpressionDirective = "serilog.expression.directive";

    /// <summary>
    /// Classification type name for expression built-in properties.
    /// </summary>
    public const string ExpressionBuiltin = "serilog.expression.builtin";

    /// <summary>
    /// Classification type name for property-argument highlights.
    /// </summary>
    public const string PropertyArgumentHighlight = "serilog.property.argument.highlight";

    /// <summary>
    /// Classification type definition for property names.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(PropertyName)]
    internal static ClassificationTypeDefinition PropertyNameType = null;

    /// <summary>
    /// Classification type definition for the destructure operator.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(DestructureOperator)]
    internal static ClassificationTypeDefinition DestructureOperatorType = null;

    /// <summary>
    /// Classification type definition for the stringify operator.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(StringifyOperator)]
    internal static ClassificationTypeDefinition StringifyOperatorType = null;

    /// <summary>
    /// Classification type definition for format specifiers.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(FormatSpecifier)]
    internal static ClassificationTypeDefinition FormatSpecifierType = null;

    /// <summary>
    /// Classification type definition for property braces.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(PropertyBrace)]
    internal static ClassificationTypeDefinition PropertyBraceType = null;

    /// <summary>
    /// Classification type definition for positional indices.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(PositionalIndex)]
    internal static ClassificationTypeDefinition PositionalIndexType = null;

    /// <summary>
    /// Classification type definition for alignment values.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(Alignment)]
    internal static ClassificationTypeDefinition AlignmentType = null;

    // Expression classification type definitions
    /// <summary>
    /// Classification type definition for expression properties.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ExpressionProperty)]
    internal static ClassificationTypeDefinition ExpressionPropertyType = null;

    /// <summary>
    /// Classification type definition for expression operators.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ExpressionOperator)]
    internal static ClassificationTypeDefinition ExpressionOperatorType = null;

    /// <summary>
    /// Classification type definition for expression functions.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ExpressionFunction)]
    internal static ClassificationTypeDefinition ExpressionFunctionType = null;

    /// <summary>
    /// Classification type definition for expression keywords.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ExpressionKeyword)]
    internal static ClassificationTypeDefinition ExpressionKeywordType = null;

    /// <summary>
    /// Classification type definition for expression literals.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ExpressionLiteral)]
    internal static ClassificationTypeDefinition ExpressionLiteralType = null;

    /// <summary>
    /// Classification type definition for expression directives.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ExpressionDirective)]
    internal static ClassificationTypeDefinition ExpressionDirectiveType = null;

    /// <summary>
    /// Classification type definition for expression built-in properties.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(ExpressionBuiltin)]
    internal static ClassificationTypeDefinition ExpressionBuiltinType = null;

    /// <summary>
    /// Classification type definition for property-argument highlights.
    /// </summary>
    [Export(typeof(ClassificationTypeDefinition))]
    [Name(PropertyArgumentHighlight)]
    internal static ClassificationTypeDefinition PropertyArgumentHighlightType = null;
}