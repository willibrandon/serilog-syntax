using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Parsing;

public class ExampleSyntaxTests
{
    private readonly IClassificationTypeRegistryService _classificationRegistry = MockClassificationTypeRegistry.Create();
    private readonly string _programPath;
    private readonly string _exampleServicePath;
    
    public ExampleSyntaxTests()
    {
        _programPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            @"..\..\..\..\Example\Program.cs");
        
        if (!File.Exists(_programPath))
        {
            _programPath = Path.Combine(Directory.GetCurrentDirectory(), 
                @"..\..\..\..\Example\Program.cs");
        }
        
        if (!File.Exists(_programPath))
        {
            throw new FileNotFoundException($"Could not find Program.cs at {_programPath}");
        }
        
        // Also find ExampleService.cs
        _exampleServicePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
            @"..\..\..\..\Example\ExampleService.cs");
        
        if (!File.Exists(_exampleServicePath))
        {
            _exampleServicePath = Path.Combine(Directory.GetCurrentDirectory(), 
                @"..\..\..\..\Example\ExampleService.cs");
        }
        
        if (!File.Exists(_exampleServicePath))
        {
            throw new FileNotFoundException($"Could not find ExampleService.cs at {_exampleServicePath}");
        }
    }
    
    [Fact]
    public void EntireProgramCs_ProcessedAsWhole_ShowsBug()
    {
        // Read the ENTIRE Program.cs file AND ExampleService.cs (since content was moved there)
        var fullProgramCode = File.ReadAllText(_programPath) + "\n" + File.ReadAllText(_exampleServicePath);
        
        // Process it as VS would - the entire file at once
        var textBuffer = MockTextBuffer.Create(fullProgramCode);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Process the entire file
        var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
        var allClassifications = classifier.GetClassificationSpans(fullSpan);
        
        // Count how many properties were found
        var propertyCount = allClassifications
            .Where(c => c.ClassificationType.Classification == "serilog.property.name")
            .Count();
        
        // Program.cs has DOZENS of properties - if we find 0 or very few, bug is reproduced
        Console.WriteLine($"Found {propertyCount} properties in entire Program.cs");
        
        // List first 10 classifications for debugging
        foreach (var c in allClassifications.Take(10))
        {
            Console.WriteLine($"  {c.ClassificationType.Classification}: {c.Span.GetText()}");
        }
        
        // This should find many properties (50+)
        // If it finds 0 or very few, we've reproduced the bug
        Assert.True(propertyCount > 20, $"Expected many properties, found only {propertyCount}");
    }
    
    [Fact]
    public void ProcessProgramCs_LineByLine_ShowsDifference()
    {
        var fullProgramCode = File.ReadAllText(_programPath) + "\n" + File.ReadAllText(_exampleServicePath);
        var textBuffer = MockTextBuffer.Create(fullProgramCode);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        int totalPropertiesLineByLine = 0;
        int totalPropertiesFullSpan = 0;
        
        // Process line by line
        for (int i = 0; i < snapshot.LineCount; i++)
        {
            var line = snapshot.GetLineFromLineNumber(i);
            var lineSpan = new SnapshotSpan(line.Start, line.End);
            var lineClassifications = classifier.GetClassificationSpans(lineSpan);
            
            var lineProperties = lineClassifications
                .Where(c => c.ClassificationType.Classification == "serilog.property.name")
                .Count();
            
            totalPropertiesLineByLine += lineProperties;
            
            if (lineProperties > 0)
            {
                Console.WriteLine($"Line {i + 1}: Found {lineProperties} properties in: {line.GetText().Trim()}");
            }
        }
        
        // Process entire file at once
        var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
        var allClassifications = classifier.GetClassificationSpans(fullSpan);
        totalPropertiesFullSpan = allClassifications
            .Where(c => c.ClassificationType.Classification == "serilog.property.name")
            .Count();
        
        Console.WriteLine($"Line-by-line processing found: {totalPropertiesLineByLine} properties");
        Console.WriteLine($"Full-span processing found: {totalPropertiesFullSpan} properties");
        
        // If these numbers are very different, we've found the issue
        Assert.Equal(totalPropertiesLineByLine, totalPropertiesFullSpan);
    }
    
    [Fact]
    public void SerilogExpressionsExamples_HighlightsExpressionSyntax()
    {
        // This test verifies that Serilog.Expressions syntax in the actual Example\Program.cs is properly highlighted
        
        // Read the actual Program.cs file AND ExampleService.cs (since content was moved there)
        var programCode = File.ReadAllText(_programPath) + "\n" + File.ReadAllText(_exampleServicePath);
        
        var textBuffer = MockTextBuffer.Create(programCode);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Process the entire file
        var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
        var allClassifications = classifier.GetClassificationSpans(fullSpan);
        
        // Count expression-related classifications
        var expressionProperties = allClassifications.Where(c => c.ClassificationType.Classification == "serilog.expression.property").Count();
        var expressionOperators = allClassifications.Where(c => c.ClassificationType.Classification == "serilog.expression.operator").Count();
        var expressionFunctions = allClassifications.Where(c => c.ClassificationType.Classification == "serilog.expression.function").Count();
        var expressionKeywords = allClassifications.Where(c => c.ClassificationType.Classification == "serilog.expression.keyword").Count();
        var expressionLiterals = allClassifications.Where(c => c.ClassificationType.Classification == "serilog.expression.literal").Count();
        var expressionDirectives = allClassifications.Where(c => c.ClassificationType.Classification == "serilog.expression.directive").Count();
        var expressionBuiltins = allClassifications.Where(c => c.ClassificationType.Classification == "serilog.expression.builtin").Count();
        
        Console.WriteLine($"Expression classifications found in Program.cs:");
        Console.WriteLine($"  Properties: {expressionProperties}");
        Console.WriteLine($"  Operators: {expressionOperators}");
        Console.WriteLine($"  Functions: {expressionFunctions}");
        Console.WriteLine($"  Keywords: {expressionKeywords}");
        Console.WriteLine($"  Literals: {expressionLiterals}");
        Console.WriteLine($"  Directives: {expressionDirectives}");
        Console.WriteLine($"  Built-ins: {expressionBuiltins}");
        
        // Log some expression classifications for debugging
        var expressionClassifications = allClassifications
            .Where(c => c.ClassificationType.Classification.StartsWith("serilog.expression"))
            .Take(20);
        
        Console.WriteLine($"\nFirst 20 expression classifications:");
        foreach (var c in expressionClassifications)
        {
            Console.WriteLine($"  {c.ClassificationType.Classification}: '{c.Span.GetText()}'");
        }

        // Verify that expression syntax is being highlighted in the actual Program.cs
        // The Program.cs SerilogExpressionsExamples method contains many expressions

        // Based on the actual Program.cs content (lines 354-395), we expect:
        // - Multiple Filter.ByExcluding and Filter.ByIncludingOnly calls with filter expressions
        // - Multiple Enrich.WithComputed calls with computed property expressions
        // - WriteTo.Conditional calls with conditional expressions
        // - ExpressionTemplate instances with directives and built-ins

        // These are the exact counts from the actual Program.cs content
        Assert.True(expressionProperties >= 45, $"Expected at least 45 expression properties in Program.cs, found {expressionProperties}");
        Assert.True(expressionOperators >= 35, $"Expected at least 35 expression operators, found {expressionOperators}");
        Assert.True(expressionFunctions >= 11, $"Expected at least 11 expression functions, found {expressionFunctions}");
        Assert.True(expressionKeywords >= 3, $"Expected at least 3 expression keywords, found {expressionKeywords}");
        Assert.True(expressionLiterals >= 32, $"Expected at least 33 expression literals, found {expressionLiterals}");
        Assert.True(expressionDirectives >= 12, $"Expected at least 12 expression directives, found {expressionDirectives}");
        Assert.True(expressionBuiltins >= 10, $"Expected at least 10 built-in properties, found {expressionBuiltins}");
    }
}