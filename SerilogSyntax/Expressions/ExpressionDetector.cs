using System;
using System.Text.RegularExpressions;
using SerilogSyntax.Utilities;

namespace SerilogSyntax.Expressions;

/// <summary>
/// Detects Serilog.Expressions contexts in source code.
/// Identifies whether a string should be parsed as an expression or template.
/// </summary>
internal class ExpressionDetector
{
    // Regex patterns for detecting expression contexts
    private static readonly Regex FilterExpressionRegex = new(
        @"\b(?:Filter\.)?(?:ByExcluding|ByIncludingOnly)\s*\(\s*""",
        RegexOptions.Compiled);
    
    private static readonly Regex ConditionalWriteRegex = new(
        @"\b(?:WriteTo\.)?Conditional\s*\(\s*""",
        RegexOptions.Compiled);
    
    private static readonly Regex EnrichWhenRegex = new(
        @"\b(?:Enrich\.)?When\s*\(\s*""",
        RegexOptions.Compiled);
    
    private static readonly Regex EnrichComputedRegex = new(
        @"\b(?:Enrich\.)?WithComputed\s*\(\s*""[^""]*""\s*,\s*""",
        RegexOptions.Compiled);
    
    private static readonly Regex ExpressionTemplateRegex = new(
        @"\bnew\s+ExpressionTemplate\s*\(\s*[@$]?""",
        RegexOptions.Compiled);
    
    private static readonly LruCache<(string line, int position), ExpressionContext> ContextCache = new(100);
    
    /// <summary>
    /// Determines if the given text contains an expression template.
    /// Expression templates support directives like {#if}, {#each}, etc.
    /// </summary>
    public static bool IsExpressionTemplate(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 4) // Min: {@t}
            return false;
        
        // Quick check for common indicators
        if (!text.Contains("{"))
            return false;
        
        // Use IndexOf instead of Contains for better performance
        return text.IndexOf("{#if", StringComparison.Ordinal) >= 0 ||
               text.IndexOf("{#each", StringComparison.Ordinal) >= 0 ||
               text.IndexOf("{#else", StringComparison.Ordinal) >= 0 ||
               text.IndexOf("{#end", StringComparison.Ordinal) >= 0 ||
               text.IndexOf("..@", StringComparison.Ordinal) >= 0 ||
               text.IndexOf("@p[", StringComparison.Ordinal) >= 0 ||
               text.IndexOf("@i", StringComparison.Ordinal) >= 0 ||
               text.IndexOf("@r", StringComparison.Ordinal) >= 0 ||
               text.IndexOf("@tr", StringComparison.Ordinal) >= 0 ||
               text.IndexOf("@sp", StringComparison.Ordinal) >= 0;
    }
    
    /// <summary>
    /// Determines if the given text contains a filter expression.
    /// </summary>
    public static bool IsFilterExpression(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 5) // Min: "x = y"
            return false;
        
        // Use single pass with early exit
        for (int i = 0; i < text.Length - 2; i++)
        {
            if (text[i] == ' ')
            {
                // Check for operators starting with space
                if (i + 5 <= text.Length && string.CompareOrdinal(text, i, " like ", 0, 6) == 0)
                    return true;
                if (i + 10 <= text.Length && string.CompareOrdinal(text, i, " not like ", 0, 10) == 0)
                    return true;
                if (i + 4 <= text.Length && string.CompareOrdinal(text, i, " in ", 0, 4) == 0)
                    return true;
                if (i + 8 <= text.Length && string.CompareOrdinal(text, i, " not in ", 0, 8) == 0)
                    return true;
                if (i + 8 <= text.Length && string.CompareOrdinal(text, i, " is null", 0, 8) == 0)
                    return true;
                if (i + 12 <= text.Length && string.CompareOrdinal(text, i, " is not null", 0, 12) == 0)
                    return true;
                if (i + 5 <= text.Length && string.CompareOrdinal(text, i, " and ", 0, 5) == 0)
                    return true;
                if (i + 4 <= text.Length && string.CompareOrdinal(text, i, " or ", 0, 4) == 0)
                    return true;
                if (i + 3 <= text.Length && string.CompareOrdinal(text, i, " ci", 0, 3) == 0)
                    return true;
            }
        }
        
        return Regex.IsMatch(text, @"\b(?:StartsWith|EndsWith|Contains|TypeOf|IsDefined|Length)\s*\(");
    }
    
    /// <summary>
    /// Gets the expression context for a given position in the text.
    /// </summary>
    public static ExpressionContext GetContext(string line, int position)
    {
        if (string.IsNullOrWhiteSpace(line))
            return ExpressionContext.None;
        
        var cacheKey = (line, position);
        if (ContextCache.TryGetValue(cacheKey, out var cached))
            return cached;
        
        var context = DetectContext(line, position);
        ContextCache.Add(cacheKey, context);
        return context;
    }

    /// <summary>
    /// Clears the internal context cache.
    /// </summary>
    public static void ClearCache()
    {
        ContextCache.Clear();
    }

    private static ExpressionContext DetectContext(string line, int position)
    {
        // Check for ExpressionTemplate
        if (ExpressionTemplateRegex.IsMatch(line))
        {
            var match = ExpressionTemplateRegex.Match(line);
            // Check if position is after the opening quote (match includes the API call + opening quote)
            if (position >= match.Index + match.Length)
                return ExpressionContext.ExpressionTemplate;
        }
        
        // Check for Filter expressions
        if (FilterExpressionRegex.IsMatch(line))
        {
            var match = FilterExpressionRegex.Match(line);
            // Check if position is after the opening quote (match includes the API call + opening quote)
            if (position >= match.Index + match.Length)
                return ExpressionContext.FilterExpression;
        }
        
        // Check for Conditional expressions
        if (ConditionalWriteRegex.IsMatch(line) || EnrichWhenRegex.IsMatch(line))
        {
            var match = ConditionalWriteRegex.IsMatch(line) 
                ? ConditionalWriteRegex.Match(line) 
                : EnrichWhenRegex.Match(line);
            if (position >= match.Index + match.Length)
                return ExpressionContext.ConditionalExpression;
        }
        
        // Check for Computed property (second string argument)
        if (EnrichComputedRegex.IsMatch(line))
        {
            var match = EnrichComputedRegex.Match(line);
            if (position >= match.Index + match.Length)
                return ExpressionContext.ComputedProperty;
        }
        
        return ExpressionContext.None;
    }
    
    /// <summary>
    /// Checks if the line appears to be calling an expression-related API.
    /// </summary>
    public static bool IsExpressionCall(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length < 10)
            return false;
        
        // Quick check for common patterns using IndexOf
        if (line.IndexOf("Filter", StringComparison.Ordinal) < 0 && 
            line.IndexOf("Conditional", StringComparison.Ordinal) < 0 && 
            line.IndexOf("Enrich", StringComparison.Ordinal) < 0 && 
            line.IndexOf("ExpressionTemplate", StringComparison.Ordinal) < 0)
            return false;
        
        return FilterExpressionRegex.IsMatch(line) ||
               ConditionalWriteRegex.IsMatch(line) ||
               EnrichWhenRegex.IsMatch(line) ||
               EnrichComputedRegex.IsMatch(line) ||
               ExpressionTemplateRegex.IsMatch(line);
    }
}