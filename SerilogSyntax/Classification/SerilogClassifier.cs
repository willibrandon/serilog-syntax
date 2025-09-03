using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Diagnostics;
using SerilogSyntax.Expressions;
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

    // Template classification types
    private readonly IClassificationType _propertyNameType;
    private readonly IClassificationType _destructureOperatorType;
    private readonly IClassificationType _stringifyOperatorType;
    private readonly IClassificationType _formatSpecifierType;
    private readonly IClassificationType _propertyBraceType;
    private readonly IClassificationType _positionalIndexType;
    private readonly IClassificationType _alignmentType;

    // Expression classification types
    private readonly IClassificationType _expressionPropertyType;
    private readonly IClassificationType _expressionOperatorType;
    private readonly IClassificationType _expressionFunctionType;
    private readonly IClassificationType _expressionKeywordType;
    private readonly IClassificationType _expressionLiteralType;
    private readonly IClassificationType _expressionDirectiveType;
    private readonly IClassificationType _expressionBuiltinType;

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

        // Get template classification types
        _propertyNameType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.PropertyName);
        _destructureOperatorType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.DestructureOperator);
        _stringifyOperatorType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.StringifyOperator);
        _formatSpecifierType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.FormatSpecifier);
        _propertyBraceType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.PropertyBrace);
        _positionalIndexType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.PositionalIndex);
        _alignmentType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.Alignment);

        // Get expression classification types
        _expressionPropertyType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.ExpressionProperty);
        _expressionOperatorType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.ExpressionOperator);
        _expressionFunctionType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.ExpressionFunction);
        _expressionKeywordType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.ExpressionKeyword);
        _expressionLiteralType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.ExpressionLiteral);
        _expressionDirectiveType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.ExpressionDirective);
        _expressionBuiltinType = _classificationRegistry.GetClassificationType(SerilogClassificationTypes.ExpressionBuiltin);

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

        var spanText = span.GetText();
        if (spanText.Contains("Message"))
        {
            DiagnosticLogger.Log($"  DEBUG: GetClassificationSpans called with span containing 'Message': " +
                $"'{spanText.Replace("\r", "\\r").Replace("\n", "\\n")}'");
        }
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
                // Check if we might be inside a multi-line string or an ExpressionTemplate
                if (text.Contains("{") || text.Contains("}"))
                {
                    // Check if we're inside a multi-line string
                    var currentLine = span.Snapshot.GetLineFromPosition(span.Start);

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
                    else
                    {
                        // Check if this is a continuation of an ExpressionTemplate call
                        // Look at previous lines to see if there's an unclosed ExpressionTemplate
                        for (int i = currentLine.LineNumber - 1; i >= Math.Max(0, currentLine.LineNumber - 10); i--)
                        {
                            var checkLine = span.Snapshot.GetLineFromLineNumber(i);
                            var checkText = checkLine.GetText();

                            if (checkText.Contains("new ExpressionTemplate("))
                            {
                                // Found ExpressionTemplate on a previous line
                                // Now check if the current line is a string literal that could be part of it
                                // We need a smarter check - if current line starts with quotes and contains template syntax
                                var trimmedCurrentText = text.TrimStart();

                                // Check if this line looks like a template string
                                if ((trimmedCurrentText.StartsWith("\"") ||
                                     trimmedCurrentText.StartsWith("@\"") ||
                                     trimmedCurrentText.StartsWith("\"\"\"")) &&
                                    (text.Contains("{") || text.Contains("}")))
                                {
                                    // This looks like a template string that could be part of ExpressionTemplate
                                    insideString = true;
                                    break;
                                }

                                // Also check if we're in the middle of string concatenation
                                // (line ends with + or starts with string and has +)
                                bool previousLineEndsWithPlus = false;
                                if (i < currentLine.LineNumber - 1)
                                {
                                    var prevLine = span.Snapshot.GetLineFromLineNumber(currentLine.LineNumber - 1);
                                    var prevText = prevLine.GetText().TrimEnd();
                                    previousLineEndsWithPlus = prevText.EndsWith("+");
                                }

                                if (previousLineEndsWithPlus && trimmedCurrentText.StartsWith("\""))
                                {
                                    // We're in a concatenated string that's part of ExpressionTemplate
                                    insideString = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (insideString)
                    {
#if DEBUG
                        if (span.GetText().Contains("Message"))
                        {
                            DiagnosticLogger.Log($"  DEBUG: Message span processing - insideString = true, using Roslyn analysis");
                        }
#endif
                        // Use Roslyn syntax tree analysis to determine if this string literal
                        // is actually part of a Serilog method call or just a string argument
                        // containing Serilog-like text (like in test code)

                        // Find the actual string literal position when we're inside a multi-line string
                        int positionToCheck = FindStringLiteralPosition(span.Snapshot, span.Start, currentLine);

                        bool isActuallySerilogTemplate = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(
                            span.Snapshot, positionToCheck);

                        if (isActuallySerilogTemplate)
                        {
                            // Check if we're inside an ExpressionTemplate
                            bool isExpressionTemplate = false;

                            // Look backwards to find if this is an ExpressionTemplate
                            for (int i = currentLine.LineNumber - 1; i >= Math.Max(0, currentLine.LineNumber - 10); i--)
                            {
                                var checkLine = span.Snapshot.GetLineFromLineNumber(i);
                                var checkText = checkLine.GetText();

                                if (checkText.Contains("new ExpressionTemplate("))
                                {
                                    isExpressionTemplate = true;
                                    break;
                                }
                            }

                            if (isExpressionTemplate)
                            {
                                // Parse as expression template
                                DiagnosticLogger.Log($"[SerilogClassifier] Parsing ExpressionTemplate text: '{text}'");
                                var parser = new ExpressionParser(text);
                                var expressionRegions = parser.ParseExpressionTemplate();

                                // Create classifications for expression regions
                                int offsetInSnapshot = span.Start;
                                DiagnosticLogger.Log($"[SerilogClassifier] Adding {expressionRegions.Count()} expression classifications at offset {offsetInSnapshot}");
                                AddExpressionClassifications(classifications, span.Snapshot, offsetInSnapshot, expressionRegions);
                            }
                            else
                            {
                                // We're inside a multi-line string that is actually a Serilog template
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
                        else
                        {
                            // Skipping property classification for line - not a real Serilog template
                        }
                    }
                }

                _classificationCache.TryAdd(span, classifications);
                return classifications;
            }

            // Find Serilog method calls
            var matches = SerilogCallDetector.FindAllSerilogCalls(text);

#if DEBUG
            if (text.Contains("Message"))
            {
                DiagnosticLogger.Log($"  DEBUG: Message span processing - outside string literals, found {matches.Count} Serilog matches");
            }
#endif

#if DEBUG
            DiagnosticLogger.Log($"FindAllSerilogCalls found {matches.Count} matches in text of length {text.Length}");

            foreach (Match m in matches)
            {
                DiagnosticLogger.Log($"  Match at {m.Index}: '{m.Value}'");
            }
#endif

            var currentLine2 = span.Snapshot.GetLineFromPosition(span.Start);

            foreach (Match match in matches)
            {
                // Use SyntaxTreeAnalyzer to determine if this match is in a real Serilog call
                int matchPosition = span.Start + match.Index;
                bool isActuallySerilogCall = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(
                    span.Snapshot, matchPosition);

                if (!isActuallySerilogCall)
                {
                    // Skipping match - not a real Serilog call
                    continue;
                }

                // Special handling for WithComputed and ExpressionTemplate - they may have multiple string arguments
                bool isWithComputed = match.Value.Contains("WithComputed");
                bool isExpressionTemplate = match.Value.Contains("ExpressionTemplate");
                List<(int start, int end, string text, bool isVerbatim, int quoteCount)> allStringLiterals;

                if (isWithComputed || isExpressionTemplate)
                {
                    // Find ALL string literals in WithComputed/ExpressionTemplate call
                    allStringLiterals = FindAllStringLiteralsInMatch(text, match.Index + match.Length, span.Start);
                }
                else
                {
                    // Find just the first string literal (normal case)
                    int searchStart = match.Index + match.Length;
                    var stringLiteral = FindStringLiteral(text, searchStart, span.Start);
                    allStringLiterals = stringLiteral.HasValue ? new List<(int, int, string, bool, int)> { stringLiteral.Value } : [];
                }

#if DEBUG
                DiagnosticLogger.Log($"Looking for string literal after match at {match.Index}");
                DiagnosticLogger.Log($"  Match value: '{match.Value}'");
                DiagnosticLogger.Log($"  IsWithComputed: {isWithComputed}");
                DiagnosticLogger.Log($"  IsExpressionTemplate: {isExpressionTemplate}");
                DiagnosticLogger.Log($"  Search starting at {match.Index + match.Length} in text: " +
                    $"'{text.Substring(match.Index + match.Length, Math.Min(50, text.Length - match.Index - match.Length))}'");
                DiagnosticLogger.Log($"  String literal(s) found: {allStringLiterals.Count}");

                if (allStringLiterals.Count > 0)
                {
                    for (int i = 0; i < allStringLiterals.Count; i++)
                    {
                        var lit = allStringLiterals[i];
                        DiagnosticLogger.Log($"    Literal {i}: '{lit.text.Substring(0, Math.Min(50, lit.text.Length))}'");
                    }
                }
#endif

                // Process each string literal found
                foreach (var stringLiteral in allStringLiterals)
                {
                    var (literalStart, literalEnd, templateText, isVerbatim, quoteCount) = stringLiteral;

#if DEBUG
                    if (templateText.Contains("Message"))
                    {
                        DiagnosticLogger.Log($"  DEBUG: Entering loop to process literal: '{templateText}'");
                        DiagnosticLogger.Log($"  DEBUG: Destructured values - literalStart={literalStart}, " +
                            $"literalEnd={literalEnd}, isVerbatim={isVerbatim}, quoteCount={quoteCount}");
                    }
#endif

                    // Use SyntaxTreeAnalyzer to accurately determine expression context
                    var expressionContext = SyntaxTreeAnalyzer.GetExpressionContext(span.Snapshot, literalStart);

#if DEBUG
                    if (templateText.Contains("Message") || templateText.Contains("Serilog"))
                    {
                        DiagnosticLogger.Log($"  DEBUG: Expression context for literal at {literalStart}: {expressionContext}");
                        DiagnosticLogger.Log($"  DEBUG: Template text: '{templateText.Substring(0, Math.Min(50, templateText.Length))}'");
                    }
#endif

#if DEBUG
                    DiagnosticLogger.Log($"String literal at {literalStart}: " +
                        $"'{templateText.Substring(0, Math.Min(50, templateText.Length))}'");
                    var lineText = GetLineContainingPosition(span.Snapshot, literalStart);
                    var lineStartPos = GetLineStart(span.Snapshot, literalStart);
                    var positionInLine = literalStart - lineStartPos;
                    DiagnosticLogger.Log($"  Line text: '{lineText}'");
                    DiagnosticLogger.Log($"  Position in line: {positionInLine}");
                    DiagnosticLogger.Log($"  Expression context: {expressionContext}");
#endif

                    if (expressionContext != ExpressionContext.None)
                    {
#if DEBUG
                        if (templateText.Contains("Message"))
                        {
                            DiagnosticLogger.Log($"  DEBUG: Processing Message as EXPRESSION with context: {expressionContext}");
                        }
#endif
                        // Parse as expression
                        var expressionRegions = ParseExpression(templateText, expressionContext);

#if DEBUG
                        var regionsList = expressionRegions.ToList();
                        DiagnosticLogger.Log($"  Expression parser returned {regionsList.Count} regions for text: " +
                            $"'{templateText.Substring(0, Math.Min(100, templateText.Length))}'");

                        if (regionsList.Count > 0)
                        {
                            for (int i = 0; i < regionsList.Count; i++)
                            {
                                var region = regionsList[i];
                                var regionText = templateText.Substring(region.Start, Math.Min(region.Length, templateText.Length - region.Start));
                                DiagnosticLogger.Log($"    Region {i + 1}: {region.ClassificationType} at {region.Start}, " +
                                    $"length {region.Length} = '{regionText}'");
                            }
                        }
                        else
                        {
                            DiagnosticLogger.Log($"    WARNING: No regions returned for expression context {expressionContext}");
                        }
#endif

                        // Create classification spans for expression elements
                        int offsetInSnapshot = literalStart + (quoteCount > 0 ? quoteCount : (isVerbatim ? 2 : 1));
                        AddExpressionClassifications(classifications, span.Snapshot, offsetInSnapshot, expressionRegions);
                    }
                    else
                    {
                        // Parse as regular template
                        var properties = GetCachedTemplateProperties(templateText);

#if DEBUG
                        if (templateText.Contains("Message"))
                        {
                            DiagnosticLogger.Log($"  DEBUG: Processing regular template containing 'Message': '{templateText}'");
                            DiagnosticLogger.Log($"  DEBUG: Properties found: {properties.Count}");
                        }
#endif

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
                } // End of foreach stringLiteral in allStringLiterals
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            DiagnosticLogger.Log($"  DEBUG: Exception caught in GetClassificationSpans: {ex.Message}");
            DiagnosticLogger.Log($"  DEBUG: Exception stack trace: {ex.StackTrace}");
#else
            _ = ex; // Suppress unused variable warning
#endif
            // Swallow exceptions to avoid crashing the editor - return empty classifications on error
        }

        // Cache the result for future requests
        _classificationCache.TryAdd(span, classifications);

#if DEBUG
        if (spanText.Contains("Message"))
        {
            DiagnosticLogger.Log($"  DEBUG: Returning final result for Message span, count: {classifications.Count}");
        }
#endif

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
    private void AddPropertyClassifications(
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
    /// Finds the bounds and content of a string literal starting from the given index.
    /// </summary>
    /// <param name="text">The text to search in.</param>
    /// <param name="startIndex">The index to start searching from.</param>
    /// <param name="spanStart">The absolute position of the span start in the document.</param>
    /// <returns>
    /// A tuple containing the start position, end position, content, whether it's a verbatim string,
    /// and the quote count for raw strings, or null if not found.
    /// </returns>
    private (int start, int end, string text, bool isVerbatim, int quoteCount)? FindStringLiteral(
        string text,
        int startIndex,
        int spanStart)
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
    /// Finds all string literals in a method call (specifically for WithComputed which has multiple string arguments).
    /// </summary>
    /// <param name="text">The text to search in.</param>
    /// <param name="startIndex">The index to start searching from.</param>
    /// <param name="spanStart">The absolute position of the span start in the document.</param>
    /// <returns>A list of all string literals found in the method call.</returns>
    private List<(int start, int end, string text, bool isVerbatim, int quoteCount)> FindAllStringLiteralsInMatch(
        string text,
        int startIndex,
        int spanStart)
    {
        var results = new List<(int start, int end, string text, bool isVerbatim, int quoteCount)>();
        int parenDepth = 1;

        while (startIndex < text.Length && parenDepth > 0)
        {
            // Skip whitespace
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
                startIndex++;

            if (startIndex >= text.Length)
                break;

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

                results.Add((spanStart + result.Start, spanStart + result.End, result.Content, isVerbatim, quoteCount));

                // Move past the string literal
                startIndex = result.End + 1;
                continue;
            }

            // Track parenthesis depth
            if (text[startIndex] == '(')
                parenDepth++;
            else if (text[startIndex] == ')')
            {
                parenDepth--;
                if (parenDepth == 0)
                    break;
            }

            startIndex++;
        }

        return results;
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
    private bool TryParseVerbatimString(
        string text,
        int startIndex,
        out (int Start, int End, string Content) result)
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
                        if (quoteCount == 0 || afterAt.Trim().Length == 0 || !afterAt.TrimEnd().EndsWith("\""))
                        {
                            // Return true - erbatim string is unclosed
                            return true;
                        }
                        else
                        {
                            // Verbatim string is closed on same line
                        }
                    }
                }
            }
        }
        catch
        {
            // If anything goes wrong, assume we're not in a verbatim string
        }

        // Returning false - not inside verbatim string
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

            // Look backwards from the current line to find any raw string that might contain it
            // We need to check if lineNumber is inside any raw string literal
            // Check all lines from the start of the lookback range up to the current line
            bool foundContainingRawString = false;
            bool foundSerilogRawString = false;
            var processedRanges = new List<(int start, int end)>(); // Track processed raw string ranges

            for (int i = Math.Max(0, lineNumber - MaxLookbackLines); i <= lineNumber; i++)
            {
                // Skip lines that are already inside a processed raw string
                bool skipLine = false;
                foreach (var (start, end) in processedRanges)
                {
                    if (i > start && i <= end)
                    {
                        skipLine = true;
                        break;
                    }
                }
                if (skipLine) continue;

                var line = snapshot.GetLineFromLineNumber(i);
                var lineText = line.GetText();

                // Only process lines that contain """ to find raw string boundaries
                if (!lineText.Contains("\"\"\""))
                {
                    // Skip this line but continue looking backwards
                    continue;
                }

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
                        // Check where this raw string closes
                        int closingLine = GetRawStringClosingLine(snapshot, i, quoteCount);

                        // Record this raw string range to avoid processing lines inside it
                        if (closingLine != -1)
                        {
                            processedRanges.Add((i, closingLine));
                        }

                        // Check if lineNumber is inside this raw string
                        if (closingLine == -1 || closingLine > lineNumber)
                        {
                            // lineNumber is inside this raw string that starts on line i
                            foundContainingRawString = true;

                            // Check if the line that STARTS the raw string (line i) is a Serilog call
                            var textBeforeQuotes = lineText.Substring(0, index);
                            bool isSerilogCall = index > 0 && SerilogCallDetector.IsSerilogCall(textBeforeQuotes);

                            if (isSerilogCall)
                            {
                                foundSerilogRawString = true;
                                // Don't return immediately, check if there are other containing strings
                            }
                        }
                    }

                    index = pos;
                }
            }

            // After checking all potential containing raw strings:
            if (foundSerilogRawString)
            {
                // Found at least one Serilog raw string containing this line
                _rawStringRegionCache.TryAdd(lineNumber, true);
                return true;
            }
            else if (foundContainingRawString)
            {
                // Found containing raw strings but none were Serilog calls
                // This line is inside documentation strings only
                _rawStringRegionCache.TryAdd(lineNumber, false);
                return false;
            }
            // If no containing raw strings found, continue with normal processing
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
    /// Finds the position of the actual string literal when we're inside a multi-line string.
    /// This is needed because line-by-line processing passes positions that may not be inside
    /// the string literal itself, but inside the content of a multi-line string.
    /// </summary>
    private int FindStringLiteralPosition(ITextSnapshot snapshot, int currentPosition, ITextSnapshotLine currentLine)
    {
        try
        {
            // For verbatim strings, search backwards to find the @" or @"""
            var currentLineSpan = new SnapshotSpan(currentLine.Start, currentLine.End);
            if (IsInsideVerbatimString(currentLineSpan))
            {
                // Search backwards from current line to find the start of verbatim string
                for (int lineNum = currentLine.LineNumber; lineNum >= Math.Max(0, currentLine.LineNumber - 10); lineNum--)
                {
                    var line = snapshot.GetLineFromLineNumber(lineNum);
                    var lineText = line.GetText();

                    // Look for @" or @"""
                    int atIndex = lineText.IndexOf("@\"");
                    if (atIndex >= 0)
                    {
                        // Return position just after the opening quote
                        int stringLiteralPosition = line.Start + atIndex + 2;

                        // Found verbatim string
                        return stringLiteralPosition;
                    }
                }
            }

            // For raw strings, search backwards to find the """
            if (IsInsideRawStringLiteral(currentLineSpan))
            {
                // Search backwards to find the raw string start
                for (int lineNum = currentLine.LineNumber; lineNum >= Math.Max(0, currentLine.LineNumber - 10); lineNum--)
                {
                    var line = snapshot.GetLineFromLineNumber(lineNum);
                    var lineText = line.GetText();

                    // Look for """
                    int rawStringIndex = lineText.IndexOf("\"\"\"");
                    if (rawStringIndex >= 0)
                    {
                        // Return position just after the opening quotes
                        int stringLiteralPosition = line.Start + rawStringIndex + 3;

                        // Found raw string
                        return stringLiteralPosition;
                    }
                }
            }

            // If we can't find the string literal, return the current position
            return currentPosition;
        }
        catch
        {
            return currentPosition;
        }
    }

    /// <summary>
    /// Gets the text of the line containing the specified position.
    /// </summary>
    private string GetLineContainingPosition(ITextSnapshot snapshot, int position)
    {
        var line = snapshot.GetLineFromPosition(position);
        return line.GetText();
    }

    /// <summary>
    /// Gets the start position of the line containing the specified position.
    /// </summary>
    private int GetLineStart(ITextSnapshot snapshot, int position)
    {
        var line = snapshot.GetLineFromPosition(position);
        return line.Start.Position;
    }

    /// <summary>
    /// Parses an expression and returns classified regions.
    /// </summary>
    /// <param name="expression">The expression text to parse.</param>
    /// <param name="context">The expression context.</param>
    /// <returns>List of classified regions in the expression.</returns>
    private IEnumerable<ClassifiedRegion> ParseExpression(string expression, ExpressionContext context)
    {
#if DEBUG
        DiagnosticLogger.Log($"      ParseExpression called with context {context} and expression: '{expression}'");
#endif

        var parser = new ExpressionParser(expression);
        IEnumerable<ClassifiedRegion> result = context switch
        {
            // These all use the same general expression parsing
            ExpressionContext.FilterExpression or ExpressionContext.ComputedProperty or ExpressionContext.ConditionalExpression => parser.Parse(),

            // Expression templates have some additional syntax (literals, directives) so use a specialized parser
            ExpressionContext.ExpressionTemplate => parser.ParseExpressionTemplate(),
            _ => [],
        };

#if DEBUG
        var resultList = result.ToList();
        DiagnosticLogger.Log($"      ParseExpression returning {resultList.Count} regions");
        foreach (var region in resultList)
        {
            var regionText = expression.Substring(region.Start, Math.Min(region.Length, expression.Length - region.Start));
            DiagnosticLogger.Log($"        {region.ClassificationType}: '{regionText}' at {region.Start}, len {region.Length}");
        }
#endif

        return result;
    }

    /// <summary>
    /// Adds classification spans for expression regions.
    /// </summary>
    /// <param name="classifications">The list to add classification spans to.</param>
    /// <param name="snapshot">The text snapshot being classified.</param>
    /// <param name="offsetInSnapshot">The offset within the snapshot where the expression starts.</param>
    /// <param name="regions">The classified regions to add.</param>
    private void AddExpressionClassifications(
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