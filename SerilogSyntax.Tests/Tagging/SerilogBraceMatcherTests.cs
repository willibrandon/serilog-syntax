using Microsoft.VisualStudio.Text;
using SerilogSyntax.Tagging;
using SerilogSyntax.Tests.TestHelpers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Tagging;

public class SerilogBraceMatcherTests
{
    [Fact]
    public void BraceMatching_SimpleProperty_MatchesCorrectly()
    {
        var text = "Log.Information(\"Hello {Name}\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace
        var openBracePos = text.IndexOf("{Name");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count); // Opening and closing brace
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos); // Opening {
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 5); // Closing }
    }

    [Fact]
    public void BraceMatching_MultipleProperties_OnlyHighlightsCurrentPair()
    {
        var text = "Log.Information(\"User {Name} logged in at {Timestamp}\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on first property's opening brace
        var firstOpenPos = text.IndexOf("{Name");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, firstOpenPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count); // Only the current pair
        Assert.Contains(tags, t => t.Span.Start.Position == firstOpenPos);
        Assert.Contains(tags, t => t.Span.Start.Position == firstOpenPos + 5);

        // Move to second property's closing brace
        var secondClosePos = text.IndexOf("Timestamp}") + 9;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, secondClosePos));
        
        tags = [.. matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length)))];

        Assert.Equal(2, tags.Count);
        var secondOpenPos = text.IndexOf("{Timestamp");
        Assert.Contains(tags, t => t.Span.Start.Position == secondOpenPos);
        Assert.Contains(tags, t => t.Span.Start.Position == secondClosePos);
    }

    [Fact]
    public void BraceMatching_EscapedBraces_IgnoresEscaped()
    {
        var text = "Log.Information(\"Use {{braces}} for {Property}\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the actual property brace
        var propertyOpenPos = text.IndexOf("{Property");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyOpenPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == propertyOpenPos);
        Assert.Contains(tags, t => t.Span.Start.Position == propertyOpenPos + 9);
    }

    [Fact]
    public void BraceMatching_ComplexProperty_WithFormatting_MatchesCorrectly()
    {
        var text = "Log.Information(\"Price: {Price,10:C2}\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace
        var openBracePos = text.IndexOf("{Price");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var closePos = text.IndexOf(":C2}") + 3;
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == closePos);
    }

    [Fact]
    public void BraceMatching_NotSerilogCall_NoMatches()
    {
        var text = "Console.WriteLine(\"Hello {World}\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on brace in non-Serilog call
        var openBracePos = text.IndexOf("{World");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tags); // No matches for non-Serilog calls
    }

    [Fact]
    public void BraceMatching_CursorAfterClosingBrace_StillMatches()
    {
        var text = "Log.Information(\"Value: {Amount}\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position cursor right after the closing }
        var closeBracePos = text.IndexOf("Amount}") + 6;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos + 1));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var openPos = text.IndexOf("{Amount");
        Assert.Contains(tags, t => t.Span.Start.Position == openPos);
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }

    [Fact]
    public void BraceMatching_DestructuredProperty_MatchesCorrectly()
    {
        var text = "Log.Information(\"User {@User} logged in\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace
        var openBracePos = text.IndexOf("{@User");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 6); // Closing }
    }

    [Fact]
    public void BraceMatching_StringifiedProperty_MatchesCorrectly()
    {
        var text = "Log.Information(\"Value: {$Value}\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on closing brace
        var closeBracePos = text.IndexOf("$Value}") + 6;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var openPos = text.IndexOf("{$Value");
        Assert.Contains(tags, t => t.Span.Start.Position == openPos);
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }

    [Fact]
    public void BraceMatching_PositionalProperty_MatchesCorrectly()
    {
        var text = "Log.Information(\"Item {0} processed\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace
        var openBracePos = text.IndexOf("{0");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 2); // Closing }
    }

    #region IsInsideMultiLineString Tests

    [Fact]
    public void BraceMatching_VerbatimString_SingleLine_ClosedSameLine()
    {
        var text = "Log.Information(@\"User {Name} logged in\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace
        var openBracePos = text.IndexOf("{Name");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 5); // Closing }
    }

    [Fact]
    public void BraceMatching_VerbatimString_MultiLine_WithEscapedQuotes()
    {
        var lines = new[]
        {
            "Log.Information(@\"User {Name}",
            "said \"\"Hello\"\" to {Friend}\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace on second line
        var openBracePos = text.IndexOf("{Friend");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 7); // Closing }
    }

    [Fact]
    public void BraceMatching_VerbatimString_MultiLine_UnclosedString()
    {
        var lines = new[]
        {
            "Log.Information(@\"User {Name}",
            "logged in at {Timestamp}"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace on second line (inside unclosed verbatim string)
        var openBracePos = text.IndexOf("{Timestamp");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 10); // Closing }
    }

    [Fact]
    public void BraceMatching_RawString_MultiLine()
    {
        var lines = new[]
        {
            "Log.Information(\"\"\"User {Name}",
            "logged in at {Timestamp}",
            "\"\"\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace on second line
        var openBracePos = text.IndexOf("{Timestamp");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 10); // Closing }
    }

    [Fact]
    public void BraceMatching_RawString_UnclosedString()
    {
        var lines = new[]
        {
            "Log.Information(\"\"\"User {Name}",
            "logged in at {Timestamp}"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of {Name} (which should work in multi-line context)
        var openBracePos = text.IndexOf("{Name");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 5); // Closing }
    }

    [Fact]
    public void BraceMatching_VerbatimString_WithPreviousLineSerilogCall()
    {
        var lines = new[]
        {
            "_logger.LogInformation(@\"User {Name}",
            "    logged in\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace on first line
        var openBracePos = text.IndexOf("{Name");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 5); // Closing }
    }

    [Fact]
    public void BraceMatching_NonSerilogVerbatimString_NoMatching()
    {
        var lines = new[]
        {
            "Console.WriteLine(@\"User {Name}",
            "logged in\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace on first line (non-Serilog call)
        var openBracePos = text.IndexOf("{Name");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tags); // No matching for non-Serilog calls
    }

    [Fact]
    public void BraceMatching_VerbatimString_MaxLookbackExceeded()
    {
        // Create a text with more than MaxLookbackLines (20) lines before the Serilog call
        var lines = new List<string>();
        for (int i = 0; i < 25; i++)
        {
            lines.Add($"// Comment line {i}");
        }
        lines.Add("Log.Information(@\"User {Name}");
        lines.Add("logged in\");");

        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the last line (should be treated as regular string, not multi-line)
        var lastLineStart = text.LastIndexOf("logged in");
        var caretPos = lastLineStart + 5; // Position somewhere in "logged in"
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, caretPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tags); // Should not find multi-line string context due to lookback limit
    }

    #endregion

    #region FindMultiLineBraceMatch Tests

    [Fact]
    public void BraceMatching_MultiLine_SimpleCase()
    {
        var lines = new[]
        {
            "Log.Information(@\"User {Name}",
            "    is active\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the opening brace
        var openBracePos = text.IndexOf("{Name");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var closeBracePos = text.IndexOf("Name}") + 4; // Position on the } after "Name"
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }

    [Fact]
    public void BraceMatching_MultiLine_InvalidSyntax_NoMatching()
    {
        // Test an invalid Serilog syntax scenario - braces with nested content spanning lines
        // This is NOT valid Serilog syntax and should not be matched
        var lines = new[]
        {
            "Log.Information(@\"User {User} with data {",
            "    Data: {NestedProperty}",
            "} processed\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the opening brace of invalid syntax
        var dataBracePos = text.IndexOf("data {") + 5; // Position on the { after "data "
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, dataBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // This should NOT work - the syntax is invalid Serilog template syntax
        // The implementation correctly does not match malformed templates
        Assert.Empty(tags);
    }

    [Fact]
    public void BraceMatching_MultiLine_ValidNestedProperty()
    {
        // Test a valid nested property in multi-line verbatim string
        var lines = new[]
        {
            "_logger.LogInformation(",
            "    @\"Processing {EntityId}",
            "    with nested {NestedProperty} value\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the nested property brace (in a valid multi-line context)
        var nestedBracePos = text.IndexOf("{NestedProperty");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, nestedBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // This should work - valid Serilog property in multi-line string
        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == nestedBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == nestedBracePos + 15); // Closing } after "NestedProperty"
    }

    [Fact]
    public void BraceMatching_MultiLine_SameLine_ValidProperty()
    {
        // Test that properties on the same line as Log call work correctly
        var text = "Log.Information(@\"User {User} logged in successfully\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the {User} brace
        var userBracePos = text.IndexOf("{User");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, userBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // This should work fine - valid Serilog property
        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == userBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == userBracePos + 5); // Closing }
    }

    [Fact]
    public void BraceMatching_MultiLine_ProperMultiLineCase()
    {
        // Test a case where the verbatim string truly spans multiple lines
        // starting from a line that doesn't contain the Serilog call
        var lines = new[]
        {
            "_logger.LogInformation(",
            "    @\"Processing {EntityId}",
            "    with details {Details}\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the {Details} brace on line 2
        var detailsBracePos = text.IndexOf("{Details");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, detailsBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // This should work because IsInsideMultiLineString should detect the multi-line context
        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == detailsBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == detailsBracePos + 8); // Closing }
    }

    [Fact]
    public void BraceMatching_MultiLine_EscapedBraces()
    {
        // Test that escaped braces don't interfere with real brace matching in single-line context
        var text = "Log.Information(@\"User {{escaped}} {RealProperty} end\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the opening brace of the real property (after escaped braces)
        var realOpenBracePos = text.IndexOf("{RealProperty");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, realOpenBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // The implementation should handle escaped braces correctly
        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == realOpenBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == realOpenBracePos + 13); // Closing } after "RealProperty"
    }

    [Fact] 
    public void BraceMatching_MultiLine_EscapedBraces_TrueMultiLine()
    {
        // Test escaped braces in a true multi-line context
        var lines = new[]
        {
            "_logger.LogInformation(",
            "    @\"User {{escaped}} here",
            "    Real property: {RealProperty}\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the real property brace on line 2 (in true multi-line context)
        var realOpenBracePos = text.IndexOf("{RealProperty");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, realOpenBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // This should work in true multi-line context
        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == realOpenBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == realOpenBracePos + 13); // Closing } after "RealProperty"
    }

    [Fact]
    public void BraceMatching_MultiLine_MaxPropertyLengthExceeded()
    {
        var lines = new[]
        {
            "Log.Information(@\"User {",
        };
        
        // Add a very long property name that exceeds MaxPropertyLength (200 chars)
        var longPropertyName = new string('A', 250);
        lines = [.. lines, longPropertyName];
        lines = [.. lines, "} processed\")"];

        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the opening brace
        var openBracePos = text.IndexOf("User {") + 5; // Position on the { after "User "
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should not find match due to exceeding MaxPropertyLength
        Assert.Empty(tags);
    }

    [Fact]
    public void BraceMatching_MultiLine_ClosingBrace_FindsOpening()
    {
        var lines = new[]
        {
            "Log.Information(@\"User {User} with nested {",
            "    Property: {NestedProp}",
            "} done\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the closing brace of the nested structure
        var closeBracePos = text.IndexOf("} done");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var openBracePos = text.IndexOf("nested {") + 7; // Position on the { after "nested "
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }

    [Fact]
    public void BraceMatching_MultiLine_EscapedClosingBrace()
    {
        var lines = new[]
        {
            "Log.Information(@\"User {",
            "    Property}} {InnerProp}",
            "} done\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the closing brace (should skip the escaped }})
        var closeBracePos = text.IndexOf("} done");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var openBracePos = text.IndexOf("User {") + 5; // Position on the { after "User "
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }

    [Fact]
    public void BraceMatching_MultiLine_EscapedOpeningBrace()
    {
        var lines = new[]
        {
            "Log.Information(@\"User {{ {",
            "    RealProperty",
            "} done\")"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the closing brace
        var closeBracePos = text.IndexOf("} done");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var openBracePos = text.IndexOf("{{ {") + 3; // Position on the { after "{{ "
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }

    #endregion

    #region FindMatchingCloseBrace and FindMatchingOpenBrace Tests

    [Fact]
    public void BraceMatching_SingleLine_EscapedOpeningBrace_NoMatch()
    {
        var text = "Log.Information(\"Use {{braces for {Property}\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the escaped opening brace (first {)
        var escapedBracePos = text.IndexOf("{{");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, escapedBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tags); // Escaped braces should not be matched
    }

    [Fact]
    public void BraceMatching_SingleLine_EscapedClosingBrace_NoMatch()
    {
        var text = "Log.Information(\"{Property} use braces}}\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the escaped closing brace (second })
        var escapedBracePos = text.IndexOf("}}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, escapedBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tags); // Escaped braces should not be matched
    }

    [Fact]
    public void BraceMatching_SingleLine_NestedBraces()
    {
        var text = "Log.Information(\"Outer {Property} with {Inner {Nested} Property} done\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the opening brace of the outer "Inner" property
        var innerOpenPos = text.IndexOf("{Inner");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, innerOpenPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        // The actual positions from the test output: 39 and 63
        Assert.Contains(tags, t => t.Span.Start.Position == 39);
        Assert.Contains(tags, t => t.Span.Start.Position == 63);
    }

    [Fact]
    public void BraceMatching_SingleLine_UnmatchedOpeningBrace()
    {
        var text = "Log.Information(\"Unclosed {Property\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the opening brace that has no matching closing brace
        var openBracePos = text.IndexOf("{Property");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tags); // No match for unmatched braces
    }

    [Fact]
    public void BraceMatching_SingleLine_UnmatchedClosingBrace()
    {
        var text = "Log.Information(\"Unmatched Property}\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the closing brace that has no matching opening brace
        var closeBracePos = text.IndexOf("Property}") + 8;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tags); // No match for unmatched braces
    }

    [Fact]
    public void BraceMatching_SingleLine_MixedEscapedAndReal()
    {
        var text = "Log.Information(\"{{escaped}} and {Real} and }}escaped{{\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the real property's opening brace
        var realOpenPos = text.IndexOf("{Real");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, realOpenPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var realClosePos = text.IndexOf("Real}") + 4;
        Assert.Contains(tags, t => t.Span.Start.Position == realOpenPos);
        Assert.Contains(tags, t => t.Span.Start.Position == realClosePos);
    }

    #endregion
}