namespace SerilogSyntax.Expressions;

/// <summary>
/// Represents a token in an expression.
/// </summary>
public class Token(TokenType type, string value, int start, int length)
{
    public TokenType Type { get; set; } = type;

    public string Value { get; set; } = value;

    public int Start { get; set; } = start;

    public int Length { get; set; } = length;
}
