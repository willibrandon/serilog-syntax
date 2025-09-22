using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using SerilogSyntax.Classification;
using SerilogSyntax.Expressions;
using SerilogSyntax.Utilities;
using System;
using System.Collections.Generic;

namespace SerilogSyntax.Tagging;

/// <summary>
/// Provides brace matching highlights for Serilog template properties.
/// Highlights matching opening and closing braces when the caret is positioned on or near them.
/// </summary>
internal sealed class SerilogBraceMatcher : ITagger<TextMarkerTag>, IDisposable
{
    /// <summary>
    /// Maximum number of lines to search backward when detecting multi-line string contexts.
    /// </summary>
    private const int MaxLookbackLines = 20;

    /// <summary>
    /// Maximum number of lines to search forward when detecting unclosed strings.
    /// </summary>
    private const int MaxLookforwardLines = 50;

    /// <summary>
    /// Maximum character distance to search for matching braces within a property.
    /// </summary>
    private const int MaxPropertyLength = 200;

    /// <summary>
    /// Maximum character distance to search for matching braces within an expression.
    /// </summary>
    private const int MaxExpressionLength = 500;

    private readonly ITextView _view;
    private readonly ITextBuffer _buffer;
    private readonly SerilogBraceHighlightState _state;

    private SnapshotPoint? _currentChar;
    private bool _disposed;

    /// <summary>
    /// Event raised when tags have changed.
    /// </summary>
    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="SerilogBraceMatcher"/> class.
    /// </summary>
    /// <param name="view">The text view.</param>
    /// <param name="buffer">The text buffer.</param>
    public SerilogBraceMatcher(ITextView view, ITextBuffer buffer)
    {
        _view = view;
        _buffer = buffer;
        _state = SerilogBraceHighlightState.GetOrCreate(view);

        // Initialize current position
        _currentChar = view.Caret.Position.Point.GetPoint(buffer, view.Caret.Position.Affinity);

        _view.Caret.PositionChanged += CaretPositionChanged;
        _view.LayoutChanged += ViewLayoutChanged;
        _state.StateChanged += StateChanged;
        _view.Closed += View_Closed;
    }

    private void View_Closed(object sender, EventArgs e) => Dispose();

    private void StateChanged(object sender, EventArgs e) => RaiseRefreshForEntireSnapshot();

    /// <summary>
    /// Handles view layout changes to update brace matching.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
    {
        if (e.NewSnapshot != e.OldSnapshot)
            UpdateAtCaretPosition(_view.Caret.Position);
    }

    /// <summary>
    /// Handles caret position changes to update brace matching.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The event arguments.</param>
    private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        => UpdateAtCaretPosition(e.NewPosition);

    /// <summary>
    /// Updates brace matching tags based on the caret position.
    /// </summary>
    /// <param name="caretPosition">The caret position.</param>
    private void UpdateAtCaretPosition(CaretPosition caretPosition)
    {
        _currentChar = caretPosition.Point.GetPoint(_buffer, caretPosition.Affinity);
        if (_currentChar.HasValue)
            RaiseRefreshForEntireSnapshot();
    }

    private void RaiseRefreshForEntireSnapshot()
    {
        var snapshot = _buffer.CurrentSnapshot;
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
    }

    /// <summary>
    /// Checks if the cursor is inside an expression template context.
    /// </summary>
    /// <param name="point">The snapshot point to check.</param>
    /// <returns>True if inside an expression template; otherwise, false.</returns>
    private bool IsInsideExpressionTemplate(SnapshotPoint point)
    {
        try
        {
            var context = SyntaxTreeAnalyzer.GetExpressionContext(point.Snapshot, point.Position);
            return context == ExpressionContext.ExpressionTemplate;
        }
        catch
        {
            // Fallback: check for ExpressionTemplate in the line text
            var line = point.GetContainingLine();
            var lineText = line.GetText();
            return lineText.Contains("ExpressionTemplate") && SerilogCallDetector.IsSerilogCall(lineText);
        }
    }

    /// <summary>
    /// Finds matching braces within expression template context, handling nested structures.
    /// </summary>
    /// <param name="point">The snapshot point at the brace.</param>
    /// <returns>A tuple of open and close brace points, or null if no match found.</returns>
    private (SnapshotPoint? open, SnapshotPoint? close)? FindExpressionBraceMatch(SnapshotPoint point)
    {
        var snapshot = point.Snapshot;
        var currentChar = point.GetChar();

        if (currentChar == '{')
        {
            // Find closing brace, handling expression template nesting
            return FindExpressionClosingBrace(snapshot, point, MaxExpressionLength);
        }
        else if (currentChar == '}')
        {
            // Find opening brace, handling expression template nesting
            return FindExpressionOpeningBrace(snapshot, point, MaxExpressionLength);
        }

        return null;
    }

