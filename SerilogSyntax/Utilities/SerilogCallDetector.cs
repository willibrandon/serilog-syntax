using System.Text.RegularExpressions;

namespace SerilogSyntax.Utilities;

/// <summary>
/// Centralized utility for detecting Serilog method calls in source code.
/// Provides a single source of truth for the regex pattern used across all components.
/// </summary>
internal static class SerilogCallDetector
{
    /// <summary>
    /// Regex pattern that matches Serilog method calls and configuration templates.
    /// Supports both direct Serilog calls and Microsoft.Extensions.Logging integration.
    /// </summary>
    private static readonly Regex SerilogCallRegex = new(
        @"(?:\b\w+\.(?:ForContext(?:<[^>]+>)?\([^)]*\)\.)?(?:Log(?:Verbose|Debug|Information|Warning|Error|Critical|Fatal)|(?:Verbose|Debug|Information|Warning|Error|Fatal|Write)|BeginScope)\s*\()|(?:outputTemplate\s*:\s*)",
        RegexOptions.Compiled);

    /// <summary>
    /// Checks if the given line contains a Serilog method call.
    /// </summary>
    /// <param name="line">The line of text to check</param>
    /// <returns>True if the line contains a Serilog call, false otherwise</returns>
    public static bool IsSerilogCall(string line)
    {
        return SerilogCallRegex.IsMatch(line);
    }

    /// <summary>
    /// Finds the first Serilog method call match in the given text.
    /// </summary>
    /// <param name="text">The text to search</param>
    /// <returns>The first match, or null if no match is found</returns>
    public static Match FindSerilogCall(string text)
    {
        var match = SerilogCallRegex.Match(text);
        return match.Success ? match : null;
    }

    /// <summary>
    /// Finds all Serilog method call matches in the given text.
    /// </summary>
    /// <param name="text">The text to search</param>
    /// <returns>Collection of all matches</returns>
    public static MatchCollection FindAllSerilogCalls(string text)
    {
        return SerilogCallRegex.Matches(text);
    }
}