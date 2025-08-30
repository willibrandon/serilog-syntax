using SerilogSyntax.Parsing;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Parsing;

/// <summary>
/// Comprehensive tests for verbatim string parsing - a critical feature.
/// </summary>
public class VerbatimStringTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_SimpleVerbatimString_ParsesProperties()
    {
        // Verbatim string with simple property
        var template = @"User {UserId} logged in";
        var result = _parser.Parse(template).ToList();
        
        Assert.Single(result);
        Assert.Equal("UserId", result[0].Name);
        Assert.Equal(5, result[0].BraceStartIndex);
        Assert.Equal(12, result[0].BraceEndIndex);
    }

    [Fact]
    public void Parse_VerbatimStringWithNewlines_ParsesCorrectly()
    {
        // Verbatim strings often contain newlines
        var template = @"First line with {Property1}
Second line with {Property2}
Third line with {Property3}";
        
        var result = _parser.Parse(template).ToList();
        
        Assert.Equal(3, result.Count);
        Assert.Equal("Property1", result[0].Name);
        Assert.Equal("Property2", result[1].Name);
        Assert.Equal("Property3", result[2].Name);
    }

    [Fact]
    public void Parse_VerbatimStringWithEscapedQuotes_ParsesCorrectly()
    {
        // Verbatim strings use "" for escaped quotes
        var template = @"Message with ""quotes"" and {Property}";
        var result = _parser.Parse(template).ToList();
        
        Assert.Single(result);
        Assert.Equal("Property", result[0].Name);
    }

    [Fact]
    public void Parse_VerbatimStringWithBackslashes_ParsesCorrectly()
    {
        // Verbatim strings don't escape backslashes
        var template = @"Path: C:\Users\{Username}\Documents";
        var result = _parser.Parse(template).ToList();
        
        Assert.Single(result);
        Assert.Equal("Username", result[0].Name);
    }

    [Fact]
    public void Parse_VerbatimStringWithComplexProperties_ParsesAll()
    {
        var template = @"User {@User} at {Timestamp:yyyy-MM-dd} with {Count,5}";
        var result = _parser.Parse(template).ToList();
        
        Assert.Equal(3, result.Count);
        Assert.Equal(PropertyType.Destructured, result[0].Type);
        Assert.Equal("yyyy-MM-dd", result[1].FormatSpecifier);
        Assert.Equal("5", result[2].Alignment);
    }

    [Fact]
    public void Parse_VerbatimStringWithConsecutiveQuotes_HandlesCorrectly()
    {
        // Edge case: multiple escaped quotes
        var template = @"Text """"text"""" with {Property}";
        var result = _parser.Parse(template).ToList();
        
        Assert.Single(result);
        Assert.Equal("Property", result[0].Name);
    }

    [Fact]
    public void Parse_EmptyVerbatimString_ReturnsNoProperties()
    {
        var template = @"";
        var result = _parser.Parse(template).ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_VerbatimStringWithOnlyEscapedQuotes_HandlesCorrectly()
    {
        var template = @""""""; // Represents: ""
        var result = _parser.Parse(template).ToList();
        
        Assert.Empty(result);
    }
}