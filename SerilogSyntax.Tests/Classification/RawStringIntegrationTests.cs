using SerilogSyntax.Parsing;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Classification;

/// <summary>
/// Integration tests for raw string literal support in the classifier.
/// </summary>
public class RawStringIntegrationTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parser_HandlesMultilineRawString_SimulatingLineByLineProcessing()
    {
        // This simulates what happens when VS processes a multi-line raw string
        // Line 1: logger.LogInformation("""
        // Line 2:     Processing record:
        // Line 3:     ID: {RecordId}
        // Line 4:     Status: {Status}
        // Line 5:     """, recordId, status);
        
        // When the classifier processes line 3, it needs to recognize it's inside a raw string
        var line3Content = "    ID: {RecordId}";
        var properties = _parser.Parse(line3Content).ToList();
        
        // The parser should find the property
        Assert.Single(properties);
        Assert.Equal("RecordId", properties[0].Name);
    }

    [Fact]
    public void Parser_ExtractsProperties_FromCompleteMultilineRawString()
    {
        // The complete content of a multi-line raw string (after extraction)
        var rawStringContent = @"    Processing record:
    ID: {RecordId}
    Status: {Status}
    User: {@User}";
        
        var properties = _parser.Parse(rawStringContent).ToList();
        
        Assert.Equal(3, properties.Count);
        Assert.Equal("RecordId", properties[0].Name);
        Assert.Equal("Status", properties[1].Name);
        Assert.Equal("User", properties[2].Name);
        Assert.Equal(PropertyType.Destructured, properties[2].Type);
    }

    [Fact]
    public void Parser_HandlesRawString_WithCustomDelimiterInContent()
    {
        // Content of a raw string with 4 quotes delimiter
        // This allows triple quotes to appear in the content
        var content = "    Template with \"\"\" inside: {Value}\n    And more: {Count,5}";
        
        var properties = _parser.Parse(content).ToList();
        
        Assert.Equal(2, properties.Count);
        Assert.Equal("Value", properties[0].Name);
        Assert.Equal("Count", properties[1].Name);
        Assert.Equal("5", properties[1].Alignment);
    }

    [Fact]
    public void Parser_ReturnsCorrectOffsets_ForMultilineRawString()
    {
        var content = @"Line 1: {Prop1}
Line 2: {Prop2}
Line 3: {Prop3}";
        
        var properties = _parser.Parse(content).ToList();
        
        Assert.Equal(3, properties.Count);
        
        // Verify offsets account for newlines
        Assert.True(properties[0].StartIndex < properties[1].StartIndex);
        Assert.True(properties[1].StartIndex < properties[2].StartIndex);
        
        // Property 2 should be after the first newline
        var firstNewline = content.IndexOf('\n');
        Assert.True(properties[1].StartIndex > firstNewline);
        
        // Property 3 should be after the second newline
        var secondNewline = content.IndexOf('\n', firstNewline + 1);
        Assert.True(properties[2].StartIndex > secondNewline);
    }

    [Fact]
    public void RawStringScenario_ComplexMultilineWithAllFeatures()
    {
        // Simulating the content extracted from a complex raw string
        var content = @"===============================================
Application: {AppName}
Version: {Version}
Environment: {Environment}
===============================================
User: {UserName} (ID: {UserId})
Session: {SessionId}
Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}
===============================================";
        
        var properties = _parser.Parse(content).ToList();
        
        Assert.Equal(7, properties.Count);
        
        // Verify all properties are found
        Assert.Contains(properties, p => p.Name == "AppName");
        Assert.Contains(properties, p => p.Name == "Version");
        Assert.Contains(properties, p => p.Name == "Environment");
        Assert.Contains(properties, p => p.Name == "UserName");
        Assert.Contains(properties, p => p.Name == "UserId");
        Assert.Contains(properties, p => p.Name == "SessionId");
        
        // Verify the timestamp property has format specifier
        var timestamp = properties.First(p => p.Name == "Timestamp");
        Assert.Equal("yyyy-MM-dd HH:mm:ss", timestamp.FormatSpecifier);
    }
}