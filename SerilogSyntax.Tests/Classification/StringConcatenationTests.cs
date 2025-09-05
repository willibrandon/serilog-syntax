using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace SerilogSyntax.Tests.Classification;

/// <summary>
/// Tests for string concatenation scenarios where Serilog templates span multiple lines.
/// </summary>
public class StringConcatenationTests
{
    private readonly IClassificationTypeRegistryService _classificationRegistry = MockClassificationTypeRegistry.Create();
    private readonly ITestOutputHelper _output;
    
    public StringConcatenationTests(ITestOutputHelper output)
    {
        _output = output;
    }
    
    [Fact]
    public void BUG_ExactScenarioFromScreenshot_SecondLineShouldHighlight()
    {
        // This is the EXACT code from the screenshot
        var code = @"logger.LogError(""Error processing {Operation}"" +
    ""for user {UserId} "" +
    ""at time {Timestamp} "",
    ""DataSync"", 42, DateTime.Now);";
        
        var textBuffer = new MockTextBuffer(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Test the SECOND LINE ONLY (as VS might send it)
        var lines = snapshot.Lines.ToArray();
        var secondLine = lines[1]; // "for user {UserId} " +
        var secondLineSpan = new SnapshotSpan(snapshot, secondLine.Start, secondLine.Length);
        var secondLineClassifications = classifier.GetClassificationSpans(secondLineSpan).ToList();
        
        _output.WriteLine($"Second line text: '{secondLine.GetText()}'");
        _output.WriteLine($"Second line classifications: {secondLineClassifications.Count}");
        foreach (var c in secondLineClassifications)
        {
            _output.WriteLine($"  {c.ClassificationType.Classification}: '{c.Span.GetText()}'");
        }
        
        // The bug: UserId should be classified but it's not when the line is processed alone
        var hasUserId = secondLineClassifications.Any(c => c.Span.GetText() == "UserId");
        Assert.True(hasUserId, "Property 'UserId' in second line should be highlighted when processed alone");
    }
    
    [Fact]
    public void StringConcatenation_WhenProcessedAsWhole_ShouldHighlightAllProperties()
    {
        // Same code but process the whole thing at once
        var code = @"logger.LogError(""Error processing {Operation}"" +
    ""for user {UserId} "" +
    ""at time {Timestamp} "",
    ""DataSync"", 42, DateTime.Now);";
        
        var textBuffer = new MockTextBuffer(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Process the entire code at once
        var fullSpan = new SnapshotSpan(snapshot, 0, code.Length);
        var allClassifications = classifier.GetClassificationSpans(fullSpan).ToList();
        
        _output.WriteLine($"Full code classifications: {allClassifications.Count}");
        foreach (var c in allClassifications)
        {
            _output.WriteLine($"  {c.ClassificationType.Classification}: '{c.Span.GetText()}'");
        }
        
        // All three properties should be highlighted
        Assert.Contains(allClassifications, c => c.Span.GetText() == "Operation");
        Assert.Contains(allClassifications, c => c.Span.GetText() == "UserId");
        Assert.Contains(allClassifications, c => c.Span.GetText() == "Timestamp");
    }
    
    [Fact]
    public void StringConcatenation_ProcessEachLineSeparately_ShowsTheBug()
    {
        var code = @"logger.LogError(""Error processing {Operation}"" +
    ""for user {UserId} "" +
    ""at time {Timestamp} "",
    ""DataSync"", 42, DateTime.Now);";
        
