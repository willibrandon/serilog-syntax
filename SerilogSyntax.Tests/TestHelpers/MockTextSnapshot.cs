using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SerilogSyntax.Tests.TestHelpers;

/// <summary>
/// Mock implementation of ITextSnapshot for testing.
/// Represents a snapshot of the text buffer at a specific point in time.
/// </summary>
public class MockTextSnapshot : ITextSnapshot
{
    private readonly string _text;
    private readonly ITextBuffer _textBuffer;
    private readonly List<ITextSnapshotLine> _lines;
    private readonly ITextVersion _version;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MockTextSnapshot"/> class.
    /// </summary>
    /// <param name="text">The text content of the snapshot.</param>
    /// <param name="textBuffer">The text buffer this snapshot belongs to.</param>
    /// <param name="versionNumber">The version number of this snapshot.</param>
    /// <param name="changes">Optional collection of changes from the previous version.</param>
    public MockTextSnapshot(string text, ITextBuffer textBuffer, int versionNumber, INormalizedTextChangeCollection changes = null)
    {
        _text = text ?? string.Empty;
        _textBuffer = textBuffer;
        _version = new MockTextVersion(versionNumber, changes);
        _lines = CreateLines(text);
    }
    
    /// <summary>
    /// Creates line objects from the text content.
    /// </summary>
    /// <param name="text">The text to parse into lines.</param>
    /// <returns>A list of text snapshot lines.</returns>
    private List<ITextSnapshotLine> CreateLines(string text)
    {
        var lines = new List<ITextSnapshotLine>();
        var lineStart = 0;
        var lineNumber = 0;
        
        for (int i = 0; i <= text.Length; i++)
        {
            bool isEndOfLine = false;
            int lineBreakLength = 0;
            
            if (i < text.Length)
            {
                if (text[i] == '\r')
                {
                    isEndOfLine = true;
                    lineBreakLength = (i + 1 < text.Length && text[i + 1] == '\n') ? 2 : 1;
                }
                else if (text[i] == '\n')
                {
                    isEndOfLine = true;
                    lineBreakLength = 1;
                }
            }
            else if (i == text.Length && (lines.Count == 0 || lineStart < text.Length))
            {
                // Add the last line even if it doesn't end with a line break
                isEndOfLine = true;
                lineBreakLength = 0;
            }
            
            if (isEndOfLine)
            {
                var lineLength = i - lineStart;
                lines.Add(new MockTextSnapshotLine(this, lineNumber, lineStart, lineLength, lineBreakLength));
                lineStart = i + lineBreakLength;
                i += lineBreakLength - 1; // -1 because the loop will increment i
                lineNumber++;
            }
        }
        
        return lines;
    }
    
    /// <inheritdoc/>
    public string GetText() => _text;

    /// <inheritdoc/>
    public string GetText(Span span) => _text.Substring(span.Start, span.Length);

    /// <inheritdoc/>
    public string GetText(int startIndex, int length) => _text.Substring(startIndex, length);
    
    /// <inheritdoc/>
    public ITextSnapshotLine GetLineFromPosition(int position)
    {
        foreach (var line in _lines)
        {
            if (position >= line.Start && position <= line.EndIncludingLineBreak)
                return line;
        }
        return _lines.LastOrDefault() ?? throw new ArgumentOutOfRangeException(nameof(position));
    }
    
    /// <inheritdoc/>
    public ITextSnapshotLine GetLineFromLineNumber(int lineNumber)
    {
        if (lineNumber >= 0 && lineNumber < _lines.Count)
            return _lines[lineNumber];
        throw new ArgumentOutOfRangeException(nameof(lineNumber));
    }
    
    /// <inheritdoc/>
    public int LineCount => _lines.Count;

    /// <inheritdoc/>
    public IEnumerable<ITextSnapshotLine> Lines => _lines;

    /// <inheritdoc/>
    public int Length => _text.Length;

    /// <inheritdoc/>
    public ITextBuffer TextBuffer => _textBuffer;

    /// <inheritdoc/>
    public IContentType ContentType => _textBuffer.ContentType;

    /// <inheritdoc/>
    public ITextVersion Version => _version;

    /// <inheritdoc/>
    public char this[int position] => _text[position];
    
    /// <inheritdoc/>
    public int GetLineNumberFromPosition(int position) => GetLineFromPosition(position).LineNumber;

