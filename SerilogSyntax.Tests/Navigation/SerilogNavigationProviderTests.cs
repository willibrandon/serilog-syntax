using Microsoft.VisualStudio.Text;
using SerilogSyntax.Navigation;
using SerilogSyntax.Parsing;
using SerilogSyntax.Tests.TestHelpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SerilogSyntax.Tests.Navigation;

/// <summary>
/// Tests for the Serilog navigation provider functionality.
/// </summary>
public class SerilogNavigationProviderTests
{
    [Fact]
    public void NavigateToArgumentAction_DisplayText_FormatsCorrectly()
    {
        // Arrange & Act
        var action = new NavigateToArgumentAction(null, 100, 4, "UserName", PropertyType.Standard);

        // Assert
        Assert.Equal("Navigate to 'UserName' argument", action.DisplayText);
    }

    [Fact]
    public void NavigateToArgumentAction_HasPreview_ReturnsFalse()
    {
        // Arrange & Act
        var action = new NavigateToArgumentAction(null, 100, 4, "Test", PropertyType.Standard);

        // Assert
        Assert.False(action.HasPreview);
    }

    [Fact]
    public void NavigateToArgumentAction_HasActionSets_ReturnsFalse()
    {
        // Arrange & Act
        var action = new NavigateToArgumentAction(null, 100, 4, "Test", PropertyType.Standard);

        // Assert
        Assert.False(action.HasActionSets);
    }

    [Fact]
    public async Task NavigateToArgumentAction_GetActionSetsAsync_ReturnsEmptyAsync()
    {
        // Arrange
        var action = new NavigateToArgumentAction(null, 100, 4, "Test", PropertyType.Standard);

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
        var action = new NavigateToArgumentAction(null, 100, 4, "Test", PropertyType.Standard);

        // Act
        var result = await action.GetPreviewAsync(CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void NavigateToArgumentAction_TryGetTelemetryId_ReturnsFalse()
    {
        // Arrange
        var action = new NavigateToArgumentAction(null, 100, 4, "Test", PropertyType.Standard);

        // Act
        var result = action.TryGetTelemetryId(out var telemetryId);

        // Assert
        Assert.False(result);
        Assert.Equal(System.Guid.Empty, telemetryId);
    }

    [Fact]
    public void NavigateToArgumentAction_DisplayText_FormatsPositionalCorrectly()
    {
        // Arrange & Act
        var action = new NavigateToArgumentAction(null, 100, 4, "0", PropertyType.Positional);

        // Assert
        Assert.Equal("Navigate to argument at position 0", action.DisplayText);
    }

    [Fact]
    public void NavigateToArgumentAction_DisplayText_FormatsNamedCorrectly()
    {
        // Arrange & Act
        var action = new NavigateToArgumentAction(null, 100, 4, "UserName", PropertyType.Standard);

        // Assert
        Assert.Equal("Navigate to 'UserName' argument", action.DisplayText);
    }

    [Fact]
    public void GetSuggestedActions_MultiLineCall_ShouldProvideNavigation()
    {
        // This test simulates the real issue: multi-line calls don't provide navigation
        // when the template is on one line and arguments are on subsequent lines
        
        // Arrange - Create mock text snapshot for multi-line scenario
        var multiLineCode = 
            "logger.LogInformation(\"User {UserId} ({UserName}) placed {OrderCount} orders\",\r\n" +
            "    userId, userName, orderCount);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Create a range within the {UserId} property (position should be inside the template)
        var userIdStart = multiLineCode.IndexOf("{UserId}");
        var range = new SnapshotSpan(mockSnapshot, userIdStart + 1, 6); // Inside "UserId"
        
        var provider = new SerilogSuggestedActionsSource(null);
        
        // Act - Try to get suggested actions for navigation
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        // Assert - Should provide navigation actions even for multi-line calls
        Assert.NotEmpty(actions); // This should fail with current implementation
    }

    [Fact]
    public void GetSuggestedActions_ThreeLineCall_ShouldProvideNavigation()
    {
        // Test arguments spread across three lines
        var multiLineCode = 
            "logger.LogInformation(\"Processing {UserId} with {UserName} in {Department} at {Timestamp:yyyy-MM-dd}\",\r\n" +
            "    userId, userName,\r\n" +
            "    department, DateTime.Now);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for different properties
        var departmentStart = multiLineCode.IndexOf("{Department}");
        var range = new SnapshotSpan(mockSnapshot, departmentStart + 1, 10); // Inside "Department"
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions);
    }

    [Fact]
    public void GetSuggestedActions_ArgumentsOnSeparateLines_ShouldProvideNavigation()
    {
        // Test with each argument on its own line
        var multiLineCode = 
            "logger.LogError(exception, \"Error processing {UserId} with {ErrorCode} and {Message}\",\r\n" +
            "    userId,\r\n" +
            "    errorCode,\r\n" +
            "    errorMessage);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for the second property {ErrorCode}
        var errorCodeStart = multiLineCode.IndexOf("{ErrorCode}");
        var range = new SnapshotSpan(mockSnapshot, errorCodeStart + 1, 9); // Inside "ErrorCode"
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions);
    }

