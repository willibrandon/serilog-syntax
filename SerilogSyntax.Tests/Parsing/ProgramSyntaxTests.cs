using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Parsing;

public class ProgramSyntaxTests
{
    private readonly IClassificationTypeRegistryService _classificationRegistry = MockClassificationTypeRegistry.Create();
    private readonly string _programPath;
    
    public ProgramSyntaxTests()
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
    }
    
    [Fact]
    public void EntireProgramCs_ProcessedAsWhole_ShowsBug()
    {
        // Read the ENTIRE Program.cs file
        var fullProgramCode = File.ReadAllText(_programPath);
        
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
        var fullProgramCode = File.ReadAllText(_programPath);
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
}