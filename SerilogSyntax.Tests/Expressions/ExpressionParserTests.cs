using System.Linq;
using Xunit;
using SerilogSyntax.Expressions;
using SerilogSyntax.Classification;

namespace SerilogSyntax.Tests.Expressions;

public class ExpressionParserTests
{
    [Fact]
    public void Parse_SimplePropertyAccess_ClassifiesAsProperty()
    {
        var parser = new ExpressionParser(ExpressionTestData.SimpleProperty);
        var regions = parser.Parse().ToList();
        
        Assert.Single(regions);
        Assert.Equal(SerilogClassificationTypes.ExpressionProperty, regions[0].ClassificationType);
        Assert.Equal("UserName", regions[0].Text);
        Assert.Equal(0, regions[0].Start);
        Assert.Equal(8, regions[0].Length);
    }
    
    [Fact]
    public void Parse_NestedPropertyAccess_ClassifiesAllParts()
    {
        var parser = new ExpressionParser(ExpressionTestData.NestedProperty);
        var regions = parser.Parse().ToList();
        
        // Should classify identifiers but not dots
        var propertyRegions = regions.Where(r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty).ToList();
        Assert.Equal(4, propertyRegions.Count);
        Assert.Equal("Order", propertyRegions[0].Text);
        Assert.Equal("Customer", propertyRegions[1].Text);
        Assert.Equal("Address", propertyRegions[2].Text);
        Assert.Equal("City", propertyRegions[3].Text);
    }
    
