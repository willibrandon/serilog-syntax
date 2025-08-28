using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Parsing;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SerilogSyntax.Classification
{
    internal class SerilogClassifier : IClassifier
    {
        private readonly IClassificationTypeRegistryService _classificationRegistry;
        private readonly ITextBuffer _buffer;
        private readonly TemplateParser _parser;
        
        // Classification types
        private readonly IClassificationType _propertyNameType;
        private readonly IClassificationType _destructureOperatorType;
        private readonly IClassificationType _stringifyOperatorType;
        private readonly IClassificationType _formatSpecifierType;
        private readonly IClassificationType _propertyBraceType;
        private readonly IClassificationType _positionalIndexType;
        private readonly IClassificationType _alignmentType;

        // Regex to find Serilog method calls and configuration - matches logging methods and outputTemplate parameters
        private static readonly Regex SerilogCallRegex = new Regex(
            @"(?:\b\w+\.(?:ForContext(?:<[^>]+>)?\([^)]*\)\.)?(?:Log(?:Verbose|Debug|Information|Warning|Error|Critical|Fatal)|(?:Verbose|Debug|Information|Warning|Error|Fatal|Write))\s*\()|(?:outputTemplate\s*:\s*)",
            RegexOptions.Compiled);

        public SerilogClassifier(ITextBuffer buffer, IClassificationTypeRegistryService classificationRegistry)
        {
            _buffer = buffer;
            _classificationRegistry = classificationRegistry;
            _parser = new TemplateParser();

            // Get classification types
            _propertyNameType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.PropertyName);
            _destructureOperatorType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.DestructureOperator);
            _stringifyOperatorType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.StringifyOperator);
            _formatSpecifierType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.FormatSpecifier);
            _propertyBraceType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.PropertyBrace);
            _positionalIndexType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.PositionalIndex);
            _alignmentType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.Alignment);
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged { add { } remove { } }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var classifications = new List<ClassificationSpan>();
            
            // Get the text from the span
            string text = span.GetText();
            
            // Find Serilog method calls
            var matches = SerilogCallRegex.Matches(text);
            
            foreach (Match match in matches)
            {
                // Find the string literal after the method call
                int searchStart = match.Index + match.Length;
                var stringLiteral = FindStringLiteral(text, searchStart, span.Start);
                
                if (stringLiteral.HasValue)
                {
                    var (literalStart, literalEnd, templateText) = stringLiteral.Value;
                    
                    // Parse the template
                    var properties = _parser.Parse(templateText);
                    
                    // Create classification spans for each property element
                    foreach (var property in properties)
                    {
                        // Adjust indices to account for the string literal position
                        int offsetInSnapshot = literalStart + 1; // +1 to skip opening quote
                        
                        // Classify braces
                        if (_propertyBraceType != null)
                        {
                            // Opening brace
                            var openBraceSpan = new SnapshotSpan(span.Snapshot, 
                                offsetInSnapshot + property.BraceStartIndex, 1);
                            classifications.Add(new ClassificationSpan(openBraceSpan, _propertyBraceType));
                            
                            // Closing brace
                            var closeBraceSpan = new SnapshotSpan(span.Snapshot,
                                offsetInSnapshot + property.BraceEndIndex, 1);
                            classifications.Add(new ClassificationSpan(closeBraceSpan, _propertyBraceType));
                        }
                        
                        // Classify operators
                        if (property.Type == PropertyType.Destructured && _destructureOperatorType != null)
                        {
                            var operatorSpan = new SnapshotSpan(span.Snapshot,
                                offsetInSnapshot + property.OperatorIndex, 1);
                            classifications.Add(new ClassificationSpan(operatorSpan, _destructureOperatorType));
                        }
                        else if (property.Type == PropertyType.Stringified && _stringifyOperatorType != null)
                        {
                            var operatorSpan = new SnapshotSpan(span.Snapshot,
                                offsetInSnapshot + property.OperatorIndex, 1);
                            classifications.Add(new ClassificationSpan(operatorSpan, _stringifyOperatorType));
                        }
                        
                        // Classify property name
                        var classificationType = property.Type == PropertyType.Positional 
                            ? _positionalIndexType 
                            : _propertyNameType;
                            
                        if (classificationType != null)
                        {
                            var nameSpan = new SnapshotSpan(span.Snapshot,
                                offsetInSnapshot + property.StartIndex, property.Length);
                            classifications.Add(new ClassificationSpan(nameSpan, classificationType));
                        }
                        
                        // Classify alignment
                        if (!string.IsNullOrEmpty(property.Alignment) && _alignmentType != null)
                        {
                            var alignmentSpan = new SnapshotSpan(span.Snapshot,
                                offsetInSnapshot + property.AlignmentStartIndex,
                                property.Alignment.Length);
                            classifications.Add(new ClassificationSpan(alignmentSpan, _alignmentType));
                        }
                        
                        // Classify format specifier
                        if (!string.IsNullOrEmpty(property.FormatSpecifier) && _formatSpecifierType != null)
                        {
                            var formatSpan = new SnapshotSpan(span.Snapshot,
                                offsetInSnapshot + property.FormatStartIndex,
                                property.FormatSpecifier.Length);
                            classifications.Add(new ClassificationSpan(formatSpan, _formatSpecifierType));
                        }
                    }
                }
            }
            
            return classifications;
        }

        private (int start, int end, string text)? FindStringLiteral(string text, int startIndex, int spanStart)
        {
            // Skip whitespace
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
                startIndex++;

            if (startIndex >= text.Length || text[startIndex] != '"')
                return null;

            // Check if it's a verbatim string or interpolated string
            if (startIndex > 0)
            {
                if (text[startIndex - 1] == '@') // Verbatim string
                    return null; // Skip for now - could be supported later
                if (text[startIndex - 1] == '$') // Interpolated string
                    return null; // Serilog doesn't use interpolated strings
            }

            // Find the end of the string literal
            int endIndex = startIndex + 1;
            bool escaped = false;
            
            while (endIndex < text.Length)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (text[endIndex] == '\\')
                {
                    escaped = true;
                }
                else if (text[endIndex] == '"')
                {
                    // Found the end
                    string literalContent = text.Substring(startIndex + 1, endIndex - startIndex - 1);
                    return (spanStart + startIndex, spanStart + endIndex, literalContent);
                }
                endIndex++;
            }

            // Incomplete string literal - return what we have so far
            if (endIndex > startIndex + 1)
            {
                string literalContent = text.Substring(startIndex + 1, endIndex - startIndex - 1);
                return (spanStart + startIndex, spanStart + endIndex, literalContent);
            }
            
            return null;
        }
    }
}