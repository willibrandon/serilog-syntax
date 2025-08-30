using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SerilogSyntax.Utilities;
using System.Collections.Generic;

namespace SerilogSyntax.Benchmarks;

/// <summary>
/// Benchmarks for SerilogCallDetector performance.
/// Validates the effectiveness of pre-check optimization and caching.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net472)]
public class CallDetectorBenchmarks
{
    private List<string> _serilogLines;
    private List<string> _nonSerilogLines;
    private List<string> _mixedLines;
    
    [GlobalSetup]
    public void Setup()
    {
        _serilogLines =
        [
            "_logger.LogInformation(\"User {UserId} logged in\", userId);",
            "Log.Information(\"Processing {Count} items\", count);",
            "_logger.LogError(ex, \"Failed to process {ItemId}\", id);",
            "logger.BeginScope(\"Operation {OperationId}\", opId);",
            ".WriteTo.Console(outputTemplate: \"[{Timestamp}] {Message}\")"
        ];
        
        _nonSerilogLines =
        [
            "Console.WriteLine(\"Hello World\");",
            "var result = ProcessData(input);",
            "if (condition) { return true; }",
            "// This is a comment about logging",
            "string message = \"User logged in\";"
        ];
        
        _mixedLines = [];
        for (int i = 0; i < 100; i++)
        {
            if (i % 3 == 0)
                _mixedLines.Add(_serilogLines[i % _serilogLines.Count]);
            else
                _mixedLines.Add(_nonSerilogLines[i % _nonSerilogLines.Count]);
        }
    }
    
    [Benchmark]
    public void DetectSerilogCalls()
    {
        foreach (var line in _serilogLines)
        {
            _ = SerilogCallDetector.IsSerilogCall(line);
        }
    }
    
    [Benchmark]
    public void DetectNonSerilogCalls()
    {
        foreach (var line in _nonSerilogLines)
        {
            _ = SerilogCallDetector.IsSerilogCall(line);
        }
    }
    
    [Benchmark]
    public void DetectMixedCalls()
    {
        foreach (var line in _mixedLines)
        {
            _ = SerilogCallDetector.IsSerilogCall(line);
        }
    }
    
    [Benchmark]
    public void DetectWithCache()
    {
        // Test cache hit performance by checking same lines multiple times
        for (int i = 0; i < 10; i++)
        {
            foreach (var line in _serilogLines)
            {
                _ = SerilogCallDetector.IsSerilogCallCached(line);
            }
        }
    }
    
    [Benchmark]
    public void FindAllSerilogCalls()
    {
        var longText = string.Join("\n", _mixedLines);
        _ = SerilogCallDetector.FindAllSerilogCalls(longText);
    }
}