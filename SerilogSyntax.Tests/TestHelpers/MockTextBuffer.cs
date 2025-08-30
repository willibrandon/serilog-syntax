using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;

namespace SerilogSyntax.Tests.TestHelpers;

/// <summary>
/// Mock implementation of ITextBuffer for testing.
/// Provides a minimal implementation of ITextBuffer that tracks text changes and raises events.
/// </summary>
public class MockTextBuffer : ITextBuffer
{
    private ITextSnapshot _currentSnapshot;
    private readonly IContentType _contentType;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MockTextBuffer"/> class.
    /// </summary>
    /// <param name="text">The initial text content of the buffer.</param>
    private MockTextBuffer(string text)
    {
        _contentType = new MockContentType("CSharp");
        _currentSnapshot = new MockTextSnapshot(text, this, 0);
    }
    
    /// <summary>
    /// Creates a new mock text buffer with the specified initial text.
    /// </summary>
    /// <param name="text">The initial text content.</param>
    /// <returns>A new ITextBuffer instance for testing.</returns>
    public static ITextBuffer Create(string text)
    {
        return new MockTextBuffer(text);
    }
    
    /// <inheritdoc/>
    public ITextSnapshot CurrentSnapshot => _currentSnapshot;
    
    /// <summary>
    /// Replaces text in the buffer and raises appropriate change events.
    /// </summary>
    /// <param name="replaceSpan">The span of text to replace.</param>
    /// <param name="replaceWith">The replacement text.</param>
    public void Replace(Span replaceSpan, string replaceWith)
    {
        var currentText = _currentSnapshot.GetText();
        var newText = currentText.Substring(0, replaceSpan.Start) + 
                     replaceWith + 
                     currentText.Substring(replaceSpan.Start + replaceSpan.Length);
        
        var oldSnapshot = _currentSnapshot;
        var textChanges = new[] { new MockTextChange(replaceSpan.Start, replaceSpan.Length, oldSnapshot, replaceWith.Length) };
        var normalizedChanges = new MockNormalizedTextChangeCollection(textChanges);
        _currentSnapshot = new MockTextSnapshot(newText, this, _currentSnapshot.Version.VersionNumber + 1, normalizedChanges);
        
        // Raise the Changed event
        var args = new TextContentChangedEventArgs(oldSnapshot, _currentSnapshot, EditOptions.None, textChanges);
        Changed?.Invoke(this, args);
    }

    #region ITextBuffer Implementation
    
    /// <inheritdoc/>
    public event EventHandler<TextContentChangedEventArgs> Changed;
#pragma warning disable CS0067 // The event is never used

    /// <inheritdoc/>
    public event EventHandler<TextContentChangingEventArgs> Changing;

    /// <inheritdoc/>
    public event EventHandler<TextContentChangedEventArgs> ChangedHighPriority;

    /// <inheritdoc/>
    public event EventHandler<TextContentChangedEventArgs> ChangedLowPriority;

    /// <inheritdoc/>
    public event EventHandler ChangingContentType;

    /// <inheritdoc/>
    public event EventHandler<ContentTypeChangedEventArgs> ContentTypeChanged;

    /// <inheritdoc/>
    public event EventHandler PostChanged;

    /// <inheritdoc/>
    public event EventHandler<SnapshotSpanEventArgs> ReadOnlyRegionsChanged;
#pragma warning restore CS0067
    
    /// <inheritdoc/>
    public IContentType ContentType => _contentType;

    /// <inheritdoc/>
    public ITextEdit CreateEdit() => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITextEdit CreateEdit(EditOptions options, int? reiteratedVersionNumber, object editTag) => throw new NotImplementedException();

    /// <inheritdoc/>
    public IReadOnlyRegionEdit CreateReadOnlyRegionEdit() => throw new NotImplementedException();

    /// <inheritdoc/>
    public void ChangeContentType(IContentType newContentType, object editTag) => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITextSnapshot Delete(Span deleteSpan) => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITextSnapshot Insert(int position, string text) => throw new NotImplementedException();

    /// <inheritdoc/>
    ITextSnapshot ITextBuffer.Replace(Span replaceSpan, string replaceWith)
    {
        Replace(replaceSpan, replaceWith);
        return _currentSnapshot;
    }

    /// <inheritdoc/>
    public bool CheckEditAccess() => true;

    /// <inheritdoc/>
    public void TakeThreadOwnership() { }

    /// <inheritdoc/>
    public bool EditInProgress => false;

    /// <inheritdoc/>
    public PropertyCollection Properties { get; } = new PropertyCollection();
    
    /// <inheritdoc/>
    public bool IsReadOnly(int position) => false;

    /// <inheritdoc/>
    public bool IsReadOnly(int position, bool isEdit) => false;

    /// <inheritdoc/>
    public bool IsReadOnly(Span span) => false;

    /// <inheritdoc/>
    public bool IsReadOnly(Span span, bool isEdit) => false;

    /// <inheritdoc/>
    public NormalizedSpanCollection GetReadOnlyExtents(Span span) => NormalizedSpanCollection.Empty;
    
    #endregion
}

/// <summary>
/// Mock implementation of ITextChange for testing.
/// Represents a single text change within a text buffer.
/// </summary>
/// <param name="oldPosition">The position in the old snapshot where the change starts.</param>
/// <param name="oldLength">The length of text being replaced in the old snapshot.</param>
/// <param name="oldSnapshot">The snapshot before the change.</param>
/// <param name="newLength">The length of the replacement text.</param>
internal class MockTextChange(int oldPosition, int oldLength, ITextSnapshot oldSnapshot, int newLength) : ITextChange
{
    /// <inheritdoc/>
    public int OldPosition { get; } = oldPosition;

    /// <inheritdoc/>
    public int OldLength { get; } = oldLength;

    /// <inheritdoc/>
    public string OldText { get; } = oldLength > 0 ? oldSnapshot.GetText(new Span(oldPosition, oldLength)) : string.Empty;

    /// <inheritdoc/>
    public int NewPosition { get; } = oldPosition;

    /// <inheritdoc/>
    public int NewLength { get; } = newLength;

    /// <inheritdoc/>
    public string NewText { get; } = string.Empty; // We don't have the new snapshot yet when creating the change

    /// <inheritdoc/>
    public Span OldSpan { get; } = new Span(oldPosition, oldLength);

    /// <inheritdoc/>
    public Span NewSpan { get; } = new Span(oldPosition, newLength);

    /// <inheritdoc/>
    public int OldEnd { get; } = oldPosition + oldLength;

    /// <inheritdoc/>
    public int NewEnd { get; } = oldPosition + newLength;

    /// <inheritdoc/>
    public int Delta { get; } = newLength - oldLength;

    /// <inheritdoc/>
    public int LineCountDelta { get; } = 0; // Simplified for testing
}