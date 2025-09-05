using SerilogSyntax.Utilities;
using System.Threading.Tasks;
using Xunit;

namespace SerilogSyntax.Tests.Utilities;

public class LruCacheTests
{
    [Fact]
    public void Add_EvictsOldestItemWhenCapacityExceeded()
    {
        // Arrange
        var cache = new LruCache<int, string>(3); // Capacity of 3
        
        // Act - Add items up to capacity
        cache.Add(1, "one");
        cache.Add(2, "two");
        cache.Add(3, "three");
        
        // Verify all are present
        Assert.True(cache.TryGetValue(1, out var value1));
        Assert.Equal("one", value1);
        Assert.True(cache.TryGetValue(2, out var value2));
        Assert.Equal("two", value2);
        Assert.True(cache.TryGetValue(3, out var value3));
        Assert.Equal("three", value3);
        
        // Add a fourth item - should evict the oldest (1)
        cache.Add(4, "four");
        
        // Assert
        Assert.False(cache.TryGetValue(1, out _), "Item 1 should have been evicted");
        Assert.True(cache.TryGetValue(2, out _));
        Assert.True(cache.TryGetValue(3, out _));
        Assert.True(cache.TryGetValue(4, out _));
    }
    
    [Fact]
    public void Add_DoesNotUpdateExistingItem()
    {
        // Arrange
        var cache = new LruCache<string, int>(5);
        cache.Add("key1", 100);
        
        // Act - Add same key with different value (LruCache keeps the first value)
        cache.Add("key1", 200);
        
        // Assert - The original value is retained
        Assert.True(cache.TryGetValue("key1", out var value));
        Assert.Equal(100, value); // LruCache doesn't update, it keeps the original
    }
    
    [Fact]
    public void Add_MovesAccessedItemToFront()
    {
        // Arrange
        var cache = new LruCache<int, string>(3);
        cache.Add(1, "one");
        cache.Add(2, "two");
        cache.Add(3, "three");
        
        // Act - Access item 1 to move it to front
        cache.TryGetValue(1, out _);
        
        // Add a fourth item - should evict item 2 (now the oldest)
        cache.Add(4, "four");
        
        // Assert
        Assert.True(cache.TryGetValue(1, out _), "Item 1 should still be in cache (was accessed)");
        Assert.False(cache.TryGetValue(2, out _), "Item 2 should have been evicted");
        Assert.True(cache.TryGetValue(3, out _));
        Assert.True(cache.TryGetValue(4, out _));
    }
    
    [Fact]
    public void Add_HandlesCapacityOfOne()
    {
        // Arrange
        var cache = new LruCache<string, string>(1);
        
        // Act
        cache.Add("first", "1");
        Assert.True(cache.TryGetValue("first", out var val1));
        Assert.Equal("1", val1);
        
        cache.Add("second", "2");
        
        // Assert
        Assert.False(cache.TryGetValue("first", out _), "First item should be evicted");
        Assert.True(cache.TryGetValue("second", out var val2));
        Assert.Equal("2", val2);
    }
    
    [Fact]
    public void Clear_RemovesAllItems()
    {
        // This is already tested but let's be thorough
        var cache = new LruCache<int, string>(10);
        
        // Add multiple items
        for (int i = 0; i < 5; i++)
        {
            cache.Add(i, $"value{i}");
        }
        
        // Verify items exist
        for (int i = 0; i < 5; i++)
        {
            Assert.True(cache.TryGetValue(i, out _));
        }
        
        // Act
        cache.Clear();
        
        // Assert - all items should be gone
        for (int i = 0; i < 5; i++)
        {
            Assert.False(cache.TryGetValue(i, out _));
        }
    }
    
    [Fact]
    public void TryGetValue_ReturnsFalseForNonExistentKey()
    {
        // Arrange
        var cache = new LruCache<string, int>(5);
        cache.Add("exists", 42);
        
        // Act & Assert
        Assert.False(cache.TryGetValue("does-not-exist", out var value));
        Assert.Equal(default, value);
    }
    
    [Fact]
    public async Task Add_ThreadSafety()
    {
        // Arrange
        var cache = new LruCache<int, int>(100);
        var tasks = new Task[10];
        
        // Act - Multiple threads adding items
        for (int t = 0; t < tasks.Length; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    cache.Add(threadId * 100 + i, i);
                }
            });
        }
        
        await Task.WhenAll(tasks);
        
        // Assert - Cache should still be functional
        // We can't assert exact contents due to race conditions, 
        // but cache should not throw and should have items
        int foundCount = 0;
        for (int i = 0; i < 1000; i++)
        {
            if (cache.TryGetValue(i, out _))
                foundCount++;
        }
        
        // Should have at most 100 items (cache capacity)
        Assert.True(foundCount <= 100);
        Assert.True(foundCount > 0);
    }
    
    [Fact]
    public async Task TryGetValue_ThreadSafety()
    {
        // Arrange
        var cache = new LruCache<int, string>(50);
        
        // Pre-populate cache
        for (int i = 0; i < 50; i++)
        {
            cache.Add(i, $"value-{i}");
        }
        
        var tasks = new Task[10];
        
        // Act - Multiple threads reading
        for (int t = 0; t < tasks.Length; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    cache.TryGetValue(i % 50, out _);
                }
            });
        }
        
        await Task.WhenAll(tasks);
        
        // Assert - Cache should still contain valid data
        for (int i = 0; i < 50; i++)
        {
            if (cache.TryGetValue(i, out var value))
            {
                Assert.Equal($"value-{i}", value);
            }
        }
    }
    
    [Fact]
    public void Add_WithDefaultValue()
    {
        // Arrange
        var cache = new LruCache<string, string>(5);
        
        // Act - Add null value
        cache.Add("key", null);
        
        // Assert
        Assert.True(cache.TryGetValue("key", out var value));
        Assert.Null(value);
    }
    
    [Fact]
    public void Add_LargeCapacity()
    {
        // Arrange
        var cache = new LruCache<int, int>(10000);
        
        // Act - Add many items
        for (int i = 0; i < 10000; i++)
        {
            cache.Add(i, i * 2);
        }
        
        // All should be present
        for (int i = 0; i < 10000; i++)
        {
            Assert.True(cache.TryGetValue(i, out var value));
            Assert.Equal(i * 2, value);
        }
        
        // Add one more - should evict the first
        cache.Add(10000, 20000);
        
        Assert.False(cache.TryGetValue(0, out _));
        Assert.True(cache.TryGetValue(10000, out _));
    }
}