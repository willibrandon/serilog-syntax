using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using SerilogSyntax.Tagging;
using SerilogSyntax.Tests.TestHelpers;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Tagging;

public class PropertyArgumentHighlighterTests
{
    [Fact]
    public void HighlightProperty_WhenCursorOnSimpleProperty_HighlightsPropertyAndArgument()
    {
        // Arrange
        var text = @"logger.LogInformation(""User {UserId} logged in"", userId);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {UserId}
        var cursorPosition = text.IndexOf("{UserId}") + 1; // Inside the property
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count); // Should highlight property and argument
    }

    [Fact]
    public void HighlightArgument_WhenCursorOnArgument_HighlightsArgumentAndProperty()
    {
        // Arrange
        var text = @"logger.LogInformation(""User {UserId} logged in"", userId);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on userId argument
        var cursorPosition = text.IndexOf(", userId") + 2; // On the argument
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count); // Should highlight argument and property
    }

    [Fact]
    public void HighlightMultipleProperties_SingleLine()
    {
        // Arrange
        var text = @"logger.LogInformation(""User {UserId} ({UserName}) placed {OrderCount} orders totaling {TotalAmount:C}"", userId, userName, orderCount, totalAmount);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Test each property
        var properties = new[] { "{UserId}", "{UserName}", "{OrderCount}", "{TotalAmount:C}" };
        var arguments = new[] { "userId", "userName", "orderCount", "totalAmount" };

        for (int i = 0; i < properties.Length; i++)
        {
            // Act - cursor on property
            var propPosition = text.IndexOf(properties[i]) + 1;
            view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, propPosition));

            var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
                new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

            // Assert
            Assert.Equal(2, tags.Count); // Property and its argument
        }
    }

    [Fact]
    public void HighlightMultipleProperties_MultiLine()
    {
        // Arrange
        var text = @"logger.LogInformation(""User {UserId} ({UserName}) placed {OrderCount} orders totaling {TotalAmount:C}"",
    userId, userName, orderCount, totalAmount);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Test cursor on second line argument
        var cursorPosition = text.IndexOf("userName");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count); // Should highlight userName and {UserName}
    }

    [Fact]
    public void HighlightDestructuredProperty()
    {
        // Arrange
        var text = @"logger.LogInformation(""Processing order {@Order} at {Timestamp:HH:mm:ss}"", order, timestamp);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {@Order}
        var cursorPosition = text.IndexOf("{@Order}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void HighlightStringifiedProperty()
    {
        // Arrange
        var text = @"logger.LogWarning(""Application version {$AppVersion} using legacy format"", appVersion);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {$AppVersion}
        var cursorPosition = text.IndexOf("{$AppVersion}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void HighlightPropertyWithFormatSpecifier()
    {
        // Arrange
        var text = @"logger.LogInformation(""Current time: {Timestamp:yyyy-MM-dd HH:mm:ss}"", now);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on formatted property
        var cursorPosition = text.IndexOf("{Timestamp:") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void HighlightPropertyWithAlignment()
    {
        // Arrange
        var text = @"logger.LogInformation(""Product {Product,-15} | Units: {Units,5}"", productName, units);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on aligned property
        var cursorPosition = text.IndexOf("{Product,-15}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void HighlightVerbatimStringTemplate()
    {
        // Arrange
        var text = @"logger.LogInformation(@""Processing files in path: {FilePath}
Multiple lines are supported"", filePath);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on property in verbatim string
        var cursorPosition = text.IndexOf("{FilePath}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void HighlightRawStringLiteral()
    {
        // Arrange
        var text = @"logger.LogInformation(""""""
Raw String Report:
Record: {RecordId} | Status: {Status}
"""""", recordId, status);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on property in raw string
        var cursorPosition = text.IndexOf("{RecordId}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void HighlightRawStringLiteral_WithMultipleProperties()
    {
        // Arrange - exactly like the failing scenario
        var text = @"var recordId = ""REC-2024"";
var status = ""Processing"";
logger.LogInformation(""""""
    Raw String Report:
    Record: {RecordId} | Status: {Status,-12}
    User: {UserName} (ID: {UserId})
    Order: {@Order}
    Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}
    """""", recordId, status, userName, userId, order, timestamp);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Test cursor on first property {RecordId}
        var recordIdPos = text.IndexOf("{RecordId}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, recordIdPos));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        Assert.Equal(2, tags.Count);

        // Verify the property span is correct
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == text.IndexOf("{RecordId}"));
        Assert.NotNull(propertyTag);
        Assert.Equal(10, propertyTag.Span.Length); // Length of "{RecordId}"

        // Verify the argument span is correct
        var argTag = tags.FirstOrDefault(t => t.Span.Start == text.IndexOf(", recordId") + 2);
        Assert.NotNull(argTag);
    }

    [Fact]
    public void HighlightLoggerBeginScope()
    {
        // Arrange
        var text = @"using (logger.BeginScope(""Operation={Operation} RequestId={RequestId}"", ""DataExport"", Guid.NewGuid()))";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on first property
        var cursorPosition = text.IndexOf("{Operation}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void HighlightLogErrorWithException()
    {
        // Arrange
        var text = @"logger.LogError(ex, ""File not found: {FileName} in directory {Directory}"", ex.FileName, Path.GetDirectoryName(ex.FileName));";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on property
        var cursorPosition = text.IndexOf("{FileName}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void NoHighlight_WhenNotSerilogCall()
    {
        // Arrange
        var text = @"Console.WriteLine(""User {0} logged in"", userId);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act
        var cursorPosition = text.IndexOf("{0}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Empty(tags);
    }

    [Fact]
    public void HighlightRawStringLiteral_VerifyNoOffsetError()
    {
        // This test captures the exact issue from the screenshot where
        // "d: {Record" was highlighted instead of "{RecordId}"
        // The bug is a 3-character offset in raw string literals
        var text = @"var recordId = ""REC-2024"";
var status = ""Processing"";
logger.LogInformation(""""""
    Raw String Report:
    Record: {RecordId} | Status: {Status,-12}
    """""", recordId, status);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {RecordId}
        var cursorPosition = text.IndexOf("{RecordId}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // CRITICAL: The property must be highlighted at exactly the right position
        // Not 3 characters before (which would highlight "d: {Record")
        var expectedStart = text.IndexOf("{RecordId}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedStart);

        Assert.NotNull(propertyTag); // This will fail if there's an offset
        Assert.Equal(10, propertyTag.Span.Length); // Length of "{RecordId}"

        // Also verify the text being highlighted is correct
        var highlightedText = text.Substring(propertyTag.Span.Start, propertyTag.Span.Length);
        Assert.Equal("{RecordId}", highlightedText);
    }

    [Fact]
    public void HighlightAnonymousObjectArgument_FullObject()
    {
        // This test captures the issue where anonymous objects are not fully highlighted
        // When cursor is on {@Customer}, it should highlight the entire anonymous object
        // including all properties, not just up to the first closing brace
        var text = @"expressionLogger.Information(""Order {OrderId} processed successfully for customer {@Customer} in {Duration}ms"",
    ""ORD-2024-0042"", new { Name = ""Bob Smith"", Tier = ""Premium"" }, 127);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {@Customer}
        var cursorPosition = text.IndexOf("{@Customer}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // Find the argument tag (should be the anonymous object)
        var anonymousObjectStart = text.IndexOf("new { Name");
        var anonymousObjectEnd = text.IndexOf("Premium\" }") + "Premium\" }".Length;
        var expectedLength = anonymousObjectEnd - anonymousObjectStart;

        // The argument tag should cover the entire anonymous object
        var argTag = tags.FirstOrDefault(t => t.Span.Start == anonymousObjectStart);
        Assert.NotNull(argTag); // This will fail if not found at expected position

        // This assertion will fail if the highlighting stops at the first }
        // The expected length should be the full "new { Name = "Bob Smith", Tier = "Premium" }"
        // not just "new { Name = "Bob Smith" }"
        Assert.Equal(expectedLength, argTag.Span.Length);

        // Also verify we're highlighting the full text
        var highlightedText = text.Substring(argTag.Span.Start, argTag.Span.Length);
        Assert.Equal(@"new { Name = ""Bob Smith"", Tier = ""Premium"" }", highlightedText);
    }

    [Fact]
    public void HighlightNestedAnonymousObject()
    {
        // Test with nested anonymous objects
        var text = @"logger.LogInformation(""User {@User} with settings {@Settings}"",
    new { Id = 42, Profile = new { Name = ""Alice"", Level = 5 } },
    new { Theme = ""dark"", Features = new[] { ""A"", ""B"" } });";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {@User}
        var cursorPosition = text.IndexOf("{@User}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // Verify the first nested object is fully highlighted
        var firstObjectStart = text.IndexOf("new { Id = 42");
        var firstObjectEnd = text.IndexOf("Level = 5 } }") + "Level = 5 } }".Length;
        var expectedLength = firstObjectEnd - firstObjectStart;

        var argTag = tags.FirstOrDefault(t => t.Span.Start == firstObjectStart);
        Assert.NotNull(argTag);
        Assert.Equal(expectedLength, argTag.Span.Length);
    }

    [Fact]
    public void HighlightPositionalParameters_VerbatimString()
    {
        // This test captures the issue where positional parameters {0}, {1} etc.
        // are not highlighted at all, especially in verbatim strings
        var text = @"var userId = 42;
logger.LogInformation(@""Database query:
SELECT * FROM Users WHERE Id = {0} AND Status = {1}
Parameters: {0}, {1}"", userId, ""Active"");";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on first {0}
        var cursorPosition = text.IndexOf("{0}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert - should highlight {0} and userId
        Assert.Equal(2, tags.Count);

        // Verify {0} is highlighted
        var expectedPropStart = text.IndexOf("{0}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);
        Assert.Equal(3, propertyTag.Span.Length); // Length of "{0}"

        // Verify userId argument is highlighted
        var expectedArgStart = text.IndexOf(", userId") + 2;
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);
        Assert.Equal(6, argTag.Span.Length); // Length of "userId"
    }

    [Fact]
    public void HighlightPositionalParameters_MultipleOccurrences()
    {
        // Test that all occurrences of the same positional parameter are highlighted
        var text = @"logger.LogInformation(""User {0} logged in at {1}. Session for {0} started."", userName, DateTime.Now);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on first {0}
        var cursorPosition = text.IndexOf("{0}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert - should highlight both {0} occurrences and userName
        // Since we're on {0}, it should highlight the property and its corresponding argument
        Assert.Equal(2, tags.Count);

        // Verify the first {0} is highlighted
        var firstPropStart = text.IndexOf("{0}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == firstPropStart);
        Assert.NotNull(propertyTag);
        Assert.Equal(3, propertyTag.Span.Length);

        // Verify userName is highlighted
        var argStart = text.IndexOf(", userName") + 2;
        var argTag = tags.FirstOrDefault(t => t.Span.Start == argStart);
        Assert.NotNull(argTag);
    }

    [Fact]
    public void HighlightDuplicatePositionalParameters()
    {
        // Test when the same positional parameter appears multiple times
        // Each occurrence should map to a separate argument
        var text = @"var userId = 42;
logger.LogInformation(@""Database query:
SELECT * FROM Users WHERE Id = {0} AND Status = {1}
Parameters: {0}, {1}"", userId, ""Active"", userId, ""Active"");";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on the SECOND {0} (in "Parameters: {0}")
        var firstZeroPos = text.IndexOf("{0}");
        var secondZeroPos = text.IndexOf("{0}", firstZeroPos + 1);
        var cursorPosition = secondZeroPos + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert - should highlight the second {0} and the THIRD argument (second userId)
        Assert.Equal(2, tags.Count);

        // Verify the second {0} is highlighted
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == secondZeroPos);
        Assert.NotNull(propertyTag);
        Assert.Equal(3, propertyTag.Span.Length); // Length of "{0}"

        // Verify the THIRD argument (second userId) is highlighted
        // Arguments are: userId, "Active", userId, "Active"
        // The second {0} should map to the third argument (index 2)
        // Find ", userId" after ", ""Active"", "
        var activeArg = text.IndexOf(@", ""Active"", ");
        var thirdArgStart = activeArg + @", ""Active"", ".Length;

        var argTag = tags.FirstOrDefault(t => t.Span.Start == thirdArgStart);
        Assert.NotNull(argTag);
        Assert.Equal(6, argTag.Span.Length); // Length of "userId"
    }

    [Fact]
    public void HighlightMixedPositionalAndNamedParameters()
    {
        // Test mixing positional {0} and named {PropertyName} parameters
        // This is actually not recommended in Serilog but should still work
        var text = @"logger.LogInformation(""Item {0} has property {Name} with value {1}"", itemId, itemValue, itemName);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {Name} (named property)
        var cursorPosition = text.IndexOf("{Name}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert - should highlight {Name} and itemName (third argument)
        Assert.Equal(2, tags.Count);

        // Verify {Name} is highlighted
        var expectedPropStart = text.IndexOf("{Name}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);

        // Verify the correct argument is highlighted
        // {0} -> itemId (index 0)
        // {1} -> itemValue (index 1)
        // {Name} -> itemName (index 2, which is maxPositionalIndex(1) + 1 + 0)
        var expectedArgStart = text.IndexOf(", itemName") + 2;
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);
        Assert.Equal(8, argTag.Span.Length); // Length of "itemName"
    }

    [Fact]
    public void HighlightVerbatimStringWithEscapedQuotes()
    {
        // Test verbatim strings with escaped quotes ("")
        // This captures the off-by-1 error where escaped quotes throw off the position calculation
        var text = @"var userName = ""Alice"";
logger.LogInformation(@""XML: <user name=""""{UserName}"""" id=""""{UserId}"""" />"",
    userName, userId);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {UserName}
        var cursorPosition = text.IndexOf("{UserName}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // Verify {UserName} is highlighted at the correct position
        var expectedPropStart = text.IndexOf("{UserName}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);

        // This assertion should fail initially if there's an off-by-1 error
        Assert.NotNull(propertyTag);
        Assert.Equal(10, propertyTag.Span.Length); // Length of "{UserName}"

        // Also verify the highlighted text is correct (not ""{UserName")
        var highlightedText = text.Substring(propertyTag.Span.Start, propertyTag.Span.Length);
        Assert.Equal("{UserName}", highlightedText);

        // Verify the argument is highlighted correctly
        var expectedArgStart = text.IndexOf("userName, userId") ; // First arg after the template
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);
        Assert.Equal(8, argTag.Span.Length); // Length of "userName"
    }

    [Fact]
    public void HighlightVerbatimStringWithManyEscapedQuotes()
    {
        // Test with multiple escaped quotes to ensure the offset doesn't accumulate
        var text = @"logger.LogInformation(@""JSON: { """"name"""": """"{Name}"""", """"value"""": """"{Value}"""" }"", name, value);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {Value} (second property, after many escaped quotes)
        var cursorPosition = text.IndexOf("{Value}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // Verify {Value} is highlighted at the correct position
        var expectedPropStart = text.IndexOf("{Value}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);

        Assert.NotNull(propertyTag);
        Assert.Equal(7, propertyTag.Span.Length); // Length of "{Value}"

        // Verify correct text is highlighted
        var highlightedText = text.Substring(propertyTag.Span.Start, propertyTag.Span.Length);
        Assert.Equal("{Value}", highlightedText);
    }

    [Fact]
    public void HighlightRawStringWithCustomDelimiter_FourQuotes()
    {
        // Test raw string literals with custom delimiter (4 quotes)
        // This allows literal triple quotes inside the string
        // Note: Using string concatenation to represent 4+ quotes since .NET Framework doesn't support it
        var fourQuotes = "\"\"\"\"";
        var text = "var data = \"test-data\";\n" +
                   "logger.LogInformation(" + fourQuotes + "\n" +
                   "    Template with \"\"\" inside: {Data}\n" +
                   "    This allows literal triple quotes in the string\n" +
                   "    " + fourQuotes + ", data);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {Data}
        var cursorPosition = text.IndexOf("{Data}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert - should highlight {Data} and data argument
        Assert.Equal(2, tags.Count);

        // Verify {Data} is highlighted
        var expectedPropStart = text.IndexOf("{Data}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);
        Assert.Equal(6, propertyTag.Span.Length); // Length of "{Data}"

        // Verify data argument is highlighted
        var expectedArgStart = text.IndexOf(", data") + 2;
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);
        Assert.Equal(4, argTag.Span.Length); // Length of "data"
    }

    [Fact]
    public void HighlightRawStringWithCustomDelimiter_FiveQuotes()
    {
        // Test raw string literals with 5 quotes delimiter
        var fiveQuotes = "\"\"\"\"\"";
        var fourQuotes = "\"\"\"\"";
        var text = "logger.LogInformation(" + fiveQuotes + "\n" +
                   "    Custom delimiter with {Property}\n" +
                   "    Can contain " + fourQuotes + " (four quotes) inside\n" +
                   "    " + fiveQuotes + ", propertyValue);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {Property}
        var cursorPosition = text.IndexOf("{Property}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        var expectedPropStart = text.IndexOf("{Property}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);
        Assert.Equal(10, propertyTag.Span.Length); // Length of "{Property}"
    }

    [Fact]
    public void HighlightRawStringWithCustomDelimiterAndArgument()
    {
        // Test clicking on the argument when using custom delimiter
        var fourQuotes = "\"\"\"\"";
        var text = "var data = \"test-data\";\n" +
                   "logger.LogInformation(" + fourQuotes + "\n" +
                   "    Template with \"\"\" inside: {Data}\n" +
                   "    " + fourQuotes + ", data);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on data argument
        var cursorPosition = text.IndexOf(", data") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert - should highlight data and {Data}
        Assert.Equal(2, tags.Count);

        // Verify data argument is highlighted
        var expectedArgStart = text.IndexOf(", data") + 2;
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);

        // Verify {Data} property is highlighted
        var expectedPropStart = text.IndexOf("{Data}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);
    }

    [Fact]
    public void NoHighlight_RegularStringWithTripleQuotes()
    {
        // Ensure we don't accidentally highlight regular strings that happen to contain """
        var text = @"var description = ""This has \""\"" quotes but isn't a raw string"";
logger.LogInformation(""Normal template {Value}"", description);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {Value}
        var cursorPosition = text.IndexOf("{Value}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Should still highlight normally
        Assert.Equal(2, tags.Count);
    }

    [Fact]
    public void HighlightLogErrorWithException_FirstProperty()
    {
        // Test LogError with exception parameter - first property should map to second argument
        var text = @"try
{
    // Some operation
}
catch (FileNotFoundException ex)
{
    logger.LogError(ex, ""File not found: {FileName} in directory {Directory}"",
        ex.FileName, Path.GetDirectoryName(ex.FileName));
}";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {FileName}
        var cursorPosition = text.IndexOf("{FileName}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // Verify {FileName} is highlighted
        var expectedPropStart = text.IndexOf("{FileName}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);

        // Verify ex.FileName (not Path.GetDirectoryName) is highlighted
        // The first argument after the exception is ex.FileName
        var expectedArgStart = text.IndexOf("ex.FileName, Path");
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);
        Assert.Equal(11, argTag.Span.Length); // Length of "ex.FileName"
    }

    [Fact]
    public void HighlightLogErrorWithException_SecondProperty()
    {
        // Test LogError with exception parameter - second property should map to third argument
        var text = @"logger.LogError(ex, ""File not found: {FileName} in directory {Directory}"",
    ex.FileName, Path.GetDirectoryName(ex.FileName));";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {Directory}
        var cursorPosition = text.IndexOf("{Directory}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // Verify {Directory} is highlighted
        var expectedPropStart = text.IndexOf("{Directory}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);

        // Verify Path.GetDirectoryName(ex.FileName) is highlighted
        var expectedArgStart = text.IndexOf("Path.GetDirectoryName");
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);
        // The argument is the full method call
        Assert.True(argTag.Span.Length > 20); // Should be longer than just "Path.GetDirectoryName"
    }

    [Fact]
    public void HighlightLogErrorWithException_DifferentLogError()
    {
        // Test second LogError in a different catch block
        var text = @"catch (Exception ex)
{
    logger.LogError(ex, ""Unexpected error during operation with file {FileName}"", ""important-file.txt"");
}";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {FileName}
        var cursorPosition = text.IndexOf("{FileName}") + 1;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // Verify {FileName} is highlighted
        var expectedPropStart = text.IndexOf("{FileName}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);

        // Verify "important-file.txt" is highlighted
        var expectedArgStart = text.IndexOf(@"""important-file.txt""");
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);
        Assert.Equal(20, argTag.Span.Length); // Length of "important-file.txt" with quotes (1 + 18 + 1 = 20)
    }

    [Fact]
    public void HighlightLogErrorWithException_ClickOnArgument()
    {
        // Test clicking on the argument in LogError with exception
        var text = @"logger.LogError(ex, ""File not found: {FileName}"", ex.FileName);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on ex.FileName argument
        var cursorPosition = text.IndexOf("ex.FileName");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // Verify ex.FileName is highlighted
        var expectedArgStart = text.IndexOf("ex.FileName");
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);

        // Verify {FileName} is highlighted
        var expectedPropStart = text.IndexOf("{FileName}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);
    }

    [Fact]
    public void HighlightForContextWithCollectionExpression()
    {
        // Test ForContext<T>().Information with collection expression argument
        var text = @"log.ForContext<Program>()
    .Information(""Cart contains {@Items}"", [""Tea"", ""Coffee""]);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on {@Items}
        var cursorPosition = text.IndexOf("{@Items}") + 2;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // Verify {@Items} is highlighted
        var expectedPropStart = text.IndexOf("{@Items}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);
        Assert.Equal(8, propertyTag.Span.Length); // Length of "{@Items}"

        // Verify ["Tea", "Coffee"] is highlighted
        var expectedArgStart = text.IndexOf(@"[""Tea"", ""Coffee""]");
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);
        Assert.Equal(17, argTag.Span.Length); // Length of ["Tea", "Coffee"]
    }

    [Fact]
    public void HighlightForContextWithCollectionExpression_ClickOnArgument()
    {
        // Test clicking on the collection expression argument
        var text = @"log.ForContext<Program>()
    .Information(""Cart contains {@Items}"", [""Apricots""]);";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor inside ["Apricots"]
        var cursorPosition = text.IndexOf(@"[""Apricots""]") + 5;
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count);

        // Verify {@Items} is highlighted
        var expectedPropStart = text.IndexOf("{@Items}");
        var propertyTag = tags.FirstOrDefault(t => t.Span.Start == expectedPropStart);
        Assert.NotNull(propertyTag);

        // Verify ["Apricots"] is highlighted
        var expectedArgStart = text.IndexOf(@"[""Apricots""]");
        var argTag = tags.FirstOrDefault(t => t.Span.Start == expectedArgStart);
        Assert.NotNull(argTag);
        Assert.Equal(12, argTag.Span.Length); // Length of ["Apricots"]
    }

    [Fact]
    public void HighlightComplexNestedArguments()
    {
        // Arrange
        var text = @"logger.LogInformation(""Processing {Count} items for user {UserId}"", items.Count(), GetUserId());";
        var buffer = MockTextBuffer.Create(text);
        var view = new MockTextView(buffer);
        var highlighter = new PropertyArgumentHighlighter(view, buffer);

        // Act - cursor on first argument
        var cursorPosition = text.IndexOf("items.Count()");
        view.Caret.MoveTo(new SnapshotPoint(buffer.CurrentSnapshot, cursorPosition));

        var tags = highlighter.GetTags(new NormalizedSnapshotSpanCollection(
            new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length))).ToList();

        // Assert
        Assert.Equal(2, tags.Count); // Should highlight items.Count() and {Count}
    }
}