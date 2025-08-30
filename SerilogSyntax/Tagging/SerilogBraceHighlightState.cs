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

        public event EventHandler StateChanged;

        public SerilogBraceHighlightState(ITextView view)
        {
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _view.Closed += OnViewClosed;
        }

        public bool HasCurrentPair => _currentOpen != null && _currentClose != null;

        /// <summary>Called by the tagger when it determines the current pair.</summary>
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

        /// <summary>Called by the tagger when cursor moves away from any brace.</summary>
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

        public bool IsDismissedForCurrentPair => _isDismissed && CurrentMatchesDismissed();

        /// <summary>Called by the ESC command handler.</summary>
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

        // Helper for get-or-create
        public static SerilogBraceHighlightState GetOrCreate(ITextView view)
            => view.Properties.GetOrCreateSingletonProperty(() => new SerilogBraceHighlightState(view));
    }
}