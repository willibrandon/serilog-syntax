using Microsoft.VisualStudio.Text;
using SerilogSyntax.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SerilogSyntax.Utilities;

/// <summary>
/// Provides methods for detecting whether a span is inside a multi-line string literal (verbatim or raw).
/// </summary>
internal class MultiLineStringDetector
{
    // Constants for lookback/lookforward limits
    private const int MaxVerbatimStringLookbackLines = 50;
    private const int MaxLookbackLines = 100;
    private const int MaxLookforwardLines = 500;

    // Cache for raw string region detection results
    private readonly ConcurrentDictionary<int, bool> _rawStringRegionCache = new();

    /// <summary>
    /// Clears cached information about raw string regions for the specified line.
    /// </summary>
    public void InvalidateCacheForLine(int lineNumber)
    {
        _rawStringRegionCache.TryRemove(lineNumber, out _);
    }

    /// <summary>
    /// Clears all cached information about raw string regions.
    /// </summary>
    public void ClearCache()
    {
        _rawStringRegionCache.Clear();
    }

    /// <summary>
    /// Checks if the given span is inside an unclosed verbatim string literal.
    /// </summary>
    public bool IsInsideVerbatimString(SnapshotSpan span)
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
    public bool IsInsideRawStringLiteral(SnapshotSpan span)
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

                    // Additional check: make sure we have exactly quoteCount quotes, not more
                    if (isClosing && nonWhitespaceIndex + quoteCount < lineText.Length)
                    {
                        if (lineText[nonWhitespaceIndex + quoteCount] == '"')
                        {
                            // More quotes than expected - not the closing
                            isClosing = false;
                        }
                    }

                    if (isClosing)
                    {
                        // Found closing quotes
                        return i;
                    }
                }
            }

            // If we've looked forward enough lines and didn't find closing, it's unclosed
            return -1;
        }
        else
        {
            // Single-line or incomplete multi-line raw string
            // Check if it's closed on the same line
            // For simplicity, return same line
            return startLineNumber;
        }
    }
}