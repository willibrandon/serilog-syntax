using Microsoft.VisualStudio.Text;
using SerilogSyntax.Tagging;
using SerilogSyntax.Tests.TestHelpers;
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
}