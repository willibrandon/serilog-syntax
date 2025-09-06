using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Diagnostics;
using SerilogSyntax.Expressions;
using SerilogSyntax.Parsing;
using SerilogSyntax.Utilities;
using System;
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
///    - Results cached in MultiLineStringDetector for performance
/// 
/// This dual approach is necessary because Visual Studio's incremental parsing may provide
/// only the currently edited line, not the complete multi-line string context.
/// 
/// PERFORMANCE OPTIMIZATIONS:
/// - Template parsing results cached via CacheManager
/// - Classification spans cached via CacheManager
/// - Raw string region detection cached in MultiLineStringDetector
/// - Smart cache invalidation only clears affected lines based on change type
/// </remarks>
internal class SerilogClassifier : IClassifier, IDisposable
{
    private readonly IClassificationTypeRegistryService _classificationRegistry;
    private readonly ITextBuffer _buffer;
    private readonly TemplateParser _parser;
    private readonly StringLiteralParser _stringLiteralParser;
    private readonly MultiLineStringDetector _multiLineStringDetector;
    private readonly CacheManager _cacheManager;
    private readonly ClassificationSpanBuilder _spanBuilder;


    // Constants for magic numbers
    private const int MaxLookbackLines = 20;
    private const int MaxLookforwardLines = 50;
    private const int MaxVerbatimStringLookbackLines = 10;
    // When detecting concatenated strings, look back up to 5 lines to find the Serilog call
    private const int MaxConcatenationLookbackLines = 5;
    
    // Static compiled regex for finding string literals
    private static readonly Regex StringLiteralRegex = new(@"""[^""]*""", RegexOptions.Compiled);

    // Performance optimizations
    private ITextSnapshot _lastSnapshot;


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
        _stringLiteralParser = new StringLiteralParser();
        _multiLineStringDetector = new MultiLineStringDetector();
        _cacheManager = new CacheManager(_parser);
        _spanBuilder = new ClassificationSpanBuilder(_classificationRegistry);

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
        _cacheManager.InvalidateCacheForSpans(changedSpans);

        // Invalidate raw string region cache for affected lines
        foreach (var lineNum in linesToInvalidate)
        {
            _multiLineStringDetector.InvalidateCacheForLine(lineNum);
        }

        // Update snapshot reference
        _lastSnapshot = snapshot;

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
        if (_cacheManager.TryGetCachedClassifications(span, out List<ClassificationSpan> cachedResult))
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
            bool isSerilogCall = !string.IsNullOrWhiteSpace(text) && SerilogCallDetector.IsSerilogCall(text);
            
#if DEBUG
            if (text.Contains("ForContext") && text.Contains(".Information"))
            {
                DiagnosticLogger.Log($"[SerilogClassifier] ForContext pattern detected, isSerilogCall={isSerilogCall}, " +
                    $"span length={span.Length}");
                DiagnosticLogger.Log($"[SerilogClassifier] Text snippet: '{text.Substring(0, Math.Min(200, text.Length))
                    .Replace("\r", "\\r").Replace("\n", "\\n")}'");
            }
#endif
            
            // If the regex-based detector fails, use SyntaxTreeAnalyzer as a fallback for complex cases
            // like multi-line ForContext chains
            if (!isSerilogCall && !string.IsNullOrWhiteSpace(text) && (text.Contains("ForContext")
                || text.Contains(".Information")
                || text.Contains(".Debug")
                || text.Contains(".Warning")
                || text.Contains(".Error")))
            {
#if DEBUG
                DiagnosticLogger.Log($"[SerilogClassifier] Using SyntaxTreeAnalyzer fallback");
#endif
                // Check if any string literal in the span is inside a Serilog template
                // Look for the first quote to find a string literal position
                int stringLiteralPos = -1;
                int quoteIndex = text.IndexOf('"');
                if (quoteIndex >= 0)
                {
                    stringLiteralPos = span.Start + quoteIndex + 1; // Position just inside the string literal
                    isSerilogCall = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(span.Snapshot, stringLiteralPos);
#if DEBUG
                    DiagnosticLogger.Log($"[SerilogClassifier] SyntaxTreeAnalyzer result: {isSerilogCall}");
#endif
                }
            }
            
