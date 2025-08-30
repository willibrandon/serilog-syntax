using System.Collections.Generic;

namespace SerilogSyntax.Utilities;

/// <summary>
/// Thread-safe LRU (Least Recently Used) cache implementation for performance optimization.
/// </summary>
/// <typeparam name="TKey">The type of cache keys.</typeparam>
/// <typeparam name="TValue">The type of cached values.</typeparam>
internal class LruCache<TKey, TValue>(int capacity)
{
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _cache = new(capacity);
    private readonly LinkedList<(TKey Key, TValue Value)> _lru = new();
    private readonly object _lock = new();

    /// <summary>
    /// Attempts to retrieve a value from the cache.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <param name="value">The cached value if found.</param>
    /// <returns>True if the key was found; otherwise, false.</returns>
    public bool TryGetValue(TKey key, out TValue value)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var node))
            {
                value = node.Value.Value;
                _lru.Remove(node);
                _lru.AddLast(node);
                return true;
            }
            
            value = default;
            return false;
        }
    }
    
    /// <summary>
    /// Adds or updates a value in the cache.
    /// </summary>
    /// <param name="key">The key to add.</param>
    /// <param name="value">The value to cache.</param>
    public void Add(TKey key, TValue value)
    {
        lock (_lock)
        {
            if (_cache.ContainsKey(key))
                return;
                
            if (_cache.Count >= capacity)
            {
                var first = _lru.First;
                _lru.RemoveFirst();
                _cache.Remove(first.Value.Key);
            }
            
            var newNode = _lru.AddLast((key, value));
            _cache[key] = newNode;
        }
    }
    
    /// <summary>
    /// Clears all entries from the cache.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _lru.Clear();
        }
    }
}