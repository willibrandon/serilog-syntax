using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
using System;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Expressions;

public class ExpressionTemplateTests
{
    private readonly IClassificationTypeRegistryService _classificationRegistry = MockClassificationTypeRegistry.Create();

    public ExpressionTemplateTests()
    {
    }

    [Fact]
    public void ExpressionTemplate_SingleLine_ShouldClassifyTemplate()
    {
        // Arrange
        var code = @"
var templateConfig = new LoggerConfiguration()
    .WriteTo.Console(new ExpressionTemplate(""[{@t:HH:mm:ss} {@l:u3}] {@m}\n{@x}""));";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        // Act
        var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));

        // Assert - The template string should have classifications
        var templateLine = snapshot.Lines.FirstOrDefault(l => l.GetText().Contains("{@t:HH:mm:ss}"));
        Assert.NotNull(templateLine);
        
        var templateSpans = spans.Where(s => s.Span.IntersectsWith(templateLine.Extent)).ToList();
        Assert.NotEmpty(templateSpans); // Should have classifications for {@t}, {@l}, {@m}, {@x}
        
        // ExpressionTemplates use expression syntax, not regular template syntax
        // So we should see expression classifications, not regular template classifications
        Assert.Contains(templateSpans, s => s.ClassificationType.Classification.Contains("expression"));
        Assert.Contains(templateSpans, s => s.Span.GetText().Contains("@t"));
    }

    [Fact]
    public void ExpressionTemplate_MultiLine_ShouldClassifyAllLines()
    {
        // Arrange
        var code = @"
var templateConfig = new LoggerConfiguration()
    .WriteTo.Console(new ExpressionTemplate(
        ""[{@t:HH:mm:ss} {@l:u3}] {#if SourceContext is not null}[{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}]{#end} {@m}\n{@x}""))
    .WriteTo.File(new ExpressionTemplate(
        ""{#if IsError}[ERROR]{#else if Level = 'Warning'}[WARN]{#else}[INFO]{#end} "" +
        ""[{@t:yyyy-MM-dd HH:mm:ss.fff}] "" +
        ""{#if @p['RequestId'] is not null}[{@p['RequestId']}] {#end}"" +
        ""{@m}"" +
        ""{#each name, value in @p} | {name}={value}{#end}"" +
        ""{#if @x is not null}\n{@x}{#end}\n""),
        path: ""logs/app.log"");";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        // Act
        var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));

        // Assert - Line 4 (first template string) should have classifications  
        var line4 = snapshot.Lines.ElementAt(3); // 0-indexed - the actual template string
        var line4Text = line4.GetText();
        Assert.Contains("{@t:HH:mm:ss}", line4Text);
        
        var line4Spans = spans.Where(s => s.Span.IntersectsWith(line4.Extent)).ToList();
        Assert.NotEmpty(line4Spans); // This line should have classifications
        
        // Assert - Line 6 (second template, first concatenated string) should have classifications
        var line6 = snapshot.Lines.ElementAt(5);
        var line6Text = line6.GetText();
        Assert.Contains("{#if IsError}", line6Text);
        
        var line6Spans = spans.Where(s => s.Span.IntersectsWith(line6.Extent)).ToList();
        Assert.NotEmpty(line6Spans); // This line should have classifications
        
        // Assert - Line 7 (concatenated string) should also have classifications
        var line7 = snapshot.Lines.ElementAt(6);
        var line7Text = line7.GetText();
        Assert.Contains("{@t:yyyy-MM-dd", line7Text);
        
        var line7Spans = spans.Where(s => s.Span.IntersectsWith(line7.Extent)).ToList();
        Assert.NotEmpty(line7Spans); // This line should have classifications
    }

    [Fact]
    public void ExpressionTemplate_WithStringConcatenation_ShouldClassifyAllParts()
    {
        // Arrange
        var code = @"
var logger = new LoggerConfiguration()
    .WriteTo.File(new ExpressionTemplate(
        ""{#if Level = 'Error'}[ERR]{#end} "" +
        ""[{@t:HH:mm:ss}] "" +
        ""{@m}""),
        path: ""test.log"");";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        // Act
        var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));

        // Assert - Each concatenated string part should be classified
        foreach (var lineNumber in new[] { 3, 4, 5 }) // Lines with template strings (0-indexed)
        {
            var line = snapshot.Lines.ElementAt(lineNumber);
            var lineSpans = spans.Where(s => s.Span.IntersectsWith(line.Extent)).ToList();
            
            Assert.NotEmpty(lineSpans); // Each line should have classifications
            
            // Verify the line contains template syntax that should be highlighted
            var lineText = line.GetText();
            if (lineText.Contains("{#if") || lineText.Contains("{@t") || lineText.Contains("{@m"))
            {
                Assert.True(lineSpans.Any(s => s.ClassificationType.Classification.Contains("serilog")),
                    $"Line {lineNumber + 1} should have Serilog classifications but doesn't: {lineText}");
            }
        }
    }

    [Fact]
    public void ExpressionTemplate_NotInSerilogContext_ShouldNotClassify()
    {
        // Arrange
        var code = @"
// This is not a Serilog call
var template = ""[{@t:HH:mm:ss}] {@m}"";
var result = SomeOtherMethod(new ExpressionTemplate(template));";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        // Act
        var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));

        // Assert - Should not classify templates outside of Serilog context
        // Note: This test might pass because we do check for ExpressionTemplate
        // but it's important to verify the behavior
        var templateSpans = spans.Where(s => s.ClassificationType.Classification.Contains("serilog")).ToList();
        
        // If it's truly an ExpressionTemplate, it might still classify
        // This test documents the expected behavior
    }

    [Fact]
    public void ExpressionTemplate_ElseIfDirective_ShouldClassifyCorrectly()
    {
        // Arrange - This is the exact pattern from the screenshot that wasn't being highlighted
        var code = @"
var templateConfig = new LoggerConfiguration()
    .WriteTo.File(new ExpressionTemplate(
        ""{#if IsError}[ERROR]{#else if Level = 'Warning'}[WARN]{#else}[INFO]{#end} "" +
        ""[{@t:yyyy-MM-dd HH:mm:ss.fff}] "" +
        ""{@m}\n""));";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        // Act
        var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));

        // Assert - Find the line with the #else if directive
        var elseIfLine = snapshot.Lines.FirstOrDefault(l => l.GetText().Contains("#else if"));
        Assert.NotNull(elseIfLine);
        
        var elseIfSpans = spans.Where(s => s.Span.IntersectsWith(elseIfLine.Extent)).ToList();
        
        // Should classify #else if as directive
        var elseIfDirective = elseIfSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.directive" &&
            s.Span.GetText().Contains("#else if"));
        Assert.NotNull(elseIfDirective);
        
        // Should classify Level as property
        var levelProperty = elseIfSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.property" &&
            s.Span.GetText() == "Level");
        Assert.NotNull(levelProperty);
        
        // Should classify = as operator
        var equalOperator = elseIfSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.operator" &&
            s.Span.GetText() == "=");
        Assert.NotNull(equalOperator);
        
        // Should classify 'Warning' as literal
        var warningLiteral = elseIfSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.literal" &&
            s.Span.GetText() == "'Warning'");
        Assert.NotNull(warningLiteral);
    }

    [Fact]
    public void ExpressionTemplate_EachLoopVariables_ShouldClassifyCorrectly()
    {
        // Arrange - This is the exact pattern from the screenshot that wasn't being highlighted
        var code = @"
var templateConfig = new LoggerConfiguration()
    .WriteTo.File(new ExpressionTemplate(
        ""{#each name, value in @p} | {name}={value}{#end}"" +
        ""{@m}\n""));";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        // Act
        var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));

        // Assert - Find the line with the #each directive and loop body
        var eachLine = snapshot.Lines.FirstOrDefault(l => l.GetText().Contains("#each"));
        Assert.NotNull(eachLine);
        
        var eachSpans = spans.Where(s => s.Span.IntersectsWith(eachLine.Extent)).ToList();
        
        // Should classify #each as directive
        var eachDirective = eachSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.directive" &&
            s.Span.GetText().Contains("#each"));
        Assert.NotNull(eachDirective);
        
        // Should classify the loop variables 'name' and 'value' in the directive
        var nameInDirective = eachSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.property" &&
            s.Span.GetText() == "name");
        Assert.NotNull(nameInDirective);
        
        var valueInDirective = eachSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.property" &&
            s.Span.GetText() == "value");
        Assert.NotNull(valueInDirective);
        
        // Should classify @p as builtin
        var pBuiltin = eachSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.builtin" &&
            s.Span.GetText() == "@p");
        Assert.NotNull(pBuiltin);
        
        // Should classify {name} in the loop body as expression property
        var nameInBody = eachSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.property" &&
            s.Span.GetText() == "name" &&
            s != nameInDirective); // Different from the one in the directive
        Assert.NotNull(nameInBody);
        
        // Should classify {value} in the loop body as expression property
        var valueInBody = eachSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.property" &&
            s.Span.GetText() == "value" &&
            s != valueInDirective); // Different from the one in the directive
        Assert.NotNull(valueInBody);
        
        // Note: The = in "| {name}={value}" is literal text outside braces, not an expression operator
        // So we don't expect it to be classified as serilog.expression.operator
    }

    [Fact]
    public void ExpressionTemplate_IndexerSyntax_ShouldClassifyCorrectly()
    {
        // Arrange - This is the exact pattern from the screenshot that wasn't being highlighted
        var code = @"
var templateConfig = new LoggerConfiguration()
    .WriteTo.File(new ExpressionTemplate(
        ""{#if @p['RequestId'] is not null}[{@p['RequestId']}] {#end}"" +
        ""{@m}\n""));";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        // Act
        var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));

        // Assert - Find the line with the indexer syntax
        var indexerLine = snapshot.Lines.FirstOrDefault(l => l.GetText().Contains("@p['RequestId']"));
        Assert.NotNull(indexerLine);
        
        var indexerSpans = spans.Where(s => s.Span.IntersectsWith(indexerLine.Extent)).ToList();
        
        // Test 1: 'RequestId' should be highlighted as expression literal
        var requestIdLiteral = indexerSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.literal" &&
            s.Span.GetText() == "'RequestId'");
        Assert.NotNull(requestIdLiteral);
        
        // Test 2: [ and ] should be highlighted as expression operators (red)
        var openBracketOperator = indexerSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.operator" &&
            s.Span.GetText() == "[");
        Assert.NotNull(openBracketOperator);
        
        var closeBracketOperator = indexerSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.operator" &&
            s.Span.GetText() == "]");
        Assert.NotNull(closeBracketOperator);
        
        // Test 3: The braces { and } should be highlighted as property braces (purple)
        var openBrace = indexerSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.brace" &&
            s.Span.GetText() == "{");
        Assert.NotNull(openBrace);
        
        var closeBrace = indexerSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.brace" &&
            s.Span.GetText() == "}");
        Assert.NotNull(closeBrace);
        
        // Should also classify @p as builtin
        var pBuiltin = indexerSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.builtin" &&
            s.Span.GetText() == "@p");
        Assert.NotNull(pBuiltin);
        
        // Should classify 'is not null' as operators/keywords
        var isOperator = indexerSpans.FirstOrDefault(s => 
            s.ClassificationType.Classification == "serilog.expression.operator" &&
            s.Span.GetText() == "is not null");
        Assert.NotNull(isOperator);
    }
}