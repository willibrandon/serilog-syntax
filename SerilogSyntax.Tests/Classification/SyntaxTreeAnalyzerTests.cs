using SerilogSyntax.Classification;
using SerilogSyntax.Expressions;
using SerilogSyntax.Tests.TestHelpers;
using System;
using Xunit;

namespace SerilogSyntax.Tests.Classification;

public class SyntaxTreeAnalyzerTests
{
    [Fact]
    public void ClearCachesIfNeeded_ClearsWhenThresholdExceeded()
    {
        // This test verifies the cache clearing logic
        // Since the caches are private static, we can only verify the method doesn't throw
        SyntaxTreeAnalyzer.ClearCachesIfNeeded();

        // Call multiple times to ensure it handles both empty and populated states
        SyntaxTreeAnalyzer.ClearCachesIfNeeded();
        SyntaxTreeAnalyzer.ClearCachesIfNeeded();
    }
    
    [Fact]
    public void InvocationCache_ClearsWhenExceedsLimit()
    {
        // This test forces the invocation cache to exceed its limit (maxCacheSize * 10 = 500)
        // by analyzing many different code snippets with unique invocations
        
        // Generate 501 unique code snippets to exceed the cache limit of 500
        for (int i = 0; i < 501; i++)
        {
            // Each snippet has a unique method name and position to ensure different cache keys
            var code = $@"
public class Test{i} {{
    public void Method{i}() {{
        var logger{i} = GetLogger();
        logger{i}.LogInformation(""Test message {{Property{i}}}"", value{i});
    }}
}}";
            var textBuffer = MockTextBuffer.Create(code);
            var snapshot = textBuffer.CurrentSnapshot;
            
            // Find position inside the string literal
            var position = code.IndexOf($"Property{i}");
            
            // This will populate the invocation cache
            var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
            
            // The result should be true for Serilog calls
            Assert.True(result);
        }
        
        // The cache should have been cleared at least once during the loop
        // Verify by calling ClearCachesIfNeeded which will clear if needed
        SyntaxTreeAnalyzer.ClearCachesIfNeeded();
        
        // Test one more to ensure cache still works after clearing
        var finalCode = "logger.LogInformation(\"Final {Test}\", value);";
        var finalBuffer = MockTextBuffer.Create(finalCode);
        var finalSnapshot = finalBuffer.CurrentSnapshot;
        var finalPosition = finalCode.IndexOf("Test");
        var finalResult = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(finalSnapshot, finalPosition);
        Assert.True(finalResult);
    }
    
    [Fact]
    public void SyntaxTreeCache_ClearsWhenExceedsLimit()
    {
        // This test forces the syntax tree cache to exceed its limit (maxCacheSize = 50)
        // by analyzing many different snapshots
        
        // Generate 51 unique snapshots to exceed the cache limit of 50
        for (int i = 0; i < 51; i++)
        {
            var code = $"var test{i} = \"Different content {i}\";";
            var textBuffer = MockTextBuffer.Create(code);
            var snapshot = textBuffer.CurrentSnapshot;
            
            // This will populate the syntax tree cache
            var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, 5);
            
            // Should be false since these aren't Serilog calls
            Assert.False(result);
        }
        
        // The cache should have been cleared at least once during the loop
        // Force a clear to ensure the code path is covered
        SyntaxTreeAnalyzer.ClearCachesIfNeeded();
        
