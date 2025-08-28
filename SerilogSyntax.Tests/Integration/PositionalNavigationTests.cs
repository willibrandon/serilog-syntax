using Microsoft.VisualStudio.Text;
using SerilogSyntax.Navigation;
using SerilogSyntax.Parsing;
using System.Linq;
using Xunit;

namespace SerilogSyntax.Tests.Integration
{
    public class PositionalNavigationTests
    {
        [Fact]
        public void GetArgumentIndex_PositionalProperty_ReturnsCorrectIndex()
        {
            // Arrange
            var properties = new[]
            {
                new TemplateProperty { Name = "0", Type = PropertyType.Positional },
                new TemplateProperty { Name = "1", Type = PropertyType.Positional },
                new TemplateProperty { Name = "UserName", Type = PropertyType.Standard }
            }.ToList();

            var source = new SerilogSuggestedActionsSource(null, null, null);
            var getArgumentIndexMethod = typeof(SerilogSuggestedActionsSource)
                .GetMethod("GetArgumentIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var index0 = (int)getArgumentIndexMethod.Invoke(source, new object[] { properties, properties[0] });
            var index1 = (int)getArgumentIndexMethod.Invoke(source, new object[] { properties, properties[1] });
            var indexNamed = (int)getArgumentIndexMethod.Invoke(source, new object[] { properties, properties[2] });

            // Assert
            Assert.Equal(0, index0);
            Assert.Equal(1, index1);
            Assert.Equal(0, indexNamed); // First named property gets index 0
        }

        [Fact]
        public void NavigateToArgumentAction_DisplayText_FormatsCorrectlyForPositional()
        {
            // Arrange & Act
            var action = new NavigateToArgumentAction(null, 100, 4, "0", PropertyType.Positional);

            // Assert
            Assert.Equal("Navigate to argument at position 0", action.DisplayText);
        }

        [Fact]
        public void NavigateToArgumentAction_DisplayText_FormatsCorrectlyForDestructured()
        {
            // Arrange & Act
            var action = new NavigateToArgumentAction(null, 100, 4, "UserId", PropertyType.Destructured);

            // Assert
            Assert.Equal("Navigate to 'UserId' argument", action.DisplayText);
        }

        [Fact]
        public void NavigateToArgumentAction_DisplayText_FormatsCorrectlyForStringified()
        {
            // Arrange & Act
            var action = new NavigateToArgumentAction(null, 100, 4, "Count", PropertyType.Stringified);

            // Assert
            Assert.Equal("Navigate to 'Count' argument", action.DisplayText);
        }

        [Fact]
        public void GetArgumentIndex_InvalidPositionalProperty_ReturnsNegativeOne()
        {
            // Arrange
            var properties = new[]
            {
                new TemplateProperty { Name = "invalid", Type = PropertyType.Positional }
            }.ToList();

            var source = new SerilogSuggestedActionsSource(null, null, null);
            var getArgumentIndexMethod = typeof(SerilogSuggestedActionsSource)
                .GetMethod("GetArgumentIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            // Act
            var result = (int)getArgumentIndexMethod.Invoke(source, new object[] { properties, properties[0] });

            // Assert
            Assert.Equal(-1, result);
        }
    }
}