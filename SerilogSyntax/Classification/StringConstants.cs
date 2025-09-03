namespace SerilogSyntax.Classification;

/// <summary>
/// String constants to avoid allocations during comparisons
/// </summary>
internal static class StringConstants
{
    public const string ExpressionTemplate = "ExpressionTemplate";
    public const string Filter = "Filter";
    public const string Enrich = "Enrich";
    public const string WriteTo = "WriteTo";
    public const string ByExcluding = "ByExcluding";
    public const string ByIncludingOnly = "ByIncludingOnly";
    public const string When = "When";
    public const string WithComputed = "WithComputed";
    public const string Conditional = "Conditional";
}
