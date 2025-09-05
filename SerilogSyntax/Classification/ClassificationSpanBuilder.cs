using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Diagnostics;
using SerilogSyntax.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SerilogSyntax.Classification;

/// <summary>
/// Builds classification spans for Serilog template properties and expression regions.
/// </summary>
internal class ClassificationSpanBuilder(IClassificationTypeRegistryService registry)
{
    private readonly IClassificationType _propertyNameType = registry.GetClassificationType(SerilogClassificationTypes.PropertyName);
    private readonly IClassificationType _destructureOperatorType = registry.GetClassificationType(SerilogClassificationTypes.DestructureOperator);
    private readonly IClassificationType _stringifyOperatorType = registry.GetClassificationType(SerilogClassificationTypes.StringifyOperator);
    private readonly IClassificationType _formatSpecifierType = registry.GetClassificationType(SerilogClassificationTypes.FormatSpecifier);
    private readonly IClassificationType _propertyBraceType = registry.GetClassificationType(SerilogClassificationTypes.PropertyBrace);
    private readonly IClassificationType _positionalIndexType = registry.GetClassificationType(SerilogClassificationTypes.PositionalIndex);
    private readonly IClassificationType _alignmentType = registry.GetClassificationType(SerilogClassificationTypes.Alignment);
    private readonly IClassificationType _expressionPropertyType = registry.GetClassificationType(SerilogClassificationTypes.ExpressionProperty);
    private readonly IClassificationType _expressionOperatorType = registry.GetClassificationType(SerilogClassificationTypes.ExpressionOperator);
    private readonly IClassificationType _expressionFunctionType = registry.GetClassificationType(SerilogClassificationTypes.ExpressionFunction);
    private readonly IClassificationType _expressionKeywordType = registry.GetClassificationType(SerilogClassificationTypes.ExpressionKeyword);
    private readonly IClassificationType _expressionLiteralType = registry.GetClassificationType(SerilogClassificationTypes.ExpressionLiteral);
    private readonly IClassificationType _expressionDirectiveType = registry.GetClassificationType(SerilogClassificationTypes.ExpressionDirective);
    private readonly IClassificationType _expressionBuiltinType = registry.GetClassificationType(SerilogClassificationTypes.ExpressionBuiltin);

    /// <summary>
    /// Adds classification spans for a template property.
    /// </summary>
    public void AddPropertyClassifications(
        List<ClassificationSpan> classifications,
        ITextSnapshot snapshot,
        int offsetInSnapshot,
        TemplateProperty property)
    {
        try
        {
            // Classify braces
            if (_propertyBraceType != null)
            {
                // Opening brace
                var openBraceSpan = new SnapshotSpan(snapshot,
                    offsetInSnapshot + property.BraceStartIndex, 1);
                classifications.Add(new ClassificationSpan(openBraceSpan, _propertyBraceType));

                // Closing brace
                var closeBraceSpan = new SnapshotSpan(snapshot,
                    offsetInSnapshot + property.BraceEndIndex, 1);
                classifications.Add(new ClassificationSpan(closeBraceSpan, _propertyBraceType));
            }

            // Classify operators
            if (property.Type == PropertyType.Destructured && _destructureOperatorType != null)
            {
                var operatorSpan = new SnapshotSpan(snapshot,
                    offsetInSnapshot + property.OperatorIndex, 1);
                classifications.Add(new ClassificationSpan(operatorSpan, _destructureOperatorType));
            }
            else if (property.Type == PropertyType.Stringified && _stringifyOperatorType != null)
            {
                var operatorSpan = new SnapshotSpan(snapshot,
                    offsetInSnapshot + property.OperatorIndex, 1);
                classifications.Add(new ClassificationSpan(operatorSpan, _stringifyOperatorType));
            }

            // Classify property name
            var classificationType = property.Type == PropertyType.Positional
                ? _positionalIndexType
                : _propertyNameType;

            if (classificationType != null)
            {
                var nameSpan = new SnapshotSpan(snapshot,
                    offsetInSnapshot + property.StartIndex, property.Length);
                classifications.Add(new ClassificationSpan(nameSpan, classificationType));
            }

            // Classify alignment
            if (!string.IsNullOrEmpty(property.Alignment) && _alignmentType != null)
            {
                var alignmentSpan = new SnapshotSpan(snapshot,
                    offsetInSnapshot + property.AlignmentStartIndex,
                    property.Alignment.Length);
                classifications.Add(new ClassificationSpan(alignmentSpan, _alignmentType));
            }

            // Classify format specifier
            if (!string.IsNullOrEmpty(property.FormatSpecifier) && _formatSpecifierType != null)
            {
                var formatSpan = new SnapshotSpan(snapshot,
                    offsetInSnapshot + property.FormatStartIndex,
                    property.FormatSpecifier.Length);
                classifications.Add(new ClassificationSpan(formatSpan, _formatSpecifierType));
            }
        }
        catch
        {
            // Ignore individual property classification errors
        }
    }

    /// <summary>
    /// Adds classification spans for expression regions.
    /// </summary>
    public void AddExpressionClassifications(
        List<ClassificationSpan> classifications,
        ITextSnapshot snapshot,
        int offsetInSnapshot,
        IEnumerable<ClassifiedRegion> regions)
    {
#if DEBUG
        var regionsList = regions.ToList();
        DiagnosticLogger.Log($"AddExpressionClassifications: Processing {regionsList.Count} regions at offset {offsetInSnapshot}");
#endif
        foreach (var region in regions)
        {
            IClassificationType classificationType = region.ClassificationType switch
            {
                SerilogClassificationTypes.ExpressionProperty => _expressionPropertyType,
                SerilogClassificationTypes.ExpressionOperator => _expressionOperatorType,
                SerilogClassificationTypes.ExpressionFunction => _expressionFunctionType,
                SerilogClassificationTypes.ExpressionKeyword => _expressionKeywordType,
                SerilogClassificationTypes.ExpressionLiteral => _expressionLiteralType,
                SerilogClassificationTypes.ExpressionDirective => _expressionDirectiveType,
                SerilogClassificationTypes.ExpressionBuiltin => _expressionBuiltinType,
                SerilogClassificationTypes.FormatSpecifier => _formatSpecifierType,
                SerilogClassificationTypes.PropertyBrace => _propertyBraceType,
                SerilogClassificationTypes.PropertyName => _propertyNameType,
                _ => null
            };

            if (classificationType != null)
            {
                try
                {
                    var span = new SnapshotSpan(snapshot, offsetInSnapshot + region.Start, region.Length);
                    var classificationSpan = new ClassificationSpan(span, classificationType);
                    classifications.Add(classificationSpan);
#if DEBUG
                    DiagnosticLogger.Log($"  Added classification: {region.ClassificationType} at {offsetInSnapshot + region.Start}, " +
                        $"len {region.Length} = '{region.Text}'");
#endif
                }
                catch (Exception ex)
                {
#if DEBUG
                    DiagnosticLogger.Log($"  Failed to add classification: {region.ClassificationType} at {offsetInSnapshot + region.Start}, " +
                        $"len {region.Length} = '{region.Text}' - Error: {ex.Message}");
#else
                    _ = ex; // Suppress unused variable warning
#endif
                    // Ignore classification errors for individual regions
                }
            }
#if DEBUG
            else
            {
                DiagnosticLogger.Log($"  Skipped region (null classification type): {region.ClassificationType} at {region.Start}, " +
                    $"len {region.Length} = '{region.Text}'");
            }
#endif
        }
    }
}