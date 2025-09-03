using SerilogSyntax.Classification;
using SerilogSyntax.Expressions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Expressions;

public class ExpressionIntegrationTests
{
    [Fact]
    public void FullPipeline_FilterExpression_EndToEnd()
    {
        // Arrange
        var line = "Filter.ByExcluding(\"RequestPath like '/health%' and StatusCode < 400\")";
        var stringStart = line.IndexOf('"') + 1;
        var expression = "RequestPath like '/health%' and StatusCode < 400";
        
        // Act - Detection
        var context = ExpressionDetector.GetContext(line, stringStart);
        Assert.Equal(ExpressionContext.FilterExpression, context);
        
        // Act - Tokenization
        var tokenizer = new ExpressionTokenizer(expression);
        var tokens = tokenizer.Tokenize().ToList();
        
        // Assert - Verify key tokens
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "RequestPath");
        Assert.Contains(tokens, t => t.Type == TokenType.StringOperator && t.Value == "like");
        Assert.Contains(tokens, t => t.Type == TokenType.StringLiteral && t.Value == "/health%");
        Assert.Contains(tokens, t => t.Type == TokenType.BooleanOperator && t.Value == "and");
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "StatusCode");
        Assert.Contains(tokens, t => t.Type == TokenType.ComparisonOperator && t.Value == "<");
        Assert.Contains(tokens, t => t.Type == TokenType.NumberLiteral && t.Value == "400");
        
        // Act - Parsing
        var parser = new ExpressionParser(expression);
        var regions = parser.Parse().ToList();
        
        // Assert - Verify classifications
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "RequestPath");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "like");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "/health%");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "and");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "StatusCode");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "<");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "400");
    }
    
    [Fact]
    public void FullPipeline_ExpressionTemplate_EndToEnd()
    {
        // Arrange
        var line = "new ExpressionTemplate(\"[{@t:HH:mm:ss}] {#if Level = 'Error'}[ERROR]{#end} {@m}\")";
        var stringStart = line.IndexOf('"') + 1;
        var template = "[{@t:HH:mm:ss}] {#if Level = 'Error'}[ERROR]{#end} {@m}";
        
        // Act - Detection
        var context = ExpressionDetector.GetContext(line, stringStart);
        Assert.Equal(ExpressionContext.ExpressionTemplate, context);
        
        // Act - Template-specific parsing
        var parser = new ExpressionParser(template);
        var regions = parser.ParseExpressionTemplate().ToList();
        
        // Assert - Verify mixed template/expression content
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionBuiltin && r.Text == "@t");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.FormatSpecifier && r.Text.Contains("HH:mm:ss"));
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionDirective && r.Text.StartsWith("{#if"));
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "Level");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "=");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "Error");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionDirective && r.Text == "{#end}");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionBuiltin && r.Text == "@m");
    }
    
    [Fact]
    public void FullPipeline_MixedTemplateAndExpression_CorrectContext()
    {
        // Arrange
        var template = "{User.Name} {#if User.IsActive}(Active){#else}(Inactive){#end} - {@p['user-id']}";
        
        // Act - Check if it's an expression template
        var isExpressionTemplate = ExpressionDetector.IsExpressionTemplate(template);
        Assert.True(isExpressionTemplate);
        
        // Act - Parse as expression template
        var parser = new ExpressionParser(template);
        var regions = parser.ParseExpressionTemplate().ToList();
        
        // Assert - Should have properties, directives, and built-ins
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.PropertyName && r.Text.Contains("User.Name"));
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionDirective);
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionBuiltin && r.Text.Contains("@p"));
    }
    
    [Fact]
    public void FullPipeline_ComputedProperty_EndToEnd()
    {
        // Arrange
        var line = "Enrich.WithComputed(\"ShortContext\", \"Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)\")";
        var lastQuote = line.LastIndexOf('"');
        var secondLastQuote = line.LastIndexOf('"', lastQuote - 1);
        var expression = line.Substring(secondLastQuote + 1, lastQuote - secondLastQuote - 1);
        
        // Act - Detection
        var context = ExpressionDetector.GetContext(line, secondLastQuote + 1);
        Assert.Equal(ExpressionContext.ComputedProperty, context);
        
        // Act - Parse expression
        var parser = new ExpressionParser(expression);
        var regions = parser.Parse().ToList();
        
        // Assert - Verify function calls and operators
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionFunction && r.Text == "Substring");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionFunction && r.Text == "LastIndexOf");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "SourceContext");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == ".");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "+");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "1");
    }
    
    [Fact]
    public void FullPipeline_ComplexNestedExpression_CorrectParsing()
    {
        // Arrange
        var expression = "User.Roles[?] in ['Admin', 'Moderator'] and (User.IsActive or User.IsPending) and Length(User.Name) > 3";
        
        // Act
        var tokenizer = new ExpressionTokenizer(expression);
        var tokens = tokenizer.Tokenize().ToList();
        var parser = new ExpressionParser(expression);
        var regions = parser.Parse().ToList();
        
        // Assert - Complex elements
        Assert.Contains(tokens, t => t.Type == TokenType.Wildcard && t.Value == "?");
        Assert.Contains(tokens, t => t.Type == TokenType.MembershipOperator && t.Value == "in");
        Assert.Contains(tokens, t => t.Type == TokenType.BooleanOperator && t.Value == "or");
        Assert.Contains(tokens, t => t.Type == TokenType.Function && t.Value == "Length");
        
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "in");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionFunction && r.Text == "Length");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "Admin");
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionLiteral && r.Text == "Moderator");
    }
    
    [Fact]
    public void FullPipeline_CaseInsensitiveComparison_CorrectModifier()
    {
        // Arrange
        var expression = "StartsWith(User.Email, 'admin@') ci or Contains(Message, 'error') ci";
        
        // Act
        var parser = new ExpressionParser(expression);
        var regions = parser.Parse().ToList();
        
        // Assert - Should have two 'ci' modifiers classified
        var ciModifiers = regions.Where(r => r.ClassificationType == SerilogClassificationTypes.ExpressionOperator && r.Text == "ci").ToList();
        Assert.Equal(2, ciModifiers.Count);
    }
    
    [Fact]
    public void FullPipeline_Performance_Under100ms()
    {
        // Arrange
        var complexExpression = ExpressionTestData.ComplexFilter;
        var stopwatch = new Stopwatch();
        
        // Act
        stopwatch.Start();
        
        // Detection
        var isFilter = ExpressionDetector.IsFilterExpression(complexExpression);
        
        // Tokenization
        var tokenizer = new ExpressionTokenizer(complexExpression);
        var tokens = tokenizer.Tokenize().ToList();
        
        // Parsing
        var parser = new ExpressionParser(complexExpression);
        var regions = parser.Parse().ToList();
        
        stopwatch.Stop();
        
        // Assert
        Assert.True(isFilter);
        Assert.NotEmpty(tokens);
        Assert.NotEmpty(regions);
        Assert.True(stopwatch.ElapsedMilliseconds < 100, $"Pipeline took {stopwatch.ElapsedMilliseconds}ms, expected < 100ms");
    }
    
    [Fact]
    public void FullPipeline_EdgeCase_UnclosedString()
    {
        // Arrange
        var expression = "Name = 'unclosed string";
        
        // Act - Should handle gracefully
        var tokenizer = new ExpressionTokenizer(expression);
        var tokens = tokenizer.Tokenize().ToList();
        
        var parser = new ExpressionParser(expression);
        var regions = parser.Parse().ToList();
        
        // Assert - Should still produce some tokens/regions
        Assert.NotEmpty(tokens);
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "Name");
        Assert.Contains(tokens, t => t.Type == TokenType.ComparisonOperator && t.Value == "=");
        Assert.Contains(tokens, t => t.Type == TokenType.StringLiteral); // Even if unclosed
    }
    
    [Fact]
    public void FullPipeline_EdgeCase_MalformedDirective()
    {
        // Arrange
        var template = "{#if Level = 'Error' {@m}"; // Missing closing brace and {#end}
        
        // Act - Should handle gracefully
        var parser = new ExpressionParser(template);
        var regions = parser.ParseExpressionTemplate().ToList();
        
        // Assert - Should still classify what it can
        Assert.NotEmpty(regions);
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionDirective);
        Assert.Contains(regions, r => r.ClassificationType == SerilogClassificationTypes.ExpressionProperty && r.Text == "Level");
    }

    [Fact]
    public void Performance_NoMemoryLeaks_AfterMultipleIterations()
    {
        // Warm-up phase - let JIT and other one-time costs settle
        for (int i = 0; i < 100; i++)
        {
            var parser = new ExpressionParser(ExpressionTestData.ComplexFilter);
            _ = parser.Parse().ToList();
        }

        // Force collection after warm-up
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        // Take multiple samples to establish a pattern
        var memorySamples = new List<long>();
        const int samplesCount = 10;
        const int iterationsPerSample = 100;

        for (int sample = 0; sample < samplesCount; sample++)
        {
            // Perform iterations
            for (int i = 0; i < iterationsPerSample; i++)
            {
                var parser = new ExpressionParser(ExpressionTestData.ComplexFilter);
                var regions = parser.Parse().ToList();
                Assert.NotEmpty(regions);
            }

            // Collect and measure
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, true);

            memorySamples.Add(GC.GetTotalMemory(false));
        }

        // Analyze trend - memory shouldn't consistently increase
        var firstHalf = memorySamples.Take(samplesCount / 2).Average();
        var secondHalf = memorySamples.Skip(samplesCount / 2).Average();

        // Allow for some variance, but no significant upward trend
        var percentageIncrease = (secondHalf - firstHalf) / firstHalf * 100;

        Assert.True(percentageIncrease < 25,
            $"Memory showed {percentageIncrease:F2}% increase trend, suggesting a potential leak. " +
            $"First half avg: {firstHalf:N0} bytes, Second half avg: {secondHalf:N0} bytes");
    }
    
    [Fact]
    public void Performance_CacheHitRate_HighForRepeatedParsing()
    {
        // Clear cache first
        ExpressionDetector.ClearCache();
        
        var testLine = "Filter.ByExcluding(\"Level = 'Debug'\")";
        var testPosition = 20;
        
        // First call - cache miss
        var result1 = ExpressionDetector.GetContext(testLine, testPosition);
        
        // Next 100 calls should all be cache hits
        for (int i = 0; i < 100; i++)
        {
            var result = ExpressionDetector.GetContext(testLine, testPosition);
            Assert.Equal(result1, result);
        }
        
        // Cache should have handled all these efficiently
        Assert.Equal(ExpressionContext.FilterExpression, result1);
    }
}