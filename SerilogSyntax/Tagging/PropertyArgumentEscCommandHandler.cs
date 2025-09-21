using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace SerilogSyntax.Tagging;

/// <summary>
/// Command handler for dismissing property-argument highlights with the ESC key.
/// </summary>
[Export(typeof(ICommandHandler))]
[ContentType("CSharp")]
[Name(nameof(PropertyArgumentEscCommandHandler))]
internal sealed class PropertyArgumentEscCommandHandler : IChainedCommandHandler<EscapeKeyCommandArgs>
{
    /// <summary>
    /// Gets the display name of this command handler.
    /// </summary>
    public string DisplayName => "Dismiss Property-Argument Highlights";

    /// <summary>
    /// Gets the command state for the ESC key.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <param name="nextHandler">The next handler in the chain.</param>
    /// <returns>The command state.</returns>
    public CommandState GetCommandState(EscapeKeyCommandArgs args, Func<CommandState> nextHandler)
    {
        // Always available - we decide whether to handle at ExecuteCommand time
        return CommandState.Available;
    }

    /// <summary>
    /// Handles the ESC key command to dismiss property-argument highlights.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <param name="nextHandler">The next command handler in the chain.</param>
    /// <param name="context">The command execution context.</param>
    public void ExecuteCommand(EscapeKeyCommandArgs args, Action nextHandler, CommandExecutionContext context)
    {
        // Get the highlight state for this view
        if (args.TextView.Properties.TryGetProperty(typeof(PropertyArgumentHighlightState), out PropertyArgumentHighlightState highlightState))
        {
            // Try to dismiss highlights
            if (highlightState.DisableHighlights())
            {
                // We handled the ESC key by dismissing highlights
                return;
            }
        }

        // No highlights to dismiss, pass the ESC key to the next handler
        nextHandler();
    }
}