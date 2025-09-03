using System.Linq;
using Xunit;
using SerilogSyntax.Expressions;

namespace SerilogSyntax.Tests.Expressions;

public class ExpressionTokenizerTests
{
    [Fact]
    public void Tokenize_StringLiteral_WithEscapedQuotes()
    {
        var tokenizer = new ExpressionTokenizer(ExpressionTestData.StringWithEscapedQuote);
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Single(tokens);
        Assert.Equal(TokenType.StringLiteral, tokens[0].Type);
        Assert.Equal("It's working", tokens[0].Value);
        Assert.Equal(0, tokens[0].Start);
        Assert.Equal(15, tokens[0].Length);
    }
    
    [Fact]
    public void Tokenize_HexNumber_Correct()
    {
        var tokenizer = new ExpressionTokenizer(ExpressionTestData.HexNumber);
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Single(tokens);
        Assert.Equal(TokenType.NumberLiteral, tokens[0].Type);
        Assert.Equal("0xC0FFEE", tokens[0].Value);
    }
    
    [Fact]
    public void Tokenize_DecimalNumber_WithNegative()
    {
        var tokenizer = new ExpressionTokenizer(ExpressionTestData.DecimalNumber);
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Single(tokens);
        Assert.Equal(TokenType.NumberLiteral, tokens[0].Type);
        Assert.Equal("-12.34", tokens[0].Value);
    }
    
    [Fact]
    public void Tokenize_BooleanLiterals_Recognized()
    {
        var tokenizer = new ExpressionTokenizer(ExpressionTestData.BooleanLiterals);
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(3, tokens.Count);
        Assert.Equal(TokenType.BooleanLiteral, tokens[0].Type);
        Assert.Equal("true", tokens[0].Value);
        Assert.Equal(TokenType.BooleanOperator, tokens[1].Type);
        Assert.Equal("and", tokens[1].Value);
        Assert.Equal(TokenType.BooleanLiteral, tokens[2].Type);
        Assert.Equal("false", tokens[2].Value);
    }
    
    [Fact]
    public void Tokenize_NullLiteral_Recognized()
    {
        var tokenizer = new ExpressionTokenizer(ExpressionTestData.NullLiteral);
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Single(tokens);
        Assert.Equal(TokenType.NullLiteral, tokens[0].Type);
        Assert.Equal("null", tokens[0].Value);
    }
    
    [Fact]
    public void Tokenize_NotLike_AsCompoundOperator()
    {
        var tokenizer = new ExpressionTokenizer("Message not like '%error%'");
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(3, tokens.Count);
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("Message", tokens[0].Value);
        Assert.Equal(TokenType.StringOperator, tokens[1].Type);
        Assert.Equal("not like", tokens[1].Value);
        Assert.Equal(TokenType.StringLiteral, tokens[2].Type);
    }
    
    [Fact]
    public void Tokenize_NotIn_AsCompoundOperator()
    {
        var tokenizer = new ExpressionTokenizer("Role not in ['Admin', 'User']");
        var tokens = tokenizer.Tokenize().ToList();
        
        var notInToken = tokens.FirstOrDefault(t => t.Type == TokenType.MembershipOperator);
        Assert.NotNull(notInToken);
        Assert.Equal("not in", notInToken.Value);
    }
    
    [Fact]
    public void Tokenize_IsNotNull_AsCompoundOperator()
    {
        var tokenizer = new ExpressionTokenizer("Exception is not null");
        var tokens = tokenizer.Tokenize().ToList();
        
        var nullOpToken = tokens.FirstOrDefault(t => t.Type == TokenType.NullOperator);
        Assert.NotNull(nullOpToken);
        Assert.Equal("is not null", nullOpToken.Value);
    }
    
