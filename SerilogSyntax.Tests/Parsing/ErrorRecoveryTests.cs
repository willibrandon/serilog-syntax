using SerilogSyntax.Parsing;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Parsing;

public class ErrorRecoveryTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_UnclosedProperty_RecoverGracefully()
    {
        var template = "Start {Unclosed and then {Complete} property";
        var result = _parser.Parse(template).ToList();
        
        // Should recover and find at least the complete property
        Assert.NotEmpty(result);
        Assert.Contains(result, p => p.Name == "Complete");
    }

    [Fact]
    public void Parse_MalformedProperty_SkipsAndContinues()
    {
        var template = "First {Invalid@#$} then {Valid} property";
        var result = _parser.Parse(template).ToList();
        
        Assert.Single(result);
        Assert.Equal("Valid", result[0].Name);
    }

    [Fact]
    public void Parse_NestedBraces_HandlesCorrectly()
    {
        var template = "Outer { {Inner} } and {Real}";
        var result = _parser.Parse(template).ToList();
        
        // Should find at least the real property
        Assert.Contains(result, p => p.Name == "Real");
    }

    [Fact]
    public void Parse_IncompleteTemplateAtEnd_ReturnsPartial()
    {
        var template = "Complete {First} and partial {Incomple";
        var result = _parser.Parse(template).ToList();
        
        // Should return both complete and partial properties
        Assert.Equal(2, result.Count);
        Assert.Equal("First", result[0].Name);
        Assert.Equal("Incomple", result[1].Name);
        Assert.Equal(-1, result[1].BraceEndIndex); // Partial property
    }

    [Fact]
    public void Parse_ConsecutiveMalformed_RecoversBetween()
    {
        var template = "{@} {Valid1} {$} {Valid2} {}";
        var result = _parser.Parse(template).ToList();
        
        Assert.Equal(2, result.Count);
        Assert.Equal("Valid1", result[0].Name);
        Assert.Equal("Valid2", result[1].Name);
    }

    [Theory]
    [InlineData("{", 0)]
    [InlineData("}", 0)]
    [InlineData("{{", 0)]
    [InlineData("}}", 0)]
    [InlineData("{}", 0)]
    [InlineData("{ }", 0)]
    [InlineData("{@}", 0)]
    [InlineData("{$}", 0)]
    public void Parse_EdgeCases_HandlesGracefully(string template, int expectedCount)
    {
        var result = _parser.Parse(template).ToList();
        Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void Parse_PropertyWithInvalidCharacters_Recovers()
    {
        var template = "Test {Prop*erty} and {ValidProp}";
        var result = _parser.Parse(template).ToList();
        
        // Should skip invalid property and find the valid one
        Assert.Single(result);
        Assert.Equal("ValidProp", result[0].Name);
    }

    [Fact]
    public void Parse_MultipleUnclosedProperties_HandlesAll()
    {
        var template = "{First {Second {Third";
        var result = _parser.Parse(template).ToList();
        
        // Should find at least one partial property
        Assert.NotEmpty(result);
        Assert.Contains(result, p => p.BraceEndIndex == -1); // At least one incomplete
    }
}