using BenchmarkDotNet.Running;
using System;
using System.Linq;

namespace SerilogSyntax.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        // Run all benchmarks
        var summaries = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        
        // Print summary count for verification
        Console.WriteLine($"Completed {summaries.Count()} benchmark(s)");
    }
}