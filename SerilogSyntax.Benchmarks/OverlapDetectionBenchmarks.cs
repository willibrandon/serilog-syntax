using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;

namespace SerilogSyntax.Benchmarks;

/// <summary>
/// Benchmarks to compare different overlap detection algorithms.
/// Tests the performance difference between O(n²) and optimized approaches.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class OverlapDetectionBenchmarks
{
    private List<(int start, int end)> _testRanges = null!;
    private (int start, int end)[] _rangesToCheck = null!;

    [Params(10, 50, 100)]
    public int NumExistingRanges { get; set; }

    [Params(20)]
    public int NumRangesToCheck { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42); // Fixed seed for reproducible results
        
        // Generate existing ranges (sorted by start position)
        _testRanges = [];
        for (int i = 0; i < NumExistingRanges; i++)
        {
            int start = i * 10 + random.Next(0, 5); // Some spacing with randomness
            int end = start + random.Next(3, 8); // Random length
            _testRanges.Add((start, end));
        }
        
        // Generate ranges to check for overlap
        _rangesToCheck = new (int start, int end)[NumRangesToCheck];
        for (int i = 0; i < NumRangesToCheck; i++)
        {
            int start = random.Next(0, NumExistingRanges * 10);
            int end = start + random.Next(3, 8);
            _rangesToCheck[i] = (start, end);
        }
    }

    [Benchmark(Baseline = true)]
    public int OriginalO2Algorithm()
    {
        var processedRanges = new List<(int start, int end)>();
        int overlaps = 0;
        
        foreach (var (matchStart, matchEnd) in _rangesToCheck)
        {
            bool isOverlapping = false;
            
            // Original O(n²) algorithm - check against all existing ranges
            foreach (var (processedStart, processedEnd) in processedRanges)
            {
                if ((matchStart >= processedStart && matchStart < processedEnd) ||
                    (processedStart >= matchStart && processedStart < matchEnd))
                {
                    isOverlapping = true;
                    break;
                }
            }
            
            if (!isOverlapping)
            {
                processedRanges.Add((matchStart, matchEnd));
            }
            else
            {
                overlaps++;
            }
        }
        
        return overlaps;
    }

    [Benchmark]
    public int BinarySearchWithInsertAlgorithm()
    {
        var processedRanges = new List<(int start, int end)>();
        int overlaps = 0;
        
        foreach (var (matchStart, matchEnd) in _rangesToCheck)
        {
            bool isOverlapping = false;
            
            // Current algorithm - binary search + Insert (still O(n²) due to Insert)
            int insertIndex = processedRanges.BinarySearch((matchStart, matchEnd), 
                Comparer<(int start, int end)>.Create((x, y) => x.start.CompareTo(y.start)));
                
            if (insertIndex < 0)
            {
                insertIndex = ~insertIndex;
            }
            
            // Check for overlap with previous interval
            if (insertIndex > 0)
            {
                var (prevStart, prevEnd) = processedRanges[insertIndex - 1];
                if (matchStart < prevEnd)
                {
                    isOverlapping = true;
                }
            }
            
            // Check for overlap with next interval
            if (!isOverlapping && insertIndex < processedRanges.Count)
            {
                var (nextStart, nextEnd) = processedRanges[insertIndex];
                if (nextStart < matchEnd)
                {
                    isOverlapping = true;
                }
            }
            
            if (!isOverlapping)
            {
                processedRanges.Insert(insertIndex, (matchStart, matchEnd)); // O(n) operation!
            }
            else
            {
                overlaps++;
            }
        }
        
        return overlaps;
    }

    [Benchmark]
    public int SortedSetAlgorithm()
    {
        // O(log n) algorithm using SortedSet with proper binary search
        var processedRanges = new SortedSet<(int start, int end)>(
            Comparer<(int start, int end)>.Create((x, y) => x.start.CompareTo(y.start)));
        int overlaps = 0;
        
        foreach (var (matchStart, matchEnd) in _rangesToCheck)
        {
            bool isOverlapping = false;
            
            // Find the first range that starts >= matchStart (O(log n))
            var view = processedRanges.GetViewBetween((matchStart, 0), (int.MaxValue, int.MaxValue));
            if (view.Count > 0)
            {
                var (start, end) = view.Min;
                if (start < matchEnd) // Overlap with successor
                {
                    isOverlapping = true;
                }
            }
            
            // Find the last range that starts < matchStart (O(log n))
            if (!isOverlapping && processedRanges.Count > 0)
            {
                var predecessorView = processedRanges.GetViewBetween((0, 0), (matchStart - 1, int.MaxValue));
                if (predecessorView.Count > 0)
                {
                    var (start, end) = predecessorView.Max;
                    if (end > matchStart) // Overlap with predecessor
                    {
                        isOverlapping = true;
                    }
                }
            }
            
            if (!isOverlapping)
            {
                processedRanges.Add((matchStart, matchEnd)); // O(log n) operation
            }
            else
            {
                overlaps++;
            }
        }
        
        return overlaps;
    }
}