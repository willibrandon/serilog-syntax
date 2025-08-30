using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SerilogSyntax.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SerilogSyntax.Benchmarks;

/// <summary>
/// Benchmarks comparing struct vs class performance for TemplateProperty.
/// Results showed classes perform better on .NET Framework 4.7.2 x86, leading to reverting the struct optimization.
/// Kept for historical reference and future testing on different platforms.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net472)]
public class StructVsClassBenchmarks
{
    private TemplateParser _parser;
    private string _complexTemplate;
    
    // Simulated class version for comparison
    private class TemplatePropertyClass
    {
        public string Name { get; set; }
        public int StartIndex { get; set; }
        public int Length { get; set; }
        public PropertyType Type { get; set; }
        public string FormatSpecifier { get; set; }
        public int FormatStartIndex { get; set; }
        public int BraceStartIndex { get; set; }
        public int BraceEndIndex { get; set; }
        public int OperatorIndex { get; set; }
        public string Alignment { get; set; }
        public int AlignmentStartIndex { get; set; }
        
        public TemplatePropertyClass(string name, int startIndex, int length, PropertyType type, int braceStartIndex, int braceEndIndex)
        {
            Name = name;
            StartIndex = startIndex;
            Length = length;
            Type = type;
            BraceStartIndex = braceStartIndex;
            BraceEndIndex = braceEndIndex;
        }
        
        public TemplatePropertyClass() { }
    }
    
    [GlobalSetup]
    public void Setup()
    {
        _parser = new TemplateParser();
        _complexTemplate = "User {@User} logged in at {Timestamp:yyyy-MM-dd HH:mm:ss} with {Count,10:N0} items from {Location} using {Method}";
    }
    
    [Benchmark(Baseline = true)]
    public void ParseWithStruct()
    {
        // Current implementation using class (was readonly struct)
        _ = _parser.Parse(_complexTemplate).ToList();
    }
    
    [Benchmark]
    public void SimulateParseWithClass()
    {
        // Simulate what the old class-based approach would do
        _ = new List<TemplatePropertyClass>
        {
            // Simulate parsing (simplified)
            new() {
                Name = "User",
                Type = PropertyType.Destructured,
                StartIndex = 7,
                Length = 4,
                BraceStartIndex = 5,
                BraceEndIndex = 12,
                OperatorIndex = 6
            },
            new() {
                Name = "Timestamp",
                Type = PropertyType.Standard,
                StartIndex = 28,
                Length = 9,
                FormatSpecifier = "yyyy-MM-dd HH:mm:ss",
                FormatStartIndex = 38,
                BraceStartIndex = 26,
                BraceEndIndex = 58
            },
            new() {
                Name = "Count",
                Type = PropertyType.Standard,
                StartIndex = 66,
                Length = 5,
                Alignment = "10",
                AlignmentStartIndex = 72,
                FormatSpecifier = "N0",
                FormatStartIndex = 76,
                BraceStartIndex = 65,
                BraceEndIndex = 78
            },
            new() {
                Name = "Location",
                Type = PropertyType.Standard,
                StartIndex = 92,
                Length = 8,
                BraceStartIndex = 91,
                BraceEndIndex = 100
            },
            new() {
                Name = "Method",
                Type = PropertyType.Standard,
                StartIndex = 109,
                Length = 6,
                BraceStartIndex = 108,
                BraceEndIndex = 115
            }
        };
    }
    
    [Benchmark]
    public void EnumerateStructs()
    {
        // Test actual usage pattern with yield return
        var count = 0;
        foreach (var prop in GenerateStructProperties())
        {
            count++;
        }
    }
    
    [Benchmark]
    public void EnumerateClasses()
    {
        // Test actual usage pattern with yield return
        var count = 0;
        foreach (var prop in GenerateClassProperties())
        {
            count++;
        }
    }
    
    [Benchmark]
    public void DirectStructCreation()
    {
        var name = "Property";
        for (int i = 0; i < 1000; i++)
        {
            var prop = new TemplateProperty(
                name,
                i * 10,
                8,
                PropertyType.Standard,
                i * 10 - 1,
                i * 10 + 9);
            // Force use so it doesn't get optimized away
            if (prop.StartIndex < 0) throw new Exception();
        }
    }
    
    [Benchmark]
    public void DirectClassCreation()
    {
        var name = "Property";
        for (int i = 0; i < 1000; i++)
        {
            var prop = new TemplatePropertyClass(
                name,
                i * 10,
                8,
                PropertyType.Standard,
                i * 10 - 1,
                i * 10 + 9);
            // Force use so it doesn't get optimized away
            if (prop.StartIndex < 0) throw new Exception();
        }
    }
    
    private IEnumerable<TemplateProperty> GenerateStructProperties()
    {
        // Pre-create names to isolate struct allocation cost
        var name = "Property";
        for (int i = 0; i < 1000; i++)
        {
            yield return new TemplateProperty(
                name,
                i * 10,
                8,
                PropertyType.Standard,
                i * 10 - 1,
                i * 10 + 9);
        }
    }
    
    private IEnumerable<TemplatePropertyClass> GenerateClassProperties()
    {
        // Pre-create names to isolate class allocation cost
        var name = "Property";
        for (int i = 0; i < 1000; i++)
        {
            yield return new TemplatePropertyClass(
                name,
                i * 10,
                8,
                PropertyType.Standard,
                i * 10 - 1,
                i * 10 + 9);
        }
    }
}