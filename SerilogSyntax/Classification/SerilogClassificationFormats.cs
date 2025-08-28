using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace SerilogSyntax.Classification
{
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
            ForegroundColor = Color.FromRgb(0x56, 0x9C, 0xD6); // Light blue (#569CD6)
        }
    }

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
            ForegroundColor = Color.FromRgb(0xDC, 0xDC, 0xAA); // Yellow (#DCDCAA)
        }
    }

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
            ForegroundColor = Color.FromRgb(0xDC, 0xDC, 0xAA); // Yellow (#DCDCAA)
        }
    }

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
            ForegroundColor = Color.FromRgb(0x4E, 0xC9, 0xB0); // Light green (#4EC9B0)
        }
    }

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
            ForegroundColor = Color.FromRgb(0xDA, 0x70, 0xD6); // Light orchid - slightly brighter than string
        }
    }

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
            ForegroundColor = Color.FromRgb(0xC5, 0x86, 0xC0); // Light purple (#C586C0)
        }
    }

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
            ForegroundColor = Color.FromRgb(0x4E, 0xC9, 0xB0); // Light green (#4EC9B0) - same as format
        }
    }
}