    [Fact]
    public void Parse_FilterWithOperators_ClassifiesCorrectly()
    {
        var parser = new ExpressionParser(ExpressionTestData.SimpleFilter);
        var regions = parser.Parse().ToList();
        
        // Check for property
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "Level");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "StatusCode");
        
        // Check for operators
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "=");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == ">=");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "and");
        
        // Check for literals
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "Error");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "400");
    }
    
    [Fact]
    public void Parse_FunctionWithArguments_CorrectRegions()
    {
        var parser = new ExpressionParser(ExpressionTestData.SimpleFunctionCall);
        var regions = parser.Parse().ToList();
        
        Assert.Equal(2, regions.Count);
        Assert.Equal(SerilogClassificationTypes.ExpressionFunction, regions[0].ClassificationType);
        Assert.Equal("Length", regions[0].Text);
        Assert.Equal(SerilogClassificationTypes.ExpressionProperty, regions[1].ClassificationType);
        Assert.Equal("Name", regions[1].Text);
    }
    
    [Fact]
    public void Parse_FunctionWithModifier_IncludesModifier()
    {
        var parser = new ExpressionParser(ExpressionTestData.FunctionWithModifier);
        var regions = parser.Parse().ToList();
        
        // Should have function, properties, literal, and modifier
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionFunction && r.Text == "StartsWith");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "Name");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "John");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "ci");
    }
    
    [Fact]
    public void Parse_BuiltinProperties_ClassifiesAsBuiltin()
    {
        var parser = new ExpressionParser("@t @m @l @x @p");
        var regions = parser.Parse().ToList();
        
        Assert.Equal(5, regions.Count);
        Assert.All(regions, r => Assert.Equal(SerilogClassificationTypes.ExpressionBuiltin, r.ClassificationType));
        Assert.Contains(regions, r => r.Text == "@t");
        Assert.Contains(regions, r => r.Text == "@m");
        Assert.Contains(regions, r => r.Text == "@l");
        Assert.Contains(regions, r => r.Text == "@x");
        Assert.Contains(regions, r => r.Text == "@p");
    }
    
    [Fact]
    public void Parse_BooleanLiterals_ClassifiesAsKeyword()
    {
        var parser = new ExpressionParser("true and false");
        var regions = parser.Parse().ToList();
        
        Assert.Equal(3, regions.Count);
        Assert.Equal(SerilogClassificationTypes.ExpressionKeyword, regions[0].ClassificationType);
        Assert.Equal("true", regions[0].Text);
        Assert.Equal(SerilogClassificationTypes.ExpressionOperator, regions[1].ClassificationType);
        Assert.Equal("and", regions[1].Text);
        Assert.Equal(SerilogClassificationTypes.ExpressionKeyword, regions[2].ClassificationType);
        Assert.Equal("false", regions[2].Text);
    }
    
    [Fact]
    public void Parse_NullLiteral_ClassifiesAsKeyword()
    {
        var parser = new ExpressionParser("value is null");
        var regions = parser.Parse().ToList();
        
        // "is null" is tokenized as a single NullOperator, not as separate tokens
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "is null");
        
        // Test standalone null literal
        var parser2 = new ExpressionParser("value = null");
        var regions2 = parser2.Parse().ToList();
        Assert.Contains(regions2, r => r.ClassificationType == SerilogClassificationTypes.ExpressionKeyword && r.Text == "null");
    }
    
    [Fact]
    public void Parse_StringLiteral_ClassifiesAsLiteral()
    {
        var parser = new ExpressionParser("'hello world'");
        var regions = parser.Parse().ToList();
        
        Assert.Single(regions);
        Assert.Equal(SerilogClassificationTypes.ExpressionLiteral, regions[0].ClassificationType);
        Assert.Equal("hello world", regions[0].Text);
    }
    
    [Fact]
    public void Parse_NumberLiteral_ClassifiesAsLiteral()
    {
        var parser = new ExpressionParser("42 and -12.34");
        var regions = parser.Parse().ToList();
        
        Assert.Equal(3, regions.Count);
        Assert.Equal(SerilogClassificationTypes.ExpressionLiteral, regions[0].ClassificationType);
        Assert.Equal("42", regions[0].Text);
        Assert.Equal(SerilogClassificationTypes.ExpressionLiteral, regions[2].ClassificationType);
        Assert.Equal("-12.34", regions[2].Text);
    }
    
    [Fact]
    public void Parse_TemplateDirective_ClassifiesAsDirective()
    {
        var parser = new ExpressionParser("{#if Level = 'Error'}");
        var regions = parser.Parse().ToList();
        
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionDirective && r.Text == "{#if");
    }
    
    [Fact]
    public void ParseExpressionTemplate_MixedContent_CorrectClassification()
    {
        var template = "{@t} {#if Level = 'Error'}ERROR{#end} {@m}";
        var parser = new ExpressionParser(template);
        var regions = parser.ParseExpressionTemplate().ToList();
        
        // Should have built-in properties and directives
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionBuiltin && r.Text == "@t");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionDirective && r.Text.StartsWith("{#if"));
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "Level");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "=");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "Error");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionDirective && r.Text == "{#end}");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionBuiltin && r.Text == "@m");
    }
    
    [Fact]
    public void ParseExpressionTemplate_WithFormatSpecifier_ClassifiesCorrectly()
    {
        var template = "{@t:HH:mm:ss}";
        var parser = new ExpressionParser(template);
        var regions = parser.ParseExpressionTemplate().ToList();
        
        Assert.Equal(5, regions.Count);
        // Brace classifications
        Assert.Equal(SerilogClassificationTypes.PropertyBrace, regions[0].ClassificationType);
        Assert.Equal("{", regions[0].Text);
        Assert.Equal(SerilogClassificationTypes.PropertyBrace, regions[1].ClassificationType);
        Assert.Equal("}", regions[1].Text);
        // Content classifications
        Assert.Equal(SerilogClassificationTypes.ExpressionBuiltin, regions[2].ClassificationType);
        Assert.Equal("@t", regions[2].Text);
        Assert.Equal(SerilogClassificationTypes.FormatSpecifier, regions[3].ClassificationType);
        Assert.Equal(":", regions[3].Text);
        Assert.Equal(SerilogClassificationTypes.FormatSpecifier, regions[4].ClassificationType);
        Assert.Equal("HH:mm:ss", regions[4].Text);
    }
    
    [Fact]
    public void Parse_SpreadOperator_ClassifiesAsOperator()
    {
        var parser = new ExpressionParser("{ ..@p }");
        var regions = parser.Parse().ToList();
        
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "..");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionBuiltin && r.Text == "@p");
    }
    
    [Fact]
    public void Parse_ArrayIndexing_RecognizesBrackets()
    {
        var parser = new ExpressionParser("Items[0]");
        var regions = parser.Parse().ToList();
        
        // Should classify identifier and number literal, but not brackets
        Assert.Equal(2, regions.Count);
        Assert.Equal(SerilogClassificationTypes.ExpressionProperty, regions[0].ClassificationType);
        Assert.Equal("Items", regions[0].Text);
        Assert.Equal(SerilogClassificationTypes.ExpressionLiteral, regions[1].ClassificationType);
        Assert.Equal("0", regions[1].Text);
    }
    
    [Fact]
    public void Parse_ComplexExpression_AllElementsClassified()
    {
        var expression = "User.Age >= 18 and StartsWith(User.Name, 'A') ci";
        var parser = new ExpressionParser(expression);
        var regions = parser.Parse().ToList();
        
        // Verify we have all the important classifications
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "User");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "Age");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == ">=");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "18");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "and");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionFunction && r.Text == "StartsWith");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "A");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "ci");
    }
    
    [Fact]
    public void Parse_EmptyString_NoRegions()
    {
        var parser = new ExpressionParser("");
        var regions = parser.Parse().ToList();
        
        Assert.Empty(regions);
    }
    
    [Fact]
    public void Parse_OnlyStructuralTokens_NoRegions()
    {
        var parser = new ExpressionParser("()[]{}.,:");
        var regions = parser.Parse().ToList();
        
        // Structural tokens don't get classified
        Assert.Empty(regions);
    }
}