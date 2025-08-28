using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Parsing;
using SerilogSyntax.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SerilogSyntax.Classification;

/// <summary>
/// Provides syntax classification for Serilog message templates within string literals.
/// Identifies and classifies properties, operators, format specifiers, and other template elements.
/// </summary>
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
    private readonly ConcurrentDictionary<string, List<TemplateProperty>> _templateCache = new();
    private readonly object _cacheLock = new();
    private ITextSnapshot _lastSnapshot;
    private readonly ConcurrentDictionary<SnapshotSpan, List<ClassificationSpan>> _classificationCache = new();


    /// <summary>
    /// Initializes a new instance of the <see cref="SerilogClassifier"/> class.
    /// </summary>
    /// <param name="buffer">The text buffer to classify.</param>
    /// <param name="classificationRegistry">The classification type registry service.</param>
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

    /// <summary>
    /// Handles buffer change events to invalidate cached results.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The text content changed event arguments.</param>
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

    /// <summary>
    /// Event raised when classifications have changed. Currently not implemented as classifications are computed on-demand.
    /// </summary>
    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged { add { } remove { } }

    /// <summary>
    /// Gets the classification spans that overlap the given span of text.
    /// </summary>
    /// <param name="span">The span of text to classify.</param>
    /// <returns>A list of classification spans for Serilog template elements within the given span.</returns>
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

    /// <summary>
    /// Gets parsed template properties from cache or parses and caches them.
    /// </summary>
    /// <param name="template">The template string to parse.</param>
    /// <returns>List of template properties found in the template.</returns>
    private List<TemplateProperty> GetCachedTemplateProperties(string template)
    {
        // Use template cache to avoid re-parsing identical templates
        return _templateCache.GetOrAdd(template, t =>
        {
            try
            {
                return [.. _parser.Parse(t)];
            }
            catch
            {
                // Return empty list on parse error
                return [];
            }
        });
    }

    /// <summary>
    /// Adds classification spans for a single template property and its components.
    /// </summary>
    /// <param name="classifications">The list to add classification spans to.</param>
    /// <param name="snapshot">The text snapshot being classified.</param>
    /// <param name="offsetInSnapshot">The offset within the snapshot where the template starts.</param>
    /// <param name="property">The template property to classify.</param>
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

    /// <summary>
    /// Finds the bounds and content of a string literal starting from the given index.
    /// </summary>
    /// <param name="text">The text to search in.</param>
    /// <param name="startIndex">The index to start searching from.</param>
    /// <param name="spanStart">The absolute position of the span start in the document.</param>
    /// <returns>A tuple containing the start position, end position, and content of the string literal, or null if not found.</returns>
    private (int start, int end, string text)? FindStringLiteral(string text, int startIndex, int spanStart)
    {
        // Look for the first string literal, which might not be immediately after the method call
        // (e.g., LogError(ex, "message") has an exception parameter first)
        int parenDepth = 1; // We're already inside the first parenthesis from the method call
        
        while (startIndex < text.Length && parenDepth > 0)
        {
            // Skip whitespace
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
                startIndex++;
            
            if (startIndex >= text.Length)
                return null;
            
            // Check if we found a string literal
            if (text[startIndex] == '"')
                break;
            
            // Track parenthesis depth to handle nested calls
            if (text[startIndex] == '(')
                parenDepth++;
            else if (text[startIndex] == ')')
            {
                parenDepth--;
                if (parenDepth == 0)
                    return null; // Reached the end of the method call without finding a string
            }
            
            // Skip this character and continue looking
            startIndex++;
        }
        
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

    /// <summary>
    /// Disposes resources and unsubscribes from buffer events.
    /// </summary>
    public void Dispose()
    {
        // Clean up event handlers to prevent memory leaks
        _buffer.Changed -= OnBufferChanged;
        
        // Clear caches
        _templateCache.Clear();
        _classificationCache.Clear();
    }
}