using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace SerilogSyntax.Classification
{
    internal static class SerilogClassificationTypes
    {
        public const string PropertyName = "serilog.property.name";
        public const string DestructureOperator = "serilog.operator.destructure";
        public const string StringifyOperator = "serilog.operator.stringify";
        public const string FormatSpecifier = "serilog.format";
        public const string PropertyBrace = "serilog.brace";
        public const string PositionalIndex = "serilog.index";
        public const string Alignment = "serilog.alignment";

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(PropertyName)]
        internal static ClassificationTypeDefinition PropertyNameType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(DestructureOperator)]
        internal static ClassificationTypeDefinition DestructureOperatorType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(StringifyOperator)]
        internal static ClassificationTypeDefinition StringifyOperatorType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(FormatSpecifier)]
        internal static ClassificationTypeDefinition FormatSpecifierType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(PropertyBrace)]
        internal static ClassificationTypeDefinition PropertyBraceType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(PositionalIndex)]
        internal static ClassificationTypeDefinition PositionalIndexType = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(Alignment)]
        internal static ClassificationTypeDefinition AlignmentType = null;
    }
}