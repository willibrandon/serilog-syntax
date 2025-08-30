using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;

namespace SerilogSyntax.Tagging
{
    /// <summary>
    /// Per-view state for Serilog brace highlighting, including ESC dismissal.
    /// </summary>
    internal sealed class SerilogBraceHighlightState : IDisposable
    {
        private readonly ITextView _view;

        // Track the current brace pair under the caret.
        private ITrackingPoint _currentOpen;
        private ITrackingPoint _currentClose;

        // Track the last pair dismissed with ESC.
        private ITrackingPoint _dismissedOpen;
        private ITrackingPoint _dismissedClose;
        private bool _isDismissed;

        /// <summary>
        /// Occurs when the highlight state changes (dismissal or restoration).
        /// </summary>
        public event EventHandler StateChanged;

        public SerilogBraceHighlightState(ITextView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.Closed += OnViewClosed;
        }

        /// <summary>
        /// Gets a value indicating whether there is a current brace pair being tracked.
        /// </summary>
        public bool HasCurrentPair => _currentOpen != null && _currentClose != null;

        /// <summary>
        /// Sets the current brace pair being highlighted.
        /// Called by the tagger when it determines the current pair.
        /// </summary>
        /// <param name="open">The opening brace position.</param>
        /// <param name="close">The closing brace position.</param>
        public void SetCurrentPair(SnapshotPoint open, SnapshotPoint close)
        {
            var snapshot = open.Snapshot;
            _currentOpen = snapshot.CreateTrackingPoint(open, PointTrackingMode.Positive);
            _currentClose = snapshot.CreateTrackingPoint(close, PointTrackingMode.Positive);

            // If we moved to a different pair, clear any prior dismissal.
            if (_isDismissed && !CurrentMatchesDismissed())
            {
                _isDismissed = false;
                _dismissedOpen = _dismissedClose = null;
                OnStateChanged();
            }
        }

        /// <summary>
        /// Clears the current brace pair and any dismissal state.
        /// Called by the tagger when cursor moves away from any brace.
        /// </summary>
        public void ClearCurrentPair()
        {
            _currentOpen = null;
            _currentClose = null;
            
            // Clear dismissal when moving away from the brace
            if (_isDismissed)
            {
                _isDismissed = false;
                _dismissedOpen = null;
                _dismissedClose = null;
                OnStateChanged();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current brace pair has been dismissed via ESC.
        /// </summary>
        public bool IsDismissedForCurrentPair => _isDismissed && CurrentMatchesDismissed();

        /// <summary>
        /// Dismisses the current brace pair highlight.
        /// Called by the ESC command handler.
        /// </summary>
        /// <returns>True if a pair was dismissed; false if there was nothing to dismiss.</returns>
        public bool DismissCurrentPair()
        {
            if (!HasCurrentPair)
                return false;

            if (_isDismissed && CurrentMatchesDismissed())
                return false; // already dismissed; let other ESC handlers run

            _dismissedOpen = _currentOpen;
            _dismissedClose = _currentClose;
            _isDismissed = true;
            OnStateChanged();
            return true;
        }

        private bool CurrentMatchesDismissed()
        {
            if (_currentOpen == null || _currentClose == null || _dismissedOpen == null || _dismissedClose == null)
                return false;

            // Compare positions in the latest snapshot
            try
            {
                var snapshot = _view.TextSnapshot;
                int curOpen = _currentOpen.GetPosition(snapshot);
                int curClose = _currentClose.GetPosition(snapshot);
                int disOpen = _dismissedOpen.GetPosition(snapshot);
                int disClose = _dismissedClose.GetPosition(snapshot);
                return curOpen == disOpen && curClose == disClose;
            }
            catch
            {
                return false;
            }
        }

        private void OnStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

        private void OnViewClosed(object sender, EventArgs e) => Dispose();

        public void Dispose()
        {
            _view.Closed -= OnViewClosed;
        }

        /// <summary>
        /// Gets or creates a singleton instance of the state for the specified view.
        /// </summary>
        /// <param name="view">The text view.</param>
        /// <returns>The state instance for the view.</returns>
        public static SerilogBraceHighlightState GetOrCreate(ITextView view)
            => view.Properties.GetOrCreateSingletonProperty(() => new SerilogBraceHighlightState(view));
    }
}