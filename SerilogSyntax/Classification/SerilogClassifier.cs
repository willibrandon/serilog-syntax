using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Diagnostics;
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
/// <remarks>
/// ARCHITECTURE: This classifier uses a dual-approach for handling string literals:
/// 
/// 1. SINGLE-LINE STRINGS (regular, verbatim, and single-line raw strings):
///    - Detected via FindStringLiteral() 
///    - Parsed completely within TryParseRawStringLiteral/TryParseVerbatimString/TryParseRegularString
///    - Properties extracted and classified immediately
/// 
/// 2. MULTI-LINE STRINGS (multi-line verbatim and raw strings):
///    - Detected via IsInsideVerbatimString() and IsInsideRawStringLiteral()
///    - Uses line-by-line scanning because VS may only provide partial spans during editing
///    - Scans backward to find opening delimiters, forward to find closing
///    - Results cached in _rawStringRegionCache for performance
/// 
/// This dual approach is necessary because Visual Studio's incremental parsing may provide
/// only the currently edited line, not the complete multi-line string context.
/// 
/// PERFORMANCE OPTIMIZATIONS:
/// - Template parsing results cached in _templateCache
/// - Classification spans cached in _classificationCache
/// - Raw string region detection cached in _rawStringRegionCache
/// - Smart cache invalidation only clears affected lines based on change type
/// </remarks>
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

    // Constants for magic numbers
    private const int MaxLookbackLines = 20;
    private const int MaxLookforwardLines = 50;
    private const int MaxVerbatimStringLookbackLines = 10;
    
    // Performance optimizations
    private readonly ConcurrentDictionary<string, List<TemplateProperty>> _templateCache = new();
    private readonly object _cacheLock = new();
    private ITextSnapshot _lastSnapshot;
    private readonly ConcurrentDictionary<SnapshotSpan, List<ClassificationSpan>> _classificationCache = new();
    private readonly ConcurrentDictionary<int, bool> _rawStringRegionCache = new(); // Line number -> is inside raw string


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
        var snapshot = e.After;
        
        // Smart invalidation for raw string region cache
        var linesToInvalidate = new HashSet<int>();
        
        foreach (var change in e.Changes)
        {
            // Calculate the affected span in the new snapshot
            var start = change.NewPosition;
            var end = start + change.NewLength;
            
            // Extend to line boundaries for context
            var startLine = snapshot.GetLineFromPosition(start);
            var endLine = snapshot.GetLineFromPosition(Math.Min(end, snapshot.Length - 1));
            
            var affectedSpan = new SnapshotSpan(
                startLine.Start,
                endLine.EndIncludingLineBreak);
            
            changedSpans.Add(affectedSpan);
            
            // Smart cache invalidation: check if this change could affect raw string boundaries
            var startLineNumber = startLine.LineNumber;
            var endLineNumber = endLine.LineNumber;
            
            // Check each affected line for raw string delimiters
            bool hasRawStringDelimiters = false;
            for (int lineNum = startLineNumber; lineNum <= endLineNumber; lineNum++)
            {
                if (lineNum < snapshot.LineCount)
                {
                    var lineText = snapshot.GetLineFromLineNumber(lineNum).GetText();
                    if (lineText.Contains("\"\"\""))
                    {
                        hasRawStringDelimiters = true;
                        break;
                    }
                }
            }
            
            if (hasRawStringDelimiters)
            {
                // This change involves raw string delimiters - need wider invalidation
                int invalidateStart = Math.Max(0, startLineNumber - MaxLookbackLines);
                int invalidateEnd = Math.Min(snapshot.LineCount - 1, endLineNumber + MaxLookforwardLines);
                
                for (int i = invalidateStart; i <= invalidateEnd; i++)
                {
                    linesToInvalidate.Add(i);
                }
            }
            else
            {
                // Normal change - only invalidate the changed lines and a small window
                for (int i = Math.Max(0, startLineNumber - 2); i <= Math.Min(snapshot.LineCount - 1, endLineNumber + 2); i++)
                {
                    linesToInvalidate.Add(i);
                }
            }
        }
        
        // Remove only affected spans from classification cache
        InvalidateCacheForSpans(changedSpans);
        
        // Invalidate raw string region cache for affected lines
        foreach (var lineNum in linesToInvalidate)
        {
            _rawStringRegionCache.TryRemove(lineNum, out _);
        }
        
        // Update snapshot reference
        lock (_cacheLock)
        {
            _lastSnapshot = snapshot;
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
        // Initialize diagnostics on first call
#if DEBUG
        DiagnosticLogger.Initialize();
#endif
        
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
            
#if DEBUG
            // Only log if processing raw strings (useful for debugging)
            if (text.Contains("\"\"\""))
            {
                var currentLine = span.Snapshot.GetLineFromPosition(span.Start);
                DiagnosticLogger.Log($"Processing raw string at line {currentLine.LineNumber}");
            }
#endif
            
            // Early exit if no Serilog calls detected - avoids expensive regex on irrelevant text
            if (string.IsNullOrWhiteSpace(text) || !SerilogCallDetector.IsSerilogCall(text))
            {
                // Check if we might be inside a multi-line string
                if (text.Contains("{") && text.Contains("}"))
                {
                    // Check if we're inside a multi-line string
                    
                    // Look backwards to see if we're inside an unclosed verbatim or raw string
                    bool insideString = false;
                    
                    if (IsInsideVerbatimString(span))
                    {
                        insideString = true;
                        // Inside verbatim string
                    }
                    else if (IsInsideRawStringLiteral(span))
                    {
                        insideString = true;
                        // Inside raw string literal
                    }
                    
                    if (insideString)
                    {
                        // We're inside a multi-line string! Parse the current span for properties
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
                    var (literalStart, literalEnd, templateText, isVerbatim, quoteCount) = stringLiteral.Value;
                    
                    // Use cached template parsing if available
                    var properties = GetCachedTemplateProperties(templateText);
                    
                    // Create classification spans for each property element
                    foreach (var property in properties)
                    {
                        // Adjust indices to account for the string literal position
                        // Raw strings ("""...""") need +quoteCount
                        // Verbatim strings (@"...") need +2
                        // Regular strings ("...") need +1
                        int offsetInSnapshot = literalStart + (quoteCount > 0 ? quoteCount : (isVerbatim ? 2 : 1));
                        
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
    /// <returns>A tuple containing the start position, end position, content, whether it's a verbatim string, and the quote count for raw strings, or null if not found.</returns>
    private (int start, int end, string text, bool isVerbatim, int quoteCount)? FindStringLiteral(string text, int startIndex, int spanStart)
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
                // Determine string type
                bool isVerbatim = startIndex < text.Length - 1 && text[startIndex] == '@' && text[startIndex + 1] == '"';
                
                // Check if it's a raw string literal
                int quoteCount = 0;
                if (!isVerbatim && startIndex < text.Length && text[startIndex] == '"')
                {
                    // Count consecutive quotes to detect raw strings
                    int pos = startIndex;
                    while (pos < text.Length && text[pos] == '"')
                    {
                        quoteCount++;
                        pos++;
                    }
                    // If less than 3 quotes, it's a regular string, not raw
                    if (quoteCount < 3)
                        quoteCount = 0;
                }
                
                return (spanStart + result.Start, spanStart + result.End, result.Content, isVerbatim, quoteCount);
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
        
        // Check for verbatim string @"..." FIRST
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
        
        // Check for quotes (could be regular or raw string)
        if (startIndex < text.Length && text[startIndex] == '"')
        {
            // Count quotes to determine if it's raw string (3+) or regular (1)
            int quoteCount = 0;
            int pos = startIndex;
            while (pos < text.Length && text[pos] == '"')
            {
                quoteCount++;
                pos++;
            }
            
            if (quoteCount >= 3)
            {
                // Raw string literal
                return TryParseRawStringLiteral(text, startIndex, out result);
            }
            else
            {
                // Regular string literal
                return TryParseRegularString(text, startIndex, out result);
            }
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
    /// Attempts to parse a raw string literal ("""...""").
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="startIndex">The starting index in the text.</param>
    /// <param name="result">The parsed result containing start, end, and content.</param>
    /// <returns>True if a raw string literal was successfully parsed; otherwise, false.</returns>
    private bool TryParseRawStringLiteral(string text, int startIndex, out (int Start, int End, string Content) result)
    {
        result = default;
        
        // Count opening quotes (minimum 3 for raw string)
        int quoteCount = 0;
        int pos = startIndex;
        while (pos < text.Length && text[pos] == '"')
        {
            quoteCount++;
            pos++;
        }
        
        if (quoteCount < 3)
        {
            return false; // Not a raw string literal
        }
        
        // Find matching closing quotes
        int contentStart = pos;
        int searchPos = pos;
        
        while (searchPos < text.Length)
        {
            if (text[searchPos] == '"')
            {
                // Count consecutive quotes
                int closeCount = 0;
                int closePos = searchPos;
                while (closePos < text.Length && text[closePos] == '"')
                {
                    closeCount++;
                    closePos++;
                }
                
                if (closeCount >= quoteCount)
                {
                    // Found matching closing quotes
                    result = (startIndex, searchPos + quoteCount - 1, 
                             text.Substring(contentStart, searchPos - contentStart));
                    return true;
                }
                
                // Skip these quotes and continue
                searchPos = closePos;
            }
            else
            {
                searchPos++;
            }
        }
        
        // Incomplete raw string - return what we have for error recovery
        if (searchPos > contentStart)
        {
            result = (startIndex, text.Length - 1, 
                     text.Substring(contentStart, text.Length - contentStart));
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
            
            // Look backwards up to MaxVerbatimStringLookbackLines to find an unclosed verbatim string
            for (int i = lineNumber - 1; i >= Math.Max(0, lineNumber - MaxVerbatimStringLookbackLines); i--)
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
    /// Checks if the given span is inside an unclosed raw string literal.
    /// Uses caching to avoid repeated expensive lookups for the same line numbers.
    /// </summary>
    /// <param name="span">The span to check.</param>
    /// <returns>True if the span is inside a raw string; otherwise, false.</returns>
    private bool IsInsideRawStringLiteral(SnapshotSpan span)
    {
        var snapshot = span.Snapshot;
        var currentLine = snapshot.GetLineFromPosition(span.Start);
        var lineNumber = currentLine.LineNumber;
        
        try
        {
            
            // Check cache first
            if (_rawStringRegionCache.TryGetValue(lineNumber, out bool cachedResult))
            {
                return cachedResult;
            }
            
            // Look at current line and backwards to find potential raw string starts
            // Start from current line to handle case where raw string starts on this line
            for (int i = lineNumber; i >= Math.Max(0, lineNumber - MaxLookbackLines); i--)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                var lineText = line.GetText();
                
                // Skip lines without triple quotes for performance
                if (!lineText.Contains("\"\"\""))
                    continue;
                
                // Find all occurrences of """ in this line
                int index = 0;
                while ((index = lineText.IndexOf("\"\"\"", index)) != -1)
                {
                    // Count quotes at this position
                    int quoteCount = 0;
                    int pos = index;
                    while (pos < lineText.Length && lineText[pos] == '"')
                    {
                        quoteCount++;
                        pos++;
                    }
                    
                    if (quoteCount >= 3)
                    {
                        // Check if this could be a Serilog raw string
                        // The line must contain a Serilog call before the quotes
                        var textBeforeQuotes = lineText.Substring(0, index);
                        bool isSerilogCall = index > 0 && SerilogCallDetector.IsSerilogCall(textBeforeQuotes);
                        
                        if (isSerilogCall)
                        {
                            // Check where this raw string closes
                            int closingLine = GetRawStringClosingLine(snapshot, i, quoteCount);
                            
                            // If closing line is -1 (not closed) or after current line, we're inside
                            if (closingLine == -1 || closingLine > lineNumber)
                            {
                                _rawStringRegionCache.TryAdd(lineNumber, true);
                                return true;
                            }
                        }
                    }
                    
                    index = pos;
                }
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            DiagnosticLogger.LogException(ex, "IsInsideRawStringLiteral");
#else
            _ = ex; // Suppress warning in Release
#endif
        }
        
        _rawStringRegionCache.TryAdd(lineNumber, false);
        return false;
    }

    /// <summary>
    /// Checks if a raw string literal starting at the given line is closed and returns the closing line number.
    /// </summary>
    /// <param name="snapshot">The text snapshot.</param>
    /// <param name="startLineNumber">The line number where the raw string starts.</param>
    /// <param name="quoteCount">The number of opening quotes.</param>
    /// <returns>The line number where the raw string closes, or -1 if not closed.</returns>
    private int GetRawStringClosingLine(ITextSnapshot snapshot, int startLineNumber, int quoteCount)
    {
        
        var startLine = snapshot.GetLineFromLineNumber(startLineNumber);
        var startLineText = startLine.GetText();
        
        // Find the opening quotes position
        int openQuoteIndex = startLineText.IndexOf(new string('"', quoteCount));
        if (openQuoteIndex < 0)
        {
            // Could not find opening quotes - assume closed on same line
            return startLineNumber;
        }
        
        // Check if there's content after the opening quotes on the same line
        int afterOpenQuotes = openQuoteIndex + quoteCount;
        string afterOpen = startLineText.Substring(afterOpenQuotes).Trim();
        
        
        // If there's nothing after the opening quotes, it's a multi-line raw string
        if (string.IsNullOrWhiteSpace(afterOpen))
        {
            // Multi-line raw string (nothing after opening quotes)
            
            // Look for closing quotes on subsequent lines
            for (int i = startLineNumber + 1; i < Math.Min(snapshot.LineCount, startLineNumber + MaxLookforwardLines); i++)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                var lineText = line.GetText();
                
                // Find the first non-whitespace position for proper indentation handling
                int nonWhitespaceIndex = 0;
                while (nonWhitespaceIndex < lineText.Length && char.IsWhiteSpace(lineText[nonWhitespaceIndex]))
                    nonWhitespaceIndex++;
                
                // Check if we have enough room for the closing quotes
                if (nonWhitespaceIndex + quoteCount <= lineText.Length)
                {
                    // Check for exact quote match at this position
                    bool isClosing = true;
                    for (int q = 0; q < quoteCount; q++)
                    {
                        if (lineText[nonWhitespaceIndex + q] != '"')
                        {
                            isClosing = false;
                            break;
                        }
                    }
                    
                    if (isClosing)
                    {
                        // Verify no extra quotes (that would mean it's not the closing)
                        if (nonWhitespaceIndex + quoteCount < lineText.Length && 
                            lineText[nonWhitespaceIndex + quoteCount] == '"')
                            continue;
                        
                        // For multi-line raw strings, closing quotes followed by comma and parameters IS valid
                        // e.g., """, recordId, status); is a valid closing
                        return i; // Found the closing line
                    }
                }
            }
            
            // No closing quotes found - raw string is unclosed
            return -1;
        }
        else
        {
            // Single-line raw string - look for closing quotes on the same line
            
            // Look for closing quotes after the content
            int searchPos = afterOpenQuotes;
            while (searchPos <= startLineText.Length - quoteCount)
            {
                bool foundClosing = true;
                for (int k = 0; k < quoteCount; k++)
                {
                    if (searchPos + k >= startLineText.Length || startLineText[searchPos + k] != '"')
                    {
                        foundClosing = false;
                        break;
                    }
                }
                
                if (foundClosing)
                {
                    // Check if there are more quotes (would mean it's not the closing)
                    if (searchPos + quoteCount < startLineText.Length && startLineText[searchPos + quoteCount] == '"')
                    {
                        searchPos++;
                        continue;
                    }
                    
                    return startLineNumber; // Closed on same line
                }
                searchPos++;
            }
            
            // No closing quotes found on same line - raw string is unclosed
            return -1;
        }
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
        _rawStringRegionCache.Clear();
    }
}