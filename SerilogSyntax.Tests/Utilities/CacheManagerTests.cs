using Microsoft.VisualStudio.Text;
using SerilogSyntax.Parsing;
using SerilogSyntax.Tests.TestHelpers;
using SerilogSyntax.Utilities;
using System.Collections.Generic;
using Xunit;

namespace SerilogSyntax.Tests.Utilities;

public class CacheManagerTests
{
    private readonly TemplateParser _parser = new();
    
    [Fact]
    public void Clear_RemovesAllCachedData()
    {
        // Arrange
        var cacheManager = new CacheManager(_parser);
        var textBuffer = MockTextBuffer.Create("test");
        var snapshot = textBuffer.CurrentSnapshot;
        var span1 = new SnapshotSpan(snapshot, 0, 4);
        var span2 = new SnapshotSpan(snapshot, 0, 2);
        
        // Add some data to both caches
        cacheManager.CacheClassifications(span1, []);
        cacheManager.CacheClassifications(span2, []);
        var templateResult1 = cacheManager.GetCachedTemplateProperties("User {Name} logged in");
        var templateResult2 = cacheManager.GetCachedTemplateProperties("Error: {Message}");
        
        // Verify data is cached
        Assert.True(cacheManager.TryGetCachedClassifications(span1, out _));
        Assert.True(cacheManager.TryGetCachedClassifications(span2, out _));
        
        // Act
        cacheManager.Clear();
        
        // Assert - all caches should be empty
        Assert.False(cacheManager.TryGetCachedClassifications(span1, out _));
        Assert.False(cacheManager.TryGetCachedClassifications(span2, out _));
        
        // Template cache should also be cleared (getting same templates will parse again)
        var newTemplateResult1 = cacheManager.GetCachedTemplateProperties("User {Name} logged in");
        var newTemplateResult2 = cacheManager.GetCachedTemplateProperties("Error: {Message}");
        
        // The results should be equivalent but not the same instance (reparsed)
        Assert.NotSame(templateResult1, newTemplateResult1);
        Assert.NotSame(templateResult2, newTemplateResult2);
    }
    
    [Fact]
    public void InvalidateCacheForSpans_RemovesOverlappingSpans()
    {
        // Arrange
        var cacheManager = new CacheManager(_parser);
        var textBuffer = MockTextBuffer.Create("Line 1\nLine 2\nLine 3\nLine 4");
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Create multiple spans
        var span1 = new SnapshotSpan(snapshot, 0, 6);   // "Line 1"
        var span2 = new SnapshotSpan(snapshot, 7, 6);   // "Line 2"
        var span3 = new SnapshotSpan(snapshot, 14, 6);  // "Line 3"
        var span4 = new SnapshotSpan(snapshot, 21, 6);  // "Line 4"
        var overlappingSpan = new SnapshotSpan(snapshot, 5, 10); // Overlaps span1 and span2
        
        // Cache all spans
        cacheManager.CacheClassifications(span1, []);
        cacheManager.CacheClassifications(span2, []);
        cacheManager.CacheClassifications(span3, []);
        cacheManager.CacheClassifications(span4, []);
        cacheManager.CacheClassifications(overlappingSpan, []);
        
        // Verify all are cached
        Assert.True(cacheManager.TryGetCachedClassifications(span1, out _));
        Assert.True(cacheManager.TryGetCachedClassifications(span2, out _));
        Assert.True(cacheManager.TryGetCachedClassifications(span3, out _));
        Assert.True(cacheManager.TryGetCachedClassifications(span4, out _));
        Assert.True(cacheManager.TryGetCachedClassifications(overlappingSpan, out _));
        
        // Act - invalidate spans that overlap with span2
        var invalidateSpan = new SnapshotSpan(snapshot, 7, 6);
        cacheManager.InvalidateCacheForSpans([invalidateSpan]);
        
        // Assert - span2 and overlappingSpan should be removed, others should remain
        Assert.True(cacheManager.TryGetCachedClassifications(span1, out _), "span1 should still be cached");
        Assert.False(cacheManager.TryGetCachedClassifications(span2, out _), "span2 should be invalidated");
        Assert.True(cacheManager.TryGetCachedClassifications(span3, out _), "span3 should still be cached");
        Assert.True(cacheManager.TryGetCachedClassifications(span4, out _), "span4 should still be cached");
        Assert.False(cacheManager.TryGetCachedClassifications(overlappingSpan, out _), "overlappingSpan should be invalidated");
    }
    
