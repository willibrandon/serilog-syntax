# Serilog Syntax Benchmarks

Performance benchmarks for the Serilog Syntax Highlighting extension using BenchmarkDotNet.

## Running Benchmarks

### Quick Run
```bash
# Run all benchmarks
.\benchmark.ps1

# Run specific benchmark category
.\benchmark.ps1 -Filter "Parser*"
.\benchmark.ps1 -Filter "Cache*"
.\benchmark.ps1 -Filter "CallDetector*"
```

### Direct Execution
```bash
cd SerilogSyntax.Benchmarks
dotnet run -c Release
```

## Benchmark Categories

### Parser Benchmarks (`ParserBenchmarks.cs`)
Measures the performance of the template parser with various template complexities:
- **Simple templates**: Basic property parsing (`{UserId}`)
- **Complex templates**: Properties with format specifiers and alignment (`{Date:yyyy-MM-dd}`, `{Name,10}`)
- **Multi-line templates**: Verbatim strings with multiple properties across lines
- **Error recovery**: Malformed templates with missing braces

**Key Results**: 
- Simple templates: ~490ns
- Complex templates: ~1.3μs
- Multi-line templates: ~1.5μs

### Cache Benchmarks (`CacheBenchmarks.cs`)
Tests the LRU cache implementation used for template caching:
- **Cache hits**: Performance when retrieving cached templates
- **Cache misses**: Performance when adding new templates
- **Cache eviction**: Behavior when cache reaches capacity

**Key Results**:
- Cache hit: ~35ns
- Cache miss (add): ~150ns
- Demonstrates 10x performance improvement for cached templates

### Call Detector Benchmarks (`CallDetectorBenchmarks.cs`)
Measures Serilog call detection performance:
- **Pre-check optimization**: Quick string contains check before regex
- **Regex matching**: Full pattern matching for Serilog calls
- **No-match scenarios**: Performance when no Serilog calls present

**Key Results**:
- Pre-check (no match): ~30ns (vs ~250ns for regex)
- With Serilog call: ~310ns (pre-check) vs ~280ns (direct regex)
- 8x faster rejection of non-Serilog code

### Struct vs Class Benchmarks (`StructVsClassBenchmarks.cs`)
Compares performance of struct vs class for `TemplateProperty`:
- **Direct creation**: Object allocation performance
- **Yield return usage**: Real-world parser usage patterns
- **Collection operations**: Adding to lists and iterating

**Key Results** (on .NET Framework 4.7.2 x86):
- Classes outperform structs by 30% for direct creation
- Classes are 4x faster with yield return patterns
- Decision: Keep `TemplateProperty` as a class

## Performance Optimizations Implemented

Based on benchmark results, the following optimizations were implemented:

1. **Template Caching**: LRU cache for parsed templates (10x improvement for repeated templates)
2. **Pre-check Optimization**: Quick string check before regex (8x faster for non-Serilog code)
3. **Incremental Cache Invalidation**: Only clear affected spans on buffer changes
4. **Class over Struct**: Using classes for better .NET Framework performance

## Benchmark Environment

The benchmarks are configured to run on:
- **Runtime**: .NET Framework 4.7.2 (matches Visual Studio's runtime)
- **Platform**: x86 (default for VS extensions)
- **Iterations**: Default BenchmarkDotNet iterations for statistical significance

## Interpreting Results

- **Mean**: Average execution time
- **Error**: Half of 99.9% confidence interval
- **StdDev**: Standard deviation of measurements
- **Allocated**: Memory allocated per operation

Lower values are better for all metrics.

## Adding New Benchmarks

To add a new benchmark:

1. Create a new class with `[MemoryDiagnoser]` attribute
2. Add `[Benchmark]` methods for scenarios to test
3. Use `[Params]` for testing different input sizes
4. Run via `benchmark.ps1` or include in the main program

Example:
```csharp
[MemoryDiagnoser]
public class MyBenchmark
{
    [Params(10, 100, 1000)]
    public int Size { get; set; }
    
    [Benchmark]
    public void MyScenario()
    {
        // Benchmark code here
    }
}
```

## Continuous Performance Monitoring

Consider running benchmarks:
- Before and after performance-related changes
- When changing core parsing or caching logic
- To validate optimization assumptions
- To establish performance baselines for new features