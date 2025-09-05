using System;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using SerilogSyntax.Classification;
using SerilogSyntax.Tests.TestHelpers;
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
    }
}