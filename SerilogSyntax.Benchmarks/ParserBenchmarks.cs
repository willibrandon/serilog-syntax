using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using SerilogSyntax.Parsing;
using System.Collections.Generic;
using System.Linq;

namespace SerilogSyntax.Benchmarks;

/// <summary>
/// Benchmarks for TemplateParser performance across various template complexities.
/// Tests simple, complex, malformed, and verbatim string templates.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net472)]
public class ParserBenchmarks
{
    private TemplateParser _parser;
    private List<string> _templates;
    
    [GlobalSetup]
    public void Setup()
    {
        _parser = new TemplateParser();
        _templates =
        [
            "Simple template with {Property}",
            "Complex {@User} with {Count:N0} and {Timestamp:yyyy-MM-dd HH:mm:ss}",
            "Multiple {Prop1} {Prop2} {Prop3} {Prop4} {Prop5}",
            "Nested {Outer,10:F2} and {@Inner} with {$String}",
            "Positional {0} {1} {2} mixed with {Named}",
            @"Verbatim string with {Property1} and {Property2}",
            "Malformed {Unclosed and {Valid} property",
            "Empty {} and {Valid} with {@Destructured}"
        ];
    }
    
    [Benchmark]
    public void ParseSimpleTemplate()
    {
        _ = _parser.Parse(_templates[0]).ToList();
    }
    
    [Benchmark]
    public void ParseComplexTemplate()
    {
        _ = _parser.Parse(_templates[1]).ToList();
    }
    
    [Benchmark]
    public void ParseMultipleProperties()
    {
        _ = _parser.Parse(_templates[2]).ToList();
    }
    
    [Benchmark]
    public void ParseAllTemplates()
    {
        foreach (var template in _templates)
        {
            _ = _parser.Parse(template).ToList();
        }
    }
    
    [Benchmark]
    public void ParseWithErrorRecovery()
    {
        _ = _parser.Parse(_templates[6]).ToList(); // Malformed template
    }
    
    [Benchmark]
    public void ParseVerbatimString()
    {
        _ = _parser.Parse(_templates[5]).ToList();
    }
}