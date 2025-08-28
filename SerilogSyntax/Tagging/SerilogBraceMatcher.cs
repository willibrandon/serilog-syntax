using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using SerilogSyntax.Classification;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;

namespace SerilogSyntax.Tagging
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("CSharp")]
    [TagType(typeof(TextMarkerTag))]
    internal class SerilogBraceMatcherProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null || buffer == null)
                return null;

            if (buffer != textView.TextBuffer)
                return null;

            return new SerilogBraceMatcher(textView, buffer) as ITagger<T>;
        }
    }

    internal class SerilogBraceMatcher : ITagger<TextMarkerTag>
    {
        private readonly ITextView _view;
        private readonly ITextBuffer _buffer;
        private SnapshotPoint? _currentChar;
        private static readonly Regex SerilogCallRegex = new Regex(
            @"(?:\b\w+\.(?:ForContext(?:<[^>]+>)?\([^)]*\)\.)?(?:Log)?(?:Verbose|Debug|Information|Warning|Error|Fatal|Write)\s*\()|(?:outputTemplate\s*:\s*)",
            RegexOptions.Compiled);

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public SerilogBraceMatcher(ITextView view, ITextBuffer buffer)
        {
            _view = view;
            _buffer = buffer;
            _view.Caret.PositionChanged += CaretPositionChanged;
            _view.LayoutChanged += ViewLayoutChanged;
        }

        private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        {
            if (e.NewSnapshot != e.OldSnapshot)
                UpdateAtCaretPosition(_view.Caret.Position);
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            UpdateAtCaretPosition(e.NewPosition);
        }

        private void UpdateAtCaretPosition(CaretPosition caretPosition)
        {
            _currentChar = caretPosition.Point.GetPoint(_buffer, caretPosition.Affinity);
            if (_currentChar.HasValue)
            {
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(
                        new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
            }
        }

        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (!_currentChar.HasValue || spans.Count == 0)
                yield break;

            var currentChar = _currentChar.Value;
            var snapshot = spans[0].Snapshot;

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

            // Check if caret is on a brace
            if (charAtCaret == '{')
            {
                var matchingBrace = FindMatchingCloseBrace(lineText, positionInLine);
                if (matchingBrace >= 0)
                {
                    yield return CreateTagSpan(snapshot, lineStart + positionInLine, 1);
                    yield return CreateTagSpan(snapshot, lineStart + matchingBrace, 1);
                }
            }
            else if (charAtCaret == '}')
            {
                var matchingBrace = FindMatchingOpenBrace(lineText, positionInLine);
                if (matchingBrace >= 0)
                {
                    yield return CreateTagSpan(snapshot, lineStart + matchingBrace, 1);
                    yield return CreateTagSpan(snapshot, lineStart + positionInLine, 1);
                }
            }
            else if (positionInLine > 0 && lineText[positionInLine - 1] == '}')
            {
                // Caret is just after a closing brace
                var matchingBrace = FindMatchingOpenBrace(lineText, positionInLine - 1);
                if (matchingBrace >= 0)
                {
                    yield return CreateTagSpan(snapshot, lineStart + matchingBrace, 1);
                    yield return CreateTagSpan(snapshot, lineStart + positionInLine - 1, 1);
                }
            }
        }

        private bool IsSerilogCall(string line)
        {
            return SerilogCallRegex.IsMatch(line);
        }

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

        private ITagSpan<TextMarkerTag> CreateTagSpan(ITextSnapshot snapshot, int start, int length)
        {
            var span = new SnapshotSpan(snapshot, start, length);
            var tag = new TextMarkerTag("bracehighlight");
            return new TagSpan<TextMarkerTag>(span, tag);
        }
    }
}