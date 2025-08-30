using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using SerilogSyntax.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace SerilogSyntax.Tagging
{
    /// <summary>
    /// Provides brace matching taggers for Serilog template properties.
    /// </summary>
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("CSharp")]
    [TagType(typeof(TextMarkerTag))]
    internal sealed class SerilogBraceMatcherProvider : IViewTaggerProvider
{
    /// <summary>
    /// Creates a tagger for brace matching in Serilog templates.
    /// </summary>
    /// <typeparam name="T">The type of tag.</typeparam>
    /// <param name="textView">The text view.</param>
    /// <param name="buffer">The text buffer.</param>
    /// <returns>A brace matching tagger, or null if the parameters are invalid.</returns>
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null || buffer == null || buffer != textView.TextBuffer)
                return null;

            return new SerilogBraceMatcher(textView, buffer) as ITagger<T>;
        }
    }

    /// <summary>
    /// Provides brace matching highlights for Serilog template properties.
    /// Highlights matching opening and closing braces when the caret is positioned on or near them.
    /// </summary>
    internal sealed class SerilogBraceMatcher : ITagger<TextMarkerTag>, IDisposable
    {
        private readonly ITextView _view;
        private readonly ITextBuffer _buffer;
        private readonly SerilogBraceHighlightState _state;
        
        private SnapshotPoint? _currentChar;
        private bool _disposed;

        /// <summary>
        /// Event raised when tags have changed.
        /// </summary>
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="SerilogBraceMatcher"/> class.
        /// </summary>
        /// <param name="view">The text view.</param>
        /// <param name="buffer">The text buffer.</param>
        public SerilogBraceMatcher(ITextView view, ITextBuffer buffer)
        {
            _view = view;
            _buffer = buffer;
            _state = SerilogBraceHighlightState.GetOrCreate(view);

            _view.Caret.PositionChanged += CaretPositionChanged;
            _view.LayoutChanged += ViewLayoutChanged;
            _state.StateChanged += StateChanged;
            _view.Closed += View_Closed;
        }

        private void View_Closed(object sender, EventArgs e) => Dispose();

        private void StateChanged(object sender, EventArgs e) => RaiseRefreshForEntireSnapshot();

        /// <summary>
        /// Handles view layout changes to update brace matching.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (e.NewSnapshot != e.OldSnapshot)
                UpdateAtCaretPosition(_view.Caret.Position);
        }

        /// <summary>
        /// Handles caret position changes to update brace matching.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
            => UpdateAtCaretPosition(e.NewPosition);

        /// <summary>
        /// Updates brace matching tags based on the caret position.
        /// </summary>
        /// <param name="caretPosition">The caret position.</param>
        private void UpdateAtCaretPosition(CaretPosition caretPosition)
        {
            _currentChar = caretPosition.Point.GetPoint(_buffer, caretPosition.Affinity);
            if (_currentChar.HasValue)
                RaiseRefreshForEntireSnapshot();
        }

        private void RaiseRefreshForEntireSnapshot()
        {
            var snapshot = _buffer.CurrentSnapshot;
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

    /// <summary>
    /// Gets the tags that intersect the given spans.
    /// </summary>
    /// <param name="spans">The spans to get tags for.</param>
    /// <returns>Tags for matching braces if the caret is positioned on a brace in a Serilog template.</returns>
        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (_disposed || !_currentChar.HasValue || spans.Count == 0)
                yield break;

            var snapshot = spans[0].Snapshot;

            // Respect user setting: if automatic delimiter highlighting is off, do nothing.
            // Note: In VS 2022, this option is accessed via the editor options system
            // For now, we'll always enable brace matching as the option check requires
            // additional references to access the option properly

            var currentChar = _currentChar.Value;
            if (currentChar.Position >= snapshot.Length)
                yield break;

            var currentLine = currentChar.GetContainingLine();
            var lineStart = currentLine.Start.Position;
            var lineText = currentLine.GetText();
            
            // Check if we're in a Serilog call
            if (!IsSerilogCall(lineText))
                yield break;

            var charAtCaret = currentChar.GetChar();
            var positionInLine = currentChar.Position - lineStart;

            int open = -1, close = -1;

            // Check if caret is on a brace
            if (charAtCaret == '{')
            {
                open = positionInLine;
                close = FindMatchingCloseBrace(lineText, positionInLine);
            }
            else if (charAtCaret == '}')
            {
                close = positionInLine;
                open = FindMatchingOpenBrace(lineText, positionInLine);
            }
            else if (positionInLine > 0 && lineText[positionInLine - 1] == '}')
            {
                // Caret is just after a closing brace
                close = positionInLine - 1;
                open = FindMatchingOpenBrace(lineText, close);
            }

            if (open >= 0 && close >= 0)
            {
                var openPt = new SnapshotPoint(snapshot, lineStart + open);
                var closePt = new SnapshotPoint(snapshot, lineStart + close);

                // Tell the state which pair we're on. This also clears prior dismissal
                // if we've moved to a different pair.
                _state.SetCurrentPair(openPt, closePt);

                if (!_state.IsDismissedForCurrentPair)
                {
                    yield return CreateTagSpan(snapshot, openPt.Position, 1);
                    yield return CreateTagSpan(snapshot, closePt.Position, 1);
                }
            }
            else
            {
                // Cursor is not on a brace - clear the current pair and any dismissal
                _state.ClearCurrentPair();
            }
        }

    /// <summary>
    /// Determines whether the given line contains a Serilog call.
    /// </summary>
    /// <param name="line">The line to check.</param>
    /// <returns>True if the line contains a Serilog call; otherwise, false.</returns>
    private bool IsSerilogCall(string line)
    {
        return SerilogCallDetector.IsSerilogCall(line);
    }

    /// <summary>
    /// Finds the matching closing brace for an opening brace.
    /// </summary>
    /// <param name="text">The text to search in.</param>
    /// <param name="openBracePos">The position of the opening brace.</param>
    /// <returns>The position of the matching closing brace, or -1 if not found.</returns>
    private int FindMatchingCloseBrace(string text, int openBracePos)
    {
        if (openBracePos + 1 < text.Length && text[openBracePos + 1] == '{')
            return -1; // Escaped brace

        int braceCount = 1;
        for (int i = openBracePos + 1; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                if (i + 1 < text.Length && text[i + 1] == '{')
                {
                    i++; // Skip escaped brace
                    continue;
                }
                braceCount++;
            }
            else if (text[i] == '}')
            {
                if (i + 1 < text.Length && text[i + 1] == '}')
                {
                    i++; // Skip escaped brace
                    continue;
                }
                braceCount--;
                if (braceCount == 0)
                    return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Finds the matching opening brace for a closing brace.
    /// </summary>
    /// <param name="text">The text to search in.</param>
    /// <param name="closeBracePos">The position of the closing brace.</param>
    /// <returns>The position of the matching opening brace, or -1 if not found.</returns>
    private int FindMatchingOpenBrace(string text, int closeBracePos)
    {
        if (closeBracePos > 0 && text[closeBracePos - 1] == '}')
            return -1; // Escaped brace

        int braceCount = 1;
        for (int i = closeBracePos - 1; i >= 0; i--)
        {
            if (text[i] == '}')
            {
                if (i > 0 && text[i - 1] == '}')
                {
                    i--; // Skip escaped brace
                    continue;
                }
                braceCount++;
            }
            else if (text[i] == '{')
            {
                if (i > 0 && text[i - 1] == '{')
                {
                    i--; // Skip escaped brace
                    continue;
                }
                braceCount--;
                if (braceCount == 0)
                    return i;
            }
        }
        return -1;
    }

        /// <summary>
        /// Creates a tag span for highlighting a brace.
        /// </summary>
        /// <param name="snapshot">The text snapshot.</param>
        /// <param name="start">The start position of the brace.</param>
        /// <param name="length">The length of the span (typically 1 for a single brace).</param>
        /// <returns>A tag span for the brace highlight.</returns>
        private ITagSpan<TextMarkerTag> CreateTagSpan(ITextSnapshot snapshot, int start, int length)
        {
            var span = new SnapshotSpan(snapshot, start, length);
            var tag = new TextMarkerTag("bracehighlight");
            return new TagSpan<TextMarkerTag>(span, tag);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _view.Caret.PositionChanged -= CaretPositionChanged;
            _view.LayoutChanged -= ViewLayoutChanged;
            _view.Closed -= View_Closed;
            _state.StateChanged -= StateChanged;
        }
    }
}