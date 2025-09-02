using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
using System;
using System.IO;
using Xunit;

namespace SerilogSyntax.Tests.Parsing;

/// <summary>
/// Tests that verify regular string assignments with braces don't get classified as Serilog properties
/// </summary>
public class RegularStringAssignmentTests
{
    private readonly IClassificationTypeRegistryService _classificationRegistry = MockClassificationTypeRegistry.Create();

    [Fact]
    public void RegularStringAssignment_WithBraces_ShouldNotClassify()
    {
        // Read the actual VerbatimStringIntegrationTests.cs file
        var testFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\SerilogSyntax.Tests\Classification\VerbatimStringIntegrationTests.cs");
        
        var testFileContent = File.ReadAllText(testFilePath);
        
        var textBuffer = MockTextBuffer.Create(testFileContent);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Find the line with: var extractedContent = "User {Name} logged in";
        for (int i = 0; i < snapshot.LineCount; i++)
        {
            var line = snapshot.GetLineFromLineNumber(i);
            var lineText = line.GetText();
            
            if (lineText.Contains("var extractedContent = \"User {Name} logged in\""))
            {
                var lineSpan = new SnapshotSpan(line.Start, line.End);
                var classifications = classifier.GetClassificationSpans(lineSpan);
                
                Console.WriteLine($"Testing line: '{lineText.Trim()}'");
                Console.WriteLine($"Classifications found: {classifications.Count}");
                
                // This is a regular string assignment, not a Serilog call
                // The {Name} should NOT be classified as a Serilog property
                Assert.Empty(classifications);
                return;
            }
        }
        
        // If we didn't find the line, fail the test
        Assert.Fail("Could not find the expected line with 'var extractedContent = \"User {Name} logged in\"'");
    }
}