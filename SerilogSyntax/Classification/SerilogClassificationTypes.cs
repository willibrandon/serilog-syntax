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
}