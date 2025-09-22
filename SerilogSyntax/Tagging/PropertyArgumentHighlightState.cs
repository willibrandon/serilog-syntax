using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;

namespace SerilogSyntax.Tagging
{
    /// <summary>
    /// Per-view state for property-argument highlighting, including ESC dismissal.
    /// </summary>
    internal sealed class PropertyArgumentHighlightState : IDisposable
    {
        private readonly ITextView _view;

        // Track whether highlighting is currently dismissed
        private bool _isDismissed;

        /// <summary>
        /// Occurs when the highlight state changes (dismissal or restoration).
        /// </summary>
        public event EventHandler StateChanged;

        public PropertyArgumentHighlightState(ITextView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.Closed += OnViewClosed;
            _view.Caret.PositionChanged += OnCaretPositionChanged;
        }

        /// <summary>
        /// Gets a value indicating whether highlighting has been dismissed via ESC.
        /// </summary>
        public bool IsDismissed => _isDismissed;

        /// <summary>
        /// Dismisses the property-argument highlights.
        /// Called by the ESC command handler.
        /// </summary>
        /// <returns>True if highlights were dismissed; false if already dismissed.</returns>
        public bool Dismiss()
        {
            if (_isDismissed)
                return false; // Already dismissed, let other ESC handlers run

            _isDismissed = true;
            OnStateChanged();
            return true;
        }

        /// <summary>
        /// Restores highlighting after being dismissed.
        /// Called when the caret position changes.
        /// </summary>
        public void Restore()
        {
            if (_isDismissed)
            {
                _isDismissed = false;
                OnStateChanged();
            }
        }

        private void OnCaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            // Restore highlighting when cursor moves after ESC dismissal
            Restore();
        }

        private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        private void OnViewClosed(object sender, EventArgs e) => Dispose();

        public void Dispose()
        {
            _view.Closed -= OnViewClosed;
            _view.Caret.PositionChanged -= OnCaretPositionChanged;
        }

        /// <summary>
        /// Gets or creates a singleton instance of the state for the specified view.
        /// </summary>
        /// <param name="view">The text view.</param>
        /// <returns>The state instance for the view.</returns>
        public static PropertyArgumentHighlightState GetOrCreate(ITextView view)
            => view.Properties.GetOrCreateSingletonProperty(() => new PropertyArgumentHighlightState(view));
    }
}