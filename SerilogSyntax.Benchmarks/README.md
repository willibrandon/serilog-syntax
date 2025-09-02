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
.\benchmark.ps1 -Filter "StructVsClass*"
```

### Direct Execution
```bash
cd SerilogSyntax.Benchmarks
dotnet run -c Release
```

**Note**: Benchmarks use `InProcessEmitToolchain` to work within the VSIX extension constraints.

## Benchmark Categories

### Parser Benchmarks (`ParserBenchmarks.cs`)
Measures the performance of the template parser with various template complexities:
- **Simple templates**: Basic property parsing (`{UserId}`)
- **Complex templates**: Properties with format specifiers and alignment (`{Date:yyyy-MM-dd}`, `{Name,10}`)
- **Multi-line templates**: Verbatim strings with multiple properties across lines
- **Error recovery**: Malformed templates with missing braces

**Latest Results** (AMD Ryzen 9 9950X, .NET Framework 4.8.1 x86):
- Simple templates: ~245ns (617B allocated)
- Complex templates: ~660ns (737B allocated)
- Multiple properties: ~816ns (1,434B allocated)
- Verbatim strings: ~442ns (649B allocated)
- Error recovery: ~266ns (609B allocated)

### Cache Benchmarks (`CacheBenchmarks.cs`)
Tests the LRU cache implementation used for template caching:
- **Cache hits**: Performance when retrieving cached templates
- **Cache misses**: Performance when adding new templates
- **Cache eviction**: Behavior when cache reaches capacity

**Latest Results** (AMD Ryzen 9 9950X, .NET Framework 4.8.1 x86):
- Cache hit: ~1.5μs (no allocations)
- Cache miss: ~1.1μs (no allocations)  
- Cache add: ~4.0μs (5,084B allocated)
- Cache eviction: ~5.4μs (4,122B allocated)
- Mixed operations: ~14.4μs (7,888B allocated)

### Call Detector Benchmarks (`CallDetectorBenchmarks.cs`)
Measures Serilog call detection performance:
- **Pre-check optimization**: Quick string contains check before regex
- **Regex matching**: Full pattern matching for Serilog calls
- **No-match scenarios**: Performance when no Serilog calls present

**Latest Results** (AMD Ryzen 9 9950X, .NET Framework 4.8.1 x86):
- Detect Serilog calls: ~8.3μs (no allocations)
- Detect non-Serilog calls: ~5.0μs (no allocations)
- Mixed detection: ~125μs (no allocations)
- With cache: ~1.9μs (no allocations)
- Find all calls: ~19.3μs (23,628B allocated)

### Struct vs Class Benchmarks (`StructVsClassBenchmarks.cs`)
Compares performance of struct vs class for `TemplateProperty`:
- **Direct creation**: Object allocation performance
- **Yield return usage**: Real-world parser usage patterns
- **Collection operations**: Adding to lists and iterating

**Latest Results** (AMD Ryzen 9 9950X, .NET Framework 4.8.1 x86):
- Parse with struct: ~1,027ns (1,546B allocated)
- Simulate parse with class: ~51ns (357B allocated) - **20x faster**
- Create many structs: ~86.5μs (158KB allocated)
- Create many classes: ~74.1μs (128KB allocated) - **14% faster, 19% less memory**
- Decision: Keep `TemplateProperty` as a class for superior performance

## Performance Optimizations Implemented

Based on benchmark results, the following optimizations were implemented:

1. **Template Caching**: LRU cache for parsed templates (demonstrates ~4x speedup with cache)
2. **Pre-check Optimization**: Quick string check before regex detection
3. **Incremental Cache Invalidation**: Only clear affected spans on buffer changes
4. **Class over Struct**: Using classes for 20x better performance in parsing scenarios
5. **Conditional Diagnostics**: Using `[Conditional("DEBUG")]` for logging to eliminate overhead in release builds

## Benchmark Environment

The benchmarks are configured to run on:
- **Runtime**: .NET Framework 4.8.1 (Visual Studio 2022's runtime)
- **Platform**: x86 (default for VS extensions)
- **Toolchain**: InProcessEmitToolchain (required for VSIX compatibility)
- **Iterations**: Default BenchmarkDotNet iterations for statistical significance

Latest benchmark system:
- **CPU**: AMD Ryzen 9 9950X (16 physical cores, 32 logical cores)
- **OS**: Windows 11 (10.0.26100)

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