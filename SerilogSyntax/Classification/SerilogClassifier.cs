using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Parsing;
using SerilogSyntax.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        // Only invalidate cache for changed spans
        var changedSpans = new List<SnapshotSpan>();
        
        foreach (var change in e.Changes)
        {
            // Calculate the affected span in the new snapshot
            var start = change.NewPosition;
            var end = start + change.NewLength;
            
            // Extend to line boundaries for context
            var startLine = e.After.GetLineFromPosition(start);
            var endLine = e.After.GetLineFromPosition(Math.Min(end, e.After.Length - 1));
            
            var affectedSpan = new SnapshotSpan(
                startLine.Start,
                endLine.EndIncludingLineBreak);
            
            changedSpans.Add(affectedSpan);
        }
        
        // Remove only affected spans from cache
        InvalidateCacheForSpans(changedSpans);
        
        // Update snapshot reference
        lock (_cacheLock)
        {
            _lastSnapshot = e.After;
        }
        
        // Raise classification changed for affected areas only
        foreach (var span in changedSpans)
        {
            ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(span));
        }
    }
    
    /// <summary>
    /// Invalidates cache entries that overlap with the given spans.
    /// </summary>
    /// <param name="spans">The spans to invalidate cache for.</param>
    private void InvalidateCacheForSpans(List<SnapshotSpan> spans)
    {
        var keysToRemove = new List<SnapshotSpan>();
        
        lock (_cacheLock)
        {
            foreach (var cachedSpan in _classificationCache.Keys)
            {
                foreach (var changedSpan in spans)
                {
                    if (cachedSpan.OverlapsWith(changedSpan))
                    {
                        keysToRemove.Add(cachedSpan);
                        break;
                    }
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _classificationCache.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Event raised when classifications have changed.
    /// </summary>
    public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

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
                // Check if we might be inside a multi-line verbatim string
                if (text.Contains("{") && text.Contains("}"))
                {
                    // Look backwards to see if we're inside an unclosed verbatim string
                    if (IsInsideVerbatimString(span))
                    {
                        // We're inside a verbatim string! Parse the current span for properties
                        var properties = GetCachedTemplateProperties(text);
                        
                        // Create classifications for properties in this span
                        foreach (var property in properties)
                        {
                            // Properties are relative to the start of this span's text
                            int offsetInSnapshot = span.Start;
                            AddPropertyClassifications(classifications, span.Snapshot, offsetInSnapshot, property);
                        }
                    }
                }
                
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
                    var (literalStart, literalEnd, templateText, isVerbatim) = stringLiteral.Value;
                    
                    // Use cached template parsing if available
                    var properties = GetCachedTemplateProperties(templateText);
                    
                    // Create classification spans for each property element
                    foreach (var property in properties)
                    {
                        // Adjust indices to account for the string literal position
                        // Verbatim strings (@"...") need +2, regular strings ("...") need +1
                        int offsetInSnapshot = literalStart + (isVerbatim ? 2 : 1);
                        
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
    /// <returns>A tuple containing the start position, end position, content, and whether it's a verbatim string, or null if not found.</returns>
    private (int start, int end, string text, bool isVerbatim)? FindStringLiteral(string text, int startIndex, int spanStart)
    {
        // Look for string literal after Serilog method call
        int parenDepth = 1;
        
        while (startIndex < text.Length && parenDepth > 0)
        {
            // Skip whitespace
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
                startIndex++;
            
            if (startIndex >= text.Length)
                return null;
            
            // Check for different string literal types
            if (TryParseStringLiteral(text, startIndex, out var result))
            {
                // Determine if it's a verbatim string
                bool isVerbatim = startIndex < text.Length - 1 && text[startIndex] == '@' && text[startIndex + 1] == '"';
                return (spanStart + result.Start, spanStart + result.End, result.Content, isVerbatim);
            }
            
            // Track parenthesis depth
            if (text[startIndex] == '(')
                parenDepth++;
            else if (text[startIndex] == ')')
            {
                parenDepth--;
                if (parenDepth == 0)
                    return null;
            }
            
            startIndex++;
        }
        
        return null;
    }

    /// <summary>
    /// Attempts to parse any type of string literal (regular, verbatim, or interpolated).
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="startIndex">The starting index of the string literal.</param>
    /// <param name="result">The parsed string boundaries and content.</param>
    /// <returns>True if a string literal was successfully parsed; otherwise, false.</returns>
    private bool TryParseStringLiteral(string text, int startIndex, out (int Start, int End, string Content) result)
    {
        result = default;
        
        // Check for verbatim string @"..."
        if (startIndex < text.Length - 1 && text[startIndex] == '@' && text[startIndex + 1] == '"')
        {
            return TryParseVerbatimString(text, startIndex, out result);
        }
        
        // Check for interpolated string $"..." (skip for Serilog)
        if (startIndex < text.Length - 1 && text[startIndex] == '$' && text[startIndex + 1] == '"')
        {
            // Serilog doesn't use interpolated strings, but we should handle them gracefully
            return false;
        }
        
        // Check for regular string "..."
        if (startIndex < text.Length && text[startIndex] == '"')
        {
            return TryParseRegularString(text, startIndex, out result);
        }
        
        return false;
    }

    /// <summary>
    /// Parses a verbatim string literal (@"...").
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="startIndex">The starting index of the @ symbol.</param>
    /// <param name="result">The parsed string boundaries and content.</param>
    /// <returns>True if successfully parsed; otherwise, false.</returns>
    private bool TryParseVerbatimString(string text, int startIndex, out (int Start, int End, string Content) result)
    {
        result = default;
        int contentStart = startIndex + 2; // Skip @"
        int current = contentStart;
        
        while (current < text.Length)
        {
            if (text[current] == '"')
            {
                // Check for escaped quote ""
                if (current + 1 < text.Length && text[current + 1] == '"')
                {
                    current += 2;
                    continue;
                }
                
                // Found the end
                result = (startIndex, current, text.Substring(contentStart, current - contentStart));
                return true;
            }
            current++;
        }
        
        // Incomplete string - return what we have
        if (current > contentStart)
        {
            result = (startIndex, current, text.Substring(contentStart, current - contentStart));
            return true;
        }
        
        return false;
    }

    private bool TryParseRegularString(string text, int startIndex, out (int Start, int End, string Content) result)
    {
        result = default;
        int contentStart = startIndex + 1; // Skip "
        int current = contentStart;
        bool escaped = false;
        
        while (current < text.Length)
        {
            if (escaped)
            {
                escaped = false;
                current++;
                continue;
            }
            
            if (text[current] == '\\')
            {
                escaped = true;
                current++;
                continue;
            }
            
            if (text[current] == '"')
            {
                // Found the end
                result = (startIndex, current, text.Substring(contentStart, current - contentStart));
                return true;
            }
            
            current++;
        }
        
        // Incomplete string - return what we have
        if (current > contentStart)
        {
            result = (startIndex, current, text.Substring(contentStart, current - contentStart));
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Checks if the given span is inside an unclosed verbatim string literal.
    /// </summary>
    /// <param name="span">The span to check.</param>
    /// <returns>True if the span is inside a verbatim string; otherwise, false.</returns>
    private bool IsInsideVerbatimString(SnapshotSpan span)
    {
        try
        {
            var snapshot = span.Snapshot;
            var currentLine = snapshot.GetLineFromPosition(span.Start);
            var lineNumber = currentLine.LineNumber;
            
            // Look backwards up to 10 lines to find an unclosed verbatim string
            for (int i = lineNumber - 1; i >= Math.Max(0, lineNumber - 10); i--)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                var lineText = line.GetText();
                
                // Check if this line contains a Serilog call with @"
                if (SerilogCallDetector.IsSerilogCall(lineText) && lineText.Contains("@\""))
                {
                    // Check if the verbatim string is unclosed on this line
                    var atIndex = lineText.IndexOf("@\"");
                    if (atIndex >= 0)
                    {
                        // Count quotes after @" to see if string is closed
                        var afterAt = lineText.Substring(atIndex + 2);
                        int quoteCount = 0;
                        bool inEscapedQuote = false;
                        
                        foreach (char c in afterAt)
                        {
                            if (c == '"')
                            {
                                if (inEscapedQuote)
                                {
                                    inEscapedQuote = false; // This is the second " of ""
                                }
                                else
                                {
                                    // Check if this is the start of ""
                                    inEscapedQuote = true;
                                    quoteCount++;
                                }
                            }
                            else
                            {
                                inEscapedQuote = false;
                            }
                        }
                        
                        // If we have an odd number of quotes (or no closing quote), the string is unclosed
                        if (quoteCount == 0 || !afterAt.TrimEnd().EndsWith("\""))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // If anything goes wrong, assume we're not in a verbatim string
        }
        
        return false;
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