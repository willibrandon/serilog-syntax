namespace SerilogSyntax.Expressions;

/// <summary>
/// Represents the type of expression context detected.
/// </summary>
public enum ExpressionContext
{
    /// <summary>
    /// Not an expression context.
    /// </summary>
    None,

    /// <summary>
    /// Filter expression (ByExcluding, ByIncludingOnly).
    /// </summary>
    FilterExpression,

    /// <summary>
    /// Expression template with formatting directives.
    /// </summary>
    ExpressionTemplate,

    /// <summary>
    /// Computed property expression.
    /// </summary>
    ComputedProperty,

    /// <summary>
    /// Conditional expression (When, Conditional).
    /// </summary>
    ConditionalExpression
}
