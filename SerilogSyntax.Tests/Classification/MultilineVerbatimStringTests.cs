using SerilogSyntax.Parsing;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Classification;

/// <summary>
/// Tests for multi-line verbatim string template parsing.
/// </summary>
public class MultilineVerbatimStringTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parser_ParsesProperties_InMultilineVerbatimTemplate()
    {
        // This is what would be extracted from a multi-line verbatim string
        var template = @"Processing files in path: {FilePath}
Multiple lines are supported in verbatim strings
With properties like {UserId} and {@Order}
Even with ""escaped quotes"" in the template";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(3, properties.Count);
        
        // First property on first line
        Assert.Equal("FilePath", properties[0].Name);
        Assert.Equal(PropertyType.Standard, properties[0].Type);
        
        // Second property on third line
        Assert.Equal("UserId", properties[1].Name);
        Assert.Equal(PropertyType.Standard, properties[1].Type);
        
        // Third property on third line (destructured)
        Assert.Equal("Order", properties[2].Name);
        Assert.Equal(PropertyType.Destructured, properties[2].Type);
    }

    [Fact]
    public void Parser_HandlesEscapedQuotes_InVerbatimTemplate()
    {
        // Verbatim strings use "" to escape quotes
        var template = @"Message with ""quotes"" and {Property} after quotes";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Single(properties);
        Assert.Equal("Property", properties[0].Name);
        // The property should be found after the escaped quotes
        Assert.True(properties[0].StartIndex > template.IndexOf(@"""quotes"""));
    }

    [Fact]
    public void Parser_ParsesProperties_WithNewlines()
    {
        var template = "Line 1: {Prop1}\nLine 2: {Prop2}\rLine 3: {Prop3}\r\nLine 4: {Prop4}";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(4, properties.Count);
        Assert.Equal("Prop1", properties[0].Name);
        Assert.Equal("Prop2", properties[1].Name);
        Assert.Equal("Prop3", properties[2].Name);
        Assert.Equal("Prop4", properties[3].Name);
        
        // Verify positions account for newlines
        Assert.True(properties[1].StartIndex > properties[0].StartIndex);
        Assert.True(properties[2].StartIndex > properties[1].StartIndex);
        Assert.True(properties[3].StartIndex > properties[2].StartIndex);
    }

    [Fact]
    public void Parser_ReturnsCorrectIndices_ForMultilineTemplate()
    {
        var template = "First line {A}\nSecond line {B}";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(2, properties.Count);
        
        // Property A on first line
        var propA = properties[0];
        Assert.Equal("A", propA.Name);
        Assert.Equal(12, propA.StartIndex); // Position of 'A' in template
        
        // Property B on second line
        var propB = properties[1];
        Assert.Equal("B", propB.Name);
        Assert.Equal(28, propB.StartIndex); // Position of 'B' after newline
    }

    [Fact]
    public void Parser_HandlesComplexMultilineTemplate()
    {
        var template = @"Starting process {@Process}
Parameters: {Count,5} items, format: {Format:yyyy-MM-dd}
Status: {$Status} with alignment {Value,-10:F2}
Positional: {0}, {1}, {2}";
        
        var properties = _parser.Parse(template).ToList();
        
        Assert.Equal(8, properties.Count); // Process, Count, Format, Status, Value, 0, 1, 2
        
        // Verify different property types are parsed correctly
        var process = properties.First(p => p.Name == "Process");
        Assert.Equal(PropertyType.Destructured, process.Type);
        
        var count = properties.First(p => p.Name == "Count");
        Assert.Equal("5", count.Alignment);
        
        var format = properties.First(p => p.Name == "Format");
        Assert.Equal("yyyy-MM-dd", format.FormatSpecifier);
        
        var status = properties.First(p => p.Name == "Status");
        Assert.Equal(PropertyType.Stringified, status.Type);
        
        var value = properties.First(p => p.Name == "Value");
        Assert.Equal("-10", value.Alignment);
        Assert.Equal("F2", value.FormatSpecifier);
        
        // Check positional properties
        var positionals = properties.Where(p => p.Type == PropertyType.Positional).ToList();
        Assert.Equal(3, positionals.Count);
        Assert.Equal("0", positionals[0].Name);
        Assert.Equal("1", positionals[1].Name);
        Assert.Equal("2", positionals[2].Name);
    }
}