    [Fact]
    public void GetSuggestedActions_ComplexFormatSpecifiers_ShouldProvideNavigation()
    {
        // Test multi-line with complex format specifiers and alignment
        var multiLineCode = 
            "logger.LogInformation(\"Order {OrderId,-10} by {CustomerName,15} for {Amount:C} on {Date:yyyy-MM-dd HH:mm}\",\r\n" +
            "    order.Id, customer.FullName, order.TotalAmount,\r\n" +
            "    order.CreatedDate);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for {Amount:C} property
        var amountStart = multiLineCode.IndexOf("{Amount:C}");
        var range = new SnapshotSpan(mockSnapshot, amountStart + 1, 6); // Inside "Amount"
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions);
    }

    [Fact]
    public void GetSuggestedActions_DestructuredProperties_ShouldProvideNavigation()
    {
        // Test multi-line with destructured and stringified properties
        var multiLineCode = 
            "logger.LogInformation(\"User {@User} performed {Action} with {@RequestData} and {$ErrorDetails}\",\r\n" +
            "    currentUser, actionType,\r\n" +
            "    requestData, errorDetails);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for destructured property {@RequestData}
        var requestDataStart = multiLineCode.IndexOf("{@RequestData}");
        var range = new SnapshotSpan(mockSnapshot, requestDataStart + 2, 11); // Inside "RequestData"
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions);
    }

    [Fact]
    public void GetSuggestedActions_PositionalProperties_ShouldProvideNavigation()
    {
        // Test multi-line with positional properties
        var multiLineCode = 
            "logger.LogWarning(\"Warning: {0} failed for user {1} with error {2} at {3:yyyy-MM-dd}\",\r\n" +
            "    operationName, userId,\r\n" +
            "    errorMessage, DateTime.Now);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for positional property {1}
        var positionalStart = multiLineCode.IndexOf("{1}");
        var range = new SnapshotSpan(mockSnapshot, positionalStart + 1, 1); // Inside "1"
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions);
    }

    [Fact]
    public void GetSuggestedActions_MultiLineNavigation_ShouldSelectCorrectArgumentWithWhitespace()
    {
        // Test case that replicates the exact issue: navigation to {@Customer} should highlight the object, not part of the string
        var multiLineCode = 
            "expressionLogger.Information(\"Order {OrderId} processed successfully for customer {@Customer} in {Duration}ms\",\r\n" +
            "    \"ORD-2024-0042\", new { Name = \"Bob Smith\", Tier = \"Premium\" }, 127);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for {@Customer} property (the destructured customer object)
        var customerStart = multiLineCode.IndexOf("{@Customer}");
        var range = new SnapshotSpan(mockSnapshot, customerStart + 2, 8); // Inside "Customer" (skip the {@)
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions);
        
        // Verify that the navigation action points to the correct argument
        var actionSet = actions.First();
        var navigateAction = actionSet.Actions.OfType<NavigateToArgumentAction>().First();
        
        // Calculate expected position: the customer object starts with "new { Name = "Bob Smith""
        var expectedStart = multiLineCode.IndexOf("new { Name = \"Bob Smith\"");
        var expectedLength = "new { Name = \"Bob Smith\", Tier = \"Premium\" }".Length;
        
        // The action should highlight the complete customer object, not part of the string literal
        Assert.Equal(expectedStart, navigateAction.ArgumentStart);
        Assert.Equal(expectedLength, navigateAction.ArgumentLength);
        
        // Verify the highlighted text is exactly what we expect
        var highlightedText = multiLineCode.Substring(navigateAction.ArgumentStart, navigateAction.ArgumentLength);
        Assert.Equal("new { Name = \"Bob Smith\", Tier = \"Premium\" }", highlightedText);
    }

    [Fact]
    public void GetSuggestedActions_VerbatimStringMultiLine_ShouldProvideNavigation()
    {
        // Test verbatim string spanning multiple lines
        var multiLineCode = 
            "logger.LogInformation(@\"Processing files in path: {FilePath}\r\n" +
            "Multiple lines are supported in verbatim strings\r\n" +
            "With properties like {UserId} and {@Order}\r\n" +
            "Even with \"\"escaped quotes\"\" in the template\",\r\n" +
            "    filePath, userId, order);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for {UserId} property on line 3 of the template
        var userIdStart = multiLineCode.IndexOf("{UserId}");
        var range = new SnapshotSpan(mockSnapshot, userIdStart + 1, 6); // Inside "UserId"
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions); // This should fail - no navigation appears
    }

    [Fact]
    public void GetSuggestedActions_VerbatimStringMultiLine_FilePath_ShouldProvideNavigation()
    {
        // Test that {FilePath} on the first line of a verbatim multi-line template provides navigation
        var multiLineCode = 
            "logger.LogInformation(@\"Processing files in path: {FilePath}\r\n" +
            "Multiple lines are supported in verbatim strings\r\n" +
            "With properties like {UserId} and {@Order}\r\n" +
            "Even with \"\"escaped quotes\"\" in the template\",\r\n" +
            "    filePath, userId, order);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for {FilePath} property on the first line of the template
        var filePathStart = multiLineCode.IndexOf("{FilePath}");
        var range = new SnapshotSpan(mockSnapshot, filePathStart + 1, 8); // Inside "FilePath"
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions); // This should fail - no navigation appears for {FilePath}
        
        // Verify it highlights the correct argument (filePath)
        var actionSet = actions.First();
        var navigateAction = actionSet.Actions.OfType<NavigateToArgumentAction>().First();
        
        var expectedStart = multiLineCode.IndexOf("filePath");
        var expectedLength = "filePath".Length;
        
        Assert.Equal(expectedStart, navigateAction.ArgumentStart);
        Assert.Equal(expectedLength, navigateAction.ArgumentLength);
        
        var highlightedText = multiLineCode.Substring(navigateAction.ArgumentStart, navigateAction.ArgumentLength);
        Assert.Equal("filePath", highlightedText);
    }

    [Fact]
    public void GetSuggestedActions_VerbatimStringMultiLine_UserId_ShouldHighlightCorrectArgument()
    {
        // Test that {UserId} highlights the correct argument (userId, not filePath)
        var multiLineCode = 
            "logger.LogInformation(@\"Processing files in path: {FilePath}\r\n" +
            "Multiple lines are supported in verbatim strings\r\n" +
            "With properties like {UserId} and {@Order}\r\n" +
            "Even with \"\"escaped quotes\"\" in the template\",\r\n" +
            "    filePath, userId, order);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for {UserId} property 
        var userIdStart = multiLineCode.IndexOf("{UserId}");
        var range = new SnapshotSpan(mockSnapshot, userIdStart + 1, 6); // Inside "UserId"
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions);
        
        // Verify it highlights the correct argument (userId, not filePath)
        var actionSet = actions.First();
        var navigateAction = actionSet.Actions.OfType<NavigateToArgumentAction>().First();
        
        var expectedStart = multiLineCode.IndexOf("userId");
        var expectedLength = "userId".Length;
        
        Assert.Equal(expectedStart, navigateAction.ArgumentStart); // This should fail - currently highlights filePath
        Assert.Equal(expectedLength, navigateAction.ArgumentLength);
        
        var highlightedText = multiLineCode.Substring(navigateAction.ArgumentStart, navigateAction.ArgumentLength);
        Assert.Equal("userId", highlightedText); // This should fail - currently highlights filePath
    }

    [Fact]
    public void GetSuggestedActions_VerbatimStringMultiLine_Order_ShouldHighlightCorrectArgument()
    {
        // Test that {@Order} highlights the correct argument (order, not userId)
        var multiLineCode = 
            "logger.LogInformation(@\"Processing files in path: {FilePath}\r\n" +
            "Multiple lines are supported in verbatim strings\r\n" +
            "With properties like {UserId} and {@Order}\r\n" +
            "Even with \"\"escaped quotes\"\" in the template\",\r\n" +
            "    filePath, userId, order);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for {@Order} property 
        var orderStart = multiLineCode.IndexOf("{@Order}");
        var range = new SnapshotSpan(mockSnapshot, orderStart + 2, 5); // Inside "Order" (skip {@)
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions);
        
        // Verify it highlights the correct argument (order, not userId)
        var actionSet = actions.First();
        var navigateAction = actionSet.Actions.OfType<NavigateToArgumentAction>().First();
        
        var expectedStart = multiLineCode.IndexOf(", order);") + 2; // Find "order" after ", "
        var expectedLength = "order".Length;
        
        Assert.Equal(expectedStart, navigateAction.ArgumentStart); // This should fail - currently highlights userId
        Assert.Equal(expectedLength, navigateAction.ArgumentLength);
        
        var highlightedText = multiLineCode.Substring(navigateAction.ArgumentStart, navigateAction.ArgumentLength);
        Assert.Equal("order", highlightedText); // This should fail - currently highlights userId
    }

    [Fact]
    public void GetSuggestedActions_RawStringLiteralMultiLine_ShouldProvideNavigation()
    {
        // Test raw string literal spanning multiple lines
        var multiLineCode = 
            "logger.LogInformation(\"\"\"\r\n" +
            "    Raw String Report:\r\n" +
            "    Record: {RecordId} | Status: {Status,-12}\r\n" +
            "    User: {UserName} (ID: {UserId})\r\n" +
            "    Order: {@Order}\r\n" +
            "    Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}\r\n" +
            "    \"\"\", recordId, status, userName, userId, order, timestamp);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        
        // Test navigation for {RecordId} property on line 3 of the template
        var recordIdStart = multiLineCode.IndexOf("{RecordId}");
        var range = new SnapshotSpan(mockSnapshot, recordIdStart + 1, 8); // Inside "RecordId"
        
        var provider = new SerilogSuggestedActionsSource(null);
        var actions = provider.GetSuggestedActions(null, range, CancellationToken.None);
        
        Assert.NotEmpty(actions); // This should fail - no navigation appears
    }

    [Fact]
    public void GetSuggestedActions_VerbatimStringMultiLine_EarlyProperties_ShouldProvideNavigation()
    {
        // Test the specific scenario where {AppName}, {Version}, {Environment} don't get navigation
        var multiLineCode = 
            "logger.LogInformation(@\"\r\n" +
            "===============================================\r\n" +
            "Application: {AppName}\r\n" +
            "Version: {Version}\r\n" +
            "Environment: {Environment}\r\n" +
            "===============================================\r\n" +
            "User: {UserName} (ID: {UserId})\r\n" +
            "Session: {SessionId}\r\n" +
            "Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}\r\n" +
            "===============================================\r\n" +
            "\", appName, version, env, userName, userId, sessionId, DateTime.Now);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        var provider = new SerilogSuggestedActionsSource(null);
        
        // Test navigation for {AppName} - this reportedly fails
        var appNameStart = multiLineCode.IndexOf("{AppName}");
        var appNameRange = new SnapshotSpan(mockSnapshot, appNameStart + 1, 7); // Inside "AppName"
        var appNameActions = provider.GetSuggestedActions(null, appNameRange, CancellationToken.None);
        
        // Test navigation for {Version} - this reportedly fails  
        var versionStart = multiLineCode.IndexOf("{Version}");
        var versionRange = new SnapshotSpan(mockSnapshot, versionStart + 1, 7); // Inside "Version"
        var versionActions = provider.GetSuggestedActions(null, versionRange, CancellationToken.None);
        
        // Test navigation for {Environment} - this reportedly fails
        var environmentStart = multiLineCode.IndexOf("{Environment}");
        var environmentRange = new SnapshotSpan(mockSnapshot, environmentStart + 1, 11); // Inside "Environment"
        var environmentActions = provider.GetSuggestedActions(null, environmentRange, CancellationToken.None);
        
        // Test navigation for {UserName} - this reportedly works
        var userNameStart = multiLineCode.IndexOf("{UserName}");
        var userNameRange = new SnapshotSpan(mockSnapshot, userNameStart + 1, 8); // Inside "UserName"
        var userNameActions = provider.GetSuggestedActions(null, userNameRange, CancellationToken.None);
        
        // All should work, but currently only later ones do
        Assert.NotEmpty(appNameActions);
        Assert.NotEmpty(versionActions); 
        Assert.NotEmpty(environmentActions);
        Assert.NotEmpty(userNameActions);
    }

    [Fact]
    public void GetSuggestedActions_RawStringMultiLine_EarlyProperties_ShouldProvideNavigation()
    {
        // Test the same issue with raw string literals
        var multiLineCode = 
            "logger.LogInformation(\"\"\"\r\n" +
            "    ===============================================\r\n" +
            "    Application: {AppName}\r\n" +
            "    Version: {Version}\r\n" +
            "    Environment: {Environment}\r\n" +
            "    ===============================================\r\n" +
            "    User: {UserName} (ID: {UserId})\r\n" +
            "    Session: {SessionId}\r\n" +
            "    Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}\r\n" +
            "    ===============================================\r\n" +
            "    \"\"\", appName, version, environment, userName, userId, sessionId, DateTime.Now);";
            
        var mockBuffer = new MockTextBuffer(multiLineCode);
        var mockSnapshot = new MockTextSnapshot(multiLineCode, mockBuffer, 1);
        var provider = new SerilogSuggestedActionsSource(null);
        
        // Test navigation for {AppName} - this reportedly fails
        var appNameStart = multiLineCode.IndexOf("{AppName}");
        var appNameRange = new SnapshotSpan(mockSnapshot, appNameStart + 1, 7); // Inside "AppName"
        var appNameActions = provider.GetSuggestedActions(null, appNameRange, CancellationToken.None);
        
        // Test navigation for {Version} - this reportedly fails  
        var versionStart = multiLineCode.IndexOf("{Version}");
        var versionRange = new SnapshotSpan(mockSnapshot, versionStart + 1, 7); // Inside "Version"
        var versionActions = provider.GetSuggestedActions(null, versionRange, CancellationToken.None);
        
        // Test navigation for {Environment} - this reportedly fails
        var environmentStart = multiLineCode.IndexOf("{Environment}");
        var environmentRange = new SnapshotSpan(mockSnapshot, environmentStart + 1, 11); // Inside "Environment"
        var environmentActions = provider.GetSuggestedActions(null, environmentRange, CancellationToken.None);
        
        // All should work, but currently only later ones do
        Assert.NotEmpty(appNameActions);
        Assert.NotEmpty(versionActions); 
        Assert.NotEmpty(environmentActions);
    }
}