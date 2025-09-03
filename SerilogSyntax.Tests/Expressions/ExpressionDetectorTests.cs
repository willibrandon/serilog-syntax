using Xunit;
using SerilogSyntax.Expressions;

namespace SerilogSyntax.Tests.Expressions;

public class ExpressionDetectorTests
{
    [Fact]
    public void IsExpressionTemplate_WithIfDirective_ReturnsTrue()
    {
        var result = ExpressionDetector.IsExpressionTemplate(ExpressionTestData.TemplateWithDirective);
        Assert.True(result);
    }
    
    [Fact]
    public void IsExpressionTemplate_WithEachDirective_ReturnsTrue()
    {
        var result = ExpressionDetector.IsExpressionTemplate(ExpressionTestData.EachDirectiveTemplate);
        Assert.True(result);
    }
    
    [Fact]
    public void IsExpressionTemplate_WithSpreadOperator_ReturnsTrue()
    {
        var result = ExpressionDetector.IsExpressionTemplate("{ ..@p }");
        Assert.True(result);
    }
    
    [Fact]
    public void IsExpressionTemplate_WithBuiltinExpressionProperties_ReturnsTrue()
    {
        var result = ExpressionDetector.IsExpressionTemplate("{@p['key']} {@i} {@r} {@tr} {@sp}");
        Assert.True(result);
    }
    
    [Fact]
    public void IsExpressionTemplate_RegularTemplate_ReturnsFalse()
    {
        var result = ExpressionDetector.IsExpressionTemplate("{UserName} logged in at {Timestamp}");
        Assert.False(result);
    }
    
    [Fact]
    public void IsExpressionTemplate_EmptyString_ReturnsFalse()
    {
        var result = ExpressionDetector.IsExpressionTemplate("");
        Assert.False(result);
    }
    
    [Fact]
    public void IsExpressionTemplate_Null_ReturnsFalse()
    {
        var result = ExpressionDetector.IsExpressionTemplate(null);
        Assert.False(result);
    }
    
    [Fact]
    public void IsFilterExpression_WithLikeOperator_ReturnsTrue()
    {
        var result = ExpressionDetector.IsFilterExpression(ExpressionTestData.LikeFilter);
        Assert.True(result);
    }
    
    [Fact]
    public void IsFilterExpression_WithNotLikeOperator_ReturnsTrue()
    {
        var result = ExpressionDetector.IsFilterExpression(ExpressionTestData.NotLikeFilter);
        Assert.True(result);
    }
    
    [Fact]
    public void IsFilterExpression_WithInOperator_ReturnsTrue()
    {
        var result = ExpressionDetector.IsFilterExpression(ExpressionTestData.InFilter);
        Assert.True(result);
    }
    
    [Fact]
    public void IsFilterExpression_WithNullCheck_ReturnsTrue()
    {
        var result = ExpressionDetector.IsFilterExpression(ExpressionTestData.NullCheckFilter);
        Assert.True(result);
    }
    
    [Fact]
    public void IsFilterExpression_WithBooleanOperators_ReturnsTrue()
    {
        var result = ExpressionDetector.IsFilterExpression("IsActive and HasPermission or IsAdmin");
        Assert.True(result);
    }
    
    [Fact]
    public void IsFilterExpression_WithCaseInsensitiveModifier_ReturnsTrue()
    {
        var result = ExpressionDetector.IsFilterExpression(ExpressionTestData.CaseInsensitiveFilter);
        Assert.True(result);
    }
    
    [Fact]
    public void IsFilterExpression_WithFunction_ReturnsTrue()
    {
        var result = ExpressionDetector.IsFilterExpression("StartsWith(Name, 'John')");
        Assert.True(result);
    }
    
    [Fact]
    public void IsFilterExpression_RegularText_ReturnsFalse()
    {
        var result = ExpressionDetector.IsFilterExpression("This is just regular text");
        Assert.False(result);
    }
    
    [Fact]
    public void GetContext_FilterByExcluding_ReturnsFilterExpression()
    {
        var line = ExpressionTestData.FilterByExcludingCall;
        var context = ExpressionDetector.GetContext(line, line.IndexOf('"') + 1);
        Assert.Equal(ExpressionContext.FilterExpression, context);
    }
    
    [Fact]
    public void GetContext_FilterByIncludingOnly_ReturnsFilterExpression()
    {
        var line = ExpressionTestData.FilterByIncludingOnlyCall;
        var context = ExpressionDetector.GetContext(line, line.IndexOf('"') + 1);
        Assert.Equal(ExpressionContext.FilterExpression, context);
    }
    
