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
        "Log", "log", "_log", "logger", "Logger", "outputTemplate",
        "Filter", "ByExcluding", "ByIncludingOnly", 
        "WithComputed", "ExpressionTemplate", "Conditional", "When"
    ];
    
    private static readonly HashSet<string> SerilogMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Verbose", "Debug", "Information", "Warning", "Error", "Critical", "Fatal", 
        "Write", "LogVerbose", "LogDebug", "LogInformation", "LogWarning", 
        "LogError", "LogCritical", "LogFatal", "BeginScope",
        // Serilog.Expressions methods
        "ByExcluding", "ByIncludingOnly", "WithComputed", "Conditional", "When",
        "ExpressionTemplate"
    };
    
    /// <summary>
    /// Regex pattern that matches Serilog method calls and configuration templates.
    /// Supports both direct Serilog calls and Microsoft.Extensions.Logging integration.
    /// Also supports Serilog.Expressions API calls.
    /// </summary>
    private static readonly Regex SerilogCallRegex = new(
        @"(?:\b\w+\.(?:ForContext(?:<[^>]+>)?\([^)]*\)\.)?(?:Log(?:Verbose|Debug|Information|Warning|Error|Critical|Fatal)|(?:Verbose|Debug|Information|Warning|Error|Fatal|Write)|BeginScope)\s*\()|(?:outputTemplate\s*:\s*)|(?:\.?(?:Filter\.)?(?:ByExcluding|ByIncludingOnly)\s*\()|(?:\.?(?:Enrich\.)?(?:WithComputed|When)\s*\()|(?:\.?(?:WriteTo\.)?Conditional\s*\()|(?:new\s+ExpressionTemplate\s*\()",
        RegexOptions.Compiled);

    /// <summary>
    /// Regex pattern that matches multi-line ForContext patterns where ForContext is on one line
    /// and the logging method is on the next line.
    /// Example: someVar.ForContext<T>()
    ///              .Information("template", ...)
    /// </summary>
    private static readonly Regex MultiLineForContextRegex = new(
        @"
        # Match variable name and ForContext call
        (\w+)                       # variable name (e.g., log, logger)
        \.ForContext                # .ForContext
        (?:<[^>]+>)?                # optional generic type parameter
        \s*\(\s*\)                  # parentheses with optional whitespace
        \s*\r?\n\s*                 # newline and optional whitespace
        \.                          # dot before logging method
        (?:Information|Debug|Warning|Error|Fatal|Verbose) # logging method
        \s*\(                       # opening parenthesis
        \s*""([^""]+)""             # string literal (template)
        ",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

    /// <summary>
    /// Regex pattern that matches multi-line outputTemplate patterns where outputTemplate:
    /// is on one line and the template string is on the next line.
    /// Example: .WriteTo.Console(outputTemplate:
    ///              "[{Timestamp:HH:mm:ss}] {Message}")
    /// </summary>
    private static readonly Regex MultiLineOutputTemplateRegex = new(
        @"
        outputTemplate\s*:\s*       # outputTemplate: with optional whitespace
        \r?\n\s*                    # newline and optional whitespace/indentation
        ""([^""]+)""                # string literal (template)
        ",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace);

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
        if (string.IsNullOrWhiteSpace(line))
            return false;

        // For multi-line blocks, check if ANY line contains Serilog patterns
        if (line.Contains("\n"))
        {
            var lines = line.Split('\n');
            foreach (var singleLine in lines)
            {
                if (IsSerilogCallSingleLine(singleLine))
                {
                    return true;
                }
            }
            return false;
        }
        else
        {
            return IsSerilogCallSingleLine(line);
        }
    }

    private static bool IsSerilogCallSingleLine(string line)
    {
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
        
        // Check for Serilog.Expressions patterns
        if (line.Contains("Filter") || line.Contains("Enrich") || line.Contains("WriteTo") || line.Contains("ExpressionTemplate"))
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
    /// Finds all multi-line ForContext patterns in the text where ForContext is on one line
    /// and the logging method is on the next line.
    /// </summary>
    /// <param name="text">The text to search</param>
    /// <returns>Matches containing the multi-line ForContext patterns</returns>
    public static MatchCollection FindMultiLineForContextCalls(string text)
    {
        return MultiLineForContextRegex.Matches(text);
    }
    
    /// <summary>
    /// Finds all multi-line outputTemplate patterns in the text where outputTemplate:
    /// is on one line and the template string is on the next line.
    /// </summary>
    /// <param name="text">The text to search</param>
    /// <returns>Matches containing the multi-line outputTemplate patterns</returns>
    public static MatchCollection FindMultiLineOutputTemplateCalls(string text)
    {
        return MultiLineOutputTemplateRegex.Matches(text);
    }
    
    /// <summary>
    /// Clears the internal cache. Useful when memory needs to be reclaimed.
    /// </summary>
    public static void ClearCache()
    {
        CallCache.Clear();
    }
}