    [Fact]
    public void Tokenize_BuiltinProperty_Recognized()
    {
        var tokenizer = new ExpressionTokenizer("@t @m @mt @l @x @p @i @r @tr @sp");
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(10, tokens.Count);
        Assert.All(tokens, t => Assert.Equal(TokenType.BuiltinProperty, t.Type));
        Assert.Contains(tokens, t => t.Value == "@t");
        Assert.Contains(tokens, t => t.Value == "@mt");
        Assert.Contains(tokens, t => t.Value == "@tr");
        Assert.Contains(tokens, t => t.Value == "@sp");
    }
    
    [Fact]
    public void Tokenize_IfDirective_Parsed()
    {
        var tokenizer = new ExpressionTokenizer("{#if Level = 'Error'}");
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(5, tokens.Count);
        Assert.Equal(TokenType.IfDirective, tokens[0].Type);
        Assert.Equal("{#if", tokens[0].Value);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("Level", tokens[1].Value);
        Assert.Equal(TokenType.ComparisonOperator, tokens[2].Type);
        Assert.Equal(TokenType.StringLiteral, tokens[3].Type);
        Assert.Equal(TokenType.CloseBrace, tokens[4].Type);
    }
    
    [Fact]
    public void Tokenize_ElseIfDirective_Parsed()
    {
        var tokenizer = new ExpressionTokenizer("{#else if Status = 'Warning'}");
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(5, tokens.Count);
        Assert.Equal(TokenType.ElseIfDirective, tokens[0].Type);
        Assert.Equal("{#else if", tokens[0].Value);
    }
    
    [Fact]
    public void Tokenize_NestedPropertyAccess_Correct()
    {
        var tokenizer = new ExpressionTokenizer(ExpressionTestData.NestedProperty);
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(7, tokens.Count);
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("Order", tokens[0].Value);
        Assert.Equal(TokenType.Dot, tokens[1].Type);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
        Assert.Equal("Customer", tokens[2].Value);
    }
    
    [Fact]
    public void Tokenize_ArrayIndexing_Correct()
    {
        var tokenizer = new ExpressionTokenizer(ExpressionTestData.ArrayIndexing);
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(4, tokens.Count);
        Assert.Equal(TokenType.Identifier, tokens[0].Type);
        Assert.Equal("Items", tokens[0].Value);
        Assert.Equal(TokenType.OpenBracket, tokens[1].Type);
        Assert.Equal(TokenType.NumberLiteral, tokens[2].Type);
        Assert.Equal("0", tokens[2].Value);
        Assert.Equal(TokenType.CloseBracket, tokens[3].Type);
    }
    
    [Fact]
    public void Tokenize_WildcardIndexing_Correct()
    {
        var tokenizer = new ExpressionTokenizer(ExpressionTestData.WildcardIndexing);
        var tokens = tokenizer.Tokenize().ToList();
        
        var wildcardToken = tokens.FirstOrDefault(t => t.Type == TokenType.Wildcard);
        Assert.NotNull(wildcardToken);
        Assert.Equal("?", wildcardToken.Value);
    }
    
    [Fact]
    public void Tokenize_FunctionCall_Recognized()
    {
        var tokenizer = new ExpressionTokenizer(ExpressionTestData.SimpleFunctionCall);
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(4, tokens.Count);
        Assert.Equal(TokenType.Function, tokens[0].Type);
        Assert.Equal("Length", tokens[0].Value);
        Assert.Equal(TokenType.OpenParen, tokens[1].Type);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
        Assert.Equal("Name", tokens[2].Value);
        Assert.Equal(TokenType.CloseParen, tokens[3].Type);
    }
    
    [Fact]
    public void Tokenize_FunctionWithCaseModifier_Correct()
    {
        var tokenizer = new ExpressionTokenizer(ExpressionTestData.FunctionWithModifier);
        var tokens = tokenizer.Tokenize().ToList();
        
        var ciToken = tokens.LastOrDefault(t => t.Type == TokenType.CaseModifier);
        Assert.NotNull(ciToken);
        Assert.Equal("ci", ciToken.Value);
    }
    
