using Microsoft.VisualStudio.Text;
using SerilogSyntax.Tagging;
using SerilogSyntax.Tests.TestHelpers;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Tagging;

public class PropertyArgumentHighlighterTests
{
    [Fact]
    public void PropertyHighlighting_CursorOnProperty_HighlightsBothPropertyAndArgument()
    {
        var text = "Log.Information(\"User {UserId} logged in\", userId);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {UserId} property (position 24 is within {UserId})
        var propertyPos = text.IndexOf("{UserId}") + 2; // Position inside the property
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count); // Should highlight both property and argument

        // Check property highlight
        var propertyStartPos = text.IndexOf("{UserId}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 8); // Length of {UserId}

        // Check argument highlight
        var argumentStartPos = text.IndexOf("userId)");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos && t.Span.Length == 6); // Length of userId
    }

    [Fact]
    public void PropertyHighlighting_CursorOnArgument_HighlightsBothArgumentAndProperty()
    {
        var text = "Log.Information(\"User {UserId} logged in\", userId);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on userId argument
        var argumentPos = text.IndexOf("userId)") + 2; // Position inside the argument
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, argumentPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count); // Should highlight both argument and property

        // Check property highlight
        var propertyStartPos = text.IndexOf("{UserId}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 8);

        // Check argument highlight
        var argumentStartPos = text.IndexOf("userId)");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos && t.Span.Length == 6);
    }

    [Fact]
    public void PropertyHighlighting_MultipleProperties_HighlightsCorrectPair()
    {
        var text = "Log.Information(\"User {UserId} logged in at {Time}\", userId, DateTime.Now);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {Time} property
        var timePos = text.IndexOf("{Time}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, timePos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);

        // Check property highlight
        var propertyStartPos = text.IndexOf("{Time}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 6); // Length of {Time}

        // Check argument highlight (DateTime.Now)
        var argumentStartPos = text.IndexOf("DateTime.Now");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos);
    }

    [Fact]
    public void PropertyHighlighting_PositionalProperty_HighlightsCorrectArgument()
    {
        var text = "Log.Information(\"Value {0} and {1}\", first, second);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {0} property
        var pos0 = text.IndexOf("{0}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, pos0));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);

        // Check property highlight
        var propertyStartPos = text.IndexOf("{0}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 3); // Length of {0}

        // Check argument highlight (first)
        var argumentStartPos = text.IndexOf("first");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos && t.Span.Length == 5); // Length of first
    }

    [Fact]
    public void PropertyHighlighting_DestructuredProperty_HighlightsCorrectly()
    {
        var text = "Log.Information(\"User {@User} logged in\", user);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {@User} property
        var userPos = text.IndexOf("{@User}") + 3;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, userPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);

        // Check property highlight (including @ symbol)
        var propertyStartPos = text.IndexOf("{@User}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 7); // Length of {@User}

        // Check argument highlight
        var argumentStartPos = text.IndexOf("user)");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos && t.Span.Length == 4); // Length of user
    }

    [Fact]
    public void PropertyHighlighting_CursorOutsideTemplate_NoHighlights()
    {
        var text = "Log.Information(\"User {UserId} logged in\", userId);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor outside template and arguments (in "Log.Information")
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, 5));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tags); // No highlights when cursor is not on property or argument
    }

    [Fact]
    public void PropertyHighlighting_MultiLineTemplate_HighlightsCorrectly()
    {
        var lines = new[]
        {
            "Log.Information(",
            "    \"User {UserId} logged in\",",
            "    userId);"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {UserId} property
        var propertyPos = text.IndexOf("{UserId}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count); // Should highlight both property and argument
    }

    [Fact]
    public void PropertyHighlighting_VerbatimString_HighlightsCorrectly()
    {
        var text = "Log.Information(@\"User {UserId} logged in\", userId);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {UserId} property
        var propertyPos = text.IndexOf("{UserId}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void PropertyHighlighting_AfterEscKeyDismissal_NoHighlights()
    {
        var text = "Log.Information(\"User {UserId} logged in\", userId);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on property and get initial highlights
        var propertyPos = text.IndexOf("{UserId}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var initialTags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();
        Assert.Equal(2, initialTags.Count);

        // Simulate ESC key dismissal
        state.DisableHighlights();

        var tagsAfterEsc = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tagsAfterEsc); // No highlights after ESC dismissal
    }

    [Fact]
    public void PropertyHighlighting_LogErrorWithException_HighlightsCorrectArgument()
    {
        var text = "logger.LogError(ex, \"Failed to process {ItemId}\", itemId);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {ItemId} property
        var propertyPos = text.IndexOf("{ItemId}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);

        // Check that it highlights itemId (not ex)
        var argumentStartPos = text.IndexOf("itemId)");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos);
    }

    [Fact]
    public void PropertyHighlighting_NotSerilogCall_NoHighlights()
    {
        var text = "Console.WriteLine(\"User {Name} logged in\");";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on what looks like a property but isn't in a Serilog call
        var propertyPos = text.IndexOf("{Name}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Empty(tags); // No highlights for non-Serilog calls
    }

    [Fact]
    public void PropertyHighlighting_MultiLineCall_CursorOnArgumentLine_HighlightsBothPropertyAndArgument()
    {
        // This test captures the real Visual Studio behavior where arguments are on a different line
        var lines = new[]
        {
            "logger.LogInformation(\"User {UserId} logged in\",",
            "    userId);"  // Cursor will be on this line, on 'userId'
        };
        var text = string.Join("\r\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on 'userId' which is on line 2
        var userIdPos = text.IndexOf("userId") + 2; // Position inside 'userId'
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, userIdPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // THIS SHOULD HIGHLIGHT BOTH BUT CURRENTLY DOESN'T
        Assert.Equal(2, tags.Count); // Should highlight both property and argument

        // Check property highlight
        var propertyStartPos = text.IndexOf("{UserId}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 8);

        // Check argument highlight
        var argumentStartPos = text.IndexOf("userId)");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos && t.Span.Length == 6);
    }

    [Fact]
    public void PropertyHighlighting_VerbatimStringMultiLine_CursorOnArgumentLine_HighlightsBothPropertyAndArgument()
    {
        // Test with verbatim string spanning multiple lines
        var lines = new[]
        {
            "logger.LogInformation(@\"Processing files in path: {FilePath}",
            "With properties like {UserId}\",",
            "    filePath, userId);"  // Cursor will be on this line
        };
        var text = string.Join("\r\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on 'filePath' which is on line 3
        var filePathPos = text.IndexOf("filePath,") + 4; // Position inside 'filePath'
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, filePathPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should highlight both filePath argument and {FilePath} property
        Assert.Equal(2, tags.Count);

        // Check property highlight
        var propertyStartPos = text.IndexOf("{FilePath}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos);

        // Check argument highlight
        var argumentStartPos = text.IndexOf("filePath,");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos);
    }

    [Fact]
    public void PropertyHighlighting_VeryLongMultiLineVerbatimString_CursorOnProperty_HighlightsBothPropertyAndArgument()
    {
        // EXACT scenario from Visual Studio
        var text = @"        // 5. Very long multi-line verbatim string
        var version = ""1.0.0"";
        var env = ""Production"";
        var sessionId = Guid.NewGuid();
        logger.LogInformation(@""
===============================================
Application: {AppName}
Version: {Version}
Environment: {Environment}
===============================================
User: {UserName} (ID: {UserId})
Session: {SessionId}
Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}
===============================================
"", appName, version, env, userName, userId, sessionId, DateTime.Now);";

        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {AppName} property
        var propertyPos = text.IndexOf("{AppName}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should highlight both property and argument
        Assert.Equal(2, tags.Count);

        // Check property highlight
        var propertyStartPos = text.IndexOf("{AppName}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 9); // Length of {AppName}

        // Check argument highlight (appName)
        var argumentStartPos = text.IndexOf("appName,");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos && t.Span.Length == 7); // Length of appName
    }

    [Fact]
    public void PropertyHighlighting_RawStringWithCustomDelimiter_CursorOnProperty_HighlightsBothPropertyAndArgument()
    {
        // EXACT scenario - Raw string with custom delimiter (4+ quotes)
        var text = @"// 4. Raw string with custom delimiter (4+ quotes)
var data = ""test-data"";
logger.LogInformation(""""""""
    Template with """""" inside: {Data}
    This allows literal triple quotes in the string
    """""""", data);";

        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {Data} property
        var propertyPos = text.IndexOf("{Data}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should highlight both property and argument
        Assert.Equal(2, tags.Count);

        // Check property highlight
        var propertyStartPos = text.IndexOf("{Data}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 6); // Length of {Data}

        // Check argument highlight
        var argumentStartPos = text.IndexOf("data);");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos && t.Span.Length == 4); // Length of data
    }

    [Fact]
    public void PropertyHighlighting_MultiLineLogErrorWithException_CursorOnProperty_HighlightsBothPropertyAndArgument()
    {
        // EXACT scenario - Multi-line LogError call with exception parameter
        var text = @"// Example 3: Multi-line LogError call (for testing navigation)
logger.LogError(new Exception(""Connection timeout""),
    ""Processing failed for {UserId} with {ErrorCode}"",
    userId,
    errorCode);";

        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {UserId} property
        var propertyPos = text.IndexOf("{UserId}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should highlight both property and argument
        Assert.Equal(2, tags.Count);

        // Check property highlight
        var propertyStartPos = text.IndexOf("{UserId}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 8); // Length of {UserId}

        // Check argument highlight
        var argumentStartPos = text.IndexOf("userId,");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos && t.Span.Length == 6); // Length of userId
    }

    [Fact]
    public void PropertyHighlighting_MultiLineLogErrorWithException_CursorOnArgument_HighlightsBothArgumentAndProperty()
    {
        // EXACT scenario - Cursor on argument in multi-line LogError call
        var text = @"// Example 3: Multi-line LogError call (for testing navigation)
logger.LogError(new Exception(""Connection timeout""),
    ""Processing failed for {UserId} with {ErrorCode}"",
    userId,
    errorCode);";

        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on errorCode argument
        var argumentPos = text.IndexOf("errorCode") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, argumentPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should highlight both argument and property
        Assert.Equal(2, tags.Count);

        // Check argument highlight
        var argumentStartPos = text.IndexOf("errorCode");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos && t.Span.Length == 9); // Length of errorCode

        // Check property highlight
        var propertyStartPos = text.IndexOf("{ErrorCode}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 11); // Length of {ErrorCode}
    }

    [Fact]
    public void PropertyHighlighting_StringifiedProperty_HighlightsCorrectly()
    {
        var text = "Log.Information(\"Value: {$Value}\", value);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {$Value} property
        var propertyPos = text.IndexOf("{$Value}") + 3;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);

        // Check property highlight
        var propertyStartPos = text.IndexOf("{$Value}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 8); // Length of {$Value}

        // Check argument highlight
        var argumentStartPos = text.IndexOf("value)");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos && t.Span.Length == 5); // Length of value
    }

    [Fact]
    public void PropertyHighlighting_ComplexPropertyWithFormatting_HighlightsCorrectly()
    {
        var text = "Log.Information(\"Price: {Price,10:C2}\", price);";
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {Price,10:C2} property
        var propertyPos = text.IndexOf("{Price") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propertyPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);

        // Check property highlight (entire {Price,10:C2})
        var propertyStartPos = text.IndexOf("{Price");
        var propertyLength = "{Price,10:C2}".Length;
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == propertyLength);

        // Check argument highlight
        var argumentStartPos = text.IndexOf("price)");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos);
    }

    [Fact]
    public void PropertyHighlighting_RawStringLiteral_HighlightsCorrectly()
    {
        var lines = new[]
        {
            "Log.Information(\"\"\"User {Name}",
            "    logged in at {Timestamp}",
            "    \"\"\", name, DateTime.Now);"
        };
        var text = string.Join("\n", lines);
        var buffer = new MockTextBuffer(text);
        var view = new MockTextView(buffer);
        var state = new PropertyArgumentHighlightState(view);
        var highlighter = new PropertyArgumentHighlighter(view, buffer, state);

        // Position cursor on {Name} property
        var namePos = text.IndexOf("{Name}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, namePos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);

        // Check property highlight
        var propertyStartPos = text.IndexOf("{Name}");
        Assert.Contains(tags, t => t.Span.Start.Position == propertyStartPos && t.Span.Length == 6);

        // Check argument highlight
        var argumentStartPos = text.IndexOf("name,");
        Assert.Contains(tags, t => t.Span.Start.Position == argumentStartPos);
    }

    [Fact]
    public void PropertyHighlightState_StateChangedEvent_FiresOnStateChange()
    {
        var view = new MockTextView(MockTextBuffer.Create("test"));
        var state = new PropertyArgumentHighlightState(view);
        bool eventFired = false;

        state.StateChanged += (sender, e) => eventFired = true;
        state.ClearHighlights();

        Assert.True(eventFired);
    }

    [Fact]
    public void PropertyHighlightState_HasHighlights_ReturnsFalseWhenEmpty()
    {
        var view = new MockTextView(MockTextBuffer.Create("test"));
        var state = new PropertyArgumentHighlightState(view);

        Assert.False(state.HasHighlights());
    }

    [Fact]
    public void PropertyHighlightState_EnableHighlights_ClearsDisabledFlag()
    {
        var view = new MockTextView(MockTextBuffer.Create("test"));
        var state = new PropertyArgumentHighlightState(view);

        state.EnableHighlights();

        Assert.False(state.IsDisabled);
    }
}