using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using SerilogSyntax.Diagnostics;
using SerilogSyntax.Parsing;
using SerilogSyntax.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SerilogSyntax.Tagging;

/// <summary>
/// Provides property-argument highlighting for Serilog message templates.
/// Highlights both the template property and its corresponding argument when the cursor is positioned on either.
/// </summary>
internal sealed class PropertyArgumentHighlighter : ITagger<TextMarkerTag>
{
    private readonly ITextView _textView;
    private readonly ITextBuffer _buffer;
    private readonly PropertyArgumentHighlightState _highlightState;
    private readonly TemplateParser _parser = new();
    private readonly LruCache<string, List<TemplateProperty>> _templateCache = new(100);

    /// <summary>
    /// Occurs when tags have changed.
    /// </summary>
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyArgumentHighlighter"/> class.
    /// </summary>
    /// <param name="textView">The text view.</param>
    /// <param name="buffer">The text buffer.</param>
    /// <param name="highlightState">The state manager for property-argument highlights.</param>
    public PropertyArgumentHighlighter(ITextView textView, ITextBuffer buffer, PropertyArgumentHighlightState highlightState)
    {
        DiagnosticLogger.Log("PropertyArgumentHighlighter: Constructor called");

        _textView = textView ?? throw new ArgumentNullException(nameof(textView));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _highlightState = highlightState ?? throw new ArgumentNullException(nameof(highlightState));

        // Subscribe to caret position changes
        DiagnosticLogger.Log("PropertyArgumentHighlighter: Subscribing to events");
        _textView.Caret.PositionChanged += OnCaretPositionChanged;
        _textView.LayoutChanged += OnLayoutChanged;
        _highlightState.StateChanged += OnHighlightStateChanged;

        // Process initial caret position
        DiagnosticLogger.Log($"PropertyArgumentHighlighter: Processing initial caret position {_textView.Caret.Position.BufferPosition.Position}");
        UpdateHighlights(_textView.Caret.Position);
    }

    /// <summary>
    /// Gets the tags that intersect the specified spans.
    /// </summary>
    /// <param name="spans">The spans to get tags for.</param>
    /// <returns>The tags within the spans.</returns>
    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        DiagnosticLogger.Log($"PropertyArgumentHighlighter.GetTags: Called with {spans.Count} spans");

        if (spans.Count == 0)
        {
            DiagnosticLogger.Log("PropertyArgumentHighlighter.GetTags: No spans, returning empty");
            yield break;
        }

        // Check if highlighting is disabled (e.g., by ESC key)
        if (_highlightState.IsDisabled)
        {
            DiagnosticLogger.Log("PropertyArgumentHighlighter.GetTags: Highlighting disabled, returning empty");
            yield break;
        }

        var snapshot = spans[0].Snapshot;

        // Get the current highlights from the state
        var (propertySpan, argumentSpan) = _highlightState.GetHighlightSpans();
        DiagnosticLogger.Log($"PropertyArgumentHighlighter.GetTags: Got highlights - Property: {(propertySpan.HasValue ? $"{propertySpan.Value.Start}-{propertySpan.Value.End}" : "null")}, Argument: {(argumentSpan.HasValue ? $"{argumentSpan.Value.Start}-{argumentSpan.Value.End}" : "null")}");

        if (propertySpan.HasValue && propertySpan.Value.Snapshot == snapshot)
        {
            // Translate to current snapshot if needed
            var currentPropertySpan = propertySpan.Value.TranslateTo(snapshot, SpanTrackingMode.EdgeExclusive);
            if (spans.Any(s => s.OverlapsWith(currentPropertySpan)))
            {
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.GetTags: Returning property tag at {currentPropertySpan.Start}-{currentPropertySpan.End}");
                yield return new TagSpan<TextMarkerTag>(currentPropertySpan, new TextMarkerTag("PropertyArgumentHighlight"));
            }
        }

        if (argumentSpan.HasValue && argumentSpan.Value.Snapshot == snapshot)
        {
            // Translate to current snapshot if needed
            var currentArgumentSpan = argumentSpan.Value.TranslateTo(snapshot, SpanTrackingMode.EdgeExclusive);
            if (spans.Any(s => s.OverlapsWith(currentArgumentSpan)))
            {
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.GetTags: Returning argument tag at {currentArgumentSpan.Start}-{currentArgumentSpan.End}");
                yield return new TagSpan<TextMarkerTag>(currentArgumentSpan, new TextMarkerTag("PropertyArgumentHighlight"));
            }
        }