    [Fact]
    public void InvalidateCacheForSpans_HandlesMultipleInvalidationSpans()
    {
        // Arrange
        var cacheManager = new CacheManager(_parser);
        var textBuffer = MockTextBuffer.Create("0123456789ABCDEFGHIJ");
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Create spans at different positions
        var span1 = new SnapshotSpan(snapshot, 0, 3);   // "012"
        var span2 = new SnapshotSpan(snapshot, 5, 3);   // "567"
        var span3 = new SnapshotSpan(snapshot, 10, 3);  // "ABC"
        var span4 = new SnapshotSpan(snapshot, 15, 3);  // "FGH"
        
        // Cache all spans
        cacheManager.CacheClassifications(span1, []);
        cacheManager.CacheClassifications(span2, []);
        cacheManager.CacheClassifications(span3, []);
        cacheManager.CacheClassifications(span4, []);
        
        // Act - invalidate with multiple spans that affect span1 and span3
        var invalidateSpans = new List<SnapshotSpan>
        {
            new(snapshot, 1, 2),  // Overlaps with span1
            new(snapshot, 11, 2)  // Overlaps with span3
        };
        cacheManager.InvalidateCacheForSpans(invalidateSpans);
        
        // Assert
        Assert.False(cacheManager.TryGetCachedClassifications(span1, out _), "span1 should be invalidated");
        Assert.True(cacheManager.TryGetCachedClassifications(span2, out _), "span2 should still be cached");
        Assert.False(cacheManager.TryGetCachedClassifications(span3, out _), "span3 should be invalidated");
        Assert.True(cacheManager.TryGetCachedClassifications(span4, out _), "span4 should still be cached");
    }
    
    [Fact]
    public void GetCachedTemplateProperties_HandlesInvalidTemplates()
    {
        // Arrange
        var cacheManager = new CacheManager(_parser);
        
        // Act - Parse a template with an incomplete property (missing closing brace)
        var result1 = cacheManager.GetCachedTemplateProperties("User {Name");  
        
        // Assert - Should return empty for invalid templates (no spillover highlighting!)
        Assert.NotNull(result1);
        Assert.Empty(result1); // This is the behavior we WANT - no properties for invalid syntax
        
        // Verify the result is cached (same instance returned)
        var result2 = cacheManager.GetCachedTemplateProperties("User {Name");
        Assert.Same(result1, result2);
        
        // Test with valid template - should work normally
        var validResult = cacheManager.GetCachedTemplateProperties("User {Name} logged in");
        Assert.NotNull(validResult);
        Assert.Single(validResult);
        Assert.Equal("Name", validResult[0].Name);
    }
    
    [Fact]
    public void InvalidateCacheForSpans_HandlesEmptyList()
    {
        // Arrange
        var cacheManager = new CacheManager(_parser);
        var textBuffer = MockTextBuffer.Create("test");
        var snapshot = textBuffer.CurrentSnapshot;
        var span = new SnapshotSpan(snapshot, 0, 4);
        
        // Cache a span
        cacheManager.CacheClassifications(span, []);
        Assert.True(cacheManager.TryGetCachedClassifications(span, out _));
        
        // Act - invalidate with empty list
        cacheManager.InvalidateCacheForSpans([]);
        
        // Assert - cached span should still be there
        Assert.True(cacheManager.TryGetCachedClassifications(span, out _));
    }
}