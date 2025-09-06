namespace Example;

/// <summary>
/// Service that demonstrates various Serilog logging features and syntax highlighting capabilities.
/// </summary>
/// <remarks>
/// This service showcases all the different ways to use Serilog logging with properties, 
/// destructuring, formatting, and expressions. It serves as a comprehensive example
/// for testing the SerilogSyntax Visual Studio extension.
/// </remarks>
public class ExampleService(ILogger<ExampleService> logger)
{
    /// <summary>
    /// Runs all example logging scenarios to demonstrate the full range of Serilog features.
    /// </summary>
    /// <returns>A task that completes when all examples have been executed.</returns>
    public async Task RunExamplesAsync()
    {
        SelfLog.Enable(Console.Error);

        await ShowcaseExample();
        await BasicLoggingExamples();
        await DestructuringExamples();
        await FormattingExamples();
        await VerbatimStringExamples();
        await RawStringLiteralExamples();
        await SerilogExpressionsExamples();
        await ErrorHandlingExamples();
        await PerformanceLoggingExamples();
        await TextFormattingExamples();
    }

    /// <summary>
    /// Demonstrates all syntax highlighting features in a single comprehensive example.
    /// </summary>
    /// <remarks>
    /// This method showcases standard properties, destructuring, stringification, formatting,
    /// alignment, verbatim strings, raw string literals, and Serilog.Expressions syntax.
    /// </remarks>
    /// <returns>A task that completes when the showcase example has been logged.</returns>
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
        var appVersion = new Version(1, 2, 3);
        logger.LogWarning("Application version {$AppVersion} using legacy format: {0}, {1}, {2}",
            appVersion, "Warning", "Code-123", 42);

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

        // Raw string literal with properties (C# 11 """...""" support)
        var recordId = "REC-2024";
        var status = "Processing";
        logger.LogInformation("""
            Raw String Report:
            Record: {RecordId} | Status: {Status,-12}
            User: {UserName} (ID: {UserId})
            Order: {@Order}
            Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}
            """, recordId, status, userName, userId, order, timestamp);

        // Serilog.Expressions syntax
        using var expressionLogger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Filter.ByExcluding("RequestPath like '/health%' and StatusCode < 400")
            .Enrich.WithComputed("IsError", "Level = 'Error' or Level = 'Fatal'")
            .Enrich.WithProperty("RequestPath", "/api/orders")
            .Enrich.WithProperty("StatusCode", 200)
            .WriteTo.Console(new ExpressionTemplate(
                "[{@t:HH:mm:ss} {@l:u3}] {#if IsError}❌{#else}✅{#end} {@m}\n{#if @x is not null}{@x}\n{#end}"))
            .CreateLogger();

        expressionLogger.Information("Order {OrderId} processed successfully for customer {@Customer} in {Duration}ms",
            "ORD-2024-0042", new { Name = "Bob Smith", Tier = "Premium" }, 127);

