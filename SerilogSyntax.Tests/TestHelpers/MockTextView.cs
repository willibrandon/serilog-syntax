using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Projection;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;

namespace SerilogSyntax.Tests.TestHelpers;

/// <summary>
/// Mock implementation of ITextView for testing.
/// </summary>
public class MockTextView : ITextView
{
    private readonly MockTextCaret _caret;
    private readonly ITextBuffer _buffer;
    private double _viewportLeft;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockTextView"/> class.
    /// </summary>
    /// <param name="buffer">The text buffer.</param>
    public MockTextView(ITextBuffer buffer)
    {
        _buffer = buffer;
        TextBuffer = buffer;
        TextSnapshot = buffer.CurrentSnapshot;
        Properties = new PropertyCollection();
        Options = new MockEditorOptions();
        _caret = new MockTextCaret(this);
    }

    /// <inheritdoc/>
    public ITextCaret Caret => _caret;

    /// <inheritdoc/>
    public ITextBuffer TextBuffer { get; }

    /// <inheritdoc/>
    public ITextSnapshot TextSnapshot { get; private set; }

    /// <inheritdoc/>
    public ITextSnapshot VisualSnapshot => TextSnapshot;

    /// <inheritdoc/>
    public ITextDataModel TextDataModel => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITextViewModel TextViewModel => throw new NotImplementedException();

    /// <inheritdoc/>
    public IBufferGraph BufferGraph => throw new NotImplementedException();

    /// <inheritdoc/>
    public PropertyCollection Properties { get; }

    /// <inheritdoc/>
    public IEditorOptions Options { get; }

    /// <inheritdoc/>
    public bool IsClosed => false;

    /// <inheritdoc/>
    public bool InLayout => false;

    /// <inheritdoc/>
    public bool IsMouseOverViewOrAdornments => false;

    /// <inheritdoc/>
    public bool HasAggregateFocus => true;

    /// <inheritdoc/>
    public IViewScroller ViewScroller => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITextViewLineCollection TextViewLines => throw new NotImplementedException();

    /// <inheritdoc/>
    public double ViewportWidth => 800;

    /// <inheritdoc/>
    public double ViewportHeight => 600;

    /// <inheritdoc/>
    public double ViewportLeft { get => _viewportLeft; set => _viewportLeft = value; }

    /// <inheritdoc/>
    public double ViewportTop => 0;

    /// <inheritdoc/>
    public double ViewportRight => ViewportLeft + ViewportWidth;

    /// <inheritdoc/>
    public double ViewportBottom => ViewportTop + ViewportHeight;

    /// <inheritdoc/>
    public double LineHeight => 15;

    /// <inheritdoc/>
    public double MaxTextRightCoordinate => 1000;

    /// <inheritdoc/>
    public ITextViewRoleSet Roles => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITextSelection Selection => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITrackingSpan ProvisionalTextHighlight { get; set; }

#pragma warning disable CS0067
    /// <inheritdoc/>
    public event EventHandler<TextViewLayoutChangedEventArgs> LayoutChanged;

    /// <inheritdoc/>
    public event EventHandler ViewportLeftChanged;

    /// <inheritdoc/>
    public event EventHandler ViewportHeightChanged;

    /// <inheritdoc/>
    public event EventHandler ViewportWidthChanged;

    /// <inheritdoc/>
    public event EventHandler<MouseHoverEventArgs> MouseHover;

    /// <inheritdoc/>
    public event EventHandler Closed;

    /// <inheritdoc/>
    public event EventHandler LostAggregateFocus;

    /// <inheritdoc/>
    public event EventHandler GotAggregateFocus;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public void Close() { }

    /// <inheritdoc/>
    public void DisplayTextLineContainingBufferPosition(SnapshotPoint bufferPosition, double verticalDistance, ViewRelativePosition relativeTo) { }

    /// <inheritdoc/>
    public void DisplayTextLineContainingBufferPosition(SnapshotPoint bufferPosition, double verticalDistance, ViewRelativePosition relativeTo, double? viewportWidthOverride, double? viewportHeightOverride) { }

    /// <inheritdoc/>
    public SnapshotSpan GetTextElementSpan(SnapshotPoint point) => new(point, 1);

    /// <inheritdoc/>
    public ITextViewLine GetTextViewLineContainingBufferPosition(SnapshotPoint bufferPosition) => throw new NotImplementedException();

    /// <inheritdoc/>
    public void QueueSpaceReservationStackRefresh() { }
}

/// <summary>
/// Mock implementation of ITextCaret for testing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MockTextCaret"/> class.
/// </remarks>
/// <param name="view">The text view.</param>
public class MockTextCaret(ITextView view) : ITextCaret
{
    private SnapshotPoint _position = new(view.TextBuffer.CurrentSnapshot, 0);

    /// <inheritdoc/>
    public ITextView TextView => view;
    
    /// <inheritdoc/>
    public CaretPosition Position => new(
        new VirtualSnapshotPoint(_position),
        new MockMappingPoint(_position),
        PositionAffinity.Successor);

    /// <inheritdoc/>
    public double Left => 0;

    /// <inheritdoc/>
    public double Width => 2;

    /// <inheritdoc/>
    public double Right => Left + Width;

    /// <inheritdoc/>
    public double Top => 0;

    /// <inheritdoc/>
    public double Height => 15;

    /// <inheritdoc/>
    public double Bottom => Top + Height;

    /// <inheritdoc/>
    public bool InVirtualSpace => false;