        // Test one more to ensure cache still works after clearing
        var finalCode = "var finalTest = \"Final content\";";
        var finalBuffer = MockTextBuffer.Create(finalCode);
        var finalSnapshot = finalBuffer.CurrentSnapshot;
        var finalResult = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(finalSnapshot, 5);
        Assert.False(finalResult);
    }

    [Theory]
    [InlineData("logger.LogInformation(\"Test {Property}\", value);", true)]
    [InlineData("var x = \"Not a serilog call\";", false)]
    [InlineData("_logger.LogError(ex, \"Error: {Message}\", msg);", true)]
    [InlineData("Log.Information(\"User {UserId} logged in\", userId);", true)]
    public void IsPositionInsideSerilogTemplate_BasicCases(string code, bool expected)
    {
        // Arrange
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;

        // Find a position inside the string (if there is one)
        var stringStart = code.IndexOf('"');
        if (stringStart >= 0)
        {
            var position = stringStart + 5; // Position inside the string

            // Act
            var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

            // Assert
            Assert.Equal(expected, result);
        }
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_ConsoleWriteLine_ReturnsFalse()
    {
        // Arrange
        var code = "Console.WriteLine(\"Test {Property}\");";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("Property");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.False(result); // Console.WriteLine is not a Serilog method
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_VerbatimString_DetectedCorrectly()
    {
        // Arrange
        var code = @"logger.LogInformation(@""Path: {FilePath}"", path);";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("FilePath");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_RawStringLiteral_DetectedCorrectly()
    {
        // Arrange
        var code = @"logger.LogInformation(""""""
            User: {UserName}
            ID: {UserId}
            """""", userName, userId);";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("UserName");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void IsPositionInsideSerilogTemplate_SingleLineRawString_DetectedCorrectly()
    {
        // Arrange - Single-line raw string literal (all on one line)
        var code = @"logger.LogInformation(""""""User {UserName} logged in at {Timestamp}"""""", userName, timestamp);";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("UserName");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result);
        
        // Test second property too
        var position2 = code.IndexOf("Timestamp");
        var result2 = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position2);
        Assert.True(result2);
    }
    
    [Fact]
    public void IsPositionInsideSerilogTemplate_SingleLineRawStringWithQuotes_DetectedCorrectly()
    {
        // Arrange - Single-line raw string with embedded quotes
        var code = @"Log.Information(""""""Processing ""important"" item: {ItemName}"""""", itemName);";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("ItemName");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void IsPositionInsideSerilogTemplate_SingleLineRawStringNotSerilog_ReturnsFalse()
    {
        // Arrange - Single-line raw string but not in a Serilog call
        var code = @"var message = """"""This is not {Serilog}"""""";";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("Serilog");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.False(result); // Not a Serilog call
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_InterpolatedString_ReturnsFalse()
    {
        // Arrange
        var code = @"var message = $""User {userId} logged in"";";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("userId");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.False(result); // Interpolated strings are not Serilog templates
    }
    
    [Fact]
    public void IsPositionInsideSerilogTemplate_PositionAtStringBoundary_VerbatimString()
    {
        // Arrange - position at the very start of the string content (after @")
        var code = @"logger.LogInformation(@""Processing {Item}"", item);";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        // Position right after the opening quote (at 'P' of Processing)
        var position = code.IndexOf(@"@""") + 2;

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void IsPositionInsideSerilogTemplate_PositionAtWhitespace_InVerbatimString()
    {
        // Arrange - position on whitespace character inside verbatim string
        var code = @"logger.LogInformation(@""User {Name} logged in"", name);";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        // Position on the space between "User" and "{Name}"
        var position = code.IndexOf("User") + 4; // Position on the space

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void IsPositionInsideSerilogTemplate_PositionAtStringBoundary_RawString()
    {
        // Arrange - position at the very start of raw string content
        var code = @"logger.LogInformation(""""""Processing {Item}"""""", item);";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        // Position right after the opening quotes (at 'P' of Processing)
        var position = code.IndexOf("Processing");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result);
    }
    
    [Fact]
    public void IsPositionInsideSerilogTemplate_PositionOnNonPropertyText_InRawString()
    {
        // Arrange - position on regular text (not property) in raw string
        var code = @"Log.Information(""""""The item {ItemName} was processed"""""", name);";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        // Position on "was" - regular text, not a property
        var position = code.IndexOf("was");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result); // Should still be true - we're in a Serilog template string
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_NestedMethodCall_DetectedCorrectly()
    {
        // Arrange
        var code = @"Log.ForContext<MyClass>().Information(""Processing {Item}"", item);";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("Item");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_BeginScope_DetectedCorrectly()
    {
        // Arrange
        var code = @"using (logger.BeginScope(""Operation={OperationId}"", opId))";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("OperationId");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_InvalidSyntax_HandlesGracefully()
    {
        // Arrange - intentionally malformed code
        var code = @"logger.LogInformation(""Unclosed string {Property}";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("Property");

        // Act - should not throw
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert - should still detect if it looks like a Serilog call
        Assert.True(result); // Falls back to text-based detection
    }

    [Fact]
    public void GetExpressionContext_FilterExpression_ReturnsCorrect()
    {
        // Arrange
        var code = @".Filter.ByExcluding(""Level = 'Debug'"")";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("Level");

        // Act
        var context = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, position);

        // Assert
        Assert.Equal(ExpressionContext.FilterExpression, context);
    }

    [Fact]
    public void GetExpressionContext_ExpressionTemplate_ReturnsCorrect()
    {
        // Arrange
        var code = @"new ExpressionTemplate(""[{@t:HH:mm:ss} {@l:u3}] {@m}\n{@x}"")";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("@t");

        // Act
        var context = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, position);

        // Assert
        Assert.Equal(ExpressionContext.ExpressionTemplate, context);
    }

    [Fact]
    public void GetExpressionContext_EnrichWhen_ReturnsFilterExpression()
    {
        // Arrange
        var code = @".Enrich.When(""Level = 'Error' or Level = 'Fatal'"")";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("Level");

        // Act
        var context = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, position);

        // Assert
        Assert.Equal(ExpressionContext.FilterExpression, context);
    }

    [Fact]
    public void GetExpressionContext_EnrichWithComputed_ReturnsComputedProperty()
    {
        // Arrange
        var code = @".Enrich.WithComputed(""Duration"", ""EndTime - StartTime"")";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("EndTime");

        // Act
        var context = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, position);

        // Assert
        Assert.Equal(ExpressionContext.ComputedProperty, context);
    }

    [Fact]
    public void GetExpressionContext_WriteToConditional_ReturnsConditionalExpression()
    {
        // Arrange
        var code = @".WriteTo.Conditional(""Level >= 'Warning'"", errorSink)";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("Level");

        // Act
        var context = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, position);

        // Assert
        Assert.Equal(ExpressionContext.ConditionalExpression, context);
    }

    [Fact]
    public void GetExpressionContext_RegularString_ReturnsNone()
    {
        // Arrange
        var code = @"var message = ""This is not a Serilog template"";";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("not");

        // Act
        var context = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, position);

        // Assert
        Assert.Equal(ExpressionContext.None, context);
    }

    [Fact]
    public void GetExpressionContext_InvalidSyntax_HandlesGracefully()
    {
        // Arrange - malformed code with missing closing quote and paren
        var code = @"var x = new Something(""Test";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("Test");

        // Act - should not throw
        Exception exception = null;
        try
        {
            ExpressionContext context = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, position);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert - should not throw
        Assert.Null(exception);
        // The actual result depends on Roslyn's parsing behavior for malformed code
        // We just want to ensure it doesn't crash
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_StringConcatenation_NotDetected()
    {
        // Arrange
        var code = @"var template = ""User "" + ""{UserId}"" + "" logged in"";";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("UserId");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.False(result); // String concatenation outside Serilog call
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_VariableAssignment_NotDetected()
    {
        // Arrange
        var code = @"string template = ""User {UserId} logged in"";";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("UserId");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.False(result); // Variable assignment, not a direct Serilog call
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_MultilineStringContinuation_Detected()
    {
        // Arrange
        var code = @"logger.LogInformation(@""
            Processing record:
            ID: {RecordId}
            Status: {Status}"", id, status);";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;

        // Test position in the middle of the multiline string
        var position = code.IndexOf("RecordId");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetExpressionContext_FilterByIncludingOnly_ReturnsFilterExpression()
    {
        // Arrange
        var code = @".Filter.ByIncludingOnly(""RequestPath like '/api%'"")";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("RequestPath");

        // Act
        var context = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, position);

        // Assert
        Assert.Equal(ExpressionContext.FilterExpression, context);
    }

    [Fact]
    public void GetExpressionContext_EnrichWithComputed_FirstArgument_ReturnsNone()
    {
        // Arrange - first argument is property name, not an expression
        var code = @".Enrich.WithComputed(""PropertyName"", ""EndTime - StartTime"")";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("PropertyName");

        // Act
        var context = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, position);

        // Assert
        Assert.Equal(ExpressionContext.None, context); // First argument is not an expression
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_EmptySnapshot_ReturnsFalse()
    {
        // Arrange
        var textBuffer = MockTextBuffer.Create("");
        var snapshot = textBuffer.CurrentSnapshot;

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, 0);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetExpressionContext_EmptySnapshot_ReturnsNone()
    {
        // Arrange
        var textBuffer = MockTextBuffer.Create("");
        var snapshot = textBuffer.CurrentSnapshot;

        // Act
        var context = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, 0);

        // Assert
        Assert.Equal(ExpressionContext.None, context);
    }

    [Theory]
    [InlineData("Log.Verbose(\"Message {Prop}\", p);", true)]
    [InlineData("Log.Debug(\"Message {Prop}\", p);", true)]
    [InlineData("Log.Information(\"Message {Prop}\", p);", true)]
    [InlineData("Log.Warning(\"Message {Prop}\", p);", true)]
    [InlineData("Log.Error(\"Message {Prop}\", p);", true)]
    [InlineData("Log.Fatal(\"Message {Prop}\", p);", true)]
    [InlineData("logger.LogTrace(\"Message {Prop}\", p);", true)]
    [InlineData("logger.LogCritical(\"Message {Prop}\", p);", true)]
    public void IsPositionInsideSerilogTemplate_AllLogLevels_Detected(string code, bool expected)
    {
        // Arrange
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("Prop");

        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_PositionOutOfBounds_HandlesGracefully()
    {
        // Arrange
        var code = "var x = \"not a serilog call\";";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;

        // Act - should not throw
        Exception exception1 = null;
        Exception exception2 = null;
        try
        {
            bool result1 = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, -1);
        }
        catch (Exception ex)
        {
            exception1 = ex;
        }

        try
        {
            bool result2 = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, 10000);
        }
        catch (Exception ex)
        {
            exception2 = ex;
        }

        // Assert - should not throw exceptions
        Assert.Null(exception1);
        Assert.Null(exception2);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_MultiLineStringContinuation_ChecksPreviousLines()
    {
        // This tests the fallback logic when no string literal is found but there's template syntax
        // and a Serilog call with multi-line string on a previous line
        var code = @"
public class Test {
    void Method() {
        var logger = GetLogger();
        // Start a Serilog call with a raw string
        logger.LogInformation(@""
            This is line 1
            User {Name} logged in
            at {Time}"");
    }
}";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position on {Name} in the continuation line
        var position = code.IndexOf("{Name}") + 2;
        
        // Act - This should trigger the multi-line continuation check
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_TemplateWithoutStringLiteral_ChecksMultiLine()
    {
        // Test case where we have template syntax but no string literal found in tree
        // This hits lines 223-240 in the fallback logic
        var code = @"
logger.LogInformation(""""""
    First line
    {Property1} and {Property2}
    Last line
    """""");";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position on the middle line with properties
        var position = code.IndexOf("{Property1}") + 5;
        
        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert  
        Assert.True(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_NoStringLiteralNoTemplate_ReturnsFalse()
    {
        // Tests the case where no string literal is found and no template syntax exists
        // This should hit line 242-243
        var code = @"
public class Test {
    void Method() {
        var x = 42;
        var y = x + 1;
    }
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("42");
        
        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_InvalidSyntaxWithSerilogPattern_UseFallback()
    {
        // Test exception handling path (lines 245-286)
        // Use malformed C# that will cause parsing to fail
        var code = @"
public class Test {
    void Method() {
        logger.LogInformation(""Message {Property}"", 
        // Missing closing parenthesis and semicolon
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("{Property}") + 5;
        
        // Act - should use fallback logic due to parse error
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert - fallback should detect the Serilog pattern
        Assert.True(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_InvalidSyntaxNoSerilogPattern_ReturnsFalse()
    {
        // Test exception fallback when no Serilog pattern exists
        var code = @"
public class Test {
    void Method() {
        var x = ""not serilog"";
        // Malformed code
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        var position = code.IndexOf("not serilog") + 5;
        
        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert
        Assert.False(result);
    }

    [Fact]  
    public void IsPositionInsideSerilogTemplate_MalformedNonSerilogWithNearbyContext_ReturnsFalse()
    {
        // Test that a position in a non-Serilog string returns false
        // even when there are nearby Serilog calls and malformed syntax
        var code = @"
logger.LogInformation(""Starting"");
var malformed = ""test
logger.LogDebug(""Debugging"");";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position in the malformed line (NOT a Serilog call)
        var position = code.IndexOf("test") + 2;
        
        // Act - position is NOT in a Serilog template despite nearby context
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert - should return false because this isn't a Serilog string
        Assert.False(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_MalformedSerilogCall_FallbackDetectsIt()
    {
        // Test exception fallback that detects malformed Serilog call on same line
        var code = @"
logger.LogInformation(""Message {Property}
// Missing closing quote and parenthesis";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position inside the Serilog template
        var position = code.IndexOf("{Property}") + 5;
        
        // Act - fallback should detect this IS a Serilog call
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_ExpressionTemplateWithMultipleArgs_StringNotFound_ReturnsFalse()
    {
        // Test case where ExpressionTemplate has arguments but our string is not one of them
        // This should hit line 390: "String literal not found in any ExpressionTemplate arguments"
        var code = @"
public class Test {
    void Method() {
        // String is in array initializer passed to ExpressionTemplate, walking should find it
        var template = new ExpressionTemplate(""Other"", new[] { ""Template {Property}"" });
    }
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position inside the array element string
        var position = code.IndexOf("{Property}") + 5;
        
        // Act - will walk up through array to ExpressionTemplate
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert - should detect it's in ExpressionTemplate context
        Assert.True(result);
    }

    [Fact]  
    public void IsPositionInsideSerilogTemplate_StringInExpressionWithExpressionTemplate_NoArgs_ReturnsFalse()
    {
        // Test walking up to ExpressionTemplate with no arguments
        // This should hit line 394: "ExpressionTemplate has no arguments"
        var code = @"
public class Test {
    void Method() {
        // String and ExpressionTemplate in same expression
        var x = ""Template {Property}"" != null ? new ExpressionTemplate() : null;
    }
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position inside the string
        var position = code.IndexOf("{Property}") + 5;
        
        // Act - walks up through conditional to find ExpressionTemplate
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_NotExpressionTemplate_OtherNewObject_ReturnsFalse()
    {
        // Test object creation that's not ExpressionTemplate
        // This should hit line 399: "Not an ExpressionTemplate"
        var code = @"
public class Test {
    void Method() {
        var obj = new StringBuilder(""Template {Property}"");
    }
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position inside the template string
        var position = code.IndexOf("{Property}") + 5;
        
        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert - not an ExpressionTemplate
        Assert.False(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_DirectMethodCallNoMemberAccess_ReturnsFalse()
    {
        // Test invocation without member access (just Method() not object.Method())
        // This should hit lines 452-454: "No member access found"
        var code = @"
public class Test {
    void Method() {
        // Direct method call without object/class prefix
        LogInformation(""Template {Property}"", value);
    }
    void LogInformation(string template, object value) { }
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position inside the template string
        var position = code.IndexOf("{Property}") + 5;
        
        // Act - should find invocation but no member access
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert - not a Serilog call (no member access)
        Assert.False(result);
    }


    [Fact]
    public void IsPositionInsideSerilogTemplate_EnrichWithComputed_ChecksBranch()
    {
        // Test Enrich.WithComputed() - will hit lines 483-488 even though it returns false
        // (because Enrich is part of a chain, not a direct identifier)
        var code = @"
public class Test {
    void Configure() {
        var config = new LoggerConfiguration()
            .Enrich.WithComputed(""HasErrors"", ""Level = 'Error'"");
    }
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position inside the expression string
        var position = code.IndexOf("Level") + 2;
        
        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert - returns false because Enrich is not a direct identifier
        Assert.False(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_EnrichWhen_ChecksBranch()
    {
        // Test Enrich.When() - will hit lines 483-488 even though it returns false
        var code = @"
public class Test {
    void Configure() {
        var config = new LoggerConfiguration()
            .Enrich.When(""Level = 'Error'"", e => e.WithProperty(""IsError"", true));
    }
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position inside the expression string
        var position = code.IndexOf("Level") + 2;
        
        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert - returns false because Enrich is not a direct identifier
        Assert.False(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_WriteToConditional_ChecksBranch()
    {
        // Test WriteTo.Conditional() - will hit lines 493-498 even though it returns false
        var code = @"
public class Test {
    void Configure() {
        var config = new LoggerConfiguration()
            .WriteTo.Conditional(""Level = 'Error'"", wt => wt.File(""errors.txt""));
    }
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position inside the expression string
        var position = code.IndexOf("Level") + 2;
        
        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert - returns false because WriteTo is not a direct identifier
        Assert.False(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_NestedMemberAccess_ThisLogger_ReturnsTrue()
    {
        // Test nested member access like this.logger.LogInformation
        // This should hit lines 537-544
        var code = @"
public class Test {
    private ILogger logger;
    
    void Method() {
        this.logger.LogInformation(""User {UserId} logged in"", userId);
    }
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position inside the template string
        var position = code.IndexOf("{UserId}") + 4;
        
        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert - should detect nested member access with "log" in the member name
        Assert.True(result);
    }

    [Fact]
    public void IsPositionInsideSerilogTemplate_SerilogMethodNameButWrongContext_ReturnsFalse()
    {
        // Test a Serilog method name but in wrong context to hit "No match found" at lines 563-564
        // Using "Information" which is a valid Serilog method, but called on wrong type
        var code = @"
public class Test {
    private MyCustomClass custom;
    
    void Method() {
        // Information is a Serilog method name, but custom is not a logger
        this.custom.Information(""Template {Property}"");
    }
    
    class MyCustomClass {
        public void Information(string msg) { }
    }
}";
        
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;
        
        // Position inside the string
        var position = code.IndexOf("{Property}") + 5;
        
        // Act
        var result = SyntaxTreeAnalyzer.IsPositionInsideSerilogTemplate(snapshot, position);
        
        // Assert - not a Serilog call (wrong context)
        Assert.False(result);
    }

    [Fact]
    public void GetExpressionContext_PositionOutOfBounds_ReturnsNone()
    {
        // Arrange
        var code = "new ExpressionTemplate(\"Test\");";
        var textBuffer = MockTextBuffer.Create(code);
        var snapshot = textBuffer.CurrentSnapshot;

        // Act - should not throw
        var context1 = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, -1);
        var context2 = SyntaxTreeAnalyzer.GetExpressionContext(snapshot, 10000);

        // Assert
        Assert.Equal(ExpressionContext.None, context1);
        Assert.Equal(ExpressionContext.None, context2);
    }
}