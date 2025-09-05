using Microsoft.VisualStudio.Text;
using SerilogSyntax.Tagging;
using SerilogSyntax.Tests.TestHelpers;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Tagging;

public class ExpressionBraceMatcherTests
{
    [Fact]
    public void ExpressionBraceMatching_SimpleBuiltinProperty_MatchesCorrectly()
    {
        var text = "new ExpressionTemplate(\"{@t:HH:mm:ss}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of {@t:HH:mm:ss}
        var openBracePos = text.IndexOf("{@t");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count); // Opening and closing brace
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos); // Opening {
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 12); // Closing }
    }

    [Fact]
    public void ExpressionBraceMatching_IfDirective_MatchesCorrectly()
    {
        var text = "new ExpressionTemplate(\"{#if Level = 'Error'}ERROR{#end}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of {#if...}
        var openBracePos = text.IndexOf("{#if");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos); // Opening {
        var closingBracePos = text.IndexOf("}ERROR");
        Assert.Contains(tags, t => t.Span.Start.Position == closingBracePos); // Closing }
    }

    [Fact]
    public void ExpressionBraceMatching_NestedBraces_MatchesOuterPair()
    {
        var text = "new ExpressionTemplate(\"{#if @p['RequestId'] is not null}[{@p['RequestId']}]{#end}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of outer {#if...}
        var outerOpenPos = text.IndexOf("{#if");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, outerOpenPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == outerOpenPos);
        
        // Should match to the } before [
        var outerClosePos = text.IndexOf("}[");
        Assert.Contains(tags, t => t.Span.Start.Position == outerClosePos);
    }

    [Fact]
    public void ExpressionBraceMatching_NestedBraces_MatchesInnerPair()
    {
        var text = "new ExpressionTemplate(\"{#if @p['RequestId'] is not null}[{@p['RequestId']}]{#end}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of inner {@p['RequestId']}
        var innerOpenPos = text.IndexOf("{@p['RequestId']");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, innerOpenPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == innerOpenPos);
        
        // Should match to the } after 'RequestId']
        var innerClosePos = text.IndexOf("}]");
        Assert.Contains(tags, t => t.Span.Start.Position == innerClosePos);
    }

    [Fact]
    public void ExpressionBraceMatching_EachLoop_MatchesCorrectly()
    {
        var text = "new ExpressionTemplate(\"{#each name, value in @p} | {name}={value}{#end}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of {#each...}
        var eachOpenPos = text.IndexOf("{#each");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, eachOpenPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == eachOpenPos);
        
        // Should match to the } after @p
        var eachClosePos = text.IndexOf("} |");
        Assert.Contains(tags, t => t.Span.Start.Position == eachClosePos);
    }

    [Fact]
    public void ExpressionBraceMatching_EachLoopVariable_MatchesCorrectly()
    {
        var text = "new ExpressionTemplate(\"{#each name, value in @p} | {name}={value}{#end}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of {name}
        var nameOpenPos = text.IndexOf("{name}");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, nameOpenPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == nameOpenPos);
        Assert.Contains(tags, t => t.Span.Start.Position == nameOpenPos + 5); // Closing }
    }

    [Fact]
    public void ExpressionBraceMatching_ComplexNesting_HandlesCorrectly()
    {
        var text = "new ExpressionTemplate(\"{#if Level = 'Error'}[ERROR]{#else if Level = 'Warning'}[WARN]{#else}[INFO]{#end} {@m}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of {#else if...}
        var elseIfPos = text.IndexOf("{#else if");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, elseIfPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == elseIfPos);
        
        // Should match to the } after 'Warning'
        var elseIfClosePos = text.IndexOf("}[WARN]");
        Assert.Contains(tags, t => t.Span.Start.Position == elseIfClosePos);
    }

    [Fact]
    public void ExpressionBraceMatching_IndexerSyntax_MatchesCorrectly()
    {
        var text = "new ExpressionTemplate(\"{@p['RequestId']}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace
        var openBracePos = text.IndexOf("{@p");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        var closeBracePos = text.IndexOf("}\"");
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }

    [Fact]
    public void ExpressionBraceMatching_CursorAfterOpeningBrace_NoMatch()
    {
        var text = "new ExpressionTemplate(\"{@t:HH:mm:ss}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position cursor just after opening brace - per VS standard, should NOT match
        var openBracePos = text.IndexOf("{@t");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos + 1));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should not match when cursor is after opening brace (VS standard)
        Assert.Empty(tags);
    }

    [Fact]
    public void ExpressionBraceMatching_CursorAfterClosingBrace_Matches()
    {
        var text = "new ExpressionTemplate(\"{@t:HH:mm:ss}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position cursor just after closing brace - per VS standard, SHOULD match
        var closeBracePos = text.IndexOf("}\"");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos + 1));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should match when cursor is after closing brace (VS standard)
        Assert.Equal(2, tags.Count);
        var openBracePos = text.IndexOf("{@t");
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }

    [Fact]
    public void ExpressionBraceMatching_CursorBeforeClosingBrace_NoMatch()
    {
        var text = "new ExpressionTemplate(\"{@t:HH:mm:ss}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position cursor ON closing brace (to its left) - per VS standard, should NOT match
        var closeBracePos = text.IndexOf("}\"");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should not match when cursor is to the left of closing brace (VS standard)
        Assert.Empty(tags);
    }

    [Fact]
    public void ExpressionBraceMatching_RegularTemplate_AlsoMatchesCorrectly()
    {
        var text = "Log.Information(\"Hello {Name}\")"; // Regular template, not expression
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on brace in regular template
        var bracePos = text.IndexOf("{Name");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, bracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Combined matcher should match regular Serilog template braces too
        Assert.Equal(2, tags.Count); 
        Assert.Contains(tags, t => t.Span.Start.Position == bracePos); // Opening {
        Assert.Contains(tags, t => t.Span.Start.Position == bracePos + 5); // Closing }
    }

    [Fact]
    public void ExpressionBraceMatching_EscapedBraces_IgnoresEscaped()
    {
        var text = "new ExpressionTemplate(\"{{escaped}} {@t}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of {@t}
        var realBracePos = text.IndexOf("{@t");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, realBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == realBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == realBracePos + 3); // Closing }
        
        // Should not match escaped braces
        var escapedPos = text.IndexOf("{{");
        Assert.DoesNotContain(tags, t => t.Span.Start.Position == escapedPos);
    }

    [Fact]
    public void ExpressionBraceMatching_UnmatchedBrace_NoMatching()
    {
        var text = "new ExpressionTemplate(\"{@t:HH:mm:ss\")"; // Missing closing brace
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace
        var openBracePos = text.IndexOf("{@t");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tags); // Should not match unmatched braces
    }

    [Fact]
    public void ExpressionBraceMatching_MultipleProperties_OnlyHighlightsCurrentPair()
    {
        var text = "new ExpressionTemplate(\"{@t:HH:mm:ss} {Level} {@m}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on first property's opening brace
        var firstOpenPos = text.IndexOf("{@t");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, firstOpenPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count); // Only the current pair
        Assert.Contains(tags, t => t.Span.Start.Position == firstOpenPos);
        Assert.Contains(tags, t => t.Span.Start.Position == firstOpenPos + 12);

        // Move to AFTER second property's closing brace (VS standard)
        var secondClosePos = text.IndexOf("} {@m");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, secondClosePos + 1));
        
        tags = [.. matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length)))];

        Assert.Equal(2, tags.Count); // Only the second pair
        var secondOpenPos = text.IndexOf("{Level");
        Assert.Contains(tags, t => t.Span.Start.Position == secondOpenPos);
        Assert.Contains(tags, t => t.Span.Start.Position == secondClosePos);
    }

    #region Expression Method Coverage Tests

    [Fact]
    public void ExpressionBraceMatching_MaxExpressionLengthExceeded_NoMatch()
    {
        // Create a very long expression that exceeds MaxExpressionLength (500 chars)
        var longExpression = new string('A', 600);
        var text = $"new ExpressionTemplate(\"{{{longExpression}}}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace
        var openBracePos = text.IndexOf("{A");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should not find match due to exceeding MaxExpressionLength
        Assert.Empty(tags);
    }

    [Fact]
    public void ExpressionBraceMatching_DeepNestedBraces()
    {
        var text = "new ExpressionTemplate(\"{#if Level = 'Error'} then {Value} else {Default} {#end}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on the {Value} opening brace (nested inside the #if)
        var valueOpenPos = text.IndexOf("{Value");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, valueOpenPos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // The implementation should support nested braces in expressions
        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == valueOpenPos);
        Assert.Contains(tags, t => t.Span.Start.Position == valueOpenPos + 6); // Closing } after "Value"
    }

    [Fact]
    public void ExpressionBraceMatching_StringBoundaryStopsSearch()
    {
        var text = "new ExpressionTemplate(\"{@t\" + \"different string {Name}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace that should not match across string boundaries
        var openBracePos = text.IndexOf("{@t");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should not find match due to string boundary (quote before {Name})
        Assert.Empty(tags);
    }

    [Fact]
    public void ExpressionBraceMatching_EscapedQuoteInString()
    {
        var text = "new ExpressionTemplate(\"{@t} with \\\"escaped quote\\\" and {Name}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of {Name}
        var openBracePos = text.IndexOf("{Name");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 5); // Closing }
    }

    [Fact]
    public void ExpressionBraceMatching_EscapedBracesInExpression()
    {
        var text = "new ExpressionTemplate(\"before {{{{ and {RealProp} after\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position on opening brace of {RealProp}
        var openBracePos = text.IndexOf("{RealProp");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, openBracePos));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos + 9); // Closing }
    }

    [Fact]
    public void ExpressionBraceMatching_BackwardSearch_EscapedBraces()
    {
        var text = "new ExpressionTemplate(\"{{{{ {RealProp} }}}}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position AFTER the closing brace of {RealProp} (VS standard)
        var closeBracePos = text.IndexOf("} }}}}");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos + 1));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
        var openBracePos = text.IndexOf("{RealProp");
        Assert.Contains(tags, t => t.Span.Start.Position == openBracePos);
        Assert.Contains(tags, t => t.Span.Start.Position == closeBracePos);
    }

    [Fact]
    public void ExpressionBraceMatching_BackwardSearch_StringBoundary()
    {
        var text = "var x = \"other {thing}\" + new ExpressionTemplate(\"dangling}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position AFTER the dangling closing brace
        var danglingClosePos = text.IndexOf("dangling}") + 8;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, danglingClosePos + 1));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should not match due to string boundary (quote after "other {thing}")
        Assert.Empty(tags);
    }

    [Fact]
    public void ExpressionBraceMatching_MaxLengthBackwardSearch()
    {
        // Create a large gap between braces that exceeds MaxExpressionLength (500)
        var longContent = new string('A', 600);
        var text = $"new ExpressionTemplate(\"{{{longContent}}}\")";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var matcher = new SerilogBraceMatcher(view, buffer);

        // Position AFTER the closing brace
        var closeBracePos = text.LastIndexOf("}");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, closeBracePos + 1));
        
        var tags = matcher.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should not find match due to exceeding MaxExpressionLength in backward search
        Assert.Empty(tags);
    }

    #endregion
}