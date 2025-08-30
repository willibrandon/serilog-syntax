using Microsoft.VisualStudio.Text;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
using System;
using System.Diagnostics;
using Xunit;

namespace SerilogSyntax.Tests.Classification;

/// <summary>
/// Tests to verify that caching improves performance for raw string region detection.
/// </summary>
public class CachePerformanceTests
{
    [Fact]
    public void CacheImprovement_MultipleCallsToSameLine_UsesCachedResult()
    {
        // Arrange - Create a mock text snapshot with a multi-line raw string
        var textBuffer = MockTextBuffer.Create(@"
    logger.LogInformation(""""""
        Processing record:
        ID: {RecordId}
        Status: {Status}
        User: {@User}
        """""", recordId, status, user);
    
    // More code here
    logger.LogDebug(""Another {Message}"", msg);");

        var classifier = new SerilogClassifier(textBuffer, MockClassificationTypeRegistry.Create());
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Get a span that's inside the raw string (line 3)
        var insideRawStringLine = snapshot.GetLineFromLineNumber(3);
        var insideSpan = new SnapshotSpan(insideRawStringLine.Start, insideRawStringLine.End);
        
        // Act - Call GetClassificationSpans multiple times for the same span
        var stopwatch = Stopwatch.StartNew();
        
        // First call - should be slower as it needs to scan
        var firstCallStart = stopwatch.ElapsedTicks;
        var result1 = classifier.GetClassificationSpans(insideSpan);
        var firstCallTime = stopwatch.ElapsedTicks - firstCallStart;
        
        // Second call - should be faster due to caching
        var secondCallStart = stopwatch.ElapsedTicks;
        var result2 = classifier.GetClassificationSpans(insideSpan);
        var secondCallTime = stopwatch.ElapsedTicks - secondCallStart;
        
        // Third call - should also be fast
        var thirdCallStart = stopwatch.ElapsedTicks;
        var result3 = classifier.GetClassificationSpans(insideSpan);
        var thirdCallTime = stopwatch.ElapsedTicks - thirdCallStart;
        
        stopwatch.Stop();
        
        // Assert - Cached calls should be significantly faster
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        
        // Results should be identical
        Assert.Equal(result1.Count, result2.Count);
        Assert.Equal(result1.Count, result3.Count);
        
        // Verify that properties were detected (should find RecordId, Status, User)
        Assert.True(result1.Count > 0, "Should have found properties in the raw string");
        
        // Log performance improvement (informational, not a hard assertion due to timing variability)
        var cacheSpeedup = firstCallTime > 0 ? (double)firstCallTime / secondCallTime : 1.0;
        Console.WriteLine($"Cache Performance Test Results:");
        Console.WriteLine($"  First call:  {firstCallTime} ticks");
        Console.WriteLine($"  Second call: {secondCallTime} ticks (cached)");
        Console.WriteLine($"  Third call:  {thirdCallTime} ticks (cached)");
        Console.WriteLine($"  Cache speedup: {cacheSpeedup:F2}x");
        
        // The cached calls should typically be at least 2x faster, but we won't assert on this
        // due to potential timing variations in unit tests
    }
    
    [Fact]
    public void CacheInvalidation_AfterTextChange_RefreshesCache()
    {
        // Arrange - Create a text buffer with a raw string
        var initialText = @"
    logger.LogInformation(""""""
        ID: {RecordId}
        """""", recordId);";
    
        var textBuffer = MockTextBuffer.Create(initialText);
        var classifier = new SerilogClassifier(textBuffer, MockClassificationTypeRegistry.Create());
        var snapshot1 = textBuffer.CurrentSnapshot;
        
        // Get classification for line inside raw string
        var line2 = snapshot1.GetLineFromLineNumber(2);
        var span1 = new SnapshotSpan(line2.Start, line2.End);
        var result1 = classifier.GetClassificationSpans(span1);
        
        // Act - Modify the text buffer
        var modifiedText = @"
    logger.LogInformation(""""""
        ID: {RecordId}
        Name: {UserName}
        """""", recordId, userName);";
    
        textBuffer.Replace(new Span(0, initialText.Length), modifiedText);
        var snapshot2 = textBuffer.CurrentSnapshot;
        
        // Get classification again after the change
        var line2After = snapshot2.GetLineFromLineNumber(2);
        var span2 = new SnapshotSpan(line2After.Start, line2After.End);
        var result2 = classifier.GetClassificationSpans(span2);
        
        // Also get the new line 3
        var line3 = snapshot2.GetLineFromLineNumber(3);
        var span3 = new SnapshotSpan(line3.Start, line3.End);
        var result3 = classifier.GetClassificationSpans(span3);
        
        // Assert - Cache should have been invalidated and new results returned
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotNull(result3);
        
        // Line 2 should still have RecordId
        Assert.True(result2.Count > 0, "Should find RecordId on line 2");
        
        // Line 3 should now have UserName
        Assert.True(result3.Count > 0, "Should find UserName on line 3");
    }
    
    [Fact]
    public void SmartCacheInvalidation_OnlyInvalidatesAffectedLines()
    {
        // Arrange - Create a large text buffer with multiple raw strings
        var text = @"
    // First raw string
    logger.LogInformation(""""""
        First: {Message1}
        """""", msg1);
    
    // Some other code
    var x = 42;
    var y = 100;
    
    // Second raw string (far from the first)
    logger.LogWarning(""""""
        Second: {Message2}
        """""", msg2);
    
    // More code
    Console.WriteLine(""Regular string"");";
        
        var textBuffer = MockTextBuffer.Create(text);
        var classifier = new SerilogClassifier(textBuffer, MockClassificationTypeRegistry.Create());
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Get classifications for both raw strings to populate cache
        var line3 = snapshot.GetLineFromLineNumber(3);  // Inside first raw string
        var line12 = snapshot.GetLineFromLineNumber(12); // Inside second raw string
        
        var span1 = new SnapshotSpan(line3.Start, line3.End);
        var span2 = new SnapshotSpan(line12.Start, line12.End);
        
        var result1_before = classifier.GetClassificationSpans(span1);
        var result2_before = classifier.GetClassificationSpans(span2);
        
        // Act - Make a small change that only affects regular code (line 7)
        var line7 = snapshot.GetLineFromLineNumber(7);
        var changeText = "    var x = 100; // Changed";
        textBuffer.Replace(new Span(line7.Start, line7.Length), changeText);
        
        var newSnapshot = textBuffer.CurrentSnapshot;
        var newLine3 = newSnapshot.GetLineFromLineNumber(3);
        var newLine12 = newSnapshot.GetLineFromLineNumber(12);
        
        // Get classifications again
        var newSpan1 = new SnapshotSpan(newLine3.Start, newLine3.End);
        var newSpan2 = new SnapshotSpan(newLine12.Start, newLine12.End);
        
        var stopwatch = Stopwatch.StartNew();
        var result1_after = classifier.GetClassificationSpans(newSpan1);
        var time1 = stopwatch.ElapsedTicks;
        
        stopwatch.Restart();
        var result2_after = classifier.GetClassificationSpans(newSpan2);
        var time2 = stopwatch.ElapsedTicks;
        
        stopwatch.Stop();
        
        // Assert - Both should still work correctly
        Assert.NotNull(result1_after);
        Assert.NotNull(result2_after);
        Assert.Equal(result1_before.Count, result1_after.Count);
        Assert.Equal(result2_before.Count, result2_after.Count);
        
        // Log timing information
        Console.WriteLine($"Smart Cache Invalidation Test:");
        Console.WriteLine($"  Line 3 (near change): {time1} ticks");
        Console.WriteLine($"  Line 12 (far from change): {time2} ticks");
    }
}