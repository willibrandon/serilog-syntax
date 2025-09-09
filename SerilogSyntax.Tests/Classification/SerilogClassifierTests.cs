using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Classification
{
    public class SerilogClassifierTests
    {
        private IClassifier CreateClassifier(ITextBuffer textBuffer)
        {
            var registry = MockClassificationTypeRegistry.Create();
            var classifier = new SerilogClassifier(textBuffer, registry);
            return classifier;
        }

        [Fact]
        public void GetClassificationSpans_EmptySpan_ReturnsEmpty()
        {
            // Arrange
            var textBuffer = MockTextBuffer.Create("");
            var classifier = CreateClassifier(textBuffer);
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, 0);

            // Act
            var result = classifier.GetClassificationSpans(span);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void TextChanged_WithRawStringDelimiters_InvalidatesWiderRange()
        {
            // Test text change handler with raw string delimiters - hits lines 97-141
            // Arrange
            var initialCode = @"
var logger = GetLogger();
logger.LogInformation(""Initial message"");";
            
            var textBuffer = MockTextBuffer.Create(initialCode);
            var classifier = CreateClassifier(textBuffer);
            
            // Act - simulate changing to raw string
            var newCode = @"
var logger = GetLogger();
logger.LogInformation(""""""
    Multi-line {Property}
    """""");";
            
            ((MockTextBuffer)textBuffer).Replace(new Span(0, textBuffer.CurrentSnapshot.Length), newCode);
            
            // Assert - the change should be processed
            // We can't directly test the private handler, but we can verify the classifier works after change
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Should find classifications (even if it's just braces)
            Assert.NotEmpty(classifications);
        }

        [Fact]
        public void TextChanged_WithoutRawStringDelimiters_InvalidatesNormalRange()
        {
            // Test text change handler without raw strings - hits lines 143-149
            // Arrange
            var initialCode = @"
var logger = GetLogger();
logger.LogInformation(""Message {OldProp}"");";
            
            var textBuffer = MockTextBuffer.Create(initialCode);
            var classifier = CreateClassifier(textBuffer);
            
            // Act - simulate changing property name (no raw strings)
            var newCode = @"
var logger = GetLogger();
logger.LogInformation(""Message {NewProp}"");";
            
            ((MockTextBuffer)textBuffer).Replace(new Span(0, textBuffer.CurrentSnapshot.Length), newCode);
            
            // Assert - verify classifier works after change
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Should find classifications
            Assert.NotEmpty(classifications);
        }

        [Fact] 
        public void TextChanged_MultipleChanges_ProcessesAllChanges()
        {
            // Test multiple changes in one event
            // Arrange
            var initialCode = @"
logger.LogInformation(""First {Prop1}"");
logger.LogInformation(""Second {Prop2}"");";
            
            var textBuffer = MockTextBuffer.Create(initialCode);
            var classifier = CreateClassifier(textBuffer);
            
            // Act - simulate multiple changes
            var newCode = @"
logger.LogInformation(""First {NewProp1}"");
logger.LogInformation(""""""
    Second {NewProp2}
    """""");";
            
            ((MockTextBuffer)textBuffer).Replace(new Span(0, textBuffer.CurrentSnapshot.Length), newCode);
            
            // Assert - both changes should be processed
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Should find classifications
            Assert.NotEmpty(classifications);
        }

        [Fact]
        public void GetClassificationSpans_SimpleTemplate_ClassifiesCorrectly()
        {
            // Arrange
            var code = @"logger.LogInformation(""User {UserId} logged in"");";
            var textBuffer = MockTextBuffer.Create(code);
            var classifier = CreateClassifier(textBuffer);
            
            // Act
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);
            var classifications = classifier.GetClassificationSpans(span);
            
            // Assert
            Assert.NotEmpty(classifications);
            // Should have classifications for braces and property
            Assert.True(classifications.Count >= 3); // At least 2 braces and 1 property
        }

        [Fact] 
        public void MockTextBuffer_RaisesChangedEvent()
        {
            // Simple test to verify MockTextBuffer event raising works
            var textBuffer = MockTextBuffer.Create("test");
            bool eventRaised = false;
            
            textBuffer.Changed += (sender, args) =>
            {
                eventRaised = true;
            };
            
            ((MockTextBuffer)textBuffer).Replace(new Span(0, 4), "changed");
            
            Assert.True(eventRaised);
        }

        [Fact]
        public void GetClassificationSpans_ForContextWithNewLine_HighlightsDestructuredProperty()
        {
            // Arrange - exact scenario from the screenshot where 'log' uses ForContext<Program>()
            // on one line followed by .Information() on the next line
            var code = @"
log.ForContext<Program>()
    .Information(""Cart contains {@Items}"", [""Tea"", ""Coffee""]);

log.ForContext<Program>()
    .Information(""Cart contains {@Items}"", [""Apricots""]);";

            var textBuffer = MockTextBuffer.Create(code);
            var classifier = CreateClassifier(textBuffer);
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);

            // Act
            var result = classifier.GetClassificationSpans(span);

            // Assert - should find classifications for {@Items} in both calls
            var classifications = result.ToList();
            Assert.NotEmpty(classifications);
            
            // Should find the @ destructuring operator
            var destructuringOps = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.DestructureOperator).ToList();
            Assert.Equal(2, destructuringOps.Count); // One @ for each {@Items}
            
            // Should find the property names
            var properties = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyName).ToList();
            Assert.Equal(2, properties.Count); // One Items for each {@Items}
        }

        [Fact]
        public void GetClassificationSpans_OutputTemplateWithNewLine_HighlightsProperties()
        {
            // Arrange - exact scenario where outputTemplate: is on one line
            // and the template string is on the next line
            var code = @"
using var log = new LoggerConfiguration()
    .Enrich.WithProperty(""Application"", ""Example"")
    .WriteTo.Console(outputTemplate:
        ""[{Timestamp:HH:mm:ss} {Level:u3} ({SourceContext})] {Message:lj} (first item is {FirstItem}){NewLine}{Exception}"")
    .CreateLogger();";

            var textBuffer = MockTextBuffer.Create(code);
            var classifier = CreateClassifier(textBuffer);
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);

            // Act
            var result = classifier.GetClassificationSpans(span);

            // Assert - should find classifications for all template properties
            var classifications = result.ToList();
            Assert.NotEmpty(classifications);
            
            // Should find property names for Timestamp, Level, SourceContext, Message, FirstItem, NewLine, Exception
            var properties = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyName).ToList();
            Assert.True(properties.Count >= 7, $"Expected at least 7 properties but found {properties.Count}");
            
            // Should find format specifiers for HH:mm:ss, u3, lj
            var formatSpecifiers = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.FormatSpecifier).ToList();
            Assert.True(formatSpecifiers.Count >= 3, $"Expected at least 3 format specifiers but found {formatSpecifiers.Count}");
        }

        [Fact]
        public void GetClassificationSpans_LogErrorWithExceptionParameter_HighlightsMessageTemplateProperties()
        {
            // Arrange - LogError with exception parameter followed by message template and arguments
            var code = @"logger.LogError(new Exception(""foo""), ""Error processing {UserId} with {ErrorCode} and {Message}"", userId, errorCode, errorMessage);";
            var textBuffer = MockTextBuffer.Create(code);
            var classifier = CreateClassifier(textBuffer);
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);

            // Act
            var result = classifier.GetClassificationSpans(span);

            // Assert - should find classifications for message template properties, not exception constructor
            var classifications = result.ToList();
            Assert.NotEmpty(classifications);
            
            // Should find property names for UserId, ErrorCode, Message from the message template
            var properties = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyName).ToList();
            Assert.Equal(3, properties.Count); // UserId, ErrorCode, Message
            
            // Should find braces for the three properties
            var braces = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyBrace).ToList();
            Assert.Equal(6, braces.Count); // 3 opening + 3 closing braces
        }

        [Fact]
        public void GetClassificationSpans_LogErrorWithExceptionVariable_HighlightsMessageTemplateProperties()
        {
            // Arrange - LogError with exception variable (not string literal) followed by message template
            var code = @"logger.LogError(new Exception(errorMessage), ""Error processing {UserId} with {ErrorCode} and {Message}"", userId, errorCode, errorMessage);";
            var textBuffer = MockTextBuffer.Create(code);
            var classifier = CreateClassifier(textBuffer);
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);

            // Act
            var result = classifier.GetClassificationSpans(span);

            // Assert - should find classifications for message template properties
            var classifications = result.ToList();
            Assert.NotEmpty(classifications);
            
            // Should find property names for UserId, ErrorCode, Message from the message template
            var properties = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyName).ToList();
            Assert.Equal(3, properties.Count); // UserId, ErrorCode, Message
            
            // Should find braces for the three properties
            var braces = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyBrace).ToList();
            Assert.Equal(6, braces.Count); // 3 opening + 3 closing braces
        }

        [Fact]
        public void GetClassificationSpans_LogErrorWithRawStringException_HighlightsMessageTemplateProperties()
        {
            // Arrange - LogError with raw string literal in exception parameter
            var code = "logger.LogError(new Exception(\"\"\"Database connection failed\"\"\"), \"Error processing {UserId} with {ErrorCode}\", userId, errorCode);";
            var textBuffer = MockTextBuffer.Create(code);
            var classifier = CreateClassifier(textBuffer);
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);

            // Act
            var result = classifier.GetClassificationSpans(span);

            // Assert - should find classifications for message template properties, not exception raw string
            var classifications = result.ToList();
            Assert.NotEmpty(classifications);
            
            // Should find property names for UserId, ErrorCode from the message template
            var properties = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyName).ToList();
            Assert.Equal(2, properties.Count); // UserId, ErrorCode
            
            // Should find braces for the two properties
            var braces = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyBrace).ToList();
            Assert.Equal(4, braces.Count); // 2 opening + 2 closing braces
        }

        [Fact]
        public void GetClassificationSpans_LogErrorWithRawStringContainingProperties_DoesNotHighlightExceptionProperties()
        {
            // Arrange - LogError with raw string literal containing template-like syntax in exception parameter
            // This tests that the raw string properties {ErrorId} and {Details} are NOT highlighted,
            // while the actual message template properties {UserId} and {Action} ARE highlighted
            var code = "logger.LogError(new Exception(\"\"\"Error {ErrorId} occurred with details {Details}\"\"\"), \"User {UserId} performed {Action}\", userId, action);";
            var textBuffer = MockTextBuffer.Create(code);
            var classifier = CreateClassifier(textBuffer);
            var span = new SnapshotSpan(textBuffer.CurrentSnapshot, 0, textBuffer.CurrentSnapshot.Length);

            // Act
            var result = classifier.GetClassificationSpans(span);

            // Assert
            var classifications = result.ToList();
            Assert.NotEmpty(classifications);
            
            // Should find property names for UserId, Action from the message template (not ErrorId, Details from exception)
            var properties = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyName).ToList();
            Assert.Equal(2, properties.Count); // UserId, Action
            
            // Verify the specific property names found
            var propertyTexts = properties.Select(p => textBuffer.CurrentSnapshot.GetText(p.Span)).ToList();
            Assert.Contains("UserId", propertyTexts);
            Assert.Contains("Action", propertyTexts);
            Assert.DoesNotContain("ErrorId", propertyTexts); // Should NOT highlight properties from raw string exception
            Assert.DoesNotContain("Details", propertyTexts); // Should NOT highlight properties from raw string exception
            
            // Should find braces for the two properties
            var braces = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyBrace).ToList();
            Assert.Equal(4, braces.Count); // 2 opening + 2 closing braces for message template only
        }

        [Fact]
        public void GetClassificationSpans_MultiLineLogErrorWithException_HighlightsMessageTemplateProperties()
        {
            // Arrange - Multi-line LogError call with exception parameter spread across multiple lines
            var code = "logger.LogError(new Exception(\"Connection timeout\"), \r\n    \"Processing failed for {UserId} with {ErrorCode}\",\r\n    userId, \r\n    errorCode);";
            var textBuffer = MockTextBuffer.Create(code);
            var classifier = CreateClassifier(textBuffer);

            // Test the specific line that's failing to get highlighted
            var lines = textBuffer.CurrentSnapshot.Lines.ToArray();
            var templateLine = lines[1]; // Line with the template string
            var templateSpan = new SnapshotSpan(templateLine.Start, templateLine.End);

            // Act - Test just the template line that should be highlighted
            var result = classifier.GetClassificationSpans(templateSpan);

            // Assert - this line SHOULD have classifications but currently doesn't
            var classifications = result.ToList();
            Assert.NotEmpty(classifications); // This will fail - proving the bug exists
            
            // Should find property names for UserId, ErrorCode from the message template
            var properties = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyName).ToList();
            Assert.Equal(2, properties.Count); // UserId, ErrorCode
            
            // Should find braces for the two properties
            var braces = classifications.Where(c => 
                c.ClassificationType.Classification == SerilogClassificationTypes.PropertyBrace).ToList();
            Assert.Equal(4, braces.Count); // 2 opening + 2 closing braces
        }
    }
}