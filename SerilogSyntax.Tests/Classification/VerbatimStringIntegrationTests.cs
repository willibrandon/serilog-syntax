using SerilogSyntax.Parsing;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Classification;

/// <summary>
/// Integration tests for verbatim string handling.
/// These tests verify that the template parser correctly handles content from verbatim strings.
/// </summary>
public class VerbatimStringIntegrationTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parser_VerbatimStringContent_ParsesProperties()
    {
        // Simulate content extracted from a verbatim string
        // In real usage, this would come from @"Processing file {FileName} for user {UserId}"
        var template = "Processing file {FileName} for user {UserId}";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(2, properties.Count);
        Assert.Equal("FileName", properties[0].Name);
        Assert.Equal("UserId", properties[1].Name);
        
        // The issue is that when this content comes from a verbatim string,
        // the indices need to be adjusted by the offset of the string content
        // within the original C# code
    }
    
    [Fact] 
    public void Parser_VerbatimStringWithEscapedQuotes_ParsesCorrectly()
    {
        // In a verbatim string: @"Message with ""quotes"" and {Property}"
        // The parser receives: Message with "quotes" and {Property}
        var template = "Message with \"quotes\" and {Property}";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Single(properties);
        Assert.Equal("Property", properties[0].Name);
        // Note: The property should be at the correct position after the quotes
        Assert.True(properties[0].StartIndex > template.IndexOf("quotes"));
    }
    
    [Fact]
    public void Parser_MultilineVerbatimContent_ParsesAllProperties()
    {
        // Content from a multiline verbatim string
        var template = @"First line with {Property1}
Second line with {Property2}
Third line with {Property3}";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(3, properties.Count);
        Assert.Equal("Property1", properties[0].Name);
        Assert.Equal("Property2", properties[1].Name);
        Assert.Equal("Property3", properties[2].Name);
    }
    
    /// <summary>
    /// This test demonstrates the index offset problem.
    /// When we extract content from a verbatim string, the property indices
    /// are relative to the content, not the original source code.
    /// </summary>
    [Fact]
    public void IndexOffsetProblem_Demonstration()
    {
        // Original C# code would be: logger.LogInformation(@"User {Name} logged in", name);
        // The verbatim string starts at position 22 (@")
        // Content starts at position 24 (after @")
        var contentStart = 24; // Position where actual content begins (after @")
        
        var extractedContent = "User {Name} logged in";
        var properties = _parser.Parse(extractedContent).ToList();
        
        Assert.Single(properties);
        var property = properties[0];
        
        // The parser returns the property name at position 6 (relative to content)
        Assert.Equal(6, property.StartIndex); // "Name" starts at position 6 in "User {Name} logged in"
        Assert.Equal("Name", property.Name);
        
        // But in the original code, the property should be at position 24 + 6 = 30
        var actualPositionInOriginalCode = contentStart + property.StartIndex;
        Assert.Equal(30, actualPositionInOriginalCode);
        
        // This is what the classifier needs to calculate!
    }
}