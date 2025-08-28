using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using SerilogSyntax.Parsing;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SerilogSyntax.Navigation
{
    [Export(typeof(ISuggestedActionsSourceProvider))]
    [Name("Serilog Navigation")]
    [ContentType("CSharp")]
    internal class SerilogSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
    {
        [Import]
        internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
        {
            if (textBuffer == null || textView == null)
                return null;
            
            return new SerilogSuggestedActionsSource(this, textView, textBuffer);
        }
    }

    internal class SerilogSuggestedActionsSource : ISuggestedActionsSource
    {
        private readonly SerilogSuggestedActionsSourceProvider _provider;
        private readonly ITextView _textView;
        private readonly ITextBuffer _textBuffer;
        private readonly TemplateParser _parser = new TemplateParser();
        private static readonly Regex SerilogCallRegex = new Regex(
            @"\b(?:Log|_?logger|_?log)\.(?:ForContext(?:<[^>]+>)?\([^)]*\)\.)?(?:Verbose|Debug|Information|Warning|Error|Fatal|Write)\s*\(",
            RegexOptions.Compiled);

        public SerilogSuggestedActionsSource(SerilogSuggestedActionsSourceProvider provider, ITextView textView, ITextBuffer textBuffer)
        {
            _provider = provider;
            _textView = textView;
            _textBuffer = textBuffer;
        }

        public async Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var triggerPoint = range.Start;
                var line = triggerPoint.GetContainingLine();
                var lineText = line.GetText();
                var lineStart = line.Start.Position;

                // Check if we're in a Serilog call
                var serilogMatch = SerilogCallRegex.Match(lineText);
                if (!serilogMatch.Success)
                    return false;

                // Find the template string
                var templateMatch = FindTemplateString(lineText, serilogMatch.Index + serilogMatch.Length);
                if (!templateMatch.HasValue)
                    return false;

                var (templateStart, templateEnd) = templateMatch.Value;
                var template = lineText.Substring(templateStart, templateEnd - templateStart);
                
                // Check if cursor is within template
                var positionInLine = triggerPoint.Position - lineStart;
                if (positionInLine < templateStart || positionInLine > templateEnd)
                    return false;

                // Parse template to find properties
                var properties = _parser.Parse(template).ToList();
                
                // Find which property the cursor is on
                var cursorPosInTemplate = positionInLine - templateStart;
                var property = properties.FirstOrDefault(p => 
                    cursorPosInTemplate >= p.BraceStartIndex && 
                    cursorPosInTemplate <= p.BraceEndIndex);

                return property != null && property.Type != PropertyType.Positional;
            }, cancellationToken);
        }

        public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
        {
            var triggerPoint = range.Start;
            var line = triggerPoint.GetContainingLine();
            var lineText = line.GetText();
            var lineStart = line.Start.Position;

            // Check if we're in a Serilog call
            var serilogMatch = SerilogCallRegex.Match(lineText);
            if (!serilogMatch.Success)
                yield break;

            // Find the template string
            var templateMatch = FindTemplateString(lineText, serilogMatch.Index + serilogMatch.Length);
            if (!templateMatch.HasValue)
                yield break;

            var (templateStart, templateEnd) = templateMatch.Value;
            var template = lineText.Substring(templateStart, templateEnd - templateStart);
            
            // Check if cursor is within template
            var positionInLine = triggerPoint.Position - lineStart;
            if (positionInLine < templateStart || positionInLine > templateEnd)
                yield break;

            // Parse template to find properties
            var properties = _parser.Parse(template).ToList();
            
            // Find which property the cursor is on
            var cursorPosInTemplate = positionInLine - templateStart;
            var property = properties.FirstOrDefault(p => 
                cursorPosInTemplate >= p.BraceStartIndex && 
                cursorPosInTemplate <= p.BraceEndIndex);

            if (property == null || property.Type == PropertyType.Positional)
                yield break;

            // Find the corresponding argument
            var argumentLocation = FindArgumentForProperty(lineText, templateEnd, property.Name);
            if (argumentLocation.HasValue)
            {
                var actions = new ISuggestedAction[] 
                {
                    new NavigateToArgumentAction(_textView, lineStart + argumentLocation.Value.Item1, argumentLocation.Value.Item2, property.Name)
                };
                yield return new SuggestedActionSet(null, actions, null, SuggestedActionSetPriority.Medium);
            }
        }

        private (int, int)? FindTemplateString(string line, int startIndex)
        {
            // Look for string literal after Serilog method call
            for (int i = startIndex; i < line.Length; i++)
            {
                if (char.IsWhiteSpace(line[i]))
                    continue;

                if (line[i] == '"')
                {
                    // Regular string
                    int end = i + 1;
                    while (end < line.Length && line[end] != '"')
                    {
                        if (line[end] == '\\')
                            end++; // Skip escaped char
                        end++;
                    }
                    if (end < line.Length)
                        return (i + 1, end);
                }
                else if (i + 1 < line.Length && line[i] == '@' && line[i + 1] == '"')
                {
                    // Verbatim string
                    int end = i + 2;
                    while (end < line.Length)
                    {
                        if (line[end] == '"')
                        {
                            if (end + 1 < line.Length && line[end + 1] == '"')
                            {
                                end += 2; // Skip escaped quote
                                continue;
                            }
                            return (i + 2, end);
                        }
                        end++;
                    }
                }
                else if (i + 2 < line.Length && line[i] == '$' && line[i + 1] == '@' && line[i + 2] == '"')
                {
                    // Interpolated verbatim string
                    return null; // Skip these for now
                }
                else if (i + 1 < line.Length && line[i] == '$' && line[i + 1] == '"')
                {
                    // Interpolated string
                    return null; // Skip these for now
                }
                else
                {
                    break;
                }
            }
            return null;
        }

        private (int, int)? FindArgumentForProperty(string line, int templateEnd, string propertyName)
        {
            // Look for comma-separated arguments after the template
            var argumentsStart = line.IndexOf(',', templateEnd);
            if (argumentsStart < 0)
                return null;

            // Simple heuristic: look for identifiers matching property name
            var propertyPattern = new Regex($@"\b{Regex.Escape(propertyName)}\b");
            var match = propertyPattern.Match(line, argumentsStart);
            
            if (match.Success)
            {
                return (match.Index, match.Length);
            }

            return null;
        }

        public void Dispose()
        {
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public event EventHandler<EventArgs> SuggestedActionsChanged { add { } remove { } }
    }

    internal class NavigateToArgumentAction : ISuggestedAction
    {
        private readonly ITextView _textView;
        private readonly int _position;
        private readonly int _length;
        private readonly string _propertyName;

        public NavigateToArgumentAction(ITextView textView, int position, int length, string propertyName)
        {
            _textView = textView;
            _position = position;
            _length = length;
            _propertyName = propertyName;
        }

        public string DisplayText => $"Navigate to '{_propertyName}' argument";

        public string IconAutomationText => null;

        public bool HasActionSets => false;

        public bool HasPreview => false;

        public string InputGestureText => null;

        public ImageMoniker IconMoniker => default(ImageMoniker);

        public async Task<IEnumerable<SuggestedActionSet>> GetActionSetsAsync(CancellationToken cancellationToken)
        {
            return await Task.FromResult(Enumerable.Empty<SuggestedActionSet>());
        }

        public async Task<object> GetPreviewAsync(CancellationToken cancellationToken)
        {
            return await Task.FromResult<object>(null);
        }

        public void Invoke(CancellationToken cancellationToken)
        {
            var snapshot = _textView.TextBuffer.CurrentSnapshot;
            var span = new SnapshotSpan(snapshot, _position, _length);
            
            _textView.Caret.MoveTo(span.Start);
            _textView.ViewScroller.EnsureSpanVisible(span);
            _textView.Selection.Select(span, false);
        }

        public bool TryGetTelemetryId(out Guid telemetryId)
        {
            telemetryId = Guid.Empty;
            return false;
        }

        public void Dispose()
        {
        }
    }
}