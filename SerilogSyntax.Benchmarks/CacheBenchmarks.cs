using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using SerilogSyntax.Utilities;
using System.Collections.Generic;

namespace SerilogSyntax.Benchmarks;

/// <summary>
/// Benchmarks for LRU cache performance under various scenarios.
/// Tests cache hits, misses, additions, evictions, and mixed operations.
/// </summary>
[Config(typeof(Config))]
[MemoryDiagnoser]
public class CacheBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance));
        }
    }
    
    private LruCache<string, bool> _cache;
    private List<string> _cacheKeys;
    
    [GlobalSetup]
    public void Setup()
    {
        _cache = new LruCache<string, bool>(100);
        _cacheKeys = [];
        
        // Prepare test data
        for (int i = 0; i < 200; i++)
        {
            _cacheKeys.Add($"Log.Information(\"Test {{Property{i}}}\")");
        }
        
        // Pre-populate cache with first 50 items
        for (int i = 0; i < 50; i++)
        {
            _cache.Add(_cacheKeys[i], i % 2 == 0);
        }
    }
    
    [Benchmark]
    public void CacheHitPerformance()
    {
        // Test cache hits (first 50 items are in cache)
        for (int i = 0; i < 50; i++)
        {
            _cache.TryGetValue(_cacheKeys[i], out _);
        }
    }
    
    [Benchmark]
    public void CacheMissPerformance()
    {
        // Test cache misses (items 150-199 are not in cache)
        for (int i = 150; i < 200; i++)
        {
            _cache.TryGetValue(_cacheKeys[i], out _);
        }
    }
    
    [Benchmark]
    public void CacheAddPerformance()
    {
        var localCache = new LruCache<string, bool>(100);
        
        // Add 100 items to fill the cache
        for (int i = 0; i < 100; i++)
        {
            localCache.Add(_cacheKeys[i], true);
        }
    }
    
    [Benchmark]
    public void CacheEvictionPerformance()
    {
        var localCache = new LruCache<string, bool>(50);
        
        // Add 100 items to a cache with capacity 50 (triggers eviction)
        for (int i = 0; i < 100; i++)
        {
            localCache.Add(_cacheKeys[i], true);
        }
    }
    
    [Benchmark]
    public void CacheMixedOperations()
    {
        var localCache = new LruCache<string, bool>(100);
        
        // Simulate real-world mixed operations
        for (int i = 0; i < 200; i++)
        {
            var key = _cacheKeys[i % _cacheKeys.Count];
            
            if (!localCache.TryGetValue(key, out _))
            {
                localCache.Add(key, i % 2 == 0);
            }
        }
    }
}