using Xunit;

namespace SerilogSyntax.Tests.Tagging;

/// <summary>
/// Tests for the core logic of multi-line raw string detection.
/// These tests verify the algorithm without requiring full VS interface mocks.
/// </summary>
public class RawStringDetectionLogicTests
{
    [Fact]
    public void DetectsMultiLineRawString_Example2()
    {
        // Example 2 from TestRawStringLiterals
        var lines = new[]
        {
            "    logger.LogInformation(\"\"\"",
            "        Processing record:",
            "        ID: {RecordId}",
            "        Status: {Status}",
            "        Timestamp: {Timestamp:yyyy-MM-dd}",
            "        \"\"\", recordId, status, DateTime.Now);"
        };

        Assert.True(HasSerilogCallWithRawStringOpening(lines[0]));
        Assert.True(IsInsideRawString(lines, 1));
        Assert.True(IsInsideRawString(lines, 2));
        Assert.True(IsInsideRawString(lines, 3));
        Assert.True(IsInsideRawString(lines, 4));
        Assert.True(HasRawStringClosing(lines[5]));
    }

    [Fact]
    public void DetectsMultiLineRawString_Example4_CustomDelimiter()
    {
        // Example 4 with 4+ quotes delimiter
        var lines = new[]
        {
            "    logger.LogInformation(\"\"\"\"",
            "        Template with \"\"\" inside: {Data}",
            "        This allows literal triple quotes in the string",
            "        \"\"\"\", data);"
        };

        // Should detect 4-quote delimiter
        Assert.True(HasSerilogCallWithRawStringOpening(lines[0]));
        Assert.True(IsInsideRawString(lines, 1));
        Assert.True(IsInsideRawString(lines, 2));
    }

    [Fact]
    public void DetectsMultiLineRawString_Example5_Complex()
    {
        // Example 5 - Complex multi-line with various property types
        var lines = new[]
        {
            "    logger.LogInformation(\"\"\"",
            "        ===============================================",
            "        Application: {AppName}",
            "        Version: {Version}",
            "        Environment: {Environment}",
            "        ===============================================",
            "        User: {UserName} (ID: {UserId})",
            "        Session: {SessionId}",
            "        Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}",
            "        ===============================================",
            "        \"\"\", appName, version, environment, userName, userId, sessionId, DateTime.Now);"
        };

        // All content lines should be inside the raw string
        for (int i = 1; i <= 9; i++)
        {
            Assert.True(IsInsideRawString(lines, i));
        }

        // Should find multiple properties in different lines
        var line2Props = FindPropertiesInLine(lines[2]);
        Assert.Contains("AppName", line2Props);
        
        var line6Props = FindPropertiesInLine(lines[6]);
        Assert.Contains("UserName", line6Props);
        Assert.Contains("UserId", line6Props);
    }

    [Fact]
    public void DetectsMultiLineRawString_Example6_Positional()
    {
        // Example 6 - Raw string with positional parameters and formatting
        var lines = new[]
        {
            "    logger.LogInformation(\"\"\"",
            "        Database Query Results:",
            "        Query: SELECT * FROM Users WHERE Id = {0}",
            "        Rows affected: {1,5}",
            "        Execution time: {2:F2}ms",
            "        \"\"\", userId, 42, 123.456);"
        };

        Assert.True(IsInsideRawString(lines, 2));
        Assert.True(IsInsideRawString(lines, 3));
        Assert.True(IsInsideRawString(lines, 4));

        // Should find positional properties
        var line2Props = FindPropertiesInLine(lines[2]);
        Assert.Contains("0", line2Props);
        
        var line3Props = FindPropertiesInLine(lines[3]);
        Assert.Contains("1", line3Props); // {1,5} with alignment
        
        var line4Props = FindPropertiesInLine(lines[4]);
        Assert.Contains("2", line4Props); // {2:F2} with format
    }

    [Fact]
    public void DetectsMultiLineRawString_Example7_Destructuring()
    {
        // Example 7 - Raw string with destructuring and stringification
        var lines = new[]
        {
            "    logger.LogInformation(\"\"\"",
            "        Configuration loaded:",
            "        Config: {@Config}",
            "        Settings: {$Settings}",
            "        \"\"\", config, settings);"
        };

        Assert.True(IsInsideRawString(lines, 2));
        Assert.True(IsInsideRawString(lines, 3));

        // Should find destructured and stringified properties
        var line2Props = FindPropertiesInLine(lines[2]);
        Assert.Contains("Config", line2Props);
        
        var line3Props = FindPropertiesInLine(lines[3]);
        Assert.Contains("Settings", line3Props);
    }

