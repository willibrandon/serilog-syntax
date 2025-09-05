using SerilogSyntax.Parsing;
using System;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Parsing;

public class TemplateParserTests
{
    private readonly TemplateParser _parser = new();

    [Fact]
    public void Parse_EmptyTemplate_ReturnsNoProperties()
    {
        var result = _parser.Parse("").ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_PlainText_ReturnsNoProperties()
    {
        var result = _parser.Parse("This is plain text without properties").ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SimpleProperty_ReturnsProperty()
    {
        var result = _parser.Parse("Hello {Name}!").ToList();
        
        Assert.Single(result);
        Assert.Equal("Name", result[0].Name);
        Assert.Equal(PropertyType.Standard, result[0].Type);
        Assert.Equal(7, result[0].StartIndex); // Position of 'N' in Name
        Assert.Equal(4, result[0].Length); // Length of "Name"
        Assert.Equal(6, result[0].BraceStartIndex); // Position of '{'
        Assert.Equal(11, result[0].BraceEndIndex); // Position of '}'
    }

    [Fact]
    public void Parse_MultipleProperties_ReturnsAll()
    {
        var result = _parser.Parse("User {Username} logged in at {Timestamp}").ToList();
        
        Assert.Equal(2, result.Count);
        
        Assert.Equal("Username", result[0].Name);
        Assert.Equal(6, result[0].StartIndex);
        Assert.Equal(8, result[0].Length);
        
        Assert.Equal("Timestamp", result[1].Name);
        Assert.Equal(30, result[1].StartIndex);
        Assert.Equal(9, result[1].Length);
    }

    [Fact]
    public void Parse_DestructuredProperty_ReturnsCorrectType()
    {
        var result = _parser.Parse("Processing {@User}").ToList();
        
        Assert.Single(result);
        Assert.Equal("User", result[0].Name);
        Assert.Equal(PropertyType.Destructured, result[0].Type);
        Assert.Equal(12, result[0].OperatorIndex); // Position of '@'
        Assert.Equal(13, result[0].StartIndex); // Position of 'U'
    }

    [Fact]
    public void Parse_StringifiedProperty_ReturnsCorrectType()
    {
        var result = _parser.Parse("Value is {$Value}").ToList();
        
        Assert.Single(result);
        Assert.Equal("Value", result[0].Name);
        Assert.Equal(PropertyType.Stringified, result[0].Type);
        Assert.Equal(10, result[0].OperatorIndex); // Position of '$'
        Assert.Equal(11, result[0].StartIndex); // Position of 'V'
    }

    [Fact]
    public void Parse_PositionalProperty_ReturnsCorrectType()
    {
        var result = _parser.Parse("Value {0} and {1}").ToList();
        
        Assert.Equal(2, result.Count);
        
        Assert.Equal("0", result[0].Name);
        Assert.Equal(PropertyType.Positional, result[0].Type);
        
        Assert.Equal("1", result[1].Name);
        Assert.Equal(PropertyType.Positional, result[1].Type);
    }

    [Fact]
    public void Parse_PropertyWithFormat_ParsesFormatSpecifier()
    {
        var result = _parser.Parse("Time: {Timestamp:yyyy-MM-dd}").ToList();
        
        Assert.Single(result);
        Assert.Equal("Timestamp", result[0].Name);
        Assert.Equal("yyyy-MM-dd", result[0].FormatSpecifier);
        Assert.Equal(17, result[0].FormatStartIndex); // Position of 'y' after ':'
    }

    [Fact]
    public void Parse_PropertyWithAlignment_ParsesAlignment()
    {
        var result = _parser.Parse("Name: {Name,-10}").ToList();
        
        Assert.Single(result);
        Assert.Equal("Name", result[0].Name);
        Assert.Equal("-10", result[0].Alignment);
        Assert.Equal(12, result[0].AlignmentStartIndex); // Position of '-' (after comma)
    }

    [Fact]
    public void Parse_PropertyWithAlignmentAndFormat_ParsesBoth()
    {
        var result = _parser.Parse("Price: {Price,10:C2}").ToList();
        
        Assert.Single(result);
        Assert.Equal("Price", result[0].Name);
        Assert.Equal("10", result[0].Alignment);
        Assert.Equal("C2", result[0].FormatSpecifier);
    }

    [Fact]
    public void Parse_ComplexTemplate_ParsesAllProperties()
    {
        var template = "User {@User} performed {Action} on {$Item} at {Timestamp:HH:mm:ss}";
        var result = _parser.Parse(template).ToList();
        
        Assert.Equal(4, result.Count);
        
        // @User
        Assert.Equal("User", result[0].Name);
        Assert.Equal(PropertyType.Destructured, result[0].Type);
        
        // Action
        Assert.Equal("Action", result[1].Name);
        Assert.Equal(PropertyType.Standard, result[1].Type);
        
        // $Item
        Assert.Equal("Item", result[2].Name);
        Assert.Equal(PropertyType.Stringified, result[2].Type);
        
        // Timestamp:HH:mm:ss
        Assert.Equal("Timestamp", result[3].Name);
        Assert.Equal("HH:mm:ss", result[3].FormatSpecifier);
    }

    [Fact]
    public void Parse_UnbalancedBraces_ReturnsEmpty()
    {
        var result = _parser.Parse("Hello {Name").ToList();
        // Parser should not return incomplete properties to prevent spillover highlighting
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EmptyProperty_HandlesGracefully()
    {
        var result = _parser.Parse("Hello {}").ToList();
        Assert.Empty(result); // Parser should skip empty properties
    }

    [Fact]
    public void Parse_ConsecutiveBraces_ParsesCorrectly()
    {
        var result = _parser.Parse("{First}{Second}").ToList();
        
        Assert.Equal(2, result.Count);
        Assert.Equal("First", result[0].Name);
        Assert.Equal("Second", result[1].Name);
    }

    [Fact]
    public void Parse_PropertyWithSpaces_ParsesCorrectly()
    {
        // Properties with spaces are literal text in Serilog, not properties
        var result = _parser.Parse("{ Name }").ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_MixedPositionalAndNamed_ParsesAll()
    {
        var result = _parser.Parse("Item {0} has name {Name} and id {1}").ToList();
        
        Assert.Equal(3, result.Count);
        Assert.Equal("0", result[0].Name);
        Assert.Equal(PropertyType.Positional, result[0].Type);
        Assert.Equal("Name", result[1].Name);
        Assert.Equal(PropertyType.Standard, result[1].Type);
        Assert.Equal("1", result[2].Name);
        Assert.Equal(PropertyType.Positional, result[2].Type);
    }

    [Fact]
    public void Parse_PropertyWithUnderscoreAndNumbers_ParsesCorrectly()
    {
        var result = _parser.Parse("Value: {User_Id123}").ToList();
        
        Assert.Single(result);
        Assert.Equal("User_Id123", result[0].Name);
    }

    [Fact]
    public void Parse_EscapedBraces_IgnoresEscaped()
    {
        // Double braces should be treated as escaped
        var result = _parser.Parse("Use {{Name}} for {RealProperty}").ToList();
        
        Assert.Single(result);
        Assert.Equal("RealProperty", result[0].Name);
    }

    [Fact]
    public void Parse_PropertyWithInternalSpaces_ReturnsNoProperty()
    {
        // Properties with spaces inside the name are invalid
        var result = _parser.Parse("Text with { and } braces").ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_PropertyWithLeadingTrailingSpaces_ReturnsProperty()
    {
        // Leading and trailing spaces make it literal text, not a property
        var result = _parser.Parse("Text with { PropertyName } here").ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_PropertyNoSpaces_ReturnsProperty()
    {
        // Standard property without spaces
        var result = _parser.Parse("Text with {PropertyName} here").ToList();
        
        Assert.Single(result);
        Assert.Equal("PropertyName", result[0].Name);
    }

    [Fact]
    public void Parse_MultipleWordsWithSpaces_ReturnsNoProperty()
    {
        // Multiple words with spaces are invalid
        var result = _parser.Parse("Text { Property Name } here").ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SpacesAroundOperators_StillInvalid()
    {
        // Even with operators, internal spaces make it invalid
        var result = _parser.Parse("Text { @ User } here").ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SpacesInFormatting_StillInvalid()
    {
        // Spaces in property name with formatting are invalid
        var result = _parser.Parse("Text { My Prop :format} here").ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_SingleCharacterProperty_ReturnsProperty()
    {
        // Single character properties are valid
        var result = _parser.Parse("Text {a} and {b} here").ToList();
        
        Assert.Equal(2, result.Count);
        Assert.Equal("a", result[0].Name);
        Assert.Equal("b", result[1].Name);
    }

    [Fact]
    public void Parse_PropertyWithLeadingSpace_ReturnsNoProperty()
    {
        // Properties with leading space are invalid
        var result = _parser.Parse("Text with { PropName} here").ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_UnclosedDestructuringProperty_ShouldNotSpillOver()
    {
        // Bug fix: Unclosed destructuring property should not be recognized as a valid property
        var template = "Performance metrics {@PerformanceData, performance);";
        var result = _parser.Parse(template).ToList();
        
        // The parser should not recognize {@PerformanceData as a valid property since it's unclosed
        // This prevents highlight spillover in the editor
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_UnclosedStandardProperty_ReturnsNoProperty()
    {
        // Unclosed standard property should not be recognized
        var template = "User {Username logged in";
        var result = _parser.Parse(template).ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_UnclosedPropertyWithFormat_ReturnsNoProperty()
    {
        // Unclosed property with format specifier should not be recognized
        var template = "Time {Timestamp:HH:mm:ss at location";
        var result = _parser.Parse(template).ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_UnclosedStringifiedProperty_ReturnsNoProperty()
    {
        // Unclosed stringified property should not be recognized
        var template = "Value {$Amount in dollars";
        var result = _parser.Parse(template).ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_UnclosedPropertyWithFormatSpecifier_PreventSpillover()
    {
        // When format specifier is unclosed (missing }), it should not highlight beyond the property
        var template = "Processing order {@Order} at {Timestamp:HH:mm:ss | Status: {Status,10}";
        var result = _parser.Parse(template).ToList();
        
        // Should find {@Order} and {Status,10} but NOT {Timestamp:HH:mm:ss with spillover
        Assert.Equal(2, result.Count);
        Assert.Equal("Order", result[0].Name);
        Assert.Equal("Status", result[1].Name);
        
        // Verify no property contains the pipe character or "Status" text
        Assert.DoesNotContain(result, p => p.Name.Contains("|"));
        Assert.DoesNotContain(result, p => p.FormatSpecifier?.Contains("|") == true);
    }

    [Fact]
    public void Parse_UnclosedPropertyWithAlignment_PreventSpillover()
    {
        // When alignment is unclosed (missing }), it should not highlight beyond the property
        var template = "Item: {Name,10 | Price: {Price,8:C} | Stock: {Stock,3}";
        var result = _parser.Parse(template).ToList();
        
        // Should find {Price,8:C} and {Stock,3} but NOT {Name,10 with spillover
        Assert.Equal(2, result.Count);
        Assert.Equal("Price", result[0].Name);
        Assert.Equal("Stock", result[1].Name);
        
        // Verify no property contains the pipe character
        Assert.DoesNotContain(result, p => p.Name.Contains("|"));
        Assert.DoesNotContain(result, p => p.Alignment?.Contains("|") == true);
    }

    [Fact]
    public void Parse_MultipleUnclosedProperties_HandleEachCorrectly()
    {
        // Multiple unclosed properties in one template
        var template = "Start {Prop1:format middle {Prop2,5 end {Prop3}";
        var result = _parser.Parse(template).ToList();
        
        // Should only find {Prop3} which is properly closed
        Assert.Single(result);
        Assert.Equal("Prop3", result[0].Name);
    }

    [Fact]
    public void Parse_PropertyWithTrailingSpace_ReturnsNoProperty()
    {
        // Properties with trailing space are invalid
        var result = _parser.Parse("Text with {PropName } here").ToList();
        
        Assert.Empty(result);
    }

    [Fact]
    public void Parse_LeadingSpaceWithOperators_ReturnsNoProperty()
    {
        // Leading space makes operators invalid too
        var result1 = _parser.Parse("Text with { @User} here").ToList();
        var result2 = _parser.Parse("Text with { $Value} here").ToList();
        
        Assert.Empty(result1);
        Assert.Empty(result2);
    }
}