        var textBuffer = new MockTextBuffer(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Process each line separately (simulating what VS does)
        var lines = snapshot.Lines.ToArray();
        
        // Line 0: logger.LogError("Error processing {Operation}" +
        var line0Span = new SnapshotSpan(snapshot, lines[0].Start, lines[0].Length);
        var line0Classifications = classifier.GetClassificationSpans(line0Span).ToList();
        _output.WriteLine($"Line 0: '{lines[0].GetText()}'");
        _output.WriteLine($"  Classifications: {line0Classifications.Count}");
        var hasOperation = line0Classifications.Any(c => c.Span.GetText() == "Operation");
        Assert.True(hasOperation, "Line 0 should have 'Operation' classified");
        
        // Line 1: "for user {UserId} " +
        var line1Span = new SnapshotSpan(snapshot, lines[1].Start, lines[1].Length);
        var line1Classifications = classifier.GetClassificationSpans(line1Span).ToList();
        _output.WriteLine($"Line 1: '{lines[1].GetText()}'");
        _output.WriteLine($"  Classifications: {line1Classifications.Count}");
        var hasUserId = line1Classifications.Any(c => c.Span.GetText() == "UserId");
        // THIS IS THE BUG - this will fail!
        Assert.True(hasUserId, "Line 1 should have 'UserId' classified when processed alone");
        
        // Line 2: "at time {Timestamp} ",
        var line2Span = new SnapshotSpan(snapshot, lines[2].Start, lines[2].Length);
        var line2Classifications = classifier.GetClassificationSpans(line2Span).ToList();
        _output.WriteLine($"Line 2: '{lines[2].GetText()}'");
        _output.WriteLine($"  Classifications: {line2Classifications.Count}");
        var hasTimestamp = line2Classifications.Any(c => c.Span.GetText() == "Timestamp");
        // THIS WILL ALSO FAIL!
        Assert.True(hasTimestamp, "Line 2 should have 'Timestamp' classified when processed alone");
    }
    
    [Fact]
    public void NonSerilogStringWithBraces_ShouldNotBeHighlighted()
    {
        // Regular strings that happen to contain braces but are not Serilog calls
        // should NOT be highlighted
        
        // Test case 1: JSON string
        var code1 = @"var json = ""{ \""name\"": \""{Name}\"", \""id\"": {Id} }"";";
        var textBuffer1 = new MockTextBuffer(code1);
        var classifier1 = new SerilogClassifier(textBuffer1, _classificationRegistry);
        var classifications1 = classifier1.GetClassificationSpans(new SnapshotSpan(textBuffer1.CurrentSnapshot, 0, code1.Length)).ToList();
        
        _output.WriteLine($"JSON string: {classifications1.Count} classifications");
        Assert.Empty(classifications1); // Should NOT classify JSON strings
        
        // Test case 2: Format string that's not in a Serilog call
        var code2 = @"var format = ""User {0} has {1} items"";";
        var textBuffer2 = new MockTextBuffer(code2);
        var classifier2 = new SerilogClassifier(textBuffer2, _classificationRegistry);
        var classifications2 = classifier2.GetClassificationSpans(new SnapshotSpan(textBuffer2.CurrentSnapshot, 0, code2.Length)).ToList();
        
        _output.WriteLine($"Format string: {classifications2.Count} classifications");
        Assert.Empty(classifications2); // Should NOT classify regular format strings
        
        // Test case 3: Documentation/comments with placeholders
        var code3 = @"// This method logs {UserId} and {Timestamp}
var doc = ""Use {PropertyName} for the property"";";
        var textBuffer3 = new MockTextBuffer(code3);
        var classifier3 = new SerilogClassifier(textBuffer3, _classificationRegistry);
        var classifications3 = classifier3.GetClassificationSpans(new SnapshotSpan(textBuffer3.CurrentSnapshot, 0, code3.Length)).ToList();
        
        _output.WriteLine($"Documentation: {classifications3.Count} classifications");
        Assert.Empty(classifications3); // Should NOT classify documentation
    }
    
    [Fact]
    public void ConcatenatedNonSerilogStrings_ShouldNotBeHighlighted()
    {
        // Concatenated strings that are NOT part of Serilog calls should NOT be highlighted
        var code = @"var message = ""Error in {Module}"" +
    "" at line {Line}"" +
    "" with code {ErrorCode}"";";
        
        var textBuffer = new MockTextBuffer(code);
        var classifier = new SerilogClassifier(textBuffer, _classificationRegistry);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Test each line separately
        var lines = snapshot.Lines.ToArray();
        
        foreach (var line in lines)
        {
            var lineSpan = new SnapshotSpan(snapshot, line.Start, line.Length);
            var classifications = classifier.GetClassificationSpans(lineSpan).ToList();
            
            _output.WriteLine($"Line: '{line.GetText()}'");
            _output.WriteLine($"  Classifications: {classifications.Count}");
            
            // None of these lines should be classified because they're not Serilog calls
            Assert.Empty(classifications);
        }
    }
}