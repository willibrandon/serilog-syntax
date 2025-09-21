using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using SerilogSyntax.Diagnostics;
using System;

namespace SerilogSyntax.Tagging;

/// <summary>
/// Manages the state of property-argument highlights for a text view.
/// Tracks the currently highlighted property and argument spans and handles ESC key dismissal.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="PropertyArgumentHighlightState"/> class.
/// </remarks>
/// <param name="textView">The text view this state is associated with.</param>
internal sealed class PropertyArgumentHighlightState(ITextView textView)
{
    private ITrackingSpan _propertyTrackingSpan;
    private ITrackingSpan _argumentTrackingSpan;
    private bool _isDisabledByEsc;

    /// <summary>
    /// Occurs when the highlight state changes.
    /// </summary>
    public event EventHandler StateChanged;

    /// <summary>
    /// Gets a value indicating whether highlighting is currently disabled (e.g., by ESC key).
    /// </summary>
    public bool IsDisabled => _isDisabledByEsc;

    /// <summary>
    /// Gets the current highlight spans.
    /// </summary>
    /// <returns>A tuple of (property span, argument span), either of which may be null.</returns>
    public (SnapshotSpan? PropertySpan, SnapshotSpan? ArgumentSpan) GetHighlightSpans()
    {
        if (_isDisabledByEsc)
            return (null, null);

        // If text view is null (in tests), we can't get a snapshot
        if (textView == null)
            return (null, null);

        var snapshot = textView.TextSnapshot;
        SnapshotSpan? propertySpan = null;
        SnapshotSpan? argumentSpan = null;

        if (_propertyTrackingSpan != null)
        {
            try
            {
                propertySpan = _propertyTrackingSpan.GetSpan(snapshot);
            }
            catch
            {
                // Tracking span is no longer valid
                _propertyTrackingSpan = null;
            }
        }

        if (_argumentTrackingSpan != null)
        {
            try
            {
                argumentSpan = _argumentTrackingSpan.GetSpan(snapshot);
            }
            catch
            {
                // Tracking span is no longer valid
                _argumentTrackingSpan = null;
            }
        }

        return (propertySpan, argumentSpan);
    }

    /// <summary>
    /// Sets the highlights for a property and its corresponding argument.
    /// </summary>
    /// <param name="propertySpan">The span of the property to highlight (optional).</param>
    /// <param name="argumentSpan">The span of the argument to highlight (optional).</param>
    public void SetHighlights(SnapshotSpan? propertySpan, SnapshotSpan? argumentSpan)
    {
        DiagnosticLogger.Log($"PropertyArgumentHighlightState.SetHighlights: Property={propertySpan?.Start}-{propertySpan?.End}, Argument={argumentSpan?.Start}-{argumentSpan?.End}");

        // Clear disabled state when setting new highlights
        _isDisabledByEsc = false;

        // Create tracking spans for the new highlights
        _propertyTrackingSpan = propertySpan.HasValue
            ? propertySpan.Value.Snapshot.CreateTrackingSpan(propertySpan.Value, SpanTrackingMode.EdgeExclusive)
            : null;

        _argumentTrackingSpan = argumentSpan.HasValue
            ? argumentSpan.Value.Snapshot.CreateTrackingSpan(argumentSpan.Value, SpanTrackingMode.EdgeExclusive)
            : null;

        DiagnosticLogger.Log($"PropertyArgumentHighlightState.SetHighlights: Created tracking spans, firing StateChanged event");
        OnStateChanged();
    }

    /// <summary>
    /// Clears all highlights.
    /// </summary>
    public void ClearHighlights()
    {
        DiagnosticLogger.Log("PropertyArgumentHighlightState.ClearHighlights: Clearing all highlights");
        _propertyTrackingSpan = null;
        _argumentTrackingSpan = null;
        _isDisabledByEsc = false;

        OnStateChanged();
    }

    /// <summary>
    /// Temporarily disables highlighting (called when ESC is pressed).
    /// </summary>
    /// <returns>True if highlights were disabled, false if there were no highlights to disable.</returns>
    public bool DisableHighlights()
    {
        if (_propertyTrackingSpan != null || _argumentTrackingSpan != null)
        {
            _isDisabledByEsc = true;
            OnStateChanged();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Re-enables highlighting after ESC dismissal.
    /// </summary>
    public void EnableHighlights()
    {
        if (_isDisabledByEsc)
        {
            _isDisabledByEsc = false;
            OnStateChanged();
        }
    }

    /// <summary>
    /// Checks if there are currently any active highlights.
    /// </summary>
    /// <returns>True if there are highlights set (even if disabled), false otherwise.</returns>
    public bool HasHighlights()
    {
        return _propertyTrackingSpan != null || _argumentTrackingSpan != null;
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}