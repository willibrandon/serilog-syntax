using Microsoft.VisualStudio.Text;
using SerilogSyntax.Tests.TestHelpers;
using SerilogSyntax.Utilities;
using Xunit;

namespace SerilogSyntax.Tests.Utilities;

public class MultiLineStringDetectorTests
{
    [Fact]
    public void InvalidateCacheForLine_RemovesCachedResultForSpecificLine()
    {
        // Arrange
        var detector = new MultiLineStringDetector();
        var text = @"
var x = 1;
logger.LogInformation(""""""
    This is inside
    a raw string
    """""");
var y = 2;";
        
        var textBuffer = MockTextBuffer.Create(text);
        var snapshot = textBuffer.CurrentSnapshot;
        var insideRawStringLine = snapshot.GetLineFromLineNumber(3); // "This is inside"
        var insideSpan = new SnapshotSpan(insideRawStringLine.Start, insideRawStringLine.End);
        
        // First call to populate cache
        var firstResult = detector.IsInsideRawStringLiteral(insideSpan);
        Assert.True(firstResult, "Line 3 should be inside raw string");
        
        // Second call should use cache (verify by calling again - should be same result)
        var cachedResult = detector.IsInsideRawStringLiteral(insideSpan);
        Assert.True(cachedResult, "Cached result should also be true");
        
        // Act - invalidate cache for line 3
        detector.InvalidateCacheForLine(3);
        
        // Assert - calling again should recompute (result should still be true)
        var recomputedResult = detector.IsInsideRawStringLiteral(insideSpan);
        Assert.True(recomputedResult, "Recomputed result should still be true");
        
        // Other lines should not be affected - test line 4
        var line4 = snapshot.GetLineFromLineNumber(4);
        var span4 = new SnapshotSpan(line4.Start, line4.End);
        var result4 = detector.IsInsideRawStringLiteral(span4);
        Assert.True(result4, "Line 4 should still be inside raw string");
    }
    
    [Fact]
    public void ClearCache_RemovesAllCachedResults()
    {
        // Arrange
        var detector = new MultiLineStringDetector();
        var text = @"
logger.LogInformation(""""""
    Line 2 inside raw string
    Line 3 inside raw string
    """""");
logger.LogDebug(@""
    Line 6 inside verbatim
    Line 7 inside verbatim"");";
        
        var textBuffer = MockTextBuffer.Create(text);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Populate cache for multiple lines
        var line2 = snapshot.GetLineFromLineNumber(2);
        var span2 = new SnapshotSpan(line2.Start, line2.End);
        var result2 = detector.IsInsideRawStringLiteral(span2);
        Assert.True(result2, "Line 2 should be inside raw string");
        
        var line3 = snapshot.GetLineFromLineNumber(3);
        var span3 = new SnapshotSpan(line3.Start, line3.End);
        var result3 = detector.IsInsideRawStringLiteral(span3);
        Assert.True(result3, "Line 3 should be inside raw string");
        
        var line6 = snapshot.GetLineFromLineNumber(6);
        var span6 = new SnapshotSpan(line6.Start, line6.End);
        var result6 = detector.IsInsideVerbatimString(span6);
        Assert.True(result6, "Line 6 should be inside verbatim string");
        
        // Act - clear all cache
        detector.ClearCache();
        
        // Assert - results should still be correct after recomputation
        var recomputed2 = detector.IsInsideRawStringLiteral(span2);
        Assert.True(recomputed2, "Line 2 should still be inside raw string after cache clear");
        
        var recomputed3 = detector.IsInsideRawStringLiteral(span3);
        Assert.True(recomputed3, "Line 3 should still be inside raw string after cache clear");
        
        var recomputed6 = detector.IsInsideVerbatimString(span6);
        Assert.True(recomputed6, "Line 6 should still be inside verbatim string after cache clear");
    }
    
    [Fact]
    public void InvalidateCacheForLine_HandlesInvalidLineNumbers()
    {
        // Arrange
        var detector = new MultiLineStringDetector();
        
        // Act & Assert - should not throw for invalid line numbers
        detector.InvalidateCacheForLine(-1);
        detector.InvalidateCacheForLine(0);
        detector.InvalidateCacheForLine(999999);
        
        // Should still work normally after invalid calls
        var text = "logger.LogInformation(\"Test {Message}\", msg);";
        var textBuffer = MockTextBuffer.Create(text);
        var snapshot = textBuffer.CurrentSnapshot;
        var span = new SnapshotSpan(snapshot, 0, text.Length);
        
        var result = detector.IsInsideRawStringLiteral(span);
        Assert.False(result, "Should not be inside raw string");
    }
    
    [Fact]
    public void ClearCache_WorksEvenWhenCacheIsEmpty()
    {
        // Arrange
        var detector = new MultiLineStringDetector();
        
        // Act - clear empty cache (should not throw)
        detector.ClearCache();
        
        // Assert - detector should still work normally
        var text = @"logger.LogInformation(""""""
    Inside raw string
    """""");";
        
        var textBuffer = MockTextBuffer.Create(text);
        var snapshot = textBuffer.CurrentSnapshot;
        var line1 = snapshot.GetLineFromLineNumber(1);
        var span = new SnapshotSpan(line1.Start, line1.End);
        
        var result = detector.IsInsideRawStringLiteral(span);
        Assert.True(result, "Should detect raw string correctly after clearing empty cache");
    }
    
    [Fact]
    public void Cache_WorksCorrectlyAcrossMultipleClearAndInvalidate()
    {
        // Arrange
        var detector = new MultiLineStringDetector();
        var text = @"
logger.LogInformation(""""""
    Line 2: {Value}
    """""");";
        
        var textBuffer = MockTextBuffer.Create(text);
        var snapshot = textBuffer.CurrentSnapshot;
        var line2 = snapshot.GetLineFromLineNumber(2);
        var span = new SnapshotSpan(line2.Start, line2.End);
        
        // First check - populates cache
        var result1 = detector.IsInsideRawStringLiteral(span);
        Assert.True(result1);
        
        // Invalidate specific line
        detector.InvalidateCacheForLine(2);
        
        // Second check - recomputes for line 2
        var result2 = detector.IsInsideRawStringLiteral(span);
        Assert.True(result2);
        
        // Clear entire cache
        detector.ClearCache();
        
        // Third check - recomputes again
        var result3 = detector.IsInsideRawStringLiteral(span);
        Assert.True(result3);
        
        // Multiple invalidations
        detector.InvalidateCacheForLine(1);
        detector.InvalidateCacheForLine(2);
        detector.InvalidateCacheForLine(3);
        
        // Should still work correctly
        var result4 = detector.IsInsideRawStringLiteral(span);
        Assert.True(result4);
    }
}