using System.Collections.Generic;

namespace SerilogSyntax.Parsing;

/// <summary>
/// Provides methods for parsing various types of C# string literals including regular, verbatim, and raw strings.
/// </summary>
internal class StringLiteralParser
{
    /// <summary>
    /// Attempts to parse any type of string literal (regular, verbatim, or interpolated).
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="startIndex">The starting index of the string literal.</param>
    /// <param name="result">The parsed string boundaries and content.</param>
    /// <returns>True if a string literal was successfully parsed; otherwise, false.</returns>
    public bool TryParseStringLiteral(string text, int startIndex, out (int Start, int End, string Content) result)
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
    public bool TryParseVerbatimString(
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

    public bool TryParseRegularString(string text, int startIndex, out (int Start, int End, string Content) result)
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
    public bool TryParseRawStringLiteral(string text, int startIndex, out (int Start, int End, string Content) result)
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
    /// Finds the bounds and content of a string literal starting from the given index.
    /// </summary>
    /// <param name="text">The text to search in.</param>
    /// <param name="startIndex">The index to start searching from.</param>
    /// <param name="spanStart">The absolute position of the span start in the document.</param>
    /// <param name="skipFirstString">If true, skips the first string literal found (used for LogError with exception parameter).</param>
    /// <returns>
    /// A tuple containing the start position, end position, content, whether it's a verbatim string,
    /// and the quote count for raw strings, or null if not found.
    /// </returns>
    public (int start, int end, string text, bool isVerbatim, int quoteCount)? FindStringLiteral(
        string text,
        int startIndex,
        int spanStart,
        bool skipFirstString = false)
    {
        // Look for string literal after Serilog method call
        int parenDepth = 1;
        bool firstStringSkipped = !skipFirstString; // If we don't need to skip, act as if we already skipped

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
                if (!firstStringSkipped)
                {
                    // Skip this string literal and continue looking for the next one
                    firstStringSkipped = true;
                    startIndex = result.End + 1;
                    continue;
                }

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

            // If we're looking to skip the first string but haven't found any string yet,
            // check if we've passed the first parameter (comma or end of parameters)
            if (!firstStringSkipped && parenDepth == 1)
            {
                if (text[startIndex] == ',')
                {
                    // Found a comma - we've passed the first parameter without finding a string
                    firstStringSkipped = true;
                    startIndex++;
                    continue;
                }
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
    public List<(int start, int end, string text, bool isVerbatim, int quoteCount)> FindAllStringLiteralsInMatch(
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
    /// Finds all concatenated string literals in a Serilog call.
    /// </summary>
    public List<(int start, int end, string text, bool isVerbatim, int quoteCount)> FindAllConcatenatedStrings(
        string text, 
        int startIndex, 
        int spanStart)
    {
        var results = new List<(int start, int end, string text, bool isVerbatim, int quoteCount)>();
        int currentPos = startIndex;
        int parenDepth = 1;
        
        while (currentPos < text.Length && parenDepth > 0)
        {
            // Skip whitespace
            while (currentPos < text.Length && char.IsWhiteSpace(text[currentPos]))
                currentPos++;
                
            if (currentPos >= text.Length)
                break;
            
            // Check for parentheses
            if (text[currentPos] == '(')
            {
                parenDepth++;
                currentPos++;
                continue;
            }
            else if (text[currentPos] == ')')
            {
                parenDepth--;
                currentPos++;
                continue;
            }
            
            // Try to parse a string literal (handles regular, verbatim @", and raw """ strings)
            if (TryParseStringLiteral(text, currentPos, out var parsed))
            {
                // Determine string type
                bool isVerbatim = currentPos < text.Length && text[currentPos] == '@';
                int quoteCount = 1;
                
                // Check for raw string literal
                if (text[currentPos] == '"')
                {
                    int countPos = currentPos;
                    quoteCount = 0;
                    while (countPos < text.Length && text[countPos] == '"')
                    {
                        quoteCount++;
                        countPos++;
                    }
                }
                
                results.Add((spanStart + parsed.Start, spanStart + parsed.End, parsed.Content, isVerbatim, quoteCount));
                
                // Move past the string literal
                currentPos = parsed.End + 1;
                
                // Skip whitespace after the string
                while (currentPos < text.Length && char.IsWhiteSpace(text[currentPos]))
                    currentPos++;
                    
                // Check for concatenation operator
                if (currentPos < text.Length && text[currentPos] == '+')
                {
                    currentPos++; // Skip the + and continue looking for more strings
                    continue;
                }
                else if (currentPos < text.Length && text[currentPos] == ',')
                {
                    // This is the next argument, not concatenation
                    break;
                }
            }
            else
            {
                currentPos++;
            }
        }
        
        return results;
    }

    /// <summary>
    /// Checks if a character at the given position is escaped.
    /// Handles cases like \" (escaped quote) and \\" (escaped backslash followed by quote).
    /// </summary>
    public bool IsEscaped(string text, int position)
    {
        if (position <= 0) return false;
        
        // Count consecutive backslashes before the position
        int backslashCount = 0;
        int checkPos = position - 1;
        
        while (checkPos >= 0 && text[checkPos] == '\\')
        {
            backslashCount++;
            checkPos--;
        }
        
        // If odd number of backslashes, the character is escaped
        // If even number (including 0), it's not escaped
        return backslashCount % 2 == 1;
    }
}