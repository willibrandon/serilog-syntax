namespace SerilogSyntax.Expressions;

/// <summary>
/// Types of tokens in expressions.
/// </summary>
public enum TokenType
{
    // Literals
    StringLiteral,      // 'single quoted'
    NumberLiteral,      // 42, -12.34, 0xC0FFEE
    BooleanLiteral,     // true, false
    NullLiteral,        // null

    // Identifiers and properties
    Identifier,         // PropertyName, functionName
    BuiltinProperty,    // @t, @m, @l, @x, @p, @i, @r, @tr, @sp

    // Operators
    ComparisonOperator, // =, <>, <, <=, >, >=
    BooleanOperator,    // and, or, not
    ArithmeticOperator, // +, -, *, /, ^, %
    StringOperator,     // like, not like
    MembershipOperator, // in, not in
    NullOperator,       // is null, is not null
    CaseModifier,       // ci

    // Functions
    Function,           // Contains(), StartsWith(), etc.

    // Keywords
    Keyword,            // if, then, else, null, true, false

    // Structural
    Dot,                // .
    Comma,              // ,
    Colon,              // :
    OpenParen,          // (
    CloseParen,         // )
    OpenBracket,        // [
    CloseBracket,       // ]
    OpenBrace,          // {
    CloseBrace,         // }

    // Template directives
    IfDirective,        // {#if
    ElseIfDirective,    // {#else if
    ElseDirective,      // {#else
    EndDirective,       // {#end
    EachDirective,      // {#each
    DelimitDirective,   // {#delimit

    // Special
    SpreadOperator,     // ..
    Wildcard,           // ?, *
    At,                 // @
    Dollar,             // $
    Hash,               // #

    // Unknown/Error
    Unknown
}
