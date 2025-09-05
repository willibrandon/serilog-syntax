using SerilogSyntax.Expressions;
using Xunit;

namespace SerilogSyntax.Tests.Expressions;

public class TokenTests
{
    [Fact]
    public void Token_Constructor_SetsAllProperties()
    {
        // Arrange & Act
        var token = new Token(TokenType.Identifier, "UserName", 10, 8);
        
        // Assert
        Assert.Equal(TokenType.Identifier, token.Type);
        Assert.Equal("UserName", token.Value);
        Assert.Equal(10, token.Start);
        Assert.Equal(8, token.Length);
    }
    
    [Fact]
    public void Token_TypeSetter_UpdatesValue()
    {
        // Arrange
        var token = new Token(TokenType.Identifier, "test", 0, 4)
        {
            // Act
            Type = TokenType.Function
        };

        // Assert
        Assert.Equal(TokenType.Function, token.Type);
    }
    
    [Fact]
    public void Token_ValueSetter_UpdatesValue()
    {
        // Arrange
        var token = new Token(TokenType.StringLiteral, "old", 0, 3)
        {
            // Act
            Value = "new value"
        };

        // Assert
        Assert.Equal("new value", token.Value);
    }
    
    [Fact]
    public void Token_StartSetter_UpdatesValue()
    {
        // Arrange
        var token = new Token(TokenType.NumberLiteral, "123", 5, 3)
        {
            // Act
            Start = 25
        };

        // Assert
        Assert.Equal(25, token.Start);
    }
    
    [Fact]
    public void Token_LengthSetter_UpdatesValue()
    {
        // Arrange
        var token = new Token(TokenType.Identifier, "id", 0, 2)
        {
            // Act
            Length = 10
        };

        // Assert
        Assert.Equal(10, token.Length);
    }
    
    [Theory]
    [InlineData(TokenType.Identifier, "prop", 0, 4)]
    [InlineData(TokenType.ComparisonOperator, "==", 5, 2)]
    [InlineData(TokenType.StringLiteral, "'test'", 10, 6)]
    [InlineData(TokenType.NumberLiteral, "42.5", 20, 4)]
    [InlineData(TokenType.Function, "Substring", 30, 9)]
    [InlineData(TokenType.Keyword, "and", 40, 3)]
    [InlineData(TokenType.BooleanLiteral, "true", 50, 4)]
    [InlineData(TokenType.Comma, ",", 60, 1)]
    [InlineData(TokenType.Unknown, "", 70, 0)]
    public void Token_AllTokenTypes_WorkCorrectly(TokenType type, string value, int start, int length)
    {
        // Arrange & Act
        var token = new Token(type, value, start, length);
        
        // Assert
        Assert.Equal(type, token.Type);
        Assert.Equal(value, token.Value);
        Assert.Equal(start, token.Start);
        Assert.Equal(length, token.Length);
    }
    
    [Fact]
    public void Token_SettersWithNullValue_HandlesCorrectly()
    {
        // Arrange
        var token = new Token(TokenType.StringLiteral, "initial", 0, 7)
        {
            // Act
            Value = null
        };

        // Assert
        Assert.Null(token.Value);
    }
    
    [Fact]
    public void Token_SettersWithNegativeValues_AllowsNegativeStartAndLength()
    {
        // Arrange
        var token = new Token(TokenType.Unknown, "", 0, 0)
        {
            // Act
            Start = -1,
            Length = -1
        };

        // Assert - negative values are allowed (might indicate error conditions)
        Assert.Equal(-1, token.Start);
        Assert.Equal(-1, token.Length);
    }
    
    [Fact]
    public void Token_MultipleSetterCalls_UpdatesCorrectly()
    {
        // Arrange
        var token = new Token(TokenType.Identifier, "initial", 0, 7)
        {
            // Act - multiple updates
            Type = TokenType.Function,
            Value = "updated",
            Start = 100,
            Length = 7
        };

        token.Type = TokenType.ComparisonOperator;
        token.Value = "final";
        token.Start = 200;
        token.Length = 5;
        
        // Assert - should have final values
        Assert.Equal(TokenType.ComparisonOperator, token.Type);
        Assert.Equal("final", token.Value);
        Assert.Equal(200, token.Start);
        Assert.Equal(5, token.Length);
    }
}