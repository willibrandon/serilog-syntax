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
        await ShowcaseExample();
        await BasicLoggingExamples();
        await DestructuringExamples();
        await FormattingExamples();
        await VerbatimStringExamples();
        await ErrorHandlingExamples();
        await PerformanceLoggingExamples();
    }

    private async Task ShowcaseExample()
    {
        logger.LogInformation("=== Serilog Syntax Showcase ===");

        // This section demonstrates all syntax highlighting features in one place
        var userId = 42;
        var userName = "Alice";
        var orderCount = 5;
        var totalAmount = 1234.56m;
        var timestamp = DateTime.Now;
        
        // Standard properties with multiple types
        logger.LogInformation("User {UserId} ({UserName}) placed {OrderCount} orders totaling {TotalAmount:C}", 
            userId, userName, orderCount, totalAmount);
        
        // Destructuring with @ and formatting with alignment
        var order = new { Id = "ORD-001", Items = 3, Total = 499.99m };
        logger.LogInformation("Processing order {@Order} at {Timestamp:HH:mm:ss} | Status: {Status,10}", 
            order, timestamp, "Pending");
        
        // Stringification with $ and positional parameters
        var config = new Dictionary<string, object> { ["timeout"] = 30, ["retries"] = 3 };
        logger.LogWarning("Configuration {$Config} using legacy format: {0}, {1}, {2}", 
            config, "Warning", "Code-123", 42);
        
        // Complex formatting with alignment and precision
        logger.LogInformation("Sales Report: Product {Product,-15} | Units: {Units,5} | Revenue: {Revenue,10:F2}", 
            "Premium Widget", 147, 4521.3456);
        
        // Verbatim string with properties (demonstrates @"..." string support)
        var filePath = @"C:\Users\alice\Documents";
        logger.LogInformation(@"Processing files in path: {FilePath}
Multiple lines are supported in verbatim strings
With properties like {UserId} and {@Order}
Even with ""escaped quotes"" in the template", 
            filePath, userId, order);

        await Task.Delay(100);
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

    private async Task VerbatimStringExamples()
    {
        logger.LogInformation("=== Additional Verbatim String Tests ===");

        // 1. Verbatim string with format specifiers and alignment
        logger.LogInformation(@"Performance Report:
    Time: {Timestamp:HH:mm:ss.fff}
    Count: {Count,10:N0}
    Status: {$Status}", DateTime.Now, 1234, "OK");

        // 2. Verbatim string with positional parameters
        var userId = 42;
        logger.LogInformation(@"Database query:
    SELECT * FROM Users WHERE Id = {0} AND Status = {1}
    Parameters: {0}, {1}", userId, "Active", userId, "Active");

        // 3. Mixed: Regular string followed by verbatim string
        var appName = "SerilogExample";
        var userContext = new { Name = "Admin", Role = "System" };
        logger.LogInformation("Starting process...");
        logger.LogInformation(@"Path: C:\Program Files\{AppName}\
    Config: {ConfigFile}
    User: {@UserContext}", appName, "app.config", userContext);

        // 4. Verbatim string with many escaped quotes
        var userName = "Alice";
        logger.LogInformation(@"XML: <user name=""{UserName}"" id=""{UserId}"" />",
            userName, userId);

        // 5. Very long multi-line verbatim string
        var version = "1.0.0";
        var env = "Production";
        var sessionId = Guid.NewGuid();
        logger.LogInformation(@"
===============================================
Application: {AppName}
Version: {Version}
Environment: {Environment}
===============================================
User: {UserName} (ID: {UserId})
Session: {SessionId}
Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}
===============================================
", appName, version, env, userName, userId, sessionId, DateTime.Now);

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