    /// <inheritdoc/>
    public char[] ToCharArray(int startIndex, int length) => _text.Substring(startIndex, length).ToCharArray();

    /// <inheritdoc/>
    public string[] GetLineTextUpToPosition(int position) => throw new NotImplementedException();

    /// <inheritdoc/>
    public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) => _text.CopyTo(sourceIndex, destination, destinationIndex, count);

    /// <inheritdoc/>
    public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode) => new MockTrackingPoint(this, position, trackingMode);

    /// <inheritdoc/>
    public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => new MockTrackingPoint(this, position, trackingMode);

    /// <inheritdoc/>
    public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode) => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode) => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();

    /// <inheritdoc/>
    public void Write(TextWriter writer, Span span) => writer.Write(GetText(span));

    /// <inheritdoc/>
    public void Write(TextWriter writer) => writer.Write(_text);
}

/// <summary>
/// Mock implementation of ITextSnapshotLine for testing.
/// Represents a single line within a text snapshot.
/// </summary>
internal class MockTextSnapshotLine : ITextSnapshotLine
{
    private readonly ITextSnapshot _snapshot;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MockTextSnapshotLine"/> class.
    /// </summary>
    /// <param name="snapshot">The snapshot this line belongs to.</param>
    /// <param name="lineNumber">The zero-based line number.</param>
    /// <param name="start">The starting position of the line in the snapshot.</param>
    /// <param name="length">The length of the line excluding line break.</param>
    /// <param name="lineBreakLength">The length of the line break (0, 1, or 2).</param>
    public MockTextSnapshotLine(ITextSnapshot snapshot, int lineNumber, int start, int length, int lineBreakLength)
    {
        _snapshot = snapshot;
        LineNumber = lineNumber;
        Start = new SnapshotPoint(snapshot, start);
        Length = length;
        LengthIncludingLineBreak = length + lineBreakLength;
        LineBreakLength = lineBreakLength;
        End = new SnapshotPoint(snapshot, start + length);
        EndIncludingLineBreak = new SnapshotPoint(snapshot, start + length + lineBreakLength);
        Extent = new SnapshotSpan(Start, End);
        ExtentIncludingLineBreak = new SnapshotSpan(Start, EndIncludingLineBreak);
    }
    
    /// <inheritdoc/>
    public string GetText() => _snapshot.GetText(new Span(Start.Position, Length));

    /// <inheritdoc/>
    public string GetTextIncludingLineBreak() => _snapshot.GetText(new Span(Start.Position, LengthIncludingLineBreak));

    /// <inheritdoc/>
    public string GetLineBreakText() => LineBreakLength > 0 ? _snapshot.GetText(new Span(End.Position, LineBreakLength)) : string.Empty;
    
    /// <inheritdoc/>
    public ITextSnapshot Snapshot => _snapshot;

    /// <inheritdoc/>
    public int LineNumber { get; }

    /// <inheritdoc/>
    public SnapshotPoint Start { get; }

    /// <inheritdoc/>
    public int Length { get; }

    /// <inheritdoc/>
    public int LengthIncludingLineBreak { get; }

    /// <inheritdoc/>
    public SnapshotPoint End { get; }

    /// <inheritdoc/>
    public SnapshotPoint EndIncludingLineBreak { get; }

    /// <inheritdoc/>
    public int LineBreakLength { get; }

    /// <inheritdoc/>
    public SnapshotSpan Extent { get; }

    /// <inheritdoc/>
    public SnapshotSpan ExtentIncludingLineBreak { get; }
}

/// <summary>
/// Mock implementation of INormalizedTextChangeCollection for testing.
/// Represents a normalized collection of text changes.
/// </summary>
/// <param name="changes">The text changes to include in the collection.</param>
internal class MockNormalizedTextChangeCollection(params ITextChange[] changes) : INormalizedTextChangeCollection
{
    private readonly List<ITextChange> _changes = [.. changes ?? []];

    /// <inheritdoc/>
    public ITextChange this[int index] 
    { 
        get => _changes[index];
        set => _changes[index] = value;
    }
    
    /// <inheritdoc/>
    public int Count => _changes.Count;

    /// <inheritdoc/>
    public bool IncludesLineChanges => false;

    /// <inheritdoc/>
    public bool IsReadOnly => false;
    
