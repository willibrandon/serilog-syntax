using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace SerilogSyntax.Tagging
{
    /// <summary>
    /// Handles ESC to dismiss the property-argument highlights.
    /// Participates in the editor command chain.
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [Name("PropertyArgumentEscHandler")]
    [ContentType("CSharp")]
    internal sealed class PropertyArgumentEscCommandHandler
        : IChainedCommandHandler<EscapeKeyCommandArgs>
    {
        /// <summary>
        /// Gets the display name of the command handler.
        /// </summary>
        public string DisplayName => "Property-Argument ESC Dismissal";

        /// <summary>
        /// Executes the ESC command, dismissing property-argument highlights if applicable.
        /// </summary>
        /// <param name="args">The ESC key command arguments.</param>
        /// <param name="nextHandler">The next handler in the command chain.</param>
        /// <param name="context">The command execution context.</param>
        public void ExecuteCommand(EscapeKeyCommandArgs args, System.Action nextHandler, CommandExecutionContext context)
        {
            var view = args.TextView;
            var state = PropertyArgumentHighlightState.GetOrCreate(view);

            // Only handle ESC if we actually dismiss something; otherwise, pass through.
            bool handled = state.Dismiss();
            if (!handled)
                nextHandler();
        }

        /// <summary>
        /// Gets the command state for the ESC key.
        /// </summary>
        /// <param name="args">The ESC key command arguments.</param>
        /// <param name="nextHandler">The next handler in the command chain.</param>
        /// <returns>The command state.</returns>
        public CommandState GetCommandState(EscapeKeyCommandArgs args, System.Func<CommandState> nextHandler)
        {
            // Enabled whenever we can access the view; we decide to handle at ExecuteCommand time.
            return CommandState.Available;
        }
    }
}