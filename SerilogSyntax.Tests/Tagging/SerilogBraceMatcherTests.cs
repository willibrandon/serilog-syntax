using Xunit;
using System.Linq;

namespace SerilogSyntax.Tests.Tagging;

public class SerilogBraceMatcherTests
{
    [Fact]
    public void FindMatchingBraces_SimpleProperty()
    {
        var text = "Log.Information(\"Hello {Name}\");";
        var braces = FindBracePairs(text);
        
        Assert.Single(braces);
        Assert.Equal(23, braces[0].Item1); // Position of {
        Assert.Equal(28, braces[0].Item2); // Position of }
    }

    [Fact]
    public void FindMatchingBraces_MultipleProperties()
    {
        var text = "Log.Information(\"User {Name} logged in at {Timestamp}\");";
        var braces = FindBracePairs(text);
        
        Assert.Equal(2, braces.Count);
        Assert.Equal(22, braces[0].Item1); // First {
        Assert.Equal(27, braces[0].Item2); // First }
        Assert.Equal(42, braces[1].Item1); // Second {
        Assert.Equal(52, braces[1].Item2); // Second }
    }

    [Fact]
    public void FindMatchingBraces_EscapedBraces()
    {
        var text = "Log.Information(\"Use {{braces}} for {Property}\");";
        var braces = FindBracePairs(text);
        
        Assert.Single(braces);
        Assert.Equal(36, braces[0].Item1); // Position of { for Property
        Assert.Equal(45, braces[0].Item2); // Position of } for Property
    }

    [Fact]
    public void FindMatchingBraces_ComplexProperty()
    {
        var text = "Log.Information(\"Price: {Price,10:C2}\");";
        var braces = FindBracePairs(text);
        
        Assert.Single(braces);
        Assert.Equal(24, braces[0].Item1); // Position of {
        Assert.Equal(36, braces[0].Item2); // Position of }
    }

    [Fact]
    public void NotSerilogCall_NoMatches()
    {
        var text = "Console.WriteLine(\"Hello {World}\");";
        var braces = FindBracePairs(text, requireSerilogCall: true);
        
        Assert.Empty(braces);
    }

    // Helper method to simulate brace matching logic
    private System.Collections.Generic.List<(int, int)> FindBracePairs(string text, bool requireSerilogCall = false)
    {
        var pairs = new System.Collections.Generic.List<(int, int)>();
        
        if (requireSerilogCall && !IsSerilogCall(text))
            return pairs;

        var stack = new System.Collections.Generic.Stack<int>();
        
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                if (i + 1 < text.Length && text[i + 1] == '{')
                {
                    i++; // Skip escaped brace
                    continue;
                }

                stack.Push(i);
            }
            else if (text[i] == '}')
            {
                if (i + 1 < text.Length && text[i + 1] == '}')
                {
                    i++; // Skip escaped brace
                    continue;
                }

                if (stack.Count > 0)
                {
                    var openPos = stack.Pop();
                    pairs.Add((openPos, i));
                }
            }
        }
        
        return [.. pairs.OrderBy(p => p.Item1)];
    }

    private bool IsSerilogCall(string text)
    {
        var patterns = new[] { "Log.", "_log.", "_logger.", "logger." };
        var methods = new[] { "Verbose", "Debug", "Information", "Warning", "Error", "Fatal", "Write" };
        
        return patterns.Any(p => text.Contains(p)) && methods.Any(m => text.Contains(m + "("));
    }
}