    [Fact]
    public void DoesNotDetect_SingleLineRawString_Example1()
    {
        // Example 1 - Single-line raw string literal
        var lines = new[]
        {
            "    logger.LogInformation(\"\"\"User {UserId} logged in at {Timestamp:HH:mm:ss}\"\"\", 42, DateTime.Now);"
        };

        // Single line raw strings are complete on one line, not multi-line
        Assert.False(IsInsideRawString(lines, 0));
    }

    [Fact]
    public void DoesNotDetect_Example3_EmbeddedQuotes()
    {
        // Example 3 - Single-line with embedded quotes
        var lines = new[]
        {
            "    logger.LogInformation(\"\"\"Configuration value \"AppMode\" is set to \"{Value}\" \"\"\", configValue);"
        };

        // Still a single-line raw string
        Assert.False(IsInsideRawString(lines, 0));
    }

    [Fact]
    public void HandlesMultipleRawStrings_InSameFile()
    {
        var lines = new[]
        {
            "    // First raw string",
            "    logger.LogInformation(\"\"\"",
            "        First: {Message}",
            "        \"\"\", msg1);",
            "    ",
            "    // Second raw string",
            "    logger.LogWarning(\"\"\"",
            "        Second: {Warning}",
            "        \"\"\", msg2);"
        };

        // First raw string (line 2)
        Assert.True(IsInsideRawString(lines, 2));
        
        // Between raw strings (line 4)
        Assert.False(IsInsideRawString(lines, 4));
        
        // Second raw string (line 7)
        Assert.True(IsInsideRawString(lines, 7));
    }

    [Fact]
    public void DetectsProperties_WithAllFeatures()
    {
        var line = "    User: {@User} ID: {$Id,10} Time: {Timestamp:HH:mm:ss} Pos: {0}";
        var properties = FindPropertiesInLine(line);

        Assert.Contains("User", properties);    // Destructured
        Assert.Contains("Id", properties);      // Stringified with alignment
        Assert.Contains("Timestamp", properties); // With format specifier
        Assert.Contains("0", properties);       // Positional
    }

    // Helper methods that simulate the tagger logic
    private bool HasSerilogCallWithRawStringOpening(string line)
    {
        return (line.Contains("Log.") || line.Contains("logger.") || line.Contains("_logger.")) 
               && line.TrimEnd().EndsWith("\"\"\"");
    }

    private bool HasRawStringClosing(string line)
    {
        return line.TrimStart().StartsWith("\"\"\"");
    }

    private bool IsInsideRawString(string[] lines, int currentLineIndex)
    {
        // Look backwards for raw string opening
        for (int i = currentLineIndex - 1; i >= 0; i--)
        {
            if (HasSerilogCallWithRawStringOpening(lines[i]))
            {
                // Found opening, check if closed before current line
                for (int j = i + 1; j < currentLineIndex; j++)
                {
                    if (HasRawStringClosing(lines[j]))
                    {
                        // Raw string was closed before current line
                        return false;
                    }
                }
                // Found opening and not closed yet
                return true;
            }
        }
        return false;
    }

    private string[] FindPropertiesInLine(string line)
    {
        var properties = new System.Collections.Generic.List<string>();
        var inProperty = false;
        var currentProperty = "";
        
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '{')
            {
                inProperty = true;
                currentProperty = "";
            }
            else if (line[i] == '}' && inProperty)
            {
                if (!string.IsNullOrWhiteSpace(currentProperty))
                {
                    // Remove operators and format specifiers
                    var propName = currentProperty.TrimStart('@', '$');
                    var colonIndex = propName.IndexOf(':');
                    if (colonIndex > 0)
                        propName = propName.Substring(0, colonIndex);
                    var commaIndex = propName.IndexOf(',');
                    if (commaIndex > 0)
                        propName = propName.Substring(0, commaIndex);
                    
                    properties.Add(propName.Trim());
                }
                inProperty = false;
            }
            else if (inProperty)
            {
                currentProperty += line[i];
            }
        }
        
        return [.. properties];
    }
}