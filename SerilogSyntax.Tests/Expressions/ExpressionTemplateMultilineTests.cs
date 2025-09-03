using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Serilog.Expressions;
using Serilog.Templates;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
using System;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Expressions
{
    /// <summary>
    /// Tests for multiline ExpressionTemplate syntax highlighting
    /// </summary>
    public class ExpressionTemplateMultilineTests
    {
        private readonly IClassificationTypeRegistryService _classificationRegistry = MockClassificationTypeRegistry.Create();
        
        [Fact]
        public void ExpressionTemplate_FormatSpecifier_InMultiline_ShouldClassify()
        {
            // This tests the exact scenario from line 389 in Example/Program.cs
            var code = @"
var templateConfig = new LoggerConfiguration()
    .WriteTo.File(new ExpressionTemplate(
        ""[{@t:yyyy-MM-dd HH:mm:ss.fff}] "" +
        ""{@m}""),
        path: ""logs/app.log"");";

            var buffer = MockTextBuffer.Create(code);
            var classifier = new SerilogClassifier(buffer, _classificationRegistry);
            
            // Get classifications for the line with the format specifier
            var line4 = buffer.CurrentSnapshot.GetLineFromLineNumber(3); // 0-indexed, so line 4 is index 3
            var span = new SnapshotSpan(line4.Start, line4.End);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Should find these classifications:
            // 1. @t - builtin property 
            // 2. : - format specifier
            // 3. yyyy-MM-dd HH:mm:ss.fff - format specifier
            // Note: Expression templates don't classify structural elements like [, ], {, }
            
            Assert.True(classifications.Count >= 3, $"Expected at least 3 classifications but got {classifications.Count}");
            
            // Check for the format specifier
            var formatSpecifiers = classifications.Where(c => 
                c.ClassificationType.Classification == "serilog.format").ToList();
            
            Assert.True(formatSpecifiers.Count >= 2, 
                $"Expected format specifier classifications (: and format string), but found {formatSpecifiers.Count}. Classifications found: {string.Join(", ", classifications.Select(c => c.ClassificationType.Classification))}");
            
            // The format specifier text should contain the date format
            var formatText = string.Join("", formatSpecifiers.Select(f => f.Span.GetText()));
            Assert.Contains("yyyy-MM-dd HH:mm:ss.fff", formatText);
        }

        [Fact]
        public void ExpressionTemplate_FormatSpecifier_SimplerCase_ShouldClassify()
        {
            // Test a simpler case first
            var code = @"
var logger = new LoggerConfiguration()
    .WriteTo.Console(new ExpressionTemplate(
        ""[{@t:HH:mm:ss}] {@m}""))
    .CreateLogger();";

            var buffer = MockTextBuffer.Create(code);
            var classifier = new SerilogClassifier(buffer, _classificationRegistry);
            
            // Get classifications for line 4 which contains the template
            var line4 = buffer.CurrentSnapshot.GetLineFromLineNumber(3);
            var span = new SnapshotSpan(line4.Start, line4.End);
            var classifications = classifier.GetClassificationSpans(span);
            
            Assert.NotEmpty(classifications);
            
            // Check for format specifier after @t:
            var formatSpecifiers = classifications.Where(c => 
                c.ClassificationType.Classification.Contains("format") ||
                c.Span.GetText().Contains("HH:mm:ss")).ToList();
            
            Assert.True(formatSpecifiers.Any(), 
                $"Expected format specifier for 'HH:mm:ss'. Classifications: {string.Join(", ", classifications.Select(c => $"{c.Span.GetText()}={c.ClassificationType.Classification}"))}");
        }

        [Fact]
        public void ExpressionTemplate_Functions_InMultiline_ShouldClassify()
        {
            // This tests expression functions like Substring, LastIndexOf
            var code = @"
var templateConfig = new LoggerConfiguration()
    .WriteTo.Console(new ExpressionTemplate(
        ""[{@t:HH:mm:ss} {@l:u3}] {#if SourceContext is not null}[{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}]{#end} {@m}""))
    .CreateLogger();";

            var buffer = MockTextBuffer.Create(code);
            var classifier = new SerilogClassifier(buffer, _classificationRegistry);
            
            var line4 = buffer.CurrentSnapshot.GetLineFromLineNumber(3);
            var span = new SnapshotSpan(line4.Start, line4.End);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Should find function classifications
            var functions = classifications.Where(c => 
                c.ClassificationType.Classification == "serilog.expression.function").ToList();
            
            Assert.True(functions.Count >= 2, 
                $"Expected at least 2 function classifications (Substring, LastIndexOf), but found {functions.Count}. " +
                $"Classifications: {string.Join(", ", classifications.Select(c => $"{c.Span.GetText()}={c.ClassificationType.Classification}"))}");
            
            // Check the function names
            var functionNames = functions.Select(f => f.Span.GetText()).ToList();
            Assert.Contains("Substring", functionNames);
            Assert.Contains("LastIndexOf", functionNames);
            
            // SourceContext should be classified as a property
            var properties = classifications.Where(c => 
                c.ClassificationType.Classification == "serilog.expression.property" &&
                c.Span.GetText() == "SourceContext").ToList();
            
            Assert.True(properties.Count >= 2, 
                $"Expected at least 2 SourceContext property classifications, found {properties.Count}");
        }

        [Fact]
        public void ExpressionTemplate_MultilineConcat_WithFunctions_ShouldClassify()
        {
            // Test multiline concatenated expression template
            var code = @"
var logger = new LoggerConfiguration()
    .WriteTo.File(new ExpressionTemplate(
        ""{#if IsError}[ERROR]{#else if Level = 'Warning'}[WARN]{#else}[INFO]{#end} "" +
        ""[{@t:yyyy-MM-dd HH:mm:ss.fff}] "" +
        ""{#if @p['RequestId'] is not null}[{@p['RequestId']}] {#end}"" +
        ""{@m}"" +
        ""{#each name, value in @p} | {name}={value}{#end}"" +
        ""{#if @x is not null}\n{@x}{#end}\n""),
        path: ""logs/app.log"");";

            var buffer = MockTextBuffer.Create(code);
            var classifier = new SerilogClassifier(buffer, _classificationRegistry);
            
            // Test line 5 with format specifier
            var line5 = buffer.CurrentSnapshot.GetLineFromLineNumber(4);
            var span5 = new SnapshotSpan(line5.Start, line5.End);
            var classifications5 = classifier.GetClassificationSpans(span5);
            
            Assert.NotEmpty(classifications5);
            
            // Should have format specifier for yyyy-MM-dd HH:mm:ss.fff
            var hasFormatSpec = classifications5.Any(c => 
                c.Span.GetText().Contains("yyyy-MM-dd") || 
                c.ClassificationType.Classification.Contains("format"));
            Assert.True(hasFormatSpec, 
                $"Line 5 should have format specifier. Found: {string.Join(", ", classifications5.Select(c => $"{c.Span.GetText()}={c.ClassificationType.Classification}"))}");
            
            // Test line 8 with {name}={value}
            var line8 = buffer.CurrentSnapshot.GetLineFromLineNumber(7);
            var span8 = new SnapshotSpan(line8.Start, line8.End);
            var classifications8 = classifier.GetClassificationSpans(span8);
            
            // Should classify 'name' and 'value' as something (property or identifier)
            var nameValue = classifications8.Where(c => 
                c.Span.GetText() == "name" || c.Span.GetText() == "value").ToList();
            
            Assert.True(nameValue.Count >= 2, 
                $"Expected 'name' and 'value' to be classified. Found: {string.Join(", ", classifications8.Select(c => $"{c.Span.GetText()}={c.ClassificationType.Classification}"))}");
        }

        [Fact]
        public void ExpressionTemplate_EachDirective_ShouldClassifyIteratorVariables()
        {
            // Test that iterator variables in #each are classified
            var code = @"
var config = new LoggerConfiguration()
    .WriteTo.Console(new ExpressionTemplate(
        ""{#each name, value in @p} | {name}={value}{#end}""))
    .CreateLogger();";

            var buffer = MockTextBuffer.Create(code);
            var classifier = new SerilogClassifier(buffer, _classificationRegistry);
            
            var line4 = buffer.CurrentSnapshot.GetLineFromLineNumber(3);
            var span = new SnapshotSpan(line4.Start, line4.End);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Should classify:
            // 1. #each as directive
            // 2. name, value as identifiers/variables in the directive
            // 3. @p as builtin property
            // 4. {name} and {value} as properties/variables
            // 5. #end as directive
            
            var directives = classifications.Where(c => 
                c.ClassificationType.Classification == "serilog.expression.directive" ||
                c.ClassificationType.Classification == "serilog.expression.keyword").ToList();
            
            Assert.True(directives.Count >= 1, 
                $"Expected directive classifications for #each/#end. Found: {string.Join(", ", classifications.Select(c => $"{c.Span.GetText()}={c.ClassificationType.Classification}"))}");
            
            // Check that {name} and {value} inside the template are classified
            var nameValueInTemplate = classifications.Where(c => 
                (c.Span.GetText() == "name" || c.Span.GetText() == "value") &&
                !c.ClassificationType.Classification.Contains("text")).ToList();
            
            Assert.True(nameValueInTemplate.Count >= 2, 
                $"Expected 'name' and 'value' in template to be classified. Found {nameValueInTemplate.Count} classifications");
        }

        [Fact]
        public void ExpressionTemplate_BuiltinProperty_WithIndexer_ShouldClassify()
        {
            // Test @p['RequestId'] syntax
            var code = @"
var logger = new LoggerConfiguration()
    .WriteTo.Console(new ExpressionTemplate(
        ""{#if @p['RequestId'] is not null}Request: {@p['RequestId']}{#end}""))
    .CreateLogger();";

            var buffer = MockTextBuffer.Create(code);
            var classifier = new SerilogClassifier(buffer, _classificationRegistry);
            
            var line4 = buffer.CurrentSnapshot.GetLineFromLineNumber(3);
            var span = new SnapshotSpan(line4.Start, line4.End);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Should classify @p as builtin property
            var builtinProps = classifications.Where(c => 
                c.ClassificationType.Classification == "serilog.expression.builtin" &&
                c.Span.GetText().Contains("@p")).ToList();
            
            Assert.True(builtinProps.Count >= 2, 
                $"Expected @p to be classified as builtin property at least twice. Found {builtinProps.Count}");
            
            // Should classify 'RequestId' as a literal (string literal in indexer)
            var literals = classifications.Where(c => 
                c.ClassificationType.Classification == "serilog.expression.literal" &&
                c.Span.GetText().Contains("RequestId")).ToList();
            
            Assert.True(literals.Any(), 
                $"Expected 'RequestId' to be classified as literal. Classifications: {string.Join(", ", classifications.Select(c => $"{c.Span.GetText()}={c.ClassificationType.Classification}"))}");
        }

        [Fact]
        public void ExpressionTemplate_UnclosedDirective_ShouldNotSpillover()
        {
            // This tests the exact scenario from the screenshot where {#end is missing closing brace
            var code = @"
var logger = new LoggerConfiguration()
    .WriteTo.Console(new ExpressionTemplate(
        ""[{@t:HH:mm:ss} {@l:u3}] {#if SourceContext is not null}[{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}]{#end {@m}\n{@x}""))
    .CreateLogger();";

            var buffer = MockTextBuffer.Create(code);
            var classifier = new SerilogClassifier(buffer, _classificationRegistry);
            
            // Get classifications for the template line
            var line4 = buffer.CurrentSnapshot.GetLineFromLineNumber(3);
            var span = new SnapshotSpan(line4.Start, line4.End);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Debug: Print all classifications
            var lineText = line4.GetText();
            var debugInfo = string.Join("\n", classifications.Select((c, i) => 
                $"  [{i}] '{c.Span.GetText()}' = {c.ClassificationType.Classification} at pos {c.Span.Start.Position - line4.Start.Position}"));
            
            // The text after {#end should NOT be classified as part of the directive
            // Since {#end is missing closing brace, it should be treated as incomplete
            
            // Find the position of {@m}
            var atMPosition = lineText.IndexOf("{@m}");
            
            if (atMPosition != -1)
            {
                // Check that {@m} is properly classified (not as part of directive)
                var mClassifications = classifications.Where(c => 
                    c.Span.Start.Position >= line4.Start + atMPosition &&
                    c.Span.Start.Position < line4.Start + atMPosition + 4).ToList();
                
                // {@m} should be classified as builtin property
                Assert.True(mClassifications.Any(c => 
                    c.ClassificationType.Classification == "serilog.expression.builtin"),
                    $"Expected @m to be classified as builtin.\nLine text: '{lineText}'\nAll classifications:\n{debugInfo}\n@m position classifications: {string.Join(", ", mClassifications.Select(c => $"{c.Span.GetText()}={c.ClassificationType.Classification}"))}");
            }
            
            // The {#end without closing brace should not cause the rest to be misclassified
            var directiveClassifications = classifications.Where(c => 
                c.ClassificationType.Classification == "serilog.expression.directive").ToList();
            
            // We should have directives for {#if ... } and potentially a partial {#end
            // But the text after {#end should not be part of a directive
            foreach (var dc in directiveClassifications)
            {
                var text = dc.Span.GetText();
                // No directive should contain {@m} or {@x}
                Assert.False(text.Contains("{@m}"), 
                    $"Directive '{text}' should not contain {{@m}}");
                Assert.False(text.Contains("{@x}"), 
                    $"Directive '{text}' should not contain {{@x}}");
            }
        }

        [Fact]
        public void ExpressionTemplate_UnclosedStringLiteral_ShouldNotSpillover()
        {
            // This tests the scenario where a string literal is unclosed (missing closing quote)
            // The literal is '. instead of '.' causing spillover
            var code = @"
var logger = new LoggerConfiguration()
    .WriteTo.Console(new ExpressionTemplate(
        ""[{@t:HH:mm:ss} {@l:u3}] {#if SourceContext is not null}[{Substring(SourceContext, LastIndexOf(SourceContext, '. + 1)}]{#end} {@m}\n{@x}""))
    .CreateLogger();";

            var buffer = MockTextBuffer.Create(code);
            var classifier = new SerilogClassifier(buffer, _classificationRegistry);
            
            // Get classifications for the template line
            var line4 = buffer.CurrentSnapshot.GetLineFromLineNumber(3);
            var span = new SnapshotSpan(line4.Start, line4.End);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Debug output
            var lineText = line4.GetText();
            var debugInfo = string.Join("\n", classifications.Select((c, i) => 
                $"  [{i}] '{c.Span.GetText()}' = {c.ClassificationType.Classification} at pos {c.Span.Start.Position - line4.Start.Position}"));
            
            // Find the + operator position
            var plusPosition = lineText.IndexOf("+ 1");
            
            if (plusPosition != -1)
            {
                // The + should be classified as an operator, not as part of a string literal
                var plusClassifications = classifications.Where(c => 
                    c.Span.Start.Position >= line4.Start + plusPosition &&
                    c.Span.Start.Position < line4.Start + plusPosition + 1).ToList();
                
                // The + should NOT be classified as a literal (which would indicate spillover)
                Assert.False(plusClassifications.Any(c => 
                    c.ClassificationType.Classification == "serilog.expression.literal"),
                    $"The + operator should not be classified as a literal (string spillover detected).\nLine text: '{lineText}'\nAll classifications:\n{debugInfo}");
                
                // Ideally, + should be classified as an operator
                Assert.True(plusClassifications.Any(c => 
                    c.ClassificationType.Classification == "serilog.expression.operator"),
                    $"The + should be classified as an operator.\nLine text: '{lineText}'\nAll classifications:\n{debugInfo}");
            }
            
            // The unclosed string literal should not cause everything after it to be misclassified
            // Check that {@m} is still properly classified
            var atMPosition = lineText.IndexOf("{@m}");
            if (atMPosition != -1)
            {
                var mClassifications = classifications.Where(c => 
                    c.Span.Start.Position >= line4.Start + atMPosition &&
                    c.Span.Start.Position < line4.Start + atMPosition + 4).ToList();
                
                Assert.True(mClassifications.Any(c => 
                    c.ClassificationType.Classification == "serilog.expression.builtin"),
                    $"Expected @m to be classified as builtin despite unclosed string literal");
            }
        }

        [Fact]
        public void ExpressionTemplate_UnclosedPropertyBrace_ShouldNotSpillover()
        {
            // This tests the scenario where a property is missing its closing brace
            // {@l:u3] instead of {@l:u3} causing the format specifier to spill over
            var code = @"
var logger = new LoggerConfiguration()
    .WriteTo.Console(new ExpressionTemplate(
        ""[{@t:HH:mm:ss} {@l:u3] {#if SourceContext is not null}[{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}]{#end} {@m}\n{@x}""))
    .CreateLogger();";

            var buffer = MockTextBuffer.Create(code);
            var classifier = new SerilogClassifier(buffer, _classificationRegistry);
            
            // Get classifications for the template line
            var line4 = buffer.CurrentSnapshot.GetLineFromLineNumber(3);
            var span = new SnapshotSpan(line4.Start, line4.End);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Debug output
            var lineText = line4.GetText();
            var debugInfo = string.Join("\n", classifications.Select((c, i) => 
                $"  [{i}] '{c.Span.GetText()}' = {c.ClassificationType.Classification} at pos {c.Span.Start.Position - line4.Start.Position}"));
            
            // Find the {#if directive position
            var ifPosition = lineText.IndexOf("{#if");
            
            if (ifPosition != -1)
            {
                // The {#if should be classified as a directive, not as part of format specifier
                var ifClassifications = classifications.Where(c => 
                    c.Span.Start.Position >= line4.Start + ifPosition &&
                    c.Span.Start.Position < line4.Start + ifPosition + 4).ToList();
                
                // The {#if should NOT be classified as a format specifier (which would indicate spillover)
                Assert.False(ifClassifications.Any(c => 
                    c.ClassificationType.Classification == "serilog.format"),
                    $"The {{#if should not be classified as a format specifier (spillover detected).\nLine text: '{lineText}'\nAll classifications:\n{debugInfo}");
                
                // It should be classified as a directive
                var directiveClassifications = classifications.Where(c =>
                    c.ClassificationType.Classification == "serilog.expression.directive" &&
                    c.Span.GetText().Contains("#if")).ToList();
                
                Assert.True(directiveClassifications.Any(),
                    $"Expected #if to be classified as a directive.\nLine text: '{lineText}'\nAll classifications:\n{debugInfo}");
            }
            
            // The format specifier u3 should only be 2 characters, not extending beyond
            var formatSpecifiers = classifications.Where(c => 
                c.ClassificationType.Classification == "serilog.format" &&
                c.Span.GetText().Contains("u3")).ToList();
            
            foreach (var fs in formatSpecifiers)
            {
                var text = fs.Span.GetText();
                // Format specifier should just be "u3", not include anything after
                Assert.False(text.Contains("]") || text.Contains("{") || text.Contains("#"),
                    $"Format specifier '{text}' should not contain brackets or directives (spillover detected)");
            }
        }

        [Fact]
        public void ExpressionTemplate_ComplexMultiline_ShouldBeValidSerilogExpression()
        {
            // Test that our complex multiline expression template is valid according to SerilogExpression.TryCompile
            var expression = 
                "{#if IsError}[ERROR]{#else if Level = 'Warning'}[WARN]{#else}[INFO]{#end} " +
                "[{@t:yyyy-MM-dd HH:mm:ss.fff}] " +
                "{#if @p['RequestId'] is not null}[{@p['RequestId']}] {#end}" +
                "{@m}" +
                "{#each name, value in @p} | {name}={value}{#end}" +
                "{#if @x is not null}\n{@x}{#end}\n";

            // Try to compile the full expression string
            if (ExpressionTemplate.TryParse(expression, out var compiled, out var error))
            {
                Assert.NotNull(compiled);
                Assert.Null(error);
            }
            else
            {
                // If it fails, let's see what the error is
                Assert.Fail($"Failed to compile expression template: {error}");
            }
        }

        [Fact]
        public void ExpressionTemplate_FromProgramCs_ShouldBeValidSerilogExpression()
        {
            // Test the actual expression templates from Program.cs
            var template1 = "[{@t:HH:mm:ss} {@l:u3}] {#if SourceContext is not null}[{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}]{#end} {@m}\n{@x}";
            
            if (ExpressionTemplate.TryParse(template1, out var compiled1, out var error1))
            {
                Assert.NotNull(compiled1);
                Assert.Null(error1);
            }
            else
            {
                Assert.Fail($"Failed to compile template1: {error1}");
            }
            
            var template2 = 
                "{#if IsError}[ERROR]{#else if Level = 'Warning'}[WARN]{#else}[INFO]{#end} " +
                "[{@t:yyyy-MM-dd HH:mm:ss.fff}] " +
                "{#if @p['RequestId'] is not null}[{@p['RequestId']}] {#end}" +
                "{@m}" +
                "{#each name, value in @p} | {name}={value}{#end}" +
                "{#if @x is not null}\n{@x}{#end}\n";
            
            if (ExpressionTemplate.TryParse(template2, out var compiled2, out var error2))
            {
                Assert.NotNull(compiled2);
                Assert.Null(error2);
            }
            else
            {
                Assert.Fail($"Failed to compile template2: {error2}");
            }
        }

        [Fact] 
        public void ExpressionTemplate_InvalidSyntax_ShouldFailCompilation()
        {
            // Test that invalid templates fail SerilogExpression.TryCompile
            var invalidTemplates = new[]
            {
                "{#if IsError [ERROR]{#end}", // Missing closing brace after IsError
                "{@t:HH:mm:ss", // Missing closing brace
                "{#if Level = 'Warning}[WARN]{#end}", // Unclosed string literal
                "{@l:u3]", // Wrong closing bracket
                "{#end", // Unclosed directive
            };
            
            foreach (var invalid in invalidTemplates)
            {
                var result = SerilogExpression.TryCompile(invalid, out var compiled, out var error);
                Assert.False(result, $"Expected '{invalid}' to fail compilation but it succeeded");
                Assert.Null(compiled);
                Assert.NotNull(error);
            }
        }

        [Fact]
        public void RegularLogStatement_AfterExpressionCalls_ShouldNotBeClassifiedAsExpression()
        {
            // This tests that a regular log statement after expression-related calls
            // is not misclassified as being inside an expression context
            var code = @"
using Serilog;

class Test
{
    void TestMethod()
    {
        var logger = new LoggerConfiguration()
            .Filter.ByExcluding(""RequestPath like '/health%' and StatusCode < 400"")
            .WriteTo.Conditional(""Environment = 'Production' and Level >= 'Warning'"", 
                wt => wt.File(""logs/prod-warnings.log""))
            .CreateLogger();
        
        // This is a regular log statement, not an expression
        logger.LogInformation(""Serilog.Expressions configuration example created"");
    }
}";

            var buffer = MockTextBuffer.Create(code);
            var classifier = new SerilogClassifier(buffer, _classificationRegistry);
            
            // Find the line with the regular LogInformation call
            var logLine = -1;
            for (int i = 0; i < buffer.CurrentSnapshot.LineCount; i++)
            {
                var line = buffer.CurrentSnapshot.GetLineFromLineNumber(i);
                if (line.GetText().Contains("\"Serilog.Expressions configuration example created\""))
                {
                    logLine = i;
                    break;
                }
            }
            
            Assert.True(logLine >= 0, "Could not find the log statement line");
            
            var targetLine = buffer.CurrentSnapshot.GetLineFromLineNumber(logLine);
            var span = new SnapshotSpan(targetLine.Start, targetLine.End);
            var classifications = classifier.GetClassificationSpans(span);
            
            // The string "Serilog.Expressions configuration example created" should NOT have any
            // expression-related classifications. It's just a regular message template string.
            var stringContent = "Serilog.Expressions configuration example created";
            
            // Find classifications within the string content
            var stringStart = targetLine.GetText().IndexOf(stringContent);
            if (stringStart >= 0)
            {
                var stringClassifications = classifications.Where(c =>
                {
                    var offsetInLine = c.Span.Start.Position - targetLine.Start.Position;
                    return offsetInLine >= stringStart && offsetInLine < stringStart + stringContent.Length;
                }).ToList();
                
                // Debug output
                if (stringClassifications.Any())
                {
                    var classificationDetails = string.Join("\n", stringClassifications.Select(c => 
                        $"  '{c.Span.GetText()}' = {c.ClassificationType.Classification}"));
                    
                    // Should not have any expression classifications
                    Assert.False(stringClassifications.Any(c => 
                        c.ClassificationType.Classification.StartsWith("serilog.expression")),
                        $"Regular log message should not have expression classifications.\nFound:\n{classificationDetails}");
                    
                    // Should not have literal classifications (blue highlighting)
                    Assert.False(stringClassifications.Any(c => 
                        c.ClassificationType.Classification == "serilog.expression.literal"),
                        $"Regular log message should not be classified as expression literal.\nFound:\n{classificationDetails}");
                }
            }
        }
    }
}