    [Fact]
    public void Tokenize_SpreadOperator_Recognized()
    {
        var tokenizer = new ExpressionTokenizer("{ ..properties }");
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(4, tokens.Count);
        Assert.Equal(TokenType.OpenBrace, tokens[0].Type);
        Assert.Equal(TokenType.SpreadOperator, tokens[1].Type);
        Assert.Equal("..", tokens[1].Value);
        Assert.Equal(TokenType.Identifier, tokens[2].Type);
        Assert.Equal(TokenType.CloseBrace, tokens[3].Type);
    }
    
    [Fact]
    public void Tokenize_ComparisonOperators_AllRecognized()
    {
        var operators = "= <> < <= > >=";
        var tokenizer = new ExpressionTokenizer(operators);
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(6, tokens.Count);
        Assert.All(tokens, t => Assert.Equal(TokenType.ComparisonOperator, t.Type));
        Assert.Equal("=", tokens[0].Value);
        Assert.Equal("<>", tokens[1].Value);
        Assert.Equal("<", tokens[2].Value);
        Assert.Equal("<=", tokens[3].Value);
        Assert.Equal(">", tokens[4].Value);
        Assert.Equal(">=", tokens[5].Value);
    }
    
    [Fact]
    public void Tokenize_ArithmeticOperators_AllRecognized()
    {
        var tokenizer = new ExpressionTokenizer("1 + 2 - 3 * 4 / 5 ^ 6 % 7");
        var tokens = tokenizer.Tokenize().Where(t => t.Type == TokenType.ArithmeticOperator).ToList();
        
        Assert.Equal(6, tokens.Count);
        Assert.Equal("+", tokens[0].Value);
        Assert.Equal("-", tokens[1].Value);
        Assert.Equal("*", tokens[2].Value);
        Assert.Equal("/", tokens[3].Value);
        Assert.Equal("^", tokens[4].Value);
        Assert.Equal("%", tokens[5].Value);
    }
    
    [Fact]
    public void Tokenize_ComplexExpression_CorrectTokens()
    {
        var expression = "User.Age >= 18 and Items[?].Price > 100.00";
        var tokenizer = new ExpressionTokenizer(expression);
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(14, tokens.Count);
        
        // Verify key tokens
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "User");
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "Age");
        Assert.Contains(tokens, t => t.Type == TokenType.ComparisonOperator && t.Value == ">=");
        Assert.Contains(tokens, t => t.Type == TokenType.NumberLiteral && t.Value == "18");
        Assert.Contains(tokens, t => t.Type == TokenType.BooleanOperator && t.Value == "and");
        Assert.Contains(tokens, t => t.Type == TokenType.Wildcard && t.Value == "?");
        Assert.Contains(tokens, t => t.Type == TokenType.NumberLiteral && t.Value == "100.00");
    }
    
    [Fact]
    public void Tokenize_KeywordsVsIdentifiers_CorrectlyDistinguished()
    {
        var tokenizer = new ExpressionTokenizer("if x then true else false");
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Equal(6, tokens.Count);
        Assert.Equal(TokenType.Keyword, tokens[0].Type);
        Assert.Equal("if", tokens[0].Value);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("x", tokens[1].Value);
        Assert.Equal(TokenType.Keyword, tokens[2].Type);
        Assert.Equal("then", tokens[2].Value);
        Assert.Equal(TokenType.BooleanLiteral, tokens[3].Type);
        Assert.Equal("true", tokens[3].Value);
        Assert.Equal(TokenType.Keyword, tokens[4].Type);
        Assert.Equal("else", tokens[4].Value);
        Assert.Equal(TokenType.BooleanLiteral, tokens[5].Type);
        Assert.Equal("false", tokens[5].Value);
    }
    
    [Fact]
    public void Tokenize_EmptyString_NoTokens()
    {
        var tokenizer = new ExpressionTokenizer("");
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Empty(tokens);
    }
    
    [Fact]
    public void Tokenize_WhitespaceOnly_NoTokens()
    {
        var tokenizer = new ExpressionTokenizer("   \t\n  ");
        var tokens = tokenizer.Tokenize().ToList();
        
        Assert.Empty(tokens);
    }
}