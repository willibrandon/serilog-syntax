using Microsoft.VisualStudio.Text;
using SerilogSyntax.Tagging;
using SerilogSyntax.Tests.TestHelpers;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Tagging;

public class MultiLineBraceMatcherTests
{
    [Fact]
    public void BraceMatching_InRawStringLiteral_MatchesAcrossLines()
    {
        var text = @"
logger.LogInformation(""""""
    Processing record:
    ID: {RecordId}
    Status: {Status}
    """""", recordId, status);";

        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Test opening brace of {RecordId}
        var openBracePos = text.IndexOf("{RecordId");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count); // Opening and closing brace
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos); // Opening {
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 9); // Closing }
    }

    [Fact]
    public void BraceMatching_InVerbatimString_MatchesAcrossLines()
    {
        var text = @"
logger.LogInformation(@""Processing:
    User: {UserName}
    ID: {UserId}
    Time: {Timestamp}"", userName, userId, timestamp);";

        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Test closing brace of {UserName}
        var closeBracePos = text.IndexOf("UserName}") + 8;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var openPos = text.IndexOf("{UserName");
        Assert.Contains(tags, t => t.Span.Start.Position == openPos);
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }

    [Fact]
    public void BraceMatching_PropertySpansMultipleLines_MatchesCorrectly()
    {
        // This is an edge case where a property with formatting spans lines
        var text = @"
logger.LogInformation(""""""
    Value: {Amount,
            10:C2}
    """""", amount);";

        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace
        var openBracePos = text.IndexOf("{Amount");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var closePos = text.IndexOf(":C2}") + 3;
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == closePos);
    }

    [Fact]
    public void BraceMatching_EscapedBracesInMultiLine_IgnoresEscaped()
    {
        var text = @"
logger.LogInformation(""""""
    Use {{double}} for literal
    Property: {Value}
    """""", value);";

        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on {Value}
        var openBracePos = text.IndexOf("{Value");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 6);
    }

    [Fact]
    public void BraceMatching_NotInMultiLineString_UsesSingleLineLogic()
    {
        var text = @"logger.LogInformation(""User {Name} logged in"", name);";

        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        var openBracePos = text.IndexOf("{Name");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 5);
    }

    [Fact]
    public void BraceMatching_CursorAfterClosingBrace_StillMatches()
    {
        var text = @"
logger.LogInformation(""""""
    ID: {RecordId}
    """""", recordId);";

        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position cursor right after the closing }
        var closeBracePos = text.IndexOf("RecordId}") + 8;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos + 1));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var openPos = text.IndexOf("{RecordId");
        Assert.Contains(tags, t => t.Span.Start.Position == openPos);
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }
}