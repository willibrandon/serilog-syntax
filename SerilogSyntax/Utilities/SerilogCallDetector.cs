using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SerilogSyntax.Utilities;

/// <summary>
/// Centralized utility for detecting Serilog method calls in source code.
/// Provides a single source of truth for the regex pattern used across all components.
/// </summary>
internal static class SerilogCallDetector
{
    // Quick check strings for early rejection
    private static readonly string[] QuickCheckPatterns = 
    [
        "Log", "log", "_log", "logger", "Logger", "outputTemplate"
    ];
    
    private static readonly HashSet<string> SerilogMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Verbose", "Debug", "Information", "Warning", "Error", "Critical", "Fatal", 
        "Write", "LogVerbose", "LogDebug", "LogInformation", "LogWarning", 
        "LogError", "LogCritical", "LogFatal", "BeginScope"
    };
    
    /// <summary>
    /// Regex pattern that matches Serilog method calls and configuration templates.
    /// Supports both direct Serilog calls and Microsoft.Extensions.Logging integration.
    /// </summary>
    private static readonly Regex SerilogCallRegex = new(
        @"(?:\b\w+\.(?:ForContext(?:<[^>]+>)?\([^)]*\)\.)?(?:Log(?:Verbose|Debug|Information|Warning|Error|Critical|Fatal)|(?:Verbose|Debug|Information|Warning|Error|Fatal|Write)|BeginScope)\s*\()|(?:outputTemplate\s*:\s*)",
        RegexOptions.Compiled);

    // Cache for recent match results
    private static readonly LruCache<string, bool> CallCache = new(100);

    /// <summary>
    /// Checks if the given line contains a Serilog method call.
    /// Uses quick pre-checks to avoid expensive regex operations when possible.
    /// </summary>
    /// <param name="line">The line of text to check</param>
    /// <returns>True if the line contains a Serilog call, false otherwise</returns>
    public static bool IsSerilogCall(string line)
    {
        // Quick rejection for lines that definitely don't contain Serilog calls
        if (string.IsNullOrWhiteSpace(line))
            return false;
            
        // Quick check: does the line contain any potential logger references?
        bool hasPotentialLogger = false;
        foreach (var pattern in QuickCheckPatterns)
        {
            if (line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                hasPotentialLogger = true;
                break;
            }
        }
        
        if (!hasPotentialLogger)
            return false;
        
        // Now check if it has a Serilog method
        foreach (var method in SerilogMethods)
        {
            if (line.IndexOf(method, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // Finally, do the full regex check
                return SerilogCallRegex.IsMatch(line);
            }
        }
        
        // Check for outputTemplate
        if (line.Contains("outputTemplate"))
            return SerilogCallRegex.IsMatch(line);
            
        return false;
    }

    /// <summary>
    /// Checks if the given line contains a Serilog method call, with caching.
    /// </summary>
    /// <param name="line">The line of text to check</param>
    /// <returns>True if the line contains a Serilog call, false otherwise</returns>
    public static bool IsSerilogCallCached(string line)
    {
        if (CallCache.TryGetValue(line, out bool result))
            return result;
            
        result = IsSerilogCall(line);
        CallCache.Add(line, result);
        return result;
    }

    /// <summary>
    /// Finds the first Serilog method call match in the given text.
    /// </summary>
    /// <param name="text">The text to search</param>
    /// <returns>The first match, or null if no match is found</returns>
    public static Match FindSerilogCall(string text)
    {
        // Use pre-check before regex
        if (!IsSerilogCall(text))
            return null;
            
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
        // Use pre-check before regex
        if (!IsSerilogCall(text))
            return SerilogCallRegex.Matches(""); // Return empty collection
            
        return SerilogCallRegex.Matches(text);
    }
    
    /// <summary>
    /// Clears the internal cache. Useful when memory needs to be reclaimed.
    /// </summary>
    public static void ClearCache()
    {
        CallCache.Clear();
    }
}