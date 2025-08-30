using Xunit;

namespace SerilogSyntax.Tests.Classification;

public class MultiLineRawStringTests
{
    [Fact]
    public void SimulateVisualStudioProcessing_MultiLineRawString()
    {
        // Simulate VS processing these lines one by one (but without the closing line yet)
        // This is what happens when VS is highlighting line 2 - the closing might not be visible yet
        var lines = new[]
        {
            "logger.LogInformation(\"\"\"",
            "    Processing record:",
            "    ID: {RecordId}",
            "    Status: {Status}",
            // Note: closing line not included to simulate what happens during editing
        };

        // When VS processes line 2 (ID: {RecordId}), the classifier needs to:
        // 1. Look backwards and find line 0 has 'logger.LogInformation("""'
        // 2. Detect that """ at end of line 0 starts an unclosed raw string
        // 3. Check forward and NOT find closing quotes (because user is still typing)
        // 4. Therefore line 2 IS inside a raw string and should be highlighted

        var result = SimulateIsInsideRawString(lines, 2);
        Assert.True(result, "Line 2 should be detected as inside raw string");
    }
    
    [Fact]
    public void DebugSimulation()
    {
        var lines = new[]
        {
            "logger.LogInformation(\"\"\"",
            "    Processing record:",
            "    ID: {RecordId}"
        };
        
        // Test line 0: Should find """ at position 25, Serilog call before it
        var line0 = lines[0];
        var quoteIndex = line0.IndexOf("\"\"\"");
        var beforeQuotes = line0.Substring(0, quoteIndex);
        var isSerilog = IsSerilogCall(beforeQuotes);
        var isClosed = IsRawStringClosed(lines, 0, 3);
        
        Assert.Equal(22, quoteIndex); // Should find """ at position 22
        Assert.True(isSerilog, "Should detect Serilog call");
        Assert.False(isClosed, "Should NOT be closed (no closing quotes in test data)");
        
        // Now test the full simulation
        var result = SimulateIsInsideRawString(lines, 2);
        Assert.True(result, "Should detect line 2 as inside raw string");
    }

    private bool SimulateIsInsideRawString(string[] lines, int currentLineIndex)
    {
        // This simulates what IsInsideRawStringLiteral should do
        
        // Look backwards for raw string starts
        for (int i = currentLineIndex - 1; i >= 0; i--)
        {
            var line = lines[i];
            
            // Find all occurrences of """ in this line
            int index = 0;
            while ((index = line.IndexOf("\"\"\"", index)) != -1)
            {
                // Count quotes at this position
                int quoteCount = 0;
                int pos = index;
                while (pos < line.Length && line[pos] == '"')
                {
                    quoteCount++;
                    pos++;
                }
                
                if (quoteCount >= 3)
                {
                    // Check if this could be a Serilog raw string
                    // The line must contain a Serilog call before the quotes
                    if (index > 0 && IsSerilogCall(line.Substring(0, index)))
                    {
                        // Check if this raw string is closed by looking forward from this line
                        if (!IsRawStringClosed(lines, i, quoteCount))
                        {
                            return true;
                        }
                    }
                }
                
                index = pos;
            }
        }
        
        return false;
    }

    private bool IsSerilogCall(string text)
    {
        return text.Contains("LogInformation(") || text.Contains("LogWarning(") || 
               text.Contains("LogError(") || text.Contains("LogDebug(") ||
               text.Contains("LogTrace(") || text.Contains("LogCritical(") ||
               text.Contains("Information(") || text.Contains("Warning(") ||
               text.Contains("Error(") || text.Contains("Debug(");
    }

    private bool IsRawStringClosed(string[] lines, int startLine, int quoteCount)
    {
        // Start checking from the same line (for single-line) or next line (for multi-line)
        bool passedOpeningLine = false;
        
        for (int i = startLine; i < lines.Length; i++)
        {
            var line = lines[i];
            int startPos = 0;
            
            if (i == startLine)
            {
                // Skip opening quotes on first line
                int quoteStart = line.IndexOf('"');
                if (quoteStart >= 0)
                {
                    int idx = quoteStart;
                    while (idx < line.Length && line[idx] == '"')
                        idx++;
                    startPos = idx;
                    
                    // If nothing else on this line, we've passed the opening
                    if (line.Substring(startPos).Trim().Length == 0)
                        passedOpeningLine = true;
                }
            }
            else
            {
                passedOpeningLine = true;
            }
            
            // Only look for closing quotes if we've moved past the opening line
            if (!passedOpeningLine && i == startLine)
                continue;
            
            // Look for closing quotes
            for (int j = startPos; j <= line.Length - quoteCount; j++)
            {
                bool foundClosing = true;
                for (int k = 0; k < quoteCount; k++)
                {
                    if (j + k >= line.Length || line[j + k] != '"')
                    {
                        foundClosing = false;
                        break;
                    }
                }
                
                if (foundClosing)
                {
                    // Check if there are more quotes (would mean it's not the closing)
                    if (j + quoteCount < line.Length && line[j + quoteCount] == '"')
                        continue;
                    
                    // Found valid closing quotes!
                    return true;
                }
            }
        }
        
        return false;
    }

    [Fact]
    public void VerifyAllFailingCases()
    {
        // Case 2 - Multi-line raw string
        Assert.True(SimulateIsInsideRawString([
            "logger.LogInformation(\"\"\"",
            "    Processing record:",
            "    ID: {RecordId}"
        ], 2), "Case 2: Multi-line raw string should be detected");

        // Case 4 - Custom delimiter (4 quotes)
        Assert.True(SimulateIsInsideRawString([
            "logger.LogInformation(\"\"\"\"",
            "    Template with \"\"\" inside: {Data}"
        ], 1), "Case 4: Custom delimiter raw string should be detected");

        // Case 6 - Positional parameters
        Assert.True(SimulateIsInsideRawString([
            "logger.LogInformation(\"\"\"",
            "    Database Query Results:",
            "    Query: SELECT * FROM Users WHERE Id = {0}"
        ], 2), "Case 6: Positional parameters in raw string should be detected");

        // Case 7 - Destructuring
        Assert.True(SimulateIsInsideRawString([
            "logger.LogInformation(\"\"\"",
            "    Configuration loaded:",
            "    Config: {@Config}"
        ], 2), "Case 7: Destructuring in raw string should be detected");
    }
}