            if (!isSerilogCall)
            {
                // Check if we might be inside a multi-line string or an ExpressionTemplate
                if (text.Contains("{") || text.Contains("}"))
                {
                    // Check if we're inside a multi-line string
                    var currentLine = span.Snapshot.GetLineFromPosition(span.Start);

                    // Look backwards to see if we're inside an unclosed verbatim or raw string
                    bool insideString = false;
                    bool isOutputTemplate = false;

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
                        // Check if this is a concatenated string that might be part of a Serilog call
                        // This happens when VS sends us just a fragment like: "for user {UserId} " +
                        var trimmedText = text.TrimStart();
                        bool looksLikeConcatenatedTemplate = false;
                        
                        // Check if this line looks like a concatenated string with template syntax
                        // It could end with + (continuation) or , (last string in concatenation)
                        if ((trimmedText.StartsWith("\"") || trimmedText.StartsWith("@\"")) &&
                            text.Contains("{") && text.Contains("}"))
                        {
                            // Check if this looks like it's part of a concatenation
                            // Look for explicit concatenation operators or ending comma
                            if (text.TrimEnd().EndsWith("+") || 
                                text.TrimEnd().EndsWith("\" +") ||
                                text.TrimEnd().EndsWith("\",") ||  // Last string in concatenation
                                (trimmedText.StartsWith("\"") && (text.Contains(",") || text.Contains(")"))))  // String followed by args or closing paren
                            {
                                looksLikeConcatenatedTemplate = true;
#if DEBUG
                                DiagnosticLogger.Log($"Detected potential concatenated template fragment: '{text.Trim()}'");
#endif
                            }
                        }
                        
                        if (looksLikeConcatenatedTemplate)
                        {
                            // Look at previous lines to see if there's a Serilog call that started the concatenation
                            for (int i = currentLine.LineNumber - 1; i >= Math.Max(0, currentLine.LineNumber - MaxConcatenationLookbackLines); i--)
                            {
                                var checkLine = span.Snapshot.GetLineFromLineNumber(i);
                                var checkText = checkLine.GetText();
                                
                                // Check if this line contains a Serilog call that might be using string concatenation
                                if (SerilogCallDetector.IsSerilogCall(checkText) && 
                                    (checkText.TrimEnd().EndsWith("+") || checkText.Contains("\" +")))
                                {
#if DEBUG
                                    DiagnosticLogger.Log($"Found Serilog call with concatenation on line {i}: '{checkText.Trim()}'");
#endif
                                    insideString = true;
                                    break;
                                }
                                
                                // Also check if this is a multi-line outputTemplate pattern
                                // Where outputTemplate: is on the previous line and template string is on current line
                                if (checkText.Contains("outputTemplate:") && i == currentLine.LineNumber - 1)
                                {
#if DEBUG
                                    DiagnosticLogger.Log($"Found outputTemplate: on previous line {i}, treating as Serilog template");
#endif
                                    insideString = true;
                                    // Mark this specifically as an outputTemplate pattern so we skip Roslyn verification
                                    isOutputTemplate = true;
                                    break;
                                }
                            }
                        }
                        
                        // Also check if this is a continuation of an ExpressionTemplate call
                        // Look at previous lines to see if there's an unclosed ExpressionTemplate
                        for (int i = currentLine.LineNumber - 1; i >= Math.Max(0, currentLine.LineNumber - 10); i--)
                        {
                            var checkLine = span.Snapshot.GetLineFromLineNumber(i);
                            var checkText = checkLine.GetText();

                            if (checkText.Contains("new ExpressionTemplate("))
                            {
                                // Found ExpressionTemplate on a previous line
                                // Now check if the current line is a string literal that could be part of it
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

                        // If we already determined this is an outputTemplate, skip Roslyn verification
                        bool isActuallySerilogTemplate = isOutputTemplate || 
                            SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(span.Snapshot, positionToCheck);

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
                                _spanBuilder.AddExpressionClassifications(classifications, span.Snapshot, offsetInSnapshot, expressionRegions);
                            }
                            else
                            {
                                // We're inside a multi-line string that is actually a Serilog template
                                var properties = _cacheManager.GetCachedTemplateProperties(text);

                                // Create classifications for properties in this span
                                foreach (var property in properties)
                                {
                                    // Properties are relative to the start of this span's text
                                    int offsetInSnapshot = span.Start;
                                    _spanBuilder.AddPropertyClassifications(classifications, span.Snapshot, offsetInSnapshot, property);
                                }
                            }
                        }
                        else
                        {
                            // Skipping property classification for line - not a real Serilog template
                        }
                    }
                }

                _cacheManager.CacheClassifications(span, classifications);
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

