using SerilogSyntax.Parsing;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Parsing;

/// <summary>
/// Tests for raw string literal support ("""...""").
/// </summary>
public class RawStringLiteralTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parser_ParsesProperties_InSingleLineRawString()
    {
        // Single-line raw string literal
        var template = """User {UserId} logged in at {Timestamp}""";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(2, properties.Count);
        Assert.Equal("UserId", properties[0].Name);
        Assert.Equal("Timestamp", properties[1].Name);
    }

    [Fact]
    public void Parser_ParsesProperties_InMultiLineRawString()
    {
        // Multi-line raw string literal
        var template = """
            Processing record:
            ID: {RecordId}
            Status: {Status}
            User: {@User}
            """;
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(3, properties.Count);
        Assert.Equal("RecordId", properties[0].Name);
        Assert.Equal("Status", properties[1].Name);
        Assert.Equal("User", properties[2].Name);
        Assert.Equal(PropertyType.Destructured, properties[2].Type);
    }

    [Fact]
    public void Parser_HandlesRawString_WithEmbeddedQuotes()
    {
        // Raw string with quotes inside (no escaping needed)
        var template = """Message with "quotes" and {Property} here""";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Single(properties);
        Assert.Equal("Property", properties[0].Name);
    }

    [Fact]
    public void Parser_HandlesRawString_WithCustomDelimiter()
    {
        // Raw string with 4+ quotes delimiter
        var template = """"
            Template with """ inside: {Value}
            And more content with {Count,5}
            """";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(2, properties.Count);
        Assert.Equal("Value", properties[0].Name);
        Assert.Equal("Count", properties[1].Name);
        Assert.Equal("5", properties[1].Alignment);
    }

    [Fact]
    public void Parser_HandlesRawString_WithFormatSpecifiers()
    {
        // Raw string with various property formats
        var template = """
            Time: {Timestamp:HH:mm:ss}
            Price: {Price,10:C2}
            Status: {$Status}
            Index: {0}
            """;
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(4, properties.Count);
        
        Assert.Equal("Timestamp", properties[0].Name);
        Assert.Equal("HH:mm:ss", properties[0].FormatSpecifier);
        
        Assert.Equal("Price", properties[1].Name);
        Assert.Equal("10", properties[1].Alignment);
        Assert.Equal("C2", properties[1].FormatSpecifier);
        
        Assert.Equal("Status", properties[2].Name);
        Assert.Equal(PropertyType.Stringified, properties[2].Type);
        
        Assert.Equal("0", properties[3].Name);
        Assert.Equal(PropertyType.Positional, properties[3].Type);
    }

    [Fact]
    public void Parser_ReturnsCorrectIndices_ForRawString()
    {
        // Test that property indices are correct in raw strings
        var template = """First {A} then {B} end""";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(2, properties.Count);
        
        var propA = properties[0];
        Assert.Equal("A", propA.Name);
        Assert.Equal(7, propA.StartIndex); // Position of 'A'
        
        var propB = properties[1];
        Assert.Equal("B", propB.Name);
        Assert.Equal(16, propB.StartIndex); // Position of 'B'
    }

    [Fact]
    public void Parser_HandlesRawString_WithIndentation()
    {
        // Raw string with indentation (closing quotes determine base indentation)
        var template = """
            Line 1: {Prop1}
                Line 2: {Prop2}
            Line 3: {Prop3}
            """;
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(3, properties.Count);
        Assert.Equal("Prop1", properties[0].Name);
        Assert.Equal("Prop2", properties[1].Name);
        Assert.Equal("Prop3", properties[2].Name);
    }

    [Fact]
    public void Parser_HandlesEmptyRawString()
    {
        var template = """
            
            """;  // Empty raw string (with just whitespace)
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Empty(properties);
    }

    [Fact]
    public void Parser_HandlesRawString_WithOnlyBraces()
    {
        // Raw string with braces - { and } is NOT a valid property because of internal spaces
        // Serilog treats this as literal text, not a property
        var template = """Text with { and } but no properties""";
        
        var properties = _parser.Parse(template).ToList();
        
        // Should return no properties - spaces within property name make it invalid
        Assert.Empty(properties);
    }

    [Fact]
    public void Parser_HandlesComplexRawString()
    {
        // Complex multi-line raw string with various features
        var template = """
            ===============================================
            Application: {AppName}
            Version: {Version}
            Environment: {Environment}
            ===============================================
            User: {UserName} (ID: {UserId})
            Session: {SessionId}
            Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}
            ===============================================
            """;
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(7, properties.Count);
        Assert.Equal("AppName", properties[0].Name);
        Assert.Equal("Version", properties[1].Name);
        Assert.Equal("Environment", properties[2].Name);
        Assert.Equal("UserName", properties[3].Name);
        Assert.Equal("UserId", properties[4].Name);
        Assert.Equal("SessionId", properties[5].Name);
        Assert.Equal("Timestamp", properties[6].Name);
        Assert.Equal("yyyy-MM-dd HH:mm:ss", properties[6].FormatSpecifier);
    }
}