        DiagnosticLogger.Log("PropertyArgumentHighlighter.GetTags: Finished");
    }

    private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
    {
        DiagnosticLogger.Log($"PropertyArgumentHighlighter.OnCaretPositionChanged: Caret moved to position {e.NewPosition.BufferPosition.Position}");

        // If the caret actually moved to a different position, re-enable highlights
        if (_highlightState.IsDisabled && e.OldPosition.BufferPosition != e.NewPosition.BufferPosition)
        {
            DiagnosticLogger.Log("PropertyArgumentHighlighter.OnCaretPositionChanged: Re-enabling highlights after caret movement");
            _highlightState.EnableHighlights();
        }

        UpdateHighlights(e.NewPosition);
    }

    private void OnLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
    {
        if (e.NewSnapshot != e.OldSnapshot)
        {
            // Clear cache for changed lines
            _templateCache.Clear();
        }

        // Update highlights with current caret position
        UpdateHighlights(_textView.Caret.Position);
    }

    private void OnHighlightStateChanged(object sender, EventArgs e)
    {
        // Refresh all tags when the state changes
        var snapshot = _buffer.CurrentSnapshot;
        var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
    }

    private void UpdateHighlights(CaretPosition caretPosition)
    {
        try
        {
            // If highlights are disabled by ESC, don't update them
            if (_highlightState.IsDisabled)
            {
                DiagnosticLogger.Log("PropertyArgumentHighlighter.UpdateHighlights: Highlights disabled by ESC, skipping update");
                return;
            }

            var position = caretPosition.BufferPosition;
            var line = position.GetContainingLine();
            var lineText = line.GetText();
            var lineStart = line.Start.Position;
            var positionInLine = position.Position - lineStart;

            DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Position {position.Position}, Line {line.LineNumber}");
            DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Line text: '{lineText}'");

            // Check if we're in a Serilog call
            var serilogMatch = SerilogCallDetector.FindSerilogCall(lineText);
            ITextSnapshotLine serilogCallLine = line;

            DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Serilog match found: {serilogMatch != null}");

            if (serilogMatch == null)
            {
                // Check if we're inside a multi-line template
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: No match on current line, searching previous lines");
                var multiLineResult = FindSerilogCallInPreviousLines(position.Snapshot, line);
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Multi-line search result: {multiLineResult != null}");
                if (multiLineResult == null)
                {
                    DiagnosticLogger.Log("PropertyArgumentHighlighter.UpdateHighlights: No Serilog call found, clearing highlights");
                    _highlightState.ClearHighlights();
                    return;
                }

                DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Found Serilog call on line {multiLineResult.Value.Line.LineNumber}");
                serilogMatch = multiLineResult.Value.Match;
                serilogCallLine = multiLineResult.Value.Line;
            }

            // Find the template string
            if (!ExtractTemplate(position.Snapshot, serilogCallLine, line, serilogMatch, out string template, out int templateStartPosition, out int templateEndPosition, out bool hasExceptionParameter))
            {
                DiagnosticLogger.Log("PropertyArgumentHighlighter.UpdateHighlights: Failed to extract template");
                _highlightState.ClearHighlights();
                return;
            }

            DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Extracted template: '{template}' at positions {templateStartPosition}-{templateEndPosition}, hasException={hasExceptionParameter}");

            // Parse template properties
            var properties = GetParsedTemplate(template);
            if (properties == null || properties.Count == 0)
            {
                DiagnosticLogger.Log("PropertyArgumentHighlighter.UpdateHighlights: No properties found in template");
                _highlightState.ClearHighlights();
                return;
            }

            DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Found {properties.Count} properties in template");

            // Check if cursor is on a property in the template
            var cursorPosInTemplate = position.Position - templateStartPosition;
            var propertyAtCursor = properties.FirstOrDefault(p =>
                cursorPosInTemplate >= p.BraceStartIndex &&
                cursorPosInTemplate <= p.BraceEndIndex);

            DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Cursor position in template: {cursorPosInTemplate}, Property at cursor: {propertyAtCursor?.Name ?? "null"}");

            if (propertyAtCursor != null)
            {
                // Cursor is on a property - highlight it and its argument
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Highlighting property '{propertyAtCursor.Name}' and its argument");
                HighlightPropertyAndArgument(position.Snapshot, propertyAtCursor, properties, templateStartPosition, templateEndPosition, hasExceptionParameter);
                return;
            }

            // Check if cursor is on an argument
            DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Checking for argument at position {position.Position}, template ends at {templateEndPosition}, hasException={hasExceptionParameter}");
            var argumentInfo = FindArgumentAtPosition(position.Snapshot, templateEndPosition, position.Position, properties.Count);
            DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Argument search result: {argumentInfo.HasValue}");

            if (argumentInfo.HasValue)
            {
                var (argumentIndex, argumentStart, argumentLength) = argumentInfo.Value;
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Found argument at index {argumentIndex}, position {argumentStart}, length {argumentLength}");
                if (argumentIndex < properties.Count)
                {
                    // Cursor is on an argument - highlight it and its property
                    DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Highlighting argument at index {argumentIndex} and its property");
                    HighlightArgumentAndProperty(position.Snapshot, argumentIndex, argumentStart, argumentLength, properties, templateStartPosition, hasExceptionParameter);
                    return;
                }
                else
                {
                    DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: Argument index {argumentIndex} >= properties count {properties.Count}");
                }
            }
            else
            {
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.UpdateHighlights: No argument found at cursor position {position.Position}");
            }

            DiagnosticLogger.Log("PropertyArgumentHighlighter.UpdateHighlights: No property or argument at cursor, clearing highlights");
            _highlightState.ClearHighlights();
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Log($"Error in UpdateHighlights: {ex}");
            _highlightState.ClearHighlights();
        }
    }

    private void HighlightPropertyAndArgument(ITextSnapshot snapshot, TemplateProperty property, List<TemplateProperty> allProperties, int templateStartPosition, int templateEndPosition, bool hasExceptionParameter = false)
    {
        // Calculate property span
        var propertyStart = templateStartPosition + property.BraceStartIndex;
        var propertyEnd = templateStartPosition + property.BraceEndIndex + 1; // Include closing brace
        var propertySpan = new SnapshotSpan(snapshot, propertyStart, propertyEnd - propertyStart);

        DiagnosticLogger.Log($"PropertyArgumentHighlighter.HighlightPropertyAndArgument: Property span: {propertyStart}-{propertyEnd}");

        // Find the corresponding argument
        var argumentIndex = GetArgumentIndex(allProperties, property);
        DiagnosticLogger.Log($"PropertyArgumentHighlighter.HighlightPropertyAndArgument: Argument index: {argumentIndex}, hasException: {hasExceptionParameter}");

        if (argumentIndex >= 0)
        {
            var argumentLocation = FindArgumentInMultiLineCall(snapshot, templateEndPosition, argumentIndex);
            if (argumentLocation.HasValue)
            {
                var (argStart, argLength) = argumentLocation.Value;
                var argumentSpan = new SnapshotSpan(snapshot, argStart, argLength);

                DiagnosticLogger.Log($"PropertyArgumentHighlighter.HighlightPropertyAndArgument: Setting highlights - Property: {propertyStart}-{propertyEnd}, Argument: {argStart}-{argStart + argLength}");
                _highlightState.SetHighlights(propertySpan, argumentSpan);
                return;
            }
            else
            {
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.HighlightPropertyAndArgument: Could not find argument at index {argumentIndex}");
            }
        }

        // Only highlight the property if we can't find the argument
        DiagnosticLogger.Log($"PropertyArgumentHighlighter.HighlightPropertyAndArgument: Setting property-only highlight: {propertyStart}-{propertyEnd}");
        _highlightState.SetHighlights(propertySpan, null);
    }

    private void HighlightArgumentAndProperty(ITextSnapshot snapshot, int argumentIndex, int argumentStart, int argumentLength, List<TemplateProperty> properties, int templateStartPosition, bool hasExceptionParameter = false)
    {
        // Calculate argument span
        var argumentSpan = new SnapshotSpan(snapshot, argumentStart, argumentLength);

        DiagnosticLogger.Log($"PropertyArgumentHighlighter.HighlightArgumentAndProperty: Argument at index {argumentIndex}, hasException: {hasExceptionParameter}");

        // Find the corresponding property
        TemplateProperty property = null;

        // Check if this is a positional argument
        var positionalProperties = properties.Where(p => p.Type == PropertyType.Positional).ToList();
        if (positionalProperties.Any())
        {
            // Try to find a positional property with matching index
            property = positionalProperties.FirstOrDefault(p => int.TryParse(p.Name, out int idx) && idx == argumentIndex);
        }

        if (property == null)
        {
            // For named properties, find by position among non-positional properties
            var namedProperties = properties.Where(p => p.Type != PropertyType.Positional).ToList();
            if (argumentIndex < namedProperties.Count)
            {
                property = namedProperties[argumentIndex];
            }
        }

        if (property != null)
        {
            var propertyStart = templateStartPosition + property.BraceStartIndex;
            var propertyEnd = templateStartPosition + property.BraceEndIndex + 1; // Include closing brace
            var propertySpan = new SnapshotSpan(snapshot, propertyStart, propertyEnd - propertyStart);

            _highlightState.SetHighlights(propertySpan, argumentSpan);
        }
        else
        {
            // Only highlight the argument if we can't find the property
            _highlightState.SetHighlights(null, argumentSpan);
        }
    }

    private bool ExtractTemplate(ITextSnapshot snapshot, ITextSnapshotLine serilogCallLine, ITextSnapshotLine currentLine, Match serilogMatch, out string template, out int templateStartPosition, out int templateEndPosition, out bool hasExceptionParameter)
    {
        template = null;
        templateStartPosition = 0;
        templateEndPosition = 0;
        hasExceptionParameter = false;

        if (serilogCallLine == currentLine)
        {
            var lineText = serilogCallLine.GetText();
            var lineStart = serilogCallLine.Start.Position;
            var templateMatch = FindTemplateString(lineText, serilogMatch.Index + serilogMatch.Length);

            if (!templateMatch.HasValue)
            {
                // Check for multi-line template
                var multiLineTemplate = ReconstructMultiLineTemplate(snapshot, serilogCallLine, currentLine);
                if (multiLineTemplate == null)
                    return false;

                template = multiLineTemplate.Value.Template;
                templateStartPosition = multiLineTemplate.Value.StartPosition;
                templateEndPosition = multiLineTemplate.Value.EndPosition;
                hasExceptionParameter = multiLineTemplate.Value.HasException;
                return true;
            }

            var (templateStart, templateEnd) = templateMatch.Value;
            template = lineText.Substring(templateStart, templateEnd - templateStart);
            templateStartPosition = lineStart + templateStart;
            templateEndPosition = lineStart + templateEnd;
            return true;
        }
        else
        {
            // Multi-line scenario
            var multiLineTemplate = ReconstructMultiLineTemplate(snapshot, serilogCallLine, currentLine);
            if (multiLineTemplate == null)
                return false;

            template = multiLineTemplate.Value.Template;
            templateStartPosition = multiLineTemplate.Value.StartPosition;
            templateEndPosition = multiLineTemplate.Value.EndPosition;
            hasExceptionParameter = multiLineTemplate.Value.HasException;
            return true;
        }
    }

    private List<TemplateProperty> GetParsedTemplate(string template)
    {
        if (string.IsNullOrEmpty(template))
            return null;

        // Try to get from cache first
        if (_templateCache.TryGetValue(template, out var cached))
            return cached;

        // Parse and cache
        var properties = _parser.Parse(template).ToList();
        _templateCache.Add(template, properties);
        return properties;
    }

    private int GetArgumentIndex(List<TemplateProperty> properties, TemplateProperty targetProperty)
    {
        if (targetProperty.Type == PropertyType.Positional)
        {
            // For positional properties, parse the index from the property name
            if (int.TryParse(targetProperty.Name, out int index))
                return index;
            return -1;
        }
        else
        {
            // For named properties, find their position among all named properties
            var namedProperties = properties.Where(p => p.Type != PropertyType.Positional).ToList();
            return namedProperties.IndexOf(targetProperty);
        }
    }

    private (int Index, int Start, int Length)? FindArgumentAtPosition(ITextSnapshot snapshot, int templateEndPosition, int cursorPosition, int propertyCount)
    {
        DiagnosticLogger.Log($"PropertyArgumentHighlighter.FindArgumentAtPosition: Looking for argument at cursor position {cursorPosition}, template ends at {templateEndPosition}");

        // Start searching from the template end position
        var templateEndLine = snapshot.GetLineFromPosition(templateEndPosition);
        var allArguments = new List<(int absolutePosition, int length)>();

        // Parse arguments starting from the template end line
        var templateEndLineText = templateEndLine.GetText();
        var templateEndInLine = templateEndPosition - templateEndLine.Start.Position;

        DiagnosticLogger.Log($"PropertyArgumentHighlighter.FindArgumentAtPosition: Template end line text: '{templateEndLineText}', end position in line: {templateEndInLine}");

        if (templateEndInLine < templateEndLineText.Length)
        {
            var commaIndex = templateEndLineText.IndexOf(',', templateEndInLine);
            if (commaIndex >= 0)
            {
                var endLineArguments = ParseArguments(templateEndLineText, commaIndex + 1);
                foreach (var (start, length) in endLineArguments)
                {
                    var absStart = templateEndLine.Start.Position + start;
                    var absEnd = absStart + length;

                    if (cursorPosition >= absStart && cursorPosition <= absEnd)
                    {
                        DiagnosticLogger.Log($"PropertyArgumentHighlighter.FindArgumentAtPosition: Found cursor in argument {allArguments.Count} at {absStart}-{absEnd}");
                        // Found the argument containing the cursor
                        return (allArguments.Count, absStart, length);
                    }

                    allArguments.Add((absStart, length));
                    DiagnosticLogger.Log($"PropertyArgumentHighlighter.FindArgumentAtPosition: Added argument {allArguments.Count - 1} at {absStart}, length {length}");
                }
            }
        }

        // Continue searching subsequent lines
        for (int lineNum = templateEndLine.LineNumber + 1; lineNum < snapshot.LineCount && allArguments.Count < propertyCount; lineNum++)
        {
            var nextLine = snapshot.GetLineFromLineNumber(lineNum);
            var originalLineText = nextLine.GetText();
            var nextLineText = originalLineText.TrimStart();

            if (string.IsNullOrEmpty(nextLineText))
                continue;

            var trimOffset = originalLineText.Length - nextLineText.Length;

            var closingParenIndex = nextLineText.IndexOf(");");
            if (closingParenIndex >= 0)
            {
                var finalLineArguments = ParseArguments(nextLineText, 0);
                foreach (var (start, length) in finalLineArguments)
                {
                    if (start < closingParenIndex)
                    {
                        var absStart = nextLine.Start.Position + trimOffset + start;
                        var absEnd = absStart + length;

                        if (cursorPosition >= absStart && cursorPosition <= absEnd)
                        {
                            return (allArguments.Count, absStart, length);
                        }

                        allArguments.Add((absStart, length));
                    }
                }
                break;
            }
            else
            {
                var lineArguments = ParseArguments(nextLineText, 0);
                foreach (var (start, length) in lineArguments)
                {
                    var absStart = nextLine.Start.Position + trimOffset + start;
                    var absEnd = absStart + length;

                    if (cursorPosition >= absStart && cursorPosition <= absEnd)
                    {
                        return (allArguments.Count, absStart, length);
                    }

                    allArguments.Add((absStart, length));
                }
            }
        }

        return null;
    }

    // Helper methods borrowed from SerilogNavigationProvider
    private (Match Match, ITextSnapshotLine Line)? FindSerilogCallInPreviousLines(ITextSnapshot snapshot, ITextSnapshotLine currentLine)
    {
        DiagnosticLogger.Log($"PropertyArgumentHighlighter.FindSerilogCallInPreviousLines: Starting search from line {currentLine.LineNumber}");

        // Increase search range to 20 lines for long multi-line strings
        for (int i = currentLine.LineNumber - 1; i >= Math.Max(0, currentLine.LineNumber - 20); i--)
        {
            var checkLine = snapshot.GetLineFromLineNumber(i);
            var checkText = checkLine.GetText();

            DiagnosticLogger.Log($"PropertyArgumentHighlighter.FindSerilogCallInPreviousLines: Checking line {i}: '{checkText.Substring(0, Math.Min(50, checkText.Length))}'...");

            var match = SerilogCallDetector.FindSerilogCall(checkText);
            if (match != null)
            {
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.FindSerilogCallInPreviousLines: Found Serilog call on line {i}");

                // Check if we're either inside the template string OR in the arguments section
                var isInside = IsInsideMultiLineTemplate(snapshot, checkLine, currentLine);
                var isInArguments = !isInside && IsInArgumentsSection(snapshot, checkLine, currentLine);

                DiagnosticLogger.Log($"PropertyArgumentHighlighter.FindSerilogCallInPreviousLines: IsInsideMultiLineTemplate = {isInside}, IsInArgumentsSection = {isInArguments}");

                if (isInside || isInArguments)
                {
                    return (match, checkLine);
                }
                else
                {
                    DiagnosticLogger.Log($"PropertyArgumentHighlighter.FindSerilogCallInPreviousLines: Not inside template or arguments, continuing search");
                }
            }
        }

        DiagnosticLogger.Log("PropertyArgumentHighlighter.FindSerilogCallInPreviousLines: No Serilog call found in range");
        return null;
    }

    private bool IsInArgumentsSection(ITextSnapshot snapshot, ITextSnapshotLine serilogCallLine, ITextSnapshotLine currentLine)
    {
        // This method checks if we're in the arguments section of a multi-line Serilog call
        // (after the template string but before the closing parenthesis)

        int parenDepth = 0;
        bool passedTemplate = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inRawString = false;
        int rawStringQuoteCount = 0;

        DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInArgumentsSection: Checking from line {serilogCallLine.LineNumber} to {currentLine.LineNumber}");

        for (int lineNum = serilogCallLine.LineNumber; lineNum <= currentLine.LineNumber; lineNum++)
        {
            var line = snapshot.GetLineFromLineNumber(lineNum);
            var lineText = line.GetText();

            int startIndex = 0;
            if (lineNum == serilogCallLine.LineNumber)
            {
                var match = SerilogCallDetector.FindSerilogCall(lineText);
                if (match != null)
                {
                    // The match includes the opening paren
                    parenDepth = 1;
                    startIndex = match.Index + match.Length;
                    DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInArgumentsSection: Starting after Serilog call at index {startIndex}");
                }
            }

            for (int i = startIndex; i < lineText.Length; i++)
            {
                char c = lineText[i];

                // Track string states
                if (!inString && !inVerbatimString && !inRawString)
                {
                    if (c == '(')
                    {
                        parenDepth++;
                    }
                    else if (c == ')')
                    {
                        parenDepth--;
                        if (parenDepth == 0)
                        {
                            DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInArgumentsSection: Method call closed at line {lineNum}");
                            return lineNum == currentLine.LineNumber && i > 0; // We're in arguments if we're on the closing line before the paren
                        }
                    }
                    else if (c == ',')
                    {
                        // We've seen a comma outside of strings - we've passed the template
                        passedTemplate = true;
                    }
                    else if (c == '"' && !inString && !inVerbatimString && !inRawString)
                    {
                        // Count consecutive quotes for raw string literals
                        int quoteCount = 1;
                        while (i + quoteCount < lineText.Length && lineText[i + quoteCount] == '"')
                        {
                            quoteCount++;
                        }

                        if (quoteCount >= 3)
                        {
                            inRawString = true;
                            rawStringQuoteCount = quoteCount;
                            i += quoteCount - 1; // -1 because loop will increment
                        }
                        else
                        {
                            inString = true;
                        }
                    }
                    else if (i + 1 < lineText.Length && c == '@' && lineText[i + 1] == '"')
                    {
                        inVerbatimString = true;
                        i++;
                    }
                }
                else if (inString)
                {
                    if (c == '\\' && i + 1 < lineText.Length)
                    {
                        i++; // Skip escaped character
                    }
                    else if (c == '"')
                    {
                        inString = false;
                        if (!passedTemplate && lineNum == serilogCallLine.LineNumber)
                        {
                            // We just closed the template string on the Serilog call line
                            passedTemplate = true;
                        }
                    }
                }
                else if (inVerbatimString)
                {
                    if (c == '"')
                    {
                        if (i + 1 < lineText.Length && lineText[i + 1] == '"')
                        {
                            i++; // Skip escaped quote
                        }
                        else
                        {
                            inVerbatimString = false;
                            if (!passedTemplate)
                            {
                                passedTemplate = true;
                            }
                        }
                    }
                }
                else if (inRawString)
                {
                    // Check for closing raw string quotes
                    bool foundClosing = true;
                    for (int j = 0; j < rawStringQuoteCount && i + j < lineText.Length; j++)
                    {
                        if (lineText[i + j] != '"')
                        {
                            foundClosing = false;
                            break;
                        }
                    }
                    if (foundClosing)
                    {
                        inRawString = false;
                        i += rawStringQuoteCount - 1;
                        if (!passedTemplate)
                        {
                            passedTemplate = true;
                        }
                    }
                }
            }

            // If we're at the current line and we've passed the template and are still in the method call
            if (lineNum == currentLine.LineNumber && passedTemplate && parenDepth > 0)
            {
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInArgumentsSection: At current line, passed template, in method call");
                return true;
            }
        }

        return false;
    }

    private bool IsInsideMultiLineTemplate(ITextSnapshot snapshot, ITextSnapshotLine serilogCallLine, ITextSnapshotLine currentLine)
    {
        bool inString = false;
        bool inVerbatimString = false;
        bool inRawString = false;
        int rawStringQuoteCount = 0;
        bool foundOpenParen = false;
        int parenDepth = 0;

        DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInsideMultiLineTemplate: Checking from line {serilogCallLine.LineNumber} to {currentLine.LineNumber}");

        // Extended range to handle cases where verbatim string continues beyond current line
        int endLine = Math.Min(currentLine.LineNumber + 5, snapshot.LineCount - 1);
        for (int lineNum = serilogCallLine.LineNumber; lineNum <= endLine; lineNum++)
        {
            var line = snapshot.GetLineFromLineNumber(lineNum);
            var lineText = line.GetText();
            DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInsideMultiLineTemplate: Line {lineNum}: '{lineText}'");

            // If we're on the Serilog call line, process the entire line but track that we found the opening paren
            int startIndex = 0;
            if (lineNum == serilogCallLine.LineNumber)
            {
                var match = SerilogCallDetector.FindSerilogCall(lineText);
                if (match != null)
                {
                    // The match includes the opening paren, so we know we're in a method call
                    foundOpenParen = true;
                    parenDepth = 1; // We've seen the opening paren in the match
                    startIndex = match.Index + match.Length;
                    DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInsideMultiLineTemplate: On Serilog line, match includes opening paren, starting at index {startIndex} of '{lineText}'");
                    if (startIndex < lineText.Length)
                    {
                        DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInsideMultiLineTemplate: First char after match: '{lineText[startIndex]}'");
                    }
                }
            }

            for (int i = startIndex; i < lineText.Length; i++)
            {
                char c = lineText[i];

                if (!inString && !inVerbatimString && !inRawString)
                {
                    // Track parentheses to know we're in a method call
                    if (c == '(')
                    {
                        parenDepth++;
                    }
                    else if (c == ')')
                    {
                        parenDepth--;
                        if (parenDepth == 0)
                        {
                            // We've closed the method call, not in template anymore
                            DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInsideMultiLineTemplate: Closed method call at line {lineNum}");
                            return false;
                        }
                    }
                    else if (c == '"')
                    {
                        // Count consecutive quotes for raw string literals
                        int quoteCount = 1;
                        while (i + quoteCount < lineText.Length && lineText[i + quoteCount] == '"')
                        {
                            quoteCount++;
                        }

                        if (quoteCount >= 3)
                        {
                            inRawString = true;
                            rawStringQuoteCount = quoteCount;
                            i += quoteCount - 1; // -1 because loop will increment
                            continue;
                        }
                        else
                        {
                            inString = true;
                            continue;
                        }
                    }
                    else if (i + 1 < lineText.Length && c == '@' && lineText[i + 1] == '"')
                    {
                        inVerbatimString = true;
                        i++;
                        continue;
                    }
                }
                else if (inRawString)
                {
                    if (c == '"')
                    {
                        int consecutiveQuotes = 1;
                        while (i + consecutiveQuotes < lineText.Length && lineText[i + consecutiveQuotes] == '"')
                            consecutiveQuotes++;

                        if (consecutiveQuotes >= rawStringQuoteCount)
                        {
                            inRawString = false;
                            i += consecutiveQuotes - 1;
                        }
                    }
                }
                else if (inVerbatimString)
                {
                    if (c == '"')
                    {
                        if (i + 1 < lineText.Length && lineText[i + 1] == '"')
                        {
                            i++;
                        }
                        else
                        {
                            inVerbatimString = false;
                        }
                    }
                }
                else if (inString)
                {
                    if (c == '\\' && i + 1 < lineText.Length)
                    {
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                }
            }

            // After processing the line, check if we're at the current line
            if (lineNum == currentLine.LineNumber)
            {
                DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInsideMultiLineTemplate: At current line {lineNum}, inString={inString}, inVerbatim={inVerbatimString}, inRaw={inRawString}, foundParen={foundOpenParen}, parenDepth={parenDepth}");

                // We're at the current line - check if we're in a string or if we have an open paren and could be in a template
                if (inString || inVerbatimString || inRawString)
                {
                    DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInsideMultiLineTemplate: At current line, in string: {inString}, in verbatim: {inVerbatimString}, in raw: {inRawString}");
                    return true;
                }
                else if (foundOpenParen && parenDepth > 0)
                {
                    // We're still inside the method call, could be on a template line
                    DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInsideMultiLineTemplate: At current line, inside method call with paren depth: {parenDepth}");
                    return true;
                }
            }
        }

        DiagnosticLogger.Log($"PropertyArgumentHighlighter.IsInsideMultiLineTemplate: Reached end, not inside template");
        return false;
    }

    private (string Template, int StartPosition, int EndPosition, bool HasException)? ReconstructMultiLineTemplate(ITextSnapshot snapshot, ITextSnapshotLine serilogCallLine, ITextSnapshotLine currentLine)
    {
        DiagnosticLogger.Log($"PropertyArgumentHighlighter.ReconstructMultiLineTemplate: Starting from line {serilogCallLine.LineNumber} to {currentLine.LineNumber}");

        var templateBuilder = new System.Text.StringBuilder();
        int templateStartPosition = -1;

        bool foundTemplateStart = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inRawString = false;
        int rawStringQuoteCount = 0;
        bool hasExceptionParameter = false;

        // Start looking from the Serilog call line and go beyond current line to find template end
        for (int lineNum = serilogCallLine.LineNumber; lineNum <= Math.Min(currentLine.LineNumber + 20, snapshot.LineCount - 1); lineNum++)
        {
            var line = snapshot.GetLineFromLineNumber(lineNum);
            var lineText = line.GetText();

            DiagnosticLogger.Log($"PropertyArgumentHighlighter.ReconstructMultiLineTemplate: Processing line {lineNum}: '{lineText}'");

            // Special handling for Serilog call line - skip to after the method name and opening paren
            int startIndex = 0;
            if (lineNum == serilogCallLine.LineNumber && !foundTemplateStart)
            {
                var match = SerilogCallDetector.FindSerilogCall(lineText);
                if (match != null)
                {
                    startIndex = match.Index + match.Length;
                    DiagnosticLogger.Log($"PropertyArgumentHighlighter.ReconstructMultiLineTemplate: Serilog call line, starting at index {startIndex}");

                    // Check if this is LogError with an exception parameter
                    if (lineText.Contains("LogError") && HasExceptionParameterBeforeTemplate(lineText, startIndex))
                    {
                        DiagnosticLogger.Log("PropertyArgumentHighlighter.ReconstructMultiLineTemplate: LogError with exception parameter detected, skipping to next parameter");
                        hasExceptionParameter = true;

                        // Skip past the exception parameter to find the template
                        int parenDepth = 1;
                        while (startIndex < lineText.Length)
                        {
                            char c = lineText[startIndex];
                            if (c == '(') parenDepth++;
                            else if (c == ')')
                            {
                                parenDepth--;
                                if (parenDepth == 0) break; // End of call
                            }
                            else if (c == ',' && parenDepth == 1)
                            {
                                startIndex++; // Move past the comma
                                // Skip whitespace after comma
                                while (startIndex < lineText.Length && char.IsWhiteSpace(lineText[startIndex]))
                                {
                                    startIndex++;
                                }
                                break;
                            }
                            startIndex++;
                        }
                        DiagnosticLogger.Log($"PropertyArgumentHighlighter.ReconstructMultiLineTemplate: After skipping exception, starting at index {startIndex}");

                        // If we reached end of line after skipping exception, continue to next line
                        if (startIndex >= lineText.Length)
                        {
                            DiagnosticLogger.Log("PropertyArgumentHighlighter.ReconstructMultiLineTemplate: Reached end of line after exception, continuing to next line");
                            continue;
                        }
                    }
                }
            }

            for (int i = startIndex; i < lineText.Length; i++)
            {
                char c = lineText[i];
                int absolutePosition = line.Start.Position + i;

                if (!foundTemplateStart && !inString && !inVerbatimString && !inRawString)
                {
                    // Check for raw string literals (3+ quotes)
                    if (c == '"')
                    {
                        int quoteCount = 1;
                        while (i + quoteCount < lineText.Length && lineText[i + quoteCount] == '"')
                        {
                            quoteCount++;
                        }

                        if (quoteCount >= 3)
                        {
                            DiagnosticLogger.Log($"PropertyArgumentHighlighter.ReconstructMultiLineTemplate: Found raw string start with {quoteCount} quotes at position {i}");
                            foundTemplateStart = true;
                            inRawString = true;
                            rawStringQuoteCount = quoteCount;
                            templateStartPosition = absolutePosition + quoteCount;
                            i += quoteCount - 1; // -1 because loop will increment
                            continue;
                        }
                        else if (quoteCount == 1)
                        {
                            // Single quote - regular string
                            DiagnosticLogger.Log($"PropertyArgumentHighlighter.ReconstructMultiLineTemplate: Found regular string start at position {i}");
                            foundTemplateStart = true;
                            inString = true;
                            templateStartPosition = absolutePosition + 1;
                            continue;
                        }
                    }

                    if (i + 1 < lineText.Length && c == '@' && lineText[i + 1] == '"')
                    {
                        DiagnosticLogger.Log($"PropertyArgumentHighlighter.ReconstructMultiLineTemplate: Found verbatim string start at position {i}");
                        foundTemplateStart = true;
                        inVerbatimString = true;
                        templateStartPosition = absolutePosition + 2;
                        i++;
                        continue;
                    }
                }
                else if (foundTemplateStart)
                {
                    if (inRawString)
                    {
                        if (c == '"')
                        {
                            int consecutiveQuotes = 1;
                            while (i + consecutiveQuotes < lineText.Length && lineText[i + consecutiveQuotes] == '"')
                                consecutiveQuotes++;

                            if (consecutiveQuotes >= rawStringQuoteCount)
                            {
                                DiagnosticLogger.Log($"PropertyArgumentHighlighter.ReconstructMultiLineTemplate: Found raw string end with {consecutiveQuotes} quotes (needed {rawStringQuoteCount})");
                                return (templateBuilder.ToString(), templateStartPosition, absolutePosition, hasExceptionParameter);
                            }
                            else
                            {
                                // Add all the quotes we found to the template
                                for (int q = 0; q < consecutiveQuotes; q++)
                                {
                                    templateBuilder.Append('"');
                                }
                                i += consecutiveQuotes - 1; // Skip the quotes we already processed
                            }
                        }
                        else
                        {
                            templateBuilder.Append(c);
                        }
                    }
                    else if (inVerbatimString)
                    {
                        if (c == '"')
                        {
                            if (i + 1 < lineText.Length && lineText[i + 1] == '"')
                            {
                                templateBuilder.Append("\"\"");
                                i++;
                            }
                            else
                            {
                                return (templateBuilder.ToString(), templateStartPosition, absolutePosition, hasExceptionParameter);
                            }
                        }
                        else
                        {
                            templateBuilder.Append(c);
                        }
                    }
                    else if (inString)
                    {
                        if (c == '\\' && i + 1 < lineText.Length)
                        {
                            templateBuilder.Append(c);
                            i++;
                            if (i < lineText.Length)
                                templateBuilder.Append(lineText[i]);
                        }
                        else if (c == '"')
                        {
                            return (templateBuilder.ToString(), templateStartPosition, absolutePosition, hasExceptionParameter);
                        }
                        else
                        {
                            templateBuilder.Append(c);
                        }
                    }
                }
            }

            if (foundTemplateStart && (inVerbatimString || inRawString) && lineNum < snapshot.LineCount - 1)
            {
                var nextLine = snapshot.GetLineFromLineNumber(lineNum + 1);
                if (lineNum + 1 <= Math.Min(snapshot.LineCount - 1, serilogCallLine.LineNumber + 20))
                {
                    var lineBreakStart = line.End.Position;
                    var lineBreakEnd = nextLine.Start.Position;
                    if (lineBreakEnd > lineBreakStart)
                    {
                        var lineBreakText = snapshot.GetText(lineBreakStart, lineBreakEnd - lineBreakStart);
                        templateBuilder.Append(lineBreakText);
                    }
                }
            }
        }

        DiagnosticLogger.Log($"PropertyArgumentHighlighter.ReconstructMultiLineTemplate: Reached end without finding template close, foundStart={foundTemplateStart}, template length={templateBuilder.Length}");
        return null;
    }

    private (int, int)? FindTemplateString(string line, int startIndex)
    {
        bool hasExceptionParam = line.Contains("LogError") && HasExceptionParameterBeforeTemplate(line, startIndex);

        int searchPos = startIndex;
        if (hasExceptionParam)
        {
            int parenDepth = 1;
            while (searchPos < line.Length && parenDepth > 0)
            {
                char c = line[searchPos];
                if (c == '(') parenDepth++;
                else if (c == ')') parenDepth--;
                else if (c == ',' && parenDepth == 1)
                {
                    searchPos++;
                    break;
                }
                searchPos++;
            }
        }

        for (int i = searchPos; i < line.Length; i++)
        {
            if (char.IsWhiteSpace(line[i]))
                continue;

            // Check for raw string literal (""")
            if (i + 2 < line.Length && line.Substring(i, 3) == "\"\"\"")
            {
                // Raw string literal - need to handle multi-line
                // For single line test, just return empty since it spans lines
                // The ReconstructMultiLineTemplate will handle it
                return null;
            }
            else if (line[i] == '"')
            {
                int end = i + 1;
                while (end < line.Length && line[end] != '"')
                {
                    if (line[end] == '\\')
                        end++;
                    end++;
                }

                if (end < line.Length)
                    return (i + 1, end);
            }
            else if (i + 1 < line.Length && line[i] == '@' && line[i + 1] == '"')
            {
                int end = i + 2;
                while (end < line.Length)
                {
                    if (line[end] == '"')
                    {
                        if (end + 1 < line.Length && line[end + 1] == '"')
                        {
                            end += 2;
                            continue;
                        }

                        return (i + 2, end);
                    }

                    end++;
                }
            }
            else
            {
                break;
            }
        }
        return null;
    }

    private bool HasExceptionParameterBeforeTemplate(string line, int searchStart)
    {
        // LogError can have (Exception, template, ...) or just (template, ...)
        // We need to detect if the first parameter is an Exception object
        // Common patterns:
        // - new Exception(...)
        // - ex (variable)
        // - null

        int pos = searchStart;

        // Skip whitespace
        while (pos < line.Length && char.IsWhiteSpace(line[pos]))
            pos++;

        if (pos >= line.Length)
            return false;

        // Check if the first parameter starts with "new " (exception constructor)
        if (pos + 4 < line.Length && line.Substring(pos, 4) == "new ")
        {
            DiagnosticLogger.Log("PropertyArgumentHighlighter.HasExceptionParameterBeforeTemplate: Found 'new' keyword, likely exception parameter");
            return true;
        }

        // Check if it's not a string (template would start with " or @" or """)
        if (line[pos] == '"')
        {
            DiagnosticLogger.Log("PropertyArgumentHighlighter.HasExceptionParameterBeforeTemplate: First parameter is a string, not an exception");
            return false;
        }

        if (pos + 1 < line.Length && line[pos] == '@' && line[pos + 1] == '"')
        {
            DiagnosticLogger.Log("PropertyArgumentHighlighter.HasExceptionParameterBeforeTemplate: First parameter is a verbatim string, not an exception");
            return false;
        }

        // If it's an identifier (like 'ex' or 'null'), it's likely an exception parameter
        if (char.IsLetter(line[pos]) || line[pos] == 'n')
        {
            DiagnosticLogger.Log("PropertyArgumentHighlighter.HasExceptionParameterBeforeTemplate: First parameter is an identifier, likely exception parameter");
            return true;
        }

        return false;
    }

    private (int, int)? FindArgumentInMultiLineCall(ITextSnapshot snapshot, int templateEndPosition, int argumentIndex)
    {
        var allArguments = new List<(int absolutePosition, int length)>();

        var templateEndLine = snapshot.GetLineFromPosition(templateEndPosition);
        var templateEndLineText = templateEndLine.GetText();
        var templateEndInLine = templateEndPosition - templateEndLine.Start.Position;

        if (templateEndInLine < templateEndLineText.Length)
        {
            var commaIndex = templateEndLineText.IndexOf(',', templateEndInLine);
            if (commaIndex >= 0)
            {
                var endLineArguments = ParseArguments(templateEndLineText, commaIndex + 1);
                foreach (var (start, length) in endLineArguments)
                {
                    allArguments.Add((templateEndLine.Start.Position + start, length));
                }
            }
        }

        for (int lineNum = templateEndLine.LineNumber + 1; lineNum < snapshot.LineCount; lineNum++)
        {
            var nextLine = snapshot.GetLineFromLineNumber(lineNum);
            var originalLineText = nextLine.GetText();
            var nextLineText = originalLineText.TrimStart();

            if (string.IsNullOrEmpty(nextLineText))
                continue;

            var trimOffset = originalLineText.Length - nextLineText.Length;

            var closingParenIndex = nextLineText.IndexOf(");");
            if (closingParenIndex >= 0)
            {
                var finalLineArguments = ParseArguments(nextLineText, 0);
                foreach (var (start, length) in finalLineArguments)
                {
                    if (start < closingParenIndex)
                    {
                        allArguments.Add((nextLine.Start.Position + trimOffset + start, length));
                    }
                }
                break;
            }
            else
            {
                var lineArguments = ParseArguments(nextLineText, 0);
                foreach (var (start, length) in lineArguments)
                {
                    allArguments.Add((nextLine.Start.Position + trimOffset + start, length));
                }
            }
        }

        if (argumentIndex < allArguments.Count)
        {
            return allArguments[argumentIndex];
        }

        return null;
    }

    private List<(int start, int length)> ParseArguments(string line, int startIndex)
    {
        var arguments = new List<(int start, int length)>();
        var current = startIndex;
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        var inString = false;
        var stringChar = '\0';

        while (current < line.Length && char.IsWhiteSpace(line[current]))
            current++;
        var argumentStart = current;

        for (; current < line.Length; current++)
        {
            var c = line[current];

            if (!inString && (c == '"' || c == '\''))
            {
                inString = true;
                stringChar = c;
                continue;
            }
            else if (inString && c == stringChar)
            {
                if (current > 0 && line[current - 1] != '\\')
                {
                    inString = false;
                }

                continue;
            }
            else if (inString)
            {
                continue;
            }

            switch (c)
            {
                case '(':
                    parenDepth++;
                    break;

                case ')':
                    parenDepth--;

                    if (parenDepth < 0)
                    {
                        if (current > argumentStart)
                        {
                            var argText = line.Substring(argumentStart, current - argumentStart).Trim();
                            if (!string.IsNullOrEmpty(argText))
                            {
                                arguments.Add((argumentStart, argText.Length));
                            }
                        }

                        return arguments;
                    }

                    break;

                case '[':
                    bracketDepth++;
                    break;

                case ']':
                    bracketDepth--;
                    break;

                case '{':
                    braceDepth++;
                    break;

                case '}':
                    braceDepth--;
                    break;

                case ',':
                    if (parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    {
                        var argText = line.Substring(argumentStart, current - argumentStart).Trim();
                        if (!string.IsNullOrEmpty(argText))
                        {
                            arguments.Add((argumentStart, argText.Length));
                        }

                        current++;
                        while (current < line.Length && char.IsWhiteSpace(line[current]))
                            current++;
                        argumentStart = current;
                        current--;
                    }

                    break;
            }
        }

        if (argumentStart < current)
        {
            var argText = line.Substring(argumentStart, current - argumentStart).Trim();
            if (!string.IsNullOrEmpty(argText))
            {
                arguments.Add((argumentStart, argText.Length));
            }
        }

        return arguments;
    }

    /// <summary>
    /// Disposes the tagger.
    /// </summary>
    public void Dispose()
    {
        _textView.Caret.PositionChanged -= OnCaretPositionChanged;
        _textView.LayoutChanged -= OnLayoutChanged;
        _highlightState.StateChanged -= OnHighlightStateChanged;
    }
}