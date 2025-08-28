using SerilogSyntax.Navigation;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SerilogSyntax.Tests.Navigation
{
    public class SerilogNavigationProviderTests
    {
        [Fact]
        public void NavigateToArgumentAction_DisplayText_FormatsCorrectly()
        {
            // Arrange & Act
            var action = new NavigateToArgumentAction(null, 100, 4, "UserName");

            // Assert
            Assert.Equal("Navigate to 'UserName' argument", action.DisplayText);
        }

        [Fact]
        public void NavigateToArgumentAction_HasPreview_ReturnsFalse()
        {
            // Arrange & Act
            var action = new NavigateToArgumentAction(null, 100, 4, "Test");

            // Assert
            Assert.False(action.HasPreview);
        }

        [Fact]
        public void NavigateToArgumentAction_HasActionSets_ReturnsFalse()
        {
            // Arrange & Act
            var action = new NavigateToArgumentAction(null, 100, 4, "Test");

            // Assert
            Assert.False(action.HasActionSets);
        }

        [Fact]
        public async Task NavigateToArgumentAction_GetActionSetsAsync_ReturnsEmptyAsync()
        {
            // Arrange
            var action = new NavigateToArgumentAction(null, 100, 4, "Test");

            // Act
            var result = await action.GetActionSetsAsync(CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task NavigateToArgumentAction_GetPreviewAsync_ReturnsNullAsync()
        {
            // Arrange
            var action = new NavigateToArgumentAction(null, 100, 4, "Test");

            // Act
            var result = await action.GetPreviewAsync(CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void NavigateToArgumentAction_TryGetTelemetryId_ReturnsFalse()
        {
            // Arrange
            var action = new NavigateToArgumentAction(null, 100, 4, "Test");

            // Act
            var result = action.TryGetTelemetryId(out var telemetryId);

            // Assert
            Assert.False(result);
            Assert.Equal(System.Guid.Empty, telemetryId);
        }
    }
}