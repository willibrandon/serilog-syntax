using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Example;

class Program
{
    static async Task Main(string[] args)
    {
        // Setup Serilog initially with basic console logging
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Starting Serilog Syntax Example Application");

            // Create a host with dependency injection
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                    .WriteTo.File("logs/example-.log", 
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                        rollingInterval: RollingInterval.Day))
                .ConfigureServices((context, services) =>
                {
                    services.AddTransient<ExampleService>();
                })
                .Build();

            // Run the example
            var exampleService = host.Services.GetRequiredService<ExampleService>();
            await exampleService.RunExamplesAsync();

            Log.Information("Application completed successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

public class ExampleService(ILogger<ExampleService> logger)
{
    public async Task RunExamplesAsync()
    {
        await BasicLoggingExamples();
        await DestructuringExamples();
        await FormattingExamples();
        await ErrorHandlingExamples();
        await PerformanceLoggingExamples();
    }

    private async Task BasicLoggingExamples()
    {
        logger.LogInformation("=== Basic Logging Examples ===");

        // Simple property logging
        var userId = 12345;
        var userName = "JohnDoe";
        logger.LogInformation("User {UserId} with name {UserName} logged in", userId, userName);

        // Multiple properties
        var loginTime = DateTime.Now;
        var ipAddress = "192.168.1.100";
        logger.LogInformation("Login event: User {UserId} from {IpAddress} at {LoginTime}", userId, ipAddress, loginTime);

        // Different log levels
        logger.LogDebug("Debug message with {DebugValue}", "debug-info");
        logger.LogInformation("Info message with {InfoValue}", "info-data");
        logger.LogWarning("Warning message with {WarningValue}", "warning-data");
        logger.LogError("Error message with {ErrorValue}", "error-data");

        await Task.Delay(100); // Simulate some work
    }

    private async Task DestructuringExamples()
    {
        logger.LogInformation("=== Destructuring Examples ===");

        // Object destructuring with @
        var user = new
        {
            Id = 123,
            Name = "Jane Smith",
            Email = "jane@example.com",
            Roles = new[] { "Admin", "User" }
        };
        logger.LogInformation("Processing user {@User}", user);

        // Complex object destructuring
        var order = new
        {
            OrderId = "ORD-001",
            Customer = new { Name = "Alice Johnson", Id = 456 },
            Items = new[]
            {
                new { Product = "Laptop", Price = 999.99m, Quantity = 1 },
                new { Product = "Mouse", Price = 29.99m, Quantity = 2 }
            },
            Total = 1059.97m
        };
        logger.LogInformation("Order created {@Order}", order);

        // Stringification with $
        var settings = new Dictionary<string, object>
        {
            ["ConnectionString"] = "Server=localhost;Database=MyApp",
            ["Timeout"] = 30,
            ["EnableLogging"] = true
        };
        logger.LogInformation("Configuration loaded {$Settings}", settings);

        await Task.Delay(100);
    }

    private async Task FormattingExamples()
    {
        logger.LogInformation("=== Formatting Examples ===");

        // Date/time formatting
        var now = DateTime.Now;
        logger.LogInformation("Current time: {Timestamp:yyyy-MM-dd HH:mm:ss}", now);
        logger.LogInformation("ISO format: {Timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}", now);
        logger.LogInformation("Custom format: {Timestamp:MMM dd, yyyy 'at' h:mm tt}", now);

        // Numeric formatting
        var price = 1234.5678m;
        logger.LogInformation("Price formatting: {Price:C}", price);
        logger.LogInformation("Fixed decimal: {Price:F2}", price);
        logger.LogInformation("Percentage: {Rate:P2}", 0.1234);

        // Alignment formatting
        var items = new[]
        {
            new { Name = "Laptop", Price = 999.99m, Stock = 15 },
            new { Name = "Mouse", Price = 29.99m, Stock = 147 },
            new { Name = "Keyboard", Price = 79.50m, Stock = 23 }
        };

        logger.LogInformation("Inventory Report:");
        foreach (var item in items)
        {
            logger.LogInformation("Item: {Name,10} | Price: {Price,8:C} | Stock: {Stock,3}", 
                item.Name, item.Price, item.Stock);
        }

        // Combined formatting and alignment
        var duration = TimeSpan.FromMilliseconds(1234.567);
        logger.LogInformation("Operation completed in {Duration,12:hh\\:mm\\:ss\\.fff}", duration);

        await Task.Delay(100);
    }

    private async Task ErrorHandlingExamples()
    {
        logger.LogInformation("=== Error Handling Examples ===");

        try
        {
            // Simulate an operation that might fail
            await SimulateOperationAsync("important-file.txt");
        }
        catch (FileNotFoundException ex)
        {
            logger.LogError(ex, "File not found: {FileName} in directory {Directory}", 
                ex.FileName, Path.GetDirectoryName(ex.FileName));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during operation with file {FileName}", "important-file.txt");
        }

        // Positional parameters (legacy style)
        logger.LogWarning("Legacy format: Error {0} occurred in method {1} at line {2}", 
            "ValidationFailed", "ProcessData", 42);

        await Task.Delay(100);
    }

    private async Task PerformanceLoggingExamples()
    {
        logger.LogInformation("=== Performance Logging Examples ===");

        var stopwatch = Stopwatch.StartNew();

        // Simulate some work
        await Task.Delay(250);

        stopwatch.Stop();

        logger.LogInformation("Database query completed in {ElapsedMilliseconds}ms for {RecordCount} records",
            stopwatch.ElapsedMilliseconds, 1543);

        // Structured performance data
        var performance = new
        {
            Operation = "DataProcessing",
            Duration = stopwatch.Elapsed,
            RecordsProcessed = 1543,
            ThroughputPerSecond = 1543.0 / stopwatch.Elapsed.TotalSeconds
        };

        logger.LogInformation("Performance metrics {@PerformanceData}", performance);

        // Context logging
        using (logger.BeginScope("Operation={Operation} RequestId={RequestId}", "DataExport", Guid.NewGuid()))
        {
            logger.LogInformation("Starting data export for {CustomerCount} customers", 25);
            await Task.Delay(100);
            logger.LogInformation("Export completed successfully");
        }
    }

    private async Task SimulateOperationAsync(string fileName)
    {
        logger.LogDebug("Attempting to process file {FileName}", fileName);
        
        // Simulate file operation
        await Task.Delay(50);
        
        // Throw an exception to demonstrate error logging
        throw new FileNotFoundException($"Could not find file '{fileName}'", fileName);
    }
}