            // Track positions we've already processed to avoid duplicates
            var processedPositions = new HashSet<int>();

            // If we know it's a Serilog call (from SyntaxTreeAnalyzer) but regex found no matches,
            // find and process all string literals in the span
            if (matches.Count == 0 && isSerilogCall)
            {
#if DEBUG
                DiagnosticLogger.Log($"[SerilogClassifier] No regex matches but isSerilogCall=true, looking for string literals");
#endif
                // Find all string literals in the text
                var stringMatches = StringLiteralRegex.Matches(text);
                
#if DEBUG
                DiagnosticLogger.Log($"[SerilogClassifier] Found {stringMatches.Count} string literals");
#endif
                
                foreach (Match stringMatch in stringMatches)
                {
                    // Skip if we've already processed this position in multi-line ForContext handling
                    if (processedPositions.Contains(stringMatch.Index))
                    {
#if DEBUG
                        DiagnosticLogger.Log($"[SerilogClassifier] Skipping already processed position {stringMatch.Index}");
#endif
                        continue;
                    }
                    
                    // Check if this string literal contains template syntax
                    var stringContent = stringMatch.Value;
                    if (stringContent.Contains("{") && stringContent.Contains("}"))
                    {
                        // Parse this as a template
                        var templateText = stringContent.Trim('"');
#if DEBUG
                        DiagnosticLogger.Log($"[SerilogClassifier] Processing template: '{templateText}'");
#endif
                        var properties = _parser.Parse(templateText).ToList();
                        
                        if (properties.Count > 0)
                        {
#if DEBUG
                            DiagnosticLogger.Log($"[SerilogClassifier] Found {properties.Count} properties in template");
#endif
                            // Add classifications for the properties
                            foreach (var property in properties)
                            {
                                _spanBuilder.AddPropertyClassifications(
                                    classifications,
                                    span.Snapshot,
                                    span.Start + stringMatch.Index + 1, // +1 to skip opening quote
                                    property);
                            }
                        }
                    }
                }
            }
            
