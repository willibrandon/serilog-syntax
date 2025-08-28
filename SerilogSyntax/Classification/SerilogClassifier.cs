using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Parsing;
using SerilogSyntax.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SerilogSyntax.Classification
{
    internal class SerilogClassifier : IClassifier, IDisposable
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

        // Performance optimizations
        private readonly ConcurrentDictionary<string, List<TemplateProperty>> _templateCache = new ConcurrentDictionary<string, List<TemplateProperty>>();
        private readonly object _cacheLock = new object();
        private ITextSnapshot _lastSnapshot;
        private ConcurrentDictionary<SnapshotSpan, List<ClassificationSpan>> _classificationCache = new ConcurrentDictionary<SnapshotSpan, List<ClassificationSpan>>();


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

            // Set up cache invalidation on buffer changes
            _buffer.Changed += OnBufferChanged;
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            // Clear caches when buffer changes - this prevents stale cached results
            lock (_cacheLock)
            {
                if (_lastSnapshot != e.After)
                {
                    _templateCache.Clear();
                    _classificationCache.Clear();
                    _lastSnapshot = e.After;
                }
            }
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged { add { } remove { } }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            // Check cache first - avoid expensive operations if possible
            if (_classificationCache.TryGetValue(span, out List<ClassificationSpan> cachedResult))
            {
                return cachedResult;
            }

            var classifications = new List<ClassificationSpan>();
            
            try
            {
                // Get the text from the span
                string text = span.GetText();
                
                // Early exit if no Serilog calls detected - avoids expensive regex on irrelevant text
                if (string.IsNullOrWhiteSpace(text) || !SerilogCallDetector.IsSerilogCall(text))
                {
                    _classificationCache.TryAdd(span, classifications);
                    return classifications;
                }
                
                // Find Serilog method calls
                var matches = SerilogCallDetector.FindAllSerilogCalls(text);
                
                foreach (Match match in matches)
                {
                    // Find the string literal after the method call
                    int searchStart = match.Index + match.Length;
                    var stringLiteral = FindStringLiteral(text, searchStart, span.Start);
                    
                    if (stringLiteral.HasValue)
                    {
                        var (literalStart, literalEnd, templateText) = stringLiteral.Value;
                        
                        // Use cached template parsing if available
                        var properties = GetCachedTemplateProperties(templateText);
                        
                        // Create classification spans for each property element
                        foreach (var property in properties)
                        {
                            // Adjust indices to account for the string literal position
                            int offsetInSnapshot = literalStart + 1; // +1 to skip opening quote
                            
                            AddPropertyClassifications(classifications, span.Snapshot, offsetInSnapshot, property);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Swallow exceptions to avoid crashing the editor - return empty classifications on error
            }
            
            // Cache the result for future requests
            _classificationCache.TryAdd(span, classifications);
            return classifications;
        }

        private List<TemplateProperty> GetCachedTemplateProperties(string template)
        {
            // Use template cache to avoid re-parsing identical templates
            return _templateCache.GetOrAdd(template, t =>
            {
                try
                {
                    return _parser.Parse(t).ToList();
                }
                catch
                {
                    // Return empty list on parse error
                    return new List<TemplateProperty>();
                }
            });
        }

        private void AddPropertyClassifications(List<ClassificationSpan> classifications, ITextSnapshot snapshot, int offsetInSnapshot, TemplateProperty property)
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

        public void Dispose()
        {
            // Clean up event handlers to prevent memory leaks
            _buffer.Changed -= OnBufferChanged;
            
            // Clear caches
            _templateCache.Clear();
            _classificationCache.Clear();
        }
    }
}