    [Fact]
    public void GetContext_ExpressionTemplate_ReturnsExpressionTemplate()
    {
        var line = ExpressionTestData.ExpressionTemplateCall;
        var context = ExpressionDetector.GetContext(line, line.IndexOf('"') + 1);
        Assert.Equal(ExpressionContext.ExpressionTemplate, context);
    }
    
    [Fact]
    public void GetContext_ConditionalWrite_ReturnsConditionalExpression()
    {
        var line = ExpressionTestData.ConditionalWriteCall;
        var context = ExpressionDetector.GetContext(line, line.IndexOf('"') + 1);
        Assert.Equal(ExpressionContext.ConditionalExpression, context);
    }
    
    [Fact]
    public void GetContext_EnrichWhen_ReturnsConditionalExpression()
    {
        var line = ExpressionTestData.EnrichWhenCall;
        var context = ExpressionDetector.GetContext(line, line.IndexOf('"') + 1);
        Assert.Equal(ExpressionContext.ConditionalExpression, context);
    }
    
    [Fact]
    public void GetContext_EnrichWithComputed_ReturnsComputedProperty()
    {
        var line = ExpressionTestData.EnrichWithComputedCall;
        var lastQuote = line.LastIndexOf('"');
        var secondLastQuote = line.LastIndexOf('"', lastQuote - 1);
        var context = ExpressionDetector.GetContext(line, secondLastQuote + 1);
        Assert.Equal(ExpressionContext.ComputedProperty, context);
    }
    
    [Fact]
    public void GetContext_RegularSerilogCall_ReturnsNone()
    {
        var line = "Log.Information(\"User {UserName} logged in\", userName)";
        var context = ExpressionDetector.GetContext(line, line.IndexOf('"') + 1);
        Assert.Equal(ExpressionContext.None, context);
    }
    
    [Fact]
    public void GetContext_OutsideString_ReturnsNone()
    {
        var line = ExpressionTestData.FilterByExcludingCall;
        var context = ExpressionDetector.GetContext(line, 0);
        Assert.Equal(ExpressionContext.None, context);
    }
    
    [Fact]
    public void IsExpressionCall_FilterByExcluding_ReturnsTrue()
    {
        var result = ExpressionDetector.IsExpressionCall(ExpressionTestData.FilterByExcludingCall);
        Assert.True(result);
    }
    
    [Fact]
    public void IsExpressionCall_ConditionalWrite_ReturnsTrue()
    {
        var result = ExpressionDetector.IsExpressionCall(ExpressionTestData.ConditionalWriteCall);
        Assert.True(result);
    }
    
    [Fact]
    public void IsExpressionCall_EnrichWhen_ReturnsTrue()
    {
        var result = ExpressionDetector.IsExpressionCall(ExpressionTestData.EnrichWhenCall);
        Assert.True(result);
    }
    
    [Fact]
    public void IsExpressionCall_ExpressionTemplate_ReturnsTrue()
    {
        var result = ExpressionDetector.IsExpressionCall(ExpressionTestData.ExpressionTemplateCall);
        Assert.True(result);
    }
    
    [Fact]
    public void IsExpressionCall_RegularLogCall_ReturnsFalse()
    {
        var result = ExpressionDetector.IsExpressionCall("Log.Information(\"Regular template\")");
        Assert.False(result);
    }
    
    [Fact]
    public void IsExpressionCall_EmptyString_ReturnsFalse()
    {
        var result = ExpressionDetector.IsExpressionCall("");
        Assert.False(result);
    }
    
    [Fact]
    public void IsExpressionCall_NoRelevantKeywords_ReturnsFalse()
    {
        var result = ExpressionDetector.IsExpressionCall("var x = 42; Console.WriteLine(x);");
        Assert.False(result);
    }
    
    [Fact]
    public void ClearCache_ResetsCache()
    {
        // First call should cache the result
        var line = ExpressionTestData.FilterByExcludingCall;
        var context1 = ExpressionDetector.GetContext(line, 20);
        
        // Clear cache
        ExpressionDetector.ClearCache();
        
        // Second call should work the same (tests that cache clearing doesn't break functionality)
        var context2 = ExpressionDetector.GetContext(line, 20);
        
        Assert.Equal(context1, context2);
    }
}