    /// <inheritdoc/>
    public bool OverwriteMode => false;
    
    private bool _isHidden;

    /// <inheritdoc/>
    public bool IsHidden { get => _isHidden; set => _isHidden = value; }
    
    /// <inheritdoc/>
    public SnapshotPoint BufferPosition => _position;

#pragma warning disable CS0067
    /// <inheritdoc/>
    public event EventHandler<CaretPositionChangedEventArgs> PositionChanged;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public CaretPosition MoveTo(SnapshotPoint position)
    {
        var oldPosition = Position;
        _position = position;
        
        var newPosition = new CaretPosition(
            new VirtualSnapshotPoint(_position),
            new MockMappingPoint(_position),
            PositionAffinity.Successor);
        
        PositionChanged?.Invoke(this, new CaretPositionChangedEventArgs(
            view, oldPosition, newPosition));
        
        return newPosition;
    }

    /// <inheritdoc/>
    public CaretPosition MoveTo(VirtualSnapshotPoint position) 
    {
        return MoveTo(position.Position);
    }
    
    /// <inheritdoc/>
    public CaretPosition MoveTo(ITextViewLine textLine) => throw new NotImplementedException();

    /// <inheritdoc/>
    public CaretPosition MoveTo(ITextViewLine textLine, double xCoordinate) => throw new NotImplementedException();

    /// <inheritdoc/>
    public CaretPosition MoveTo(ITextViewLine textLine, double xCoordinate, bool captureHorizontalPosition) => throw new NotImplementedException();

    /// <inheritdoc/>
    public CaretPosition MoveTo(VirtualSnapshotPoint position, PositionAffinity caretAffinity) => throw new NotImplementedException();

    /// <inheritdoc/>
    public CaretPosition MoveTo(VirtualSnapshotPoint position, PositionAffinity caretAffinity, bool captureHorizontalPosition) => throw new NotImplementedException();

    /// <inheritdoc/>
    public CaretPosition MoveTo(SnapshotPoint position, PositionAffinity caretAffinity) => throw new NotImplementedException();

    /// <inheritdoc/>
    public CaretPosition MoveTo(SnapshotPoint position, PositionAffinity caretAffinity, bool captureHorizontalPosition) => throw new NotImplementedException();

    /// <inheritdoc/>
    public CaretPosition MoveToNextCaretPosition() => throw new NotImplementedException();

    /// <inheritdoc/>
    public CaretPosition MoveToPreviousCaretPosition() => throw new NotImplementedException();

    /// <inheritdoc/>
    public CaretPosition MoveToPreferredCoordinates() => throw new NotImplementedException();

    /// <inheritdoc/>
    public void EnsureVisible() { }

    /// <inheritdoc/>
    public ITextViewLine ContainingTextViewLine => throw new NotImplementedException();
}

/// <summary>
/// Mock implementation of IMappingPoint for testing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MockMappingPoint"/> class.
/// </remarks>
/// <param name="point">The snapshot point.</param>
public class MockMappingPoint(SnapshotPoint point) : IMappingPoint
{
    /// <inheritdoc/>
    public ITextBuffer AnchorBuffer => point.Snapshot.TextBuffer;

    /// <inheritdoc/>
    public IBufferGraph BufferGraph => throw new NotImplementedException();

    /// <inheritdoc/>
    public SnapshotPoint? GetInsertionPoint(Predicate<ITextBuffer> match) => point;

    /// <inheritdoc/>
    public SnapshotPoint? GetPoint(ITextSnapshot targetSnapshot, PositionAffinity affinity) => point;

    /// <inheritdoc/>
    public SnapshotPoint? GetPoint(ITextBuffer targetBuffer, PositionAffinity affinity) => point;

    /// <inheritdoc/>
    public SnapshotPoint? GetPoint(Predicate<ITextBuffer> match, PositionAffinity affinity) => point;
}

/// <summary>
/// Mock implementation of IEditorOptions for testing.
/// </summary>
public class MockEditorOptions : IEditorOptions
{
    private IEditorOptions _parent;

    /// <inheritdoc/>
    public IEditorOptions Parent { get => _parent; set => _parent = value; }

    /// <inheritdoc/>
    public IEditorOptions GlobalOptions => this;

#pragma warning disable CS0067
    /// <inheritdoc/>
    public event EventHandler<EditorOptionChangedEventArgs> OptionChanged;
#pragma warning restore CS0067

    /// <inheritdoc/>
    public bool ClearOptionValue(string optionId) => false;

    /// <inheritdoc/>
    public bool ClearOptionValue<T>(EditorOptionKey<T> key) => false;

    /// <inheritdoc/>
    public object GetOptionValue(string optionId) => null;

    /// <inheritdoc/>
    public T GetOptionValue<T>(EditorOptionKey<T> key) => default;

    /// <inheritdoc/>
    public T GetOptionValue<T>(string optionId) => default;

    /// <inheritdoc/>
    public bool IsOptionDefined(string optionId, bool localScopeOnly) => false;

    /// <inheritdoc/>
    public bool IsOptionDefined<T>(EditorOptionKey<T> key, bool localScopeOnly) => false;

    /// <inheritdoc/>
    public void SetOptionValue(string optionId, object value) { }

    /// <inheritdoc/>
    public void SetOptionValue<T>(EditorOptionKey<T> key, T value) { }

    /// <inheritdoc/>
    public IEnumerable<EditorOptionDefinition> SupportedOptions => throw new NotImplementedException();
}