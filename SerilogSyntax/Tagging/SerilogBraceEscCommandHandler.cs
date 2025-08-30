using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace SerilogSyntax.Tagging
{
    /// <summary>
    /// Handles ESC to dismiss the current Serilog brace highlight.
    /// Participates in the editor command chain.
    /// </summary>
    [Export(typeof(ICommandHandler))]
    [Name("SerilogBraceEscHandler")]
    [ContentType("CSharp")]
    internal sealed class SerilogBraceEscCommandHandler
        : IChainedCommandHandler<EscapeKeyCommandArgs>
    {
        /// <summary>
        /// Gets the display name of the command handler.
        /// </summary>
        public string DisplayName => "Serilog Brace ESC Dismissal";

        /// <summary>
        /// Executes the ESC command, dismissing brace highlights if applicable.
        /// </summary>
        /// <param name="args">The ESC key command arguments.</param>
        /// <param name="nextHandler">The next handler in the command chain.</param>
        /// <param name="context">The command execution context.</param>
        public void ExecuteCommand(EscapeKeyCommandArgs args, System.Action nextHandler, CommandExecutionContext context)
        {
            var view = args.TextView;
            var state = SerilogBraceHighlightState.GetOrCreate(view);

            // Only handle ESC if we actually dismiss something; otherwise, pass through.
            bool handled = state.DismissCurrentPair();
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