using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
using System;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Parsing;

/// <summary>
/// Tests that verify raw string literals containing logger text are NOT treated as Serilog calls
/// when they are passed as arguments to methods.
/// </summary>
public class RawStringInArgumentTests
{
    private readonly IClassificationTypeRegistryService _classificationRegistry = MockClassificationTypeRegistry.Create();

    [Fact]
    public void RawStringAsMethodArgument_ContainingLoggerText_ShouldNotClassify()
    {
        // This reproduces the exact issue from the screenshot:
        // Raw string literals containing "logger.LogInformation" text passed as method arguments
        // should NOT be treated as actual Serilog calls
        var code = @"var textBuffer = MockTextBuffer.Create(@""
logger.LogInformation(""""""
    Processing record:
    ID: {RecordId}
    Status: {Status}
    User: {@User}
"""""", recordId, status, user);";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        // Process each line individually (as VS would do)
        for (int i = 0; i < snapshot.LineCount; i++)
        {
            var line = snapshot.GetLineFromLineNumber(i);
            var lineText = line.GetText().Trim();
            var lineSpan = new SnapshotSpan(line.Start, line.End);
            var classifications = classifier.GetClassificationSpans(lineSpan);

            Console.WriteLine($"Line {i}: '{lineText}'");
            Console.WriteLine($"  Classifications: {classifications.Count}");
            foreach (var c in classifications)
            {
                Console.WriteLine($"  - {c.ClassificationType.Classification} at {c.Span.GetText()}");
            }

            // Lines containing {RecordId}, {Status}, {@User} should have NO classifications
            // because this is a raw string argument, not a real Serilog call
            if (lineText.Contains("{RecordId}") || lineText.Contains("{Status}") || lineText.Contains("{@User}"))
            {
                Assert.Empty(classifications); // BUG: This will fail if properties are incorrectly classified
            }
        }
    }

    [Fact]
    public void MultipleRawStringArguments_WithLoggerText_ShouldNotClassify()
    {
        // Test multiple method calls with raw string arguments containing logger text
        var code = @"
// Arrange - Create a mock text snapshot with a multi-line raw string
var textBuffer = MockTextBuffer.Create(@""
logger.LogInformation(""""""
    Processing record:
    ID: {RecordId}
    Status: {Status}
"""""");

// More code here  
logger.LogDebug(""Another {Message}"", msg);";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        int propertiesFoundInArguments = 0;
        int propertiesFoundInRealCalls = 0;

        for (int i = 0; i < snapshot.LineCount; i++)
        {
            var line = snapshot.GetLineFromLineNumber(i);
            var lineText = line.GetText().Trim();
            var lineSpan = new SnapshotSpan(line.Start, line.End);
            var classifications = classifier.GetClassificationSpans(lineSpan);

            var propertyCount = classifications
                .Where(c => c.ClassificationType.Classification == "serilog.property.name")
                .Count();

            if (lineText.Contains("{RecordId}") || lineText.Contains("{Status}"))
            {
                // These are inside raw string arguments - should not be classified
                propertiesFoundInArguments += propertyCount;
            }
            else if (lineText.Contains("{Message}"))
            {
                // This is a real Serilog call - should be classified
                propertiesFoundInRealCalls += propertyCount;
            }
        }

        // Properties in raw string arguments should not be classified
        Assert.Equal(0, propertiesFoundInArguments);
        
        // Properties in real Serilog calls should be classified
        Assert.Equal(1, propertiesFoundInRealCalls);
    }

    [Fact]
    public void CachePerformanceTest_RawStringArgument_ShouldNotClassify()
    {
        // This reproduces the exact scenario from the screenshot
        var code = @"[Fact]
public void CacheImprovement_MultipleCalls_ToSameLine_UsesCachedResult()
{
    // Arrange - Create a mock text snapshot with a multi-line raw string
    var textBuffer = MockTextBuffer.Create(@""
logger.LogInformation(""""""
    Processing record:
    ID: {RecordId}
    Status: {Status}
    User: {@User}
"""""", recordId, status, user);";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        // Find lines with properties that should NOT be classified
        var problemLines = new[] { "ID: {RecordId}", "Status: {Status}", "User: {@User}" };
        
        foreach (string expectedContent in problemLines)
        {
            // Find the line containing this content
            for (int i = 0; i < snapshot.LineCount; i++)
            {
                var line = snapshot.GetLineFromLineNumber(i);
                var lineText = line.GetText();
                
                if (lineText.Contains(expectedContent))
                {
                    var lineSpan = new SnapshotSpan(line.Start, line.End);
                    var classifications = classifier.GetClassificationSpans(lineSpan);
                    
                    Console.WriteLine($"Testing line: '{lineText.Trim()}'");
                    Console.WriteLine($"Classifications found: {classifications.Count}");
                    
                    // This line is inside a raw string argument, not a real Serilog call
                    // It should have NO classifications
                    Assert.Empty(classifications);
                    break;
                }
            }
        }
    }
}