    /// <summary>
    /// Checks if the cursor is inside a multi-line string (verbatim or raw).
    /// </summary>
    /// <remarks>
    /// This method searches backward up to <see cref="MaxLookbackLines"/> lines (20 by default)
    /// and forward up to <see cref="MaxLookforwardLines"/> lines (50 by default) to find
    /// unclosed string delimiters, balancing performance with accuracy.
    /// </remarks>
    /// <param name="point">The snapshot point to check.</param>
    /// <returns>True if inside a multi-line string; otherwise, false.</returns>
    private bool IsInsideMultiLineString(SnapshotPoint point)
    {
        var line = point.GetContainingLine();
        var snapshot = point.Snapshot;

        // Look backwards for unclosed verbatim (@") or raw string (""")
        for (int i = line.LineNumber; i >= Math.Max(0, line.LineNumber - MaxLookbackLines); i--)
        {
            var checkLine = snapshot.GetLineFromLineNumber(i);
            var lineText = checkLine.GetText();

            // Check for raw string opener
            if (lineText.Contains("\"\"\""))
            {
                // Check if it's a Serilog call
                if (SerilogCallDetector.IsSerilogCall(lineText))
                {
                    // Look forward to see if it's closed
                    for (int j = i + 1; j < Math.Min(snapshot.LineCount, i + MaxLookforwardLines); j++)
                    {
                        var forwardLine = snapshot.GetLineFromLineNumber(j);
                        if (forwardLine.GetText().TrimStart().StartsWith("\"\"\""))
                        {
                            // Found closing, check if we're between them
                            if (line.LineNumber > i && line.LineNumber < j)
                                return true;
                            break;
                        }
                    }
                }
            }

            // Check for verbatim string opener
            if (lineText.Contains("@\""))
            {
                var atIndex = lineText.IndexOf("@\"");
                if (atIndex >= 0)
                {
                    // Check if this line OR a previous line has a Serilog call
                    bool hasSerilogCall = SerilogCallDetector.IsSerilogCall(lineText);
                    if (!hasSerilogCall && i > 0)
                    {
                        // Check previous line for Serilog call
                        var prevLine = snapshot.GetLineFromLineNumber(i - 1);
                        hasSerilogCall = SerilogCallDetector.IsSerilogCall(prevLine.GetText());
                    }

                    if (hasSerilogCall)
                    {
                        // For verbatim strings, we need to track if it's closed
                        // by looking for an unescaped quote
                        bool stringClosed = false;

                        // First check if it closes on the same line
                        var restOfLine = lineText.Substring(atIndex + 2);
                        int pos = 0;
                        while (pos < restOfLine.Length)
                        {
                            if (restOfLine[pos] == '"')
                            {
                                // Check if it's escaped (followed by another quote)
                                if (pos + 1 < restOfLine.Length && restOfLine[pos + 1] == '"')
                                {
                                    pos += 2; // Skip escaped quote pair
                                }
                                else
                                {
                                    // Found closing quote on same line
                                    stringClosed = true;
                                    break;
                                }
                            }
                            else
                            {
                                pos++;
                            }
                        }

                        if (!stringClosed)
                        {
                            // String didn't close on this line, look forward for closing
                            for (int j = i + 1; j < Math.Min(snapshot.LineCount, i + MaxLookforwardLines); j++)
                            {
                                var forwardLine = snapshot.GetLineFromLineNumber(j);
                                var forwardText = forwardLine.GetText();

                                // Look for closing quote in this line
                                pos = 0;
                                while (pos < forwardText.Length)
                                {
                                    if (forwardText[pos] == '"')
                                    {
                                        // Check if escaped
                                        if (pos + 1 < forwardText.Length && forwardText[pos + 1] == '"')
                                        {
                                            pos += 2; // Skip escaped pair
                                        }
                                        else
                                        {
                                            // Found unescaped quote - this closes the string
                                            stringClosed = true;

                                            // Check if we're between opening and closing
                                            if (line.LineNumber > i && line.LineNumber <= j)
                                                return true;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        pos++;
                                    }
                                }

                                if (stringClosed)
                                    break;
                            }

                            // If still not closed and we're after the opening line
                            if (!stringClosed && line.LineNumber > i)
                                return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Finds matching braces across line boundaries.
    /// </summary>
    /// <remarks>
    /// Searches are limited to <see cref="MaxPropertyLength"/> characters (200 by default)
    /// in either direction from the starting brace to prevent performance issues with
    /// malformed or extremely long properties.
    /// </remarks>
    /// <param name="point">The snapshot point at the brace.</param>
    /// <returns>A tuple of open and close brace points, or null if no match found.</returns>
    private (SnapshotPoint? open, SnapshotPoint? close)? FindMultiLineBraceMatch(SnapshotPoint point)
    {
        var snapshot = point.Snapshot;
        var currentChar = point.GetChar();

        if (currentChar == '{')
        {
            // Find closing brace, potentially on another line
            int braceCount = 1;
            for (int pos = point.Position + 1; pos < snapshot.Length; pos++)
            {
                char ch = snapshot[pos];

                // Check for escaped braces
                if (ch == '{' && pos + 1 < snapshot.Length && snapshot[pos + 1] == '{')
                {
                    pos++; // Skip escaped
                    continue;
                }

                if (ch == '}' && pos + 1 < snapshot.Length && snapshot[pos + 1] == '}')
                {
                    pos++; // Skip escaped
                    continue;
                }

                if (ch == '{')
                    braceCount++;
                else if (ch == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        return (point, new SnapshotPoint(snapshot, pos));
                    }
                }

                // Don't search too far (e.g., max chars for a property)
                if (pos - point.Position > MaxPropertyLength)
                    break;
            }
        }
        else if (currentChar == '}')
        {
            // Find opening brace, potentially on another line
            int braceCount = 1;
            for (int pos = point.Position - 1; pos >= 0; pos--)
            {
                char ch = snapshot[pos];

                // Check for escaped braces
                if (ch == '}' && pos > 0 && snapshot[pos - 1] == '}')
                {
                    pos--; // Skip escaped
                    continue;
                }

                if (ch == '{' && pos > 0 && snapshot[pos - 1] == '{')
                {
                    pos--; // Skip escaped
                    continue;
                }

                if (ch == '}')
                    braceCount++;
                else if (ch == '{')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        return (new SnapshotPoint(snapshot, pos), point);
                    }
                }

                // Don't search too far
                if (point.Position - pos > MaxPropertyLength)
                    break;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the tags that intersect the given spans.
    /// </summary>
    /// <param name="spans">The spans to get tags for.</param>
    /// <returns>Tags for matching braces if the caret is positioned on a brace in a Serilog template.</returns>
    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        if (_disposed || !_currentChar.HasValue || spans.Count == 0)
            yield break;

        var snapshot = spans[0].Snapshot;

        // Respect user setting: if automatic delimiter highlighting is off, do nothing.
        // Note: In VS 2022, this option is accessed via the editor options system
        // For now, we'll always enable brace matching as the option check requires
        // additional references to access the option properly

        var currentChar = _currentChar.Value;
        if (currentChar.Position >= snapshot.Length)
            yield break;

        var currentLine = currentChar.GetContainingLine();
        var lineStart = currentLine.Start.Position;
        var lineText = currentLine.GetText();

        // First check if we're in a multi-line string context
        bool inMultiLineString = IsInsideMultiLineString(currentChar);

        // Check if we're in an expression template context
        bool inExpressionTemplate = IsInsideExpressionTemplate(currentChar);

        // For single-line strings, check if we're in a Serilog context
        bool inSerilogContext = IsSerilogCall(lineText);

        // If not found on current line, check previous line for configuration patterns
        // Use SerilogCallDetector for more robust detection
        if (!inSerilogContext && currentLine.LineNumber > 0)
        {
            var prevLine = snapshot.GetLineFromLineNumber(currentLine.LineNumber - 1);
            var prevText = prevLine.GetText();
            // Use the centralized detector which has more precise pattern matching
            inSerilogContext = SerilogCallDetector.IsSerilogCall(prevText);
        }

        if (!inMultiLineString && !inExpressionTemplate && !inSerilogContext)
            yield break;

        var charAtCaret = currentChar.GetChar();
        var positionInLine = currentChar.Position - lineStart;

        SnapshotPoint? openPt = null;
        SnapshotPoint? closePt = null;

        if (inExpressionTemplate)
        {
            // Use expression template brace matching
            // Following VS standard: highlight when cursor is to LEFT of { or RIGHT of }
            if (charAtCaret == '{')
            {
                // Cursor is to the left of opening brace - should match
                var match = FindExpressionBraceMatch(currentChar);
                if (match.HasValue)
                {
                    openPt = match.Value.open;
                    closePt = match.Value.close;
                }
            }
            // Check if cursor is just after closing brace
            else if (currentChar.Position > 0)
            {
                var prevPoint = new SnapshotPoint(snapshot, currentChar.Position - 1);
                var prevChar = prevPoint.GetChar();
                // Only match when cursor is after closing brace (VS standard)
                if (prevChar == '}')
                {
                    var match = FindExpressionBraceMatch(prevPoint);
                    if (match.HasValue)
                    {
                        openPt = match.Value.open;
                        closePt = match.Value.close;
                    }
                }
            }
        }
        else if (inMultiLineString)
        {
            // Use multi-line brace matching
            var match = FindMultiLineBraceMatch(currentChar);
            if (match.HasValue)
            {
                openPt = match.Value.open;
                closePt = match.Value.close;
            }
            else if (currentChar.Position > 0)
            {
                // Check if cursor is just after a brace
                var prevPoint = new SnapshotPoint(snapshot, currentChar.Position - 1);
                if (prevPoint.GetChar() == '}')
                {
                    var prevMatch = FindMultiLineBraceMatch(prevPoint);
                    if (prevMatch.HasValue)
                    {
                        openPt = prevMatch.Value.open;
                        closePt = prevMatch.Value.close;
                    }
                }
            }
        }
        else
        {
            // Use existing single-line logic
            int open = -1, close = -1;

            if (charAtCaret == '{')
            {
                open = positionInLine;
                close = FindMatchingCloseBrace(lineText, positionInLine);
            }
            else if (charAtCaret == '}')
            {
                close = positionInLine;
                open = FindMatchingOpenBrace(lineText, positionInLine);
            }
            else if (positionInLine > 0 && lineText[positionInLine - 1] == '}')
            {
                close = positionInLine - 1;
                open = FindMatchingOpenBrace(lineText, close);
            }

            if (open >= 0 && close >= 0)
            {
                openPt = new SnapshotPoint(snapshot, lineStart + open);
                closePt = new SnapshotPoint(snapshot, lineStart + close);
            }
        }

        // Create tag spans if we found a match
        if (openPt.HasValue && closePt.HasValue)
        {
            _state.SetCurrentPair(openPt.Value, closePt.Value);

            if (!_state.IsDismissedForCurrentPair)
            {
                yield return CreateTagSpan(snapshot, openPt.Value.Position, 1);
                yield return CreateTagSpan(snapshot, closePt.Value.Position, 1);
            }
        }
        else
        {
            _state.ClearCurrentPair();
        }
    }

    /// <summary>
    /// Determines whether the given line contains a Serilog call.
    /// </summary>
    /// <param name="line">The line to check.</param>
    /// <returns>True if the line contains a Serilog call; otherwise, false.</returns>
    private bool IsSerilogCall(string line)
    {
        return SerilogCallDetector.IsSerilogCall(line);
    }


    /// <summary>
    /// Finds the matching closing brace for an opening brace in expression context.
    /// </summary>
    /// <param name="snapshot">The text snapshot to search in.</param>
    /// <param name="openPoint">The position of the opening brace.</param>
    /// <param name="maxLength">Maximum search distance.</param>
    /// <returns>A tuple of open and close points, or null if no match found.</returns>
    private (SnapshotPoint? open, SnapshotPoint? close)? FindExpressionClosingBrace(
        ITextSnapshot snapshot,
        SnapshotPoint openPoint,
        int maxLength)
    {
        int braceCount = 1;

        for (int pos = openPoint.Position + 1; pos < snapshot.Length && pos < openPoint.Position + maxLength; pos++)
        {
            char ch = snapshot[pos];

            // Handle escaped braces
            if (ch == '{' && pos + 1 < snapshot.Length && snapshot[pos + 1] == '{')
            {
                pos++; // Skip escaped pair
                continue;
            }
            if (ch == '}' && pos + 1 < snapshot.Length && snapshot[pos + 1] == '}')
            {
                pos++; // Skip escaped pair
                continue;
            }

            if (ch == '{')
            {
                braceCount++;
            }
            else if (ch == '}')
            {
                braceCount--;
                if (braceCount == 0)
                {
                    return (openPoint, new SnapshotPoint(snapshot, pos));
                }
            }

            // Stop at string boundaries to avoid matching across different string literals
            if (ch == '"' && !IsEscapedQuote(snapshot, pos))
            {
                break;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the matching opening brace for a closing brace in expression context.
    /// </summary>
    /// <param name="snapshot">The text snapshot to search in.</param>
    /// <param name="closePoint">The position of the closing brace.</param>
    /// <param name="maxLength">Maximum search distance.</param>
    /// <returns>A tuple of open and close points, or null if no match found.</returns>
    private (SnapshotPoint? open, SnapshotPoint? close)? FindExpressionOpeningBrace(
        ITextSnapshot snapshot,
        SnapshotPoint closePoint,
        int maxLength)
    {
        int braceCount = 1;

        for (int pos = closePoint.Position - 1; pos >= 0 && pos > closePoint.Position - maxLength; pos--)
        {
            char ch = snapshot[pos];

            // Handle escaped braces
            if (ch == '}' && pos > 0 && snapshot[pos - 1] == '}')
            {
                pos--; // Skip escaped pair
                continue;
            }
            if (ch == '{' && pos > 0 && snapshot[pos - 1] == '{')
            {
                pos--; // Skip escaped pair
                continue;
            }

            if (ch == '}')
            {
                braceCount++;
            }
            else if (ch == '{')
            {
                braceCount--;
                if (braceCount == 0)
                {
                    return (new SnapshotPoint(snapshot, pos), closePoint);
                }
            }

            // Stop at string boundaries to avoid matching across different string literals
            if (ch == '"' && !IsEscapedQuote(snapshot, pos))
            {
                break;
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a quote character at the given position is escaped.
    /// </summary>
    /// <param name="snapshot">The text snapshot.</param>
    /// <param name="position">The position of the quote character.</param>
    /// <returns>True if the quote is escaped; otherwise, false.</returns>
    private bool IsEscapedQuote(ITextSnapshot snapshot, int position)
    {
        if (position == 0) return false;

        int backslashCount = 0;
        for (int i = position - 1; i >= 0 && snapshot[i] == '\\'; i--)
        {
            backslashCount++;
        }

        // Odd number of backslashes means the quote is escaped
        return backslashCount % 2 == 1;
    }

    /// <summary>
    /// Finds the matching closing brace for an opening brace.
    /// </summary>
    /// <param name="text">The text to search in.</param>
    /// <param name="openBracePos">The position of the opening brace.</param>
    /// <returns>The position of the matching closing brace, or -1 if not found.</returns>
    private int FindMatchingCloseBrace(string text, int openBracePos)
    {
        if (openBracePos + 1 < text.Length && text[openBracePos + 1] == '{')
            return -1; // Escaped brace

        int braceCount = 1;
        for (int i = openBracePos + 1; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                if (i + 1 < text.Length && text[i + 1] == '{')
                {
                    i++; // Skip escaped brace
                    continue;
                }

                braceCount++;
            }
            else if (text[i] == '}')
            {
                if (i + 1 < text.Length && text[i + 1] == '}')
                {
                    i++; // Skip escaped brace
                    continue;
                }

                braceCount--;
                if (braceCount == 0)
                    return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Finds the matching opening brace for a closing brace.
    /// </summary>
    /// <param name="text">The text to search in.</param>
    /// <param name="closeBracePos">The position of the closing brace.</param>
    /// <returns>The position of the matching opening brace, or -1 if not found.</returns>
    private int FindMatchingOpenBrace(string text, int closeBracePos)
    {
        if (closeBracePos > 0 && text[closeBracePos - 1] == '}')
            return -1; // Escaped brace

        int braceCount = 1;
        for (int i = closeBracePos - 1; i >= 0; i--)
        {
            if (text[i] == '}')
            {
                if (i > 0 && text[i - 1] == '}')
                {
                    i--; // Skip escaped brace
                    continue;
                }

                braceCount++;
            }
            else if (text[i] == '{')
            {
                if (i > 0 && text[i - 1] == '{')
                {
                    i--; // Skip escaped brace
                    continue;
                }

                braceCount--;
                if (braceCount == 0)
                    return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Creates a tag span for highlighting a brace.
    /// </summary>
    /// <param name="snapshot">The text snapshot.</param>
    /// <param name="start">The start position of the brace.</param>
    /// <param name="length">The length of the span (typically 1 for a single brace).</param>
    /// <returns>A tag span for the brace highlight.</returns>
    private ITagSpan<TextMarkerTag> CreateTagSpan(ITextSnapshot snapshot, int start, int length)
    {
        var span = new SnapshotSpan(snapshot, start, length);
        var tag = new TextMarkerTag("bracehighlight");
        return new TagSpan<TextMarkerTag>(span, tag);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _view.Caret.PositionChanged -= CaretPositionChanged;
        _view.LayoutChanged -= ViewLayoutChanged;
        _view.Closed -= View_Closed;
        _state.StateChanged -= StateChanged;
    }
}