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
        public string DisplayName => "Serilog Brace ESC Dismissal";

        public void ExecuteCommand(EscapeKeyCommandArgs args, System.Action nextHandler, CommandExecutionContext context)
        {
            var view = args.TextView;
            var state = SerilogBraceHighlightState.GetOrCreate(view);

            // Only handle ESC if we actually dismiss something; otherwise, pass through.
            bool handled = state.DismissCurrentPair();
            if (!handled)
                nextHandler();
        }

        public CommandState GetCommandState(EscapeKeyCommandArgs args, System.Func<CommandState> nextHandler)
        {
            // Enabled whenever we can access the view; we decide to handle at ExecuteCommand time.
            return CommandState.Available;
        }
    }
}