            // Special handling for ForContext patterns that span multiple lines
            // These won't be matched by the main regex but need to be processed
            // BUT skip this if we already processed via the fallback (matches.Count == 0 && isSerilogCall)
            if (!(matches.Count == 0 && isSerilogCall) && text.Contains("ForContext")
                && (text.Contains(".Information") || text.Contains(".Debug")
                    || text.Contains(".Warning") || text.Contains(".Error")))
            {
#if DEBUG
                DiagnosticLogger.Log($"[SerilogClassifier] Checking for multi-line ForContext patterns");
#endif
                // Look for patterns like:
                // someVar.ForContext<T>()
                //     .Information("template", ...)
                var multiLineMatches = SerilogCallDetector.FindMultiLineForContextCalls(text);
                
#if DEBUG
                DiagnosticLogger.Log($"[SerilogClassifier] Found {multiLineMatches.Count} multi-line ForContext patterns");
#endif
                
                foreach (Match mlMatch in multiLineMatches)
                {
                    if (mlMatch.Groups.Count > 2)
                    {
                        var templateText = mlMatch.Groups[2].Value;
                        // Find the position of this template in the text
                        var templatePattern = "\"" + Regex.Escape(templateText) + "\"";
                        var templateMatch = Regex.Match(text.Substring(mlMatch.Index), templatePattern);
                        if (templateMatch.Success)
                        {
                            var templateStartInText = mlMatch.Index + templateMatch.Index;
                            
                            // Track this position so we don't process it again
                            processedPositions.Add(templateStartInText);
                            
#if DEBUG
                            DiagnosticLogger.Log($"[SerilogClassifier] Processing multi-line template at position {templateStartInText}: " +
                                $"'{templateText}'");
#endif
                            if (templateText.Contains("{") && templateText.Contains("}"))
                            {
                                var properties = _parser.Parse(templateText).ToList();
                                
                                if (properties.Count > 0)
                                {
                                    var templateStartInSnapshot = span.Start + templateStartInText + 1; // +1 to skip opening quote
                                    
#if DEBUG
                                    DiagnosticLogger.Log($"[SerilogClassifier] Adding {properties.Count} properties from multi-line ForContext");
#endif
                                    foreach (var property in properties)
                                    {
                                        _spanBuilder.AddPropertyClassifications(
                                            classifications,
                                            span.Snapshot,
                                            templateStartInSnapshot,
                                            property);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Special handling for outputTemplate patterns that span multiple lines
            // Where outputTemplate: is on one line and the template string is on the next
            // BUT only if we haven't already processed these templates through normal matching
            // Also check if we're processing just a string literal that might be part of a multi-line outputTemplate
            if (text.Contains("outputTemplate:") || (text.Trim().StartsWith("\"") && span.Start.Position > 0))
            {
#if DEBUG
                DiagnosticLogger.Log($"[SerilogClassifier] Checking for multi-line outputTemplate patterns");
#endif
                
                // If we're processing a single line that starts with a string literal,
                // check if the previous line contains outputTemplate:
                var textToSearch = text;
                if (!text.Contains("outputTemplate:") && text.Trim().StartsWith("\""))
                {
                    // Look at the previous line
                    var currentLine = span.Snapshot.GetLineFromPosition(span.Start);
                    if (currentLine.LineNumber > 0)
                    {
                        var previousLine = span.Snapshot.GetLineFromLineNumber(currentLine.LineNumber - 1);
                        var previousText = previousLine.GetText();
                        if (previousText.Contains("outputTemplate:"))
                        {
                            // Combine previous line with current for pattern matching
                            textToSearch = previousText + "\r\n" + text;
#if DEBUG
                            DiagnosticLogger.Log($"[SerilogClassifier] Found outputTemplate: on previous line, combining for search");
#endif
                        }
                    }
                }
                
                var outputTemplateMatches = SerilogCallDetector.FindMultiLineOutputTemplateCalls(textToSearch);
                
#if DEBUG
                DiagnosticLogger.Log($"[SerilogClassifier] Found {outputTemplateMatches.Count} multi-line outputTemplate patterns");
#endif
                
                foreach (Match otMatch in outputTemplateMatches)
                {
                    if (otMatch.Groups.Count > 1)
                    {
                        var templateText = otMatch.Groups[1].Value;
                        // Find the exact position of this template in the text
                        // If we combined with previous line, adjust the position
                        var templateStartInText = otMatch.Index + otMatch.Value.IndexOf('"');
                        
                        // If we added previous line text, we need to adjust position back to current span
                        if (textToSearch != text && !text.Contains("outputTemplate:"))
                        {
                            // The match position includes the previous line, so subtract it
                            var previousLineLength = textToSearch.Length - text.Length;
                            templateStartInText -= previousLineLength;
                            
                            // If the template position is negative, it means the template is on current line
                            if (templateStartInText < 0)
                            {
                                // The template starts at the beginning of current line
                                templateStartInText = text.IndexOf('"');
                            }
                        }
                        
                        // Check if this template was already processed by the normal regex matching
                        // This happens when processing full spans that include both lines
                        bool alreadyProcessed = false;
                        
                        // Check if there's already a match that would have processed this template
                        foreach (Match existingMatch in matches)
                        {
                            // If there's an outputTemplate match that's close to this position,
                            // it means the template was already processed
                            if (existingMatch.Value.Contains("outputTemplate") && 
                                Math.Abs(existingMatch.Index - otMatch.Index) < 100)
                            {
                                alreadyProcessed = true;
#if DEBUG
                                DiagnosticLogger.Log($"[SerilogClassifier] Skipping multi-line outputTemplate at {templateStartInText} - " +
                                    $"already processed by normal matching");
#endif
                                break;
                            }
                        }
                        
                        // Only process if not already handled and not in our processed positions set
                        if (!alreadyProcessed && !processedPositions.Contains(templateStartInText))
                        {
                            processedPositions.Add(templateStartInText);
                            
#if DEBUG
                            DiagnosticLogger.Log($"[SerilogClassifier] Processing multi-line outputTemplate at position " +
                                $"{templateStartInText}: {templateText}'");
#endif
                            if (templateText.Contains("{") && templateText.Contains("}"))
                            {
                                var properties = _parser.Parse(templateText).ToList();
                                
                                if (properties.Count > 0)
                                {
                                    var templateStartInSnapshot = span.Start + templateStartInText + 1; // +1 to skip opening quote
                                    
#if DEBUG
                                    DiagnosticLogger.Log($"[SerilogClassifier] Adding {properties.Count} properties from multi-line " +
                                        $"outputTemplate");
#endif
                                    foreach (var property in properties)
                                    {
                                        _spanBuilder.AddPropertyClassifications(
                                            classifications,
                                            span.Snapshot,
                                            templateStartInSnapshot,
                                            property);
                                    }
                                }
                            }
                        }
                    }
                }
            }

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
                    allStringLiterals = _stringLiteralParser.FindAllStringLiteralsInMatch(text, match.Index + match.Length, span.Start);
                }
                else
                {
                    // For regular Serilog calls, check if we have string concatenation
                    int searchStart = match.Index + match.Length;
                    
                    // Look ahead to see if there's string concatenation
                    bool hasConcatenation = false;
                    int checkPos = searchStart;
                    int parenDepth = 1;
                    bool inString = false;
                    
                    while (checkPos < text.Length && parenDepth > 0)
                    {
                        char c = text[checkPos];
                        if (!inString)
                        {
                            if (c == '"' && !_stringLiteralParser.IsEscaped(text, checkPos))
                                inString = true;
                            else if (c == '(')
                                parenDepth++;
                            else if (c == ')')
                                parenDepth--;
                            else if (c == '+' && checkPos + 1 < text.Length)
                            {
                                // Look for string concatenation pattern: " + or +\s"
                                int nextNonWhitespace = checkPos + 1;
                                while (nextNonWhitespace < text.Length && char.IsWhiteSpace(text[nextNonWhitespace]))
                                    nextNonWhitespace++;
                                    
                                if (nextNonWhitespace < text.Length && 
                                    (text[nextNonWhitespace] == '"' || 
                                     (text[nextNonWhitespace] == '@' && nextNonWhitespace + 1 < text.Length && text[nextNonWhitespace + 1] == '"')))
                                {
                                    hasConcatenation = true;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if (c == '"' && !_stringLiteralParser.IsEscaped(text, checkPos))
                                inString = false;
                        }

                        checkPos++;
                    }
                    
                    if (hasConcatenation)
                    {
                        // Find ALL concatenated string literals
                        allStringLiterals = _stringLiteralParser.FindAllConcatenatedStrings(text, searchStart, span.Start);
#if DEBUG
                        DiagnosticLogger.Log($"Found {allStringLiterals.Count} strings in concatenation");
                        foreach (var str in allStringLiterals)
                        {
                            DiagnosticLogger.Log($"  String: '{str.text}' (verbatim: {str.isVerbatim})");
                        }
#endif
                    }
                    else
                    {
                        // Find just the first string literal (simple case)
                        var stringLiteral = _stringLiteralParser.FindStringLiteral(text, searchStart, span.Start);
                        allStringLiterals = stringLiteral.HasValue ? new List<(int, int, string, bool, int)> { stringLiteral.Value } : [];
                    }
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
                        _spanBuilder.AddExpressionClassifications(classifications, span.Snapshot, offsetInSnapshot, expressionRegions);
                    }
                    else
                    {
                        // Parse as regular template
                        var properties = _cacheManager.GetCachedTemplateProperties(templateText);

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

                            _spanBuilder.AddPropertyClassifications(classifications, span.Snapshot, offsetInSnapshot, property);
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
        _cacheManager.CacheClassifications(span, classifications);

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

    /// <summary>
    /// Adds classification spans for a single template property and its components.
    /// </summary>
    /// <param name="classifications">The list to add classification spans to.</param>
    /// <param name="snapshot">The text snapshot being classified.</param>
    /// <param name="offsetInSnapshot">The offset within the snapshot where the template starts.</param>
    /// <param name="property">The template property to classify.</param>

    /// <summary>
    /// Checks if the given span is inside an unclosed verbatim string literal.
    /// </summary>
    /// <param name="span">The span to check.</param>
    /// <returns>True if the span is inside a verbatim string; otherwise, false.</returns>
    private bool IsInsideVerbatimString(SnapshotSpan span)
    {
        return _multiLineStringDetector.IsInsideVerbatimString(span);
    }

    /// <summary>
    /// Checks if the given span is inside an unclosed raw string literal.
    /// </summary>
    /// <param name="span">The span to check.</param>
    /// <returns>True if the span is inside a raw string; otherwise, false.</returns>
    private bool IsInsideRawStringLiteral(SnapshotSpan span)
    {
        return _multiLineStringDetector.IsInsideRawStringLiteral(span);
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

    /// <summary>
    /// Disposes resources and unsubscribes from buffer events.
    /// </summary>
    public void Dispose()
    {
        // Clean up event handlers to prevent memory leaks
        _buffer.Changed -= OnBufferChanged;

        // Clear caches
        _cacheManager.Clear();
        _multiLineStringDetector.ClearCache();
    }
}