    /// <inheritdoc/>
    public int IndexOf(ITextChange item) => _changes.IndexOf(item);

    /// <inheritdoc/>
    public void Insert(int index, ITextChange item) => _changes.Insert(index, item);

    /// <inheritdoc/>
    public void RemoveAt(int index) => _changes.RemoveAt(index);

    /// <inheritdoc/>
    public void Add(ITextChange item) => _changes.Add(item);

    /// <inheritdoc/>
    public void Clear() => _changes.Clear();

    /// <inheritdoc/>
    public bool Contains(ITextChange item) => _changes.Contains(item);

    /// <inheritdoc/>
    public void CopyTo(ITextChange[] array, int arrayIndex) => _changes.CopyTo(array, arrayIndex);

    /// <inheritdoc/>
    public bool Remove(ITextChange item) => _changes.Remove(item);
    
    /// <inheritdoc/>
    public IEnumerator<ITextChange> GetEnumerator() => _changes.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => _changes.GetEnumerator();
}

/// <summary>
/// Mock implementation of ITextVersion for testing.
/// Represents version information for a text snapshot.
/// </summary>
/// <param name="versionNumber">The version number.</param>
/// <param name="changes">Optional collection of changes from the previous version.</param>
internal class MockTextVersion(int versionNumber, INormalizedTextChangeCollection changes = null) : ITextVersion
{
    private readonly INormalizedTextChangeCollection _changes = changes ?? new MockNormalizedTextChangeCollection();

    /// <inheritdoc/>
    public ITextBuffer TextBuffer => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITextVersion Next => null;

    /// <inheritdoc/>
    public int Length => throw new NotImplementedException();

    /// <inheritdoc/>
    public INormalizedTextChangeCollection Changes => _changes;

    /// <inheritdoc/>
    public int VersionNumber { get; } = versionNumber;

    /// <inheritdoc/>
    public int ReiteratedVersionNumber { get; } = versionNumber;

    /// <inheritdoc/>
    public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode) => new MockTrackingPoint(null, position, trackingMode);

    /// <inheritdoc/>
    public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => new MockTrackingPoint(null, position, trackingMode);

    /// <inheritdoc/>
    public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode) => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode) => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) => throw new NotImplementedException();

    /// <inheritdoc/>
    public ITrackingSpan CreateCustomTrackingSpan(Span span, TrackingFidelityMode trackingFidelity, object customState, CustomTrackToVersion behavior) => throw new NotImplementedException();
}

/// <summary>
/// Mock implementation of IContentType for testing.
/// Represents the content type of a text buffer.
/// </summary>
/// <param name="typeName">The name of the content type.</param>
internal class MockContentType(string typeName) : IContentType
{
    /// <inheritdoc/>
    public string TypeName { get; } = typeName;

    /// <inheritdoc/>
    public string DisplayName { get; } = typeName;

    /// <inheritdoc/>
    public IEnumerable<IContentType> BaseTypes { get; } = [];

    /// <inheritdoc/>
    public bool IsOfType(string type) => TypeName.Equals(type, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Mock implementation of ITrackingPoint for testing.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MockTrackingPoint"/> class.
/// </remarks>
/// <param name="snapshot">The text snapshot.</param>
/// <param name="position">The position in the snapshot.</param>
/// <param name="trackingMode">The tracking mode.</param>
public class MockTrackingPoint(ITextSnapshot snapshot, int position, PointTrackingMode trackingMode) : ITrackingPoint
{
    /// <inheritdoc/>
    public ITextBuffer TextBuffer => snapshot?.TextBuffer;

    /// <inheritdoc/>
    public PointTrackingMode TrackingMode => trackingMode;

    /// <inheritdoc/>
    public TrackingFidelityMode TrackingFidelity => TrackingFidelityMode.Forward;

    /// <inheritdoc/>
    public SnapshotPoint GetPoint(ITextSnapshot snapshot) => new(snapshot, Math.Min(position, snapshot.Length));

    /// <inheritdoc/>
    public char GetCharacter(ITextSnapshot snapshot) => GetPoint(snapshot).GetChar();

    /// <inheritdoc/>
    public int GetPosition(ITextSnapshot snapshot) => GetPoint(snapshot).Position;

    /// <inheritdoc/>
    public int GetPosition(ITextVersion version) => position;
}