        await Task.Delay(100);
    }

    /// <summary>
    /// Demonstrates basic Serilog logging patterns with simple property substitution.
    /// </summary>
    /// <remarks>
    /// Shows how to log with single and multiple properties at different log levels
    /// including Debug, Information, Warning, and Error.
    /// </remarks>
    /// <returns>A task that completes when the basic examples have been logged.</returns>
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

    /// <summary>
    /// Demonstrates object destructuring and stringification in Serilog templates.
    /// </summary>
    /// <remarks>
    /// Shows the difference between destructuring with @ (captures object structure)
    /// and stringification with $ (captures ToString() representation).
    /// </remarks>
    /// <returns>A task that completes when the destructuring examples have been logged.</returns>
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
        var runtimeVersion = new Version(2, 0, 1);
        logger.LogInformation("Runtime version {$RuntimeVersion}", runtimeVersion);

        await Task.Delay(100);
    }

    /// <summary>
    /// Demonstrates format specifiers and alignment options in Serilog templates.
    /// </summary>
    /// <remarks>
    /// Shows various formatting options for dates, numbers, currency, percentages,
    /// and how to use alignment to create tabular output.
    /// </remarks>
    /// <returns>A task that completes when the formatting examples have been logged.</returns>
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

    /// <summary>
    /// Demonstrates Serilog properties within C# verbatim string literals (@"...").
    /// </summary>
    /// <remarks>
    /// Tests various edge cases including multi-line verbatim strings, escaped quotes,
    /// positional parameters, and complex property combinations within verbatim strings.
    /// </remarks>
    /// <returns>A task that completes when the verbatim string examples have been logged.</returns>
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

    /// <summary>
    /// Demonstrates Serilog properties within C# 11 raw string literals ("""...""").
    /// </summary>
    /// <remarks>
    /// Shows how the extension handles properties in single-line and multi-line raw strings,
    /// including custom delimiter counts and embedded quotes without escaping.
    /// </remarks>
    /// <returns>A task that completes when the raw string literal examples have been logged.</returns>
    private async Task RawStringLiteralExamples()
    {
        logger.LogInformation("=== Raw String Literal Tests (C# 11+) ===");

        // 1. Single-line raw string literal
        logger.LogInformation("""User {UserId} logged in at {Timestamp:HH:mm:ss}""", 42, DateTime.Now);

        // 2. Multi-line raw string literal
        var recordId = "REC-001";
        var status = "Active";
        logger.LogInformation("""
            Processing record:
            ID: {RecordId}
            Status: {Status}
            Timestamp: {Timestamp:yyyy-MM-dd}
            """, recordId, status, DateTime.Now);

        // 3. Raw string with embedded quotes (no escaping needed)
        var configValue = "production";
        logger.LogInformation("""Configuration value "AppMode" is set to "{Value}" """, configValue);

        // 4. Raw string with custom delimiter (4+ quotes)
        var data = "test-data";
        logger.LogInformation(""""
            Template with """ inside: {Data}
            This allows literal triple quotes in the string
            """", data);

        // 5. Complex multi-line raw string with various property types
        var appName = "ExampleApp";
        var version = "2.0.0";
        var environment = "Production";
        var userName = "Admin";
        var userId = 1;
        var sessionId = Guid.NewGuid();

        logger.LogInformation("""
            ===============================================
            Application: {AppName}
            Version: {Version}
            Environment: {Environment}
            ===============================================
            User: {UserName} (ID: {UserId})
            Session: {SessionId}
            Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}
            ===============================================
            """, appName, version, environment, userName, userId, sessionId, DateTime.Now);

        // 6. Raw string with positional parameters and formatting
        logger.LogInformation("""
            Database Query Results:
            Query: SELECT * FROM Users WHERE Id = {0}
            Rows affected: {1,5}
            Execution time: {2:F2}ms
            """, userId, 42, 123.456);

        // 7. Raw string with destructuring and stringification
        var config = new { Mode = "Debug", Timeout = 30 };
        var frameworkVersion = new Version(3, 1, 4);

        logger.LogInformation("""
            System information:
            Config: {@Config}
            Framework Version: {$FrameworkVersion}
            """, config, frameworkVersion);

        await Task.Delay(100);
    }

    /// <summary>
    /// Demonstrates Serilog.Expressions syntax including filters, conditionals, and expression templates.
    /// </summary>
    /// <remarks>
    /// Shows filter expressions with ByExcluding/ByIncludingOnly, conditional enrichment,
    /// computed properties, conditional writes, and expression template control flow directives.
    /// </remarks>
    /// <returns>A task that completes when the expression examples have been logged.</returns>
    private async Task SerilogExpressionsExamples()
    {
        logger.LogInformation("=== Serilog.Expressions Syntax Examples ===");

        // These demonstrate actual Serilog.Expressions API calls with expression syntax
        var expressionConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()

            // Filter expressions
            .Filter.ByExcluding("RequestPath like '/health%' and StatusCode < 400")
            .Filter.ByExcluding("SourceContext = 'Microsoft.AspNetCore.Hosting.Diagnostics' and Level < 'Warning'")
            .Filter.ByIncludingOnly("Level >= 'Information' or SourceContext = 'Example.ExampleService'")
            .Filter.ByExcluding("Message not like '%debug%' ci")
            .Filter.ByIncludingOnly("User.Role in ['Admin', 'Moderator'] or Level = 'Error'")
            .Filter.ByExcluding("Exception is not null and Contains(Exception.Type, 'OperationCanceled')")

            // Conditional enrichment
            .Enrich.When("Level >= 'Warning'", e => e.WithProperty("Alert", true))
            .Enrich.When("Contains(RequestPath, '/api')", e => e.WithProperty("IsApi", true))
            .Enrich.When("User.IsAuthenticated and User.Role = 'Admin'", e => e.WithProperty("Privileged", true))

            // Computed properties
            .Enrich.WithComputed("ShortContext", "Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)")
            .Enrich.WithComputed("IsError", "Level = 'Error' or Level = 'Fatal'")
            .Enrich.WithComputed("RequestType", "if StartsWith(RequestPath, '/api') then 'API' else 'Web'")
            .Enrich.WithComputed("Duration", "Round(Elapsed.TotalMilliseconds, 2)")
            .Enrich.WithComputed("UserDisplayName", "Coalesce(User.FullName, User.Email, 'Anonymous')")

            // Conditional writes
            .WriteTo.Conditional("Environment = 'Production' and Level >= 'Warning'",
                wt => wt.File("logs/prod-warnings.log"))
            .WriteTo.Conditional("Contains(SourceContext, 'Security') or Contains(Message, 'Authentication')",
                wt => wt.File("logs/security.log"))
            .WriteTo.Conditional("RequestPath like '/api%' and StatusCode >= 400",
                wt => wt.File("logs/api-errors.log"));

        logger.LogInformation("Serilog.Expressions configuration example created");

        // Expression template examples
        var templateConfig = new LoggerConfiguration()
            .WriteTo.Console(new ExpressionTemplate(
                "[{@t:HH:mm:ss} {@l:u3}] {#if SourceContext is not null}[{Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}]{#end} {@m}\n{@x}"))
            .WriteTo.File(new ExpressionTemplate(
                "{#if IsError}[ERROR]{#else if Level = 'Warning'}[WARN]{#else}[INFO]{#end} " +
                "[{@t:yyyy-MM-dd HH:mm:ss.fff}] " +
                "{#if @p['RequestId'] is not null}[{@p['RequestId']}] {#end}" +
                "{@m}" +
                "{#each name, value in @p} | {name}={value}{#end}" +
                "{#if @x is not null}\n{@x}{#end}\n"),
                path: "logs/app.log");

        logger.LogInformation("Expression template configuration created");

        await Task.Delay(100);
    }

    /// <summary>
    /// Demonstrates logging exceptions and error scenarios with structured data.
    /// </summary>
    /// <remarks>
    /// Shows how to log exceptions with LogError, including exception properties
    /// and legacy positional parameter formats.
    /// </remarks>
    /// <returns>A task that completes when the error handling examples have been logged.</returns>
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

    /// <summary>
    /// Demonstrates performance metrics logging with timing and throughput data.
    /// </summary>
    /// <remarks>
    /// Shows how to log structured performance data, use logging scopes for context,
    /// and track operation durations with detailed metrics.
    /// </remarks>
    /// <returns>A task that completes when the performance examples have been logged.</returns>
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

    private async Task TextFormattingExamples()
    {
        using var log = new LoggerConfiguration()
            .Enrich.WithProperty("Application", "Sample")
            .WriteTo.Console(new ExpressionTemplate(
                "[{@t:HH:mm:ss} {@l:u3}" +
                "{#if SourceContext is not null} ({Substring(SourceContext, LastIndexOf(SourceContext, '.') + 1)}){#end}] " +
                "{@m} (first item is {coalesce(Items[0], '<empty>')}) {rest()}\n{@x}",
                theme: TemplateTheme.Code))
            .CreateLogger();

        log.Information("Running {Example}", nameof(TextFormattingExamples));

        log.ForContext<Program>()
            .Information("Cart contains {@Items}", ["Tea", "Coffee"]);

        log.ForContext<Program>()
            .Information("Cart contains {@Items}", ["Apricots"]);

        await Task.Delay(100);
    }

    /// <summary>
    /// Simulates a file operation that throws an exception for error logging demonstration.
    /// </summary>
    /// <param name="fileName">The name of the file to simulate processing.</param>
    /// <returns>A task that fails with a FileNotFoundException.</returns>
    /// <exception cref="FileNotFoundException">Always thrown to demonstrate error logging.</exception>
    private async Task SimulateOperationAsync(string fileName)
    {
        logger.LogDebug("Attempting to process file {FileName}", fileName);

        // Simulate file operation
        await Task.Delay(50);

        // Throw an exception to demonstrate error logging
        throw new FileNotFoundException($"Could not find file '{fileName}'", fileName);
    }
}
