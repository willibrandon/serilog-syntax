using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Parsing;

/// <summary>
/// Tests for verbatim string classification - demonstrates the BUG where
/// SerilogCallDetector has false positives, causing properties to be 
/// incorrectly highlighted in non-Serilog strings.
/// </summary>
public class VerbatimStringTests
{
    private readonly IClassificationTypeRegistryService _classificationRegistry = MockClassificationTypeRegistry.Create();

    [Fact]
    public void BUG_REPRODUCTION_RawStringContainingLoggerText()
    {
        // The REAL bug: A raw string that CONTAINS text about logger methods
        // This is NOT a Serilog call, just documentation/example text
        var code = @"var documentation = """"""
logger.LogInformation(""""""
    ID: {RecordId}
    Status: {Status}
"""""");
"""""";";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Verify we have the expected lines
        Assert.True(snapshot.LineCount >= 4);
        
        // Line 1 contains "logger.LogInformation(""""""" 
        // This will match SerilogCallDetector even though it's inside a string
        var line1 = snapshot.GetLineFromLineNumber(1);
        var line1Text = line1.GetText();
        Assert.Contains("logger.LogInformation(", line1Text);
        
        // Test if SerilogCallDetector matches this line
        var isSerilogCall = SerilogSyntax.Utilities.SerilogCallDetector.IsSerilogCall(line1Text);
        Console.WriteLine($"Line 1 text: '{line1Text}'");
        Console.WriteLine($"IsSerilogCall: {isSerilogCall}");
        
        // Line 2 has {RecordId} - check if it gets classified
        var line2 = snapshot.GetLineFromLineNumber(2);
        var line2Span = new SnapshotSpan(line2.Start, line2.End);
        Console.WriteLine($"Line 2 text: '{line2.GetText()}'");
        
        var classifications = classifier.GetClassificationSpans(line2Span);
        Console.WriteLine($"Line 2 classifications: {classifications.Count}");
        foreach (var c in classifications)
        {
            Console.WriteLine($"  - {c.ClassificationType.Classification} at {c.Span}");
        }
        
        // BUG: Properties should NOT be classified in documentation strings
        // This test will FAIL while bug exists (classifications not empty)
        // and will PASS when bug is fixed (classifications empty)
        Assert.Empty(classifications); // Should be no classifications in documentation
    }

    [Fact]
    public void BUG_FalsePositiveSerilogDetection_InRawString()
    {
        // Bug: Raw string containing text about logger methods incorrectly triggers classification
        var code = @"var description = """"""
Here's how logger.LogInformation(template) works:
    Properties like {UserName} get highlighted
    And {OrderId} too
Even though this is just documentation
"""""";";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Line 0 contains both "logger.LogInformation(" AND @" with no closing quote
        // SerilogCallDetector will incorrectly identify this as a Serilog call
        // because it just searches for text patterns without checking context
        
        // Now check line 1 which has {UserName}
        var line1 = snapshot.GetLineFromLineNumber(1);
        var classifications1 = classifier.GetClassificationSpans(
            new SnapshotSpan(line1.Start, line1.End));
        
        // BUG: Should be empty but isn't - properties in documentation are incorrectly classified
        Assert.Empty(classifications1); // This will FAIL while bug exists
    }

    [Fact]
    public void BUG_RawStringWithLogPattern_TriggersClassification()
    {
        // Bug: Raw string with Log.Information( pattern
        var code = @"var example = """"""
// Example: Log.Information(template) usage
    This {Property} gets highlighted incorrectly
"""""";";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Check line 1 which has {Property}
        var line1 = snapshot.GetLineFromLineNumber(1);
        var classifications = classifier.GetClassificationSpans(
            new SnapshotSpan(line1.Start, line1.End));
        
        // BUG: Should be empty - property in documentation shouldn't be classified
        Assert.Empty(classifications); // FAILS while bug exists
    }

    [Fact]
    public void BUG_DocumentationString_MentioningLogger()
    {
        // Documentation string mentioning logger triggers false positive
        var code = @"var docs = @""The logger.Warning(msg, args) method:
    Takes {parameters} in braces
    And {values} too
"";";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Check line 1 with {parameters}
        var line1 = snapshot.GetLineFromLineNumber(1);
        var classifications1 = classifier.GetClassificationSpans(
            new SnapshotSpan(line1.Start, line1.End));

        // Should be empty - properties in documentation should not be classified
        Assert.Empty(classifications1);
        
        // Check line 2 with {values}
        var line2 = snapshot.GetLineFromLineNumber(2);
        var classifications2 = classifier.GetClassificationSpans(
            new SnapshotSpan(line2.Start, line2.End));

        // Should be empty - properties in documentation should not be classified
        Assert.Empty(classifications2);
    }

    [Fact]
    public void BUG_SqlCommentWithLogKeyword()
    {
        // SQL comment with "Log.Information(" triggers detection
        var code = @"var sql = @""-- Example: Log.Information(template) usage
    SELECT * FROM Users WHERE Name = {UserName}
    AND Status = {Status}
"";";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        var line1 = snapshot.GetLineFromLineNumber(1);
        var classifications1 = classifier.GetClassificationSpans(
            new SnapshotSpan(line1.Start, line1.End));

        // Change to expect no classifications (bug is fixed)
        Assert.Empty(classifications1);

        var line2 = snapshot.GetLineFromLineNumber(2);
        var classifications2 = classifier.GetClassificationSpans(
            new SnapshotSpan(line2.Start, line2.End));

        // Change to expect no classifications (bug is fixed)
        Assert.Empty(classifications2);
    }

    [Fact]
    public void BUG_RawStringWithLoggerText()
    {
        // Raw string literal containing "logger" text triggers detection
        var code = @"var example = """"""
logger.LogDebug uses these patterns:
    {PropertyName} for values
    {@Object} for destructuring
"""""";";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Line 1 contains "logger.LogDebug" text
        // Lines 2 and 3 have properties that will be incorrectly classified
        
        var line2 = snapshot.GetLineFromLineNumber(2);
        var classifications2 = classifier.GetClassificationSpans(
            new SnapshotSpan(line2.Start, line2.End));
        
        // Bug fixed - documentation examples should NOT get syntax highlighting
        Assert.Empty(classifications2); // No classifications in documentation
        
        var line3 = snapshot.GetLineFromLineNumber(3);
        var classifications3 = classifier.GetClassificationSpans(
            new SnapshotSpan(line3.Start, line3.End));
        Assert.Empty(classifications3); // No classifications in documentation
    }

    [Fact] 
    public void Classifier_ActualSerilogCall_CorrectlyClassifies()
    {
        // This is CORRECT behavior - actual Serilog calls should be classified
        var code = @"logger.LogInformation(@""
    Processing user {UserId}
    With status {Status}
"", userId, status);";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Check line 1 with {UserId} (was line 2 before removing leading newline)
        var line1 = snapshot.GetLineFromLineNumber(1);
        var classifications1 = classifier.GetClassificationSpans(
            new SnapshotSpan(line1.Start, line1.End));
        
        // This SHOULD classify (correct behavior)
        Assert.NotEmpty(classifications1);
        var props1 = classifications1
            .Where(c => c.ClassificationType.Classification == "serilog.property.name")
            .ToList();
        Assert.Single(props1);
        
        // Check line 2 with {Status} (was line 3 before)
        var line2 = snapshot.GetLineFromLineNumber(2);
        var classifications2 = classifier.GetClassificationSpans(
            new SnapshotSpan(line2.Start, line2.End));
        Assert.NotEmpty(classifications2);
    }

    [Fact]
    public void SingleLineVerbatimString_NotInSerilogCall_DoesNotClassify()
    {
        // Single-line strings without "Log" or "logger" keywords work correctly
        var code = @"var template = @""This has {Property} but no trigger keywords"";";

        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        var line0 = snapshot.GetLineFromLineNumber(0);
        var classifications = classifier.GetClassificationSpans(
            new SnapshotSpan(line0.Start, line0.End));
        
        // Correctly does NOT classify
        Assert.Empty(classifications);
    }

    [Fact]
    public void Classifier_EmptyVerbatimString_InSerilogCall_NoClassifications()
    {
        var code = @"logger.LogInformation(@"""");";
        
        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        var line0 = snapshot.GetLineFromLineNumber(0);
        var classifications = classifier.GetClassificationSpans(
            new SnapshotSpan(line0.Start, line0.End));
        
        // Empty string has no properties
        Assert.Empty(classifications);
    }

    [Fact]
    public void Classifier_InterpolatedString_DoesNotClassify()
    {
        // Interpolated strings should not be classified
        var code = @"var x = 5; var msg = $@""Value is {x}"";";
        
        var textBuffer = MockTextBuffer.Create(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        var line0 = snapshot.GetLineFromLineNumber(0);
        var classifications = classifier.GetClassificationSpans(
            new SnapshotSpan(line0.Start, line0.End));
        
        // Correctly does not classify interpolated strings
        Assert.Empty(classifications);
    }

    [Fact]
    public void TestFile_VerbatimStringInTest_ShouldNotClassify()
    {
        // This is the actual content of a test file that exhibits the bug
        var testFileContent = File.ReadAllText(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\SerilogSyntax.Tests\Parsing\RawStringInArgumentTests.cs"));

        var textBuffer = MockTextBuffer.Create(testFileContent);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;

        // Find the specific test that has the verbatim string
        int startLine = -1;
        for (int i = 0; i < snapshot.LineCount; i++)
        {
            var line = snapshot.GetLineFromLineNumber(i);
            if (line.GetText().Contains(@"var code = @""var textBuffer = MockTextBuffer.Create"))
            {
                startLine = i;
                break;
            }
        }

        Assert.True(startLine >= 0, "Could not find the test code in the file");

        // Check the lines with properties (should be 3-5 lines after the start)
        for (int i = startLine + 3; i <= startLine + 5; i++)
        {
            var line = snapshot.GetLineFromLineNumber(i);
            var lineSpan = new SnapshotSpan(line.Start, line.End);
            var classifications = classifier.GetClassificationSpans(lineSpan);

            // These lines have {RecordId}, {Status}, {@User} and should NOT be classified
            Assert.Empty(classifications);
        }
    }
}