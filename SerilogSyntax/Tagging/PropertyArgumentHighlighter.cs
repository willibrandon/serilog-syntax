using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using SerilogSyntax.Parsing;
using SerilogSyntax.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SerilogSyntax.Tagging;

/// <summary>
/// Provides property-argument highlighting for Serilog templates.
/// </summary>
internal sealed class PropertyArgumentHighlighter : ITagger<TextMarkerTag>, IDisposable
{
    // Note: We use SerilogCallDetector for finding Serilog calls and StringLiteralParser for string extraction
    // to avoid duplicating regex patterns and parsing logic

    private readonly ITextView _view;
    private readonly ITextBuffer _buffer;
    private readonly TemplateParser _parser = new();
    private readonly StringLiteralParser _stringParser = new();
    private SnapshotPoint? _currentChar;
    private readonly List<ITagSpan<TextMarkerTag>> _currentTags = [];
    private bool _disposed;
    private readonly PropertyArgumentHighlightState _state;

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    public PropertyArgumentHighlighter(ITextView view, ITextBuffer buffer)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

        _currentChar = view.Caret.Position.Point.GetPoint(buffer, view.Caret.Position.Affinity);

        // Set up state tracking for ESC dismissal
        _state = PropertyArgumentHighlightState.GetOrCreate(_view);
        _state.StateChanged += OnStateChanged;

        _view.Caret.PositionChanged += CaretPositionChanged;
        _view.LayoutChanged += ViewLayoutChanged;
    }

    private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
        => UpdateAtCaretPosition(e.NewPosition);

    private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
    {
        if (e.NewSnapshot != e.OldSnapshot)
            UpdateAtCaretPosition(_view.Caret.Position);
    }

    private void OnStateChanged(object sender, EventArgs e)
    {
        // When state changes (ESC dismiss/restore), refresh tags
        var snapshot = _buffer.CurrentSnapshot;
        var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
        TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(fullSpan));
    }

    private void UpdateAtCaretPosition(CaretPosition caretPosition)
    {
        _currentChar = caretPosition.Point.GetPoint(_buffer, caretPosition.Affinity);
        if (_currentChar.HasValue)
        {
            var newTags = GetHighlightTags(_currentChar.Value).ToList();

            // Only raise event if tags actually changed
            if (!TagsEqual(newTags, _currentTags))
            {
                _currentTags.Clear();
                _currentTags.AddRange(newTags);

                var snapshot = _currentChar.Value.Snapshot;
                var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(fullSpan));
            }
        }
    }

    private bool TagsEqual(List<ITagSpan<TextMarkerTag>> tags1, List<ITagSpan<TextMarkerTag>> tags2)
    {
        if (tags1.Count != tags2.Count) return false;
        for (int i = 0; i < tags1.Count; i++)
        {
            if (tags1[i].Span != tags2[i].Span) return false;
        }
        return true;
    }

    private IEnumerable<ITagSpan<TextMarkerTag>> GetHighlightTags(SnapshotPoint caretPoint)
    {
        // Find the complete Serilog call, which may span multiple lines
        var callInfo = FindSerilogCall(caretPoint);
        if (callInfo == null)
            yield break;

        // Parse the template to get all properties
        var allProperties = _parser.Parse(callInfo.Template).ToList();
        if (!allProperties.Any())
            yield break;

        // Separate positional and named properties
        var positionalProperties = allProperties.Where(p => p.Type == PropertyType.Positional).ToList();
        var namedProperties = allProperties.Where(p => p.Type != PropertyType.Positional).ToList();

        // Check if cursor is on a property or argument
        var caretPosition = caretPoint.Position;

        // Check if cursor is on a positional property
        // For duplicate positional parameters, we need to find which occurrence the cursor is on
        for (int propIndex = 0; propIndex < allProperties.Count; propIndex++)
        {
            var property = allProperties[propIndex];
            if (property.Type != PropertyType.Positional) continue;

            // Adjust property positions if we have a verbatim string with escaped quotes
            var propStart = callInfo.TemplateStart + property.BraceStartIndex;
            var propEnd = callInfo.TemplateStart + property.BraceEndIndex + 1;

            // For verbatim strings, we need to adjust positions to account for escaped quotes
            if (callInfo.IsVerbatimString && callInfo.OriginalTemplate != null)
            {
                // Map position from cleaned template to original template with escaped quotes
                propStart = callInfo.TemplateStart + MapCleanedPositionToOriginal(callInfo.OriginalTemplate, property.BraceStartIndex);
                propEnd = callInfo.TemplateStart + MapCleanedPositionToOriginal(callInfo.OriginalTemplate, property.BraceEndIndex + 1);
            }

            if (caretPosition >= propStart && caretPosition <= propEnd)
            {
                // Highlight the positional property
                var propertySpan = new SnapshotSpan(caretPoint.Snapshot, propStart, propEnd - propStart);
                yield return new TagSpan<TextMarkerTag>(propertySpan, new TextMarkerTag("MarkerFormatDefinition/HighlightedReference"));

                // For positional parameters, count how many positional properties come before this one
                // Each positional property consumes one argument in order
                var positionalsBefore = allProperties.Take(propIndex).Count(p => p.Type == PropertyType.Positional);

                // The argument index is the count of positional properties before this one
                if (positionalsBefore < callInfo.Arguments.Count)
                {
                    var arg = callInfo.Arguments[positionalsBefore];
                    var argSpan = new SnapshotSpan(caretPoint.Snapshot, arg.Start, arg.Length);
                    yield return new TagSpan<TextMarkerTag>(argSpan, new TextMarkerTag("MarkerFormatDefinition/HighlightedReference"));
                }
                yield break;
            }
        }

        // Check if cursor is on a named property
        for (int i = 0; i < namedProperties.Count; i++)
        {
            var property = namedProperties[i];
            var propStart = callInfo.TemplateStart + property.BraceStartIndex;
            var propEnd = callInfo.TemplateStart + property.BraceEndIndex + 1;

            // For verbatim strings, adjust positions to account for escaped quotes
            if (callInfo.IsVerbatimString && callInfo.OriginalTemplate != null)
            {
                propStart = callInfo.TemplateStart + MapCleanedPositionToOriginal(callInfo.OriginalTemplate, property.BraceStartIndex);
                propEnd = callInfo.TemplateStart + MapCleanedPositionToOriginal(callInfo.OriginalTemplate, property.BraceEndIndex + 1);
            }

            if (caretPosition >= propStart && caretPosition <= propEnd)
            {
                // Highlight the property
                var propertySpan = new SnapshotSpan(caretPoint.Snapshot, propStart, propEnd - propStart);
                yield return new TagSpan<TextMarkerTag>(propertySpan, new TextMarkerTag("MarkerFormatDefinition/HighlightedReference"));

                // For named properties, find the argument index
                // Named properties map to arguments after the highest positional index
                var maxPositionalIndex = positionalProperties
                    .Select(p => int.TryParse(p.Name, out var idx) ? idx : -1)
                    .DefaultIfEmpty(-1)
                    .Max();
                var argIndex = maxPositionalIndex + 1 + i;
                if (argIndex < callInfo.Arguments.Count)
                {
                    var arg = callInfo.Arguments[argIndex];
                    var argSpan = new SnapshotSpan(caretPoint.Snapshot, arg.Start, arg.Length);
                    yield return new TagSpan<TextMarkerTag>(argSpan, new TextMarkerTag("MarkerFormatDefinition/HighlightedReference"));
                }
                yield break;
            }
        }

        // Check if cursor is on an argument
        for (int i = 0; i < callInfo.Arguments.Count; i++)
        {
            var arg = callInfo.Arguments[i];
            if (caretPosition >= arg.Start && caretPosition <= arg.Start + arg.Length)
            {
                // Highlight the argument
                var argSpan = new SnapshotSpan(caretPoint.Snapshot, arg.Start, arg.Length);
                yield return new TagSpan<TextMarkerTag>(argSpan, new TextMarkerTag("MarkerFormatDefinition/HighlightedReference"));

                // Check if this argument corresponds to a positional property
                // Count how many positional properties we've seen so far
                var positionalsSeen = 0;
                var foundPositional = false;
                foreach (var property in allProperties)
                {
                    if (property.Type == PropertyType.Positional)
                    {
                        if (positionalsSeen == i)
                        {
                            // This is the property that corresponds to this argument
                            var propStart = callInfo.TemplateStart + property.BraceStartIndex;
                            var propEnd = callInfo.TemplateStart + property.BraceEndIndex + 1;

                            // Adjust for verbatim strings
                            if (callInfo.IsVerbatimString && callInfo.OriginalTemplate != null)
                            {
                                propStart = callInfo.TemplateStart + MapCleanedPositionToOriginal(callInfo.OriginalTemplate, property.BraceStartIndex);
                                propEnd = callInfo.TemplateStart + MapCleanedPositionToOriginal(callInfo.OriginalTemplate, property.BraceEndIndex + 1);
                            }

                            var propertySpan = new SnapshotSpan(caretPoint.Snapshot, propStart, propEnd - propStart);
                            yield return new TagSpan<TextMarkerTag>(propertySpan, new TextMarkerTag("MarkerFormatDefinition/HighlightedReference"));
                            foundPositional = true;
                            break;
                        }
                        positionalsSeen++;
                    }
                }

                // Otherwise check if it corresponds to a named property
                if (!foundPositional && namedProperties.Any())
                {
                    // Calculate which named property this argument maps to
                    var maxPositionalIndex = positionalProperties
                        .Select(p => int.TryParse(p.Name, out var idx) ? idx : -1)
                        .DefaultIfEmpty(-1)
                        .Max();
                    var namedPropIndex = i - (maxPositionalIndex + 1);
                    if (namedPropIndex >= 0 && namedPropIndex < namedProperties.Count)
                    {
                        var property = namedProperties[namedPropIndex];
                        var propStart = callInfo.TemplateStart + property.BraceStartIndex;
                        var propEnd = callInfo.TemplateStart + property.BraceEndIndex + 1;

                        // Adjust for verbatim strings
                        if (callInfo.IsVerbatimString && callInfo.OriginalTemplate != null)
                        {
                            propStart = callInfo.TemplateStart + MapCleanedPositionToOriginal(callInfo.OriginalTemplate, property.BraceStartIndex);
                            propEnd = callInfo.TemplateStart + MapCleanedPositionToOriginal(callInfo.OriginalTemplate, property.BraceEndIndex + 1);
                        }

                        var propertySpan = new SnapshotSpan(caretPoint.Snapshot, propStart, propEnd - propStart);
                        yield return new TagSpan<TextMarkerTag>(propertySpan, new TextMarkerTag("MarkerFormatDefinition/HighlightedReference"));
                    }
                }
                yield break;
            }
        }
    }

    private SerilogCallInfo FindSerilogCall(SnapshotPoint caretPoint)
    {
        var snapshot = caretPoint.Snapshot;
        var text = snapshot.GetText();

        // Use SerilogCallDetector to find all Serilog calls to avoid duplicating regex patterns
        var methodMatches = SerilogCallDetector.FindAllSerilogCalls(text);

        foreach (Match methodMatch in methodMatches)
        {
            var methodStart = methodMatch.Index;
            var methodEnd = methodStart + methodMatch.Length;

            // Find the closing parenthesis for this method call
            var parenDepth = 1;
            var pos = methodEnd;
            var callEnd = -1;

            while (pos < text.Length && parenDepth > 0)
            {
                if (text[pos] == '(') parenDepth++;
                else if (text[pos] == ')')
                {
                    parenDepth--;
                    if (parenDepth == 0)
                    {
                        callEnd = pos;
                        break;
                    }
                }
                pos++;
            }

            if (callEnd < 0) continue;

            // Check if caret is within this call
            if (caretPoint.Position < methodStart || caretPoint.Position > callEnd)
                continue;

            // Extract the call content
            var callContent = text.Substring(methodEnd, callEnd - methodEnd);

            // Skip ExpressionTemplate calls - they don't have argument mapping
            if (methodMatch.Value.Contains("ExpressionTemplate"))
                continue;

            // Handle LogError special case (first parameter might be exception)
            var hasException = methodMatch.Value.Contains("LogError") && HasExceptionFirstParam(callContent);
            string adjustedCallContent = callContent;

            if (hasException)
            {
                // Skip the exception parameter to find the template
                // Find the first comma after the exception parameter
                var firstCommaIndex = callContent.IndexOf(',');
                if (firstCommaIndex > 0)
                {
                    // Create adjusted content that starts after the exception parameter
                    adjustedCallContent = callContent.Substring(firstCommaIndex + 1);
                }
            }

            // Find template string (handles various formats)
            var templateInfo = ExtractTemplate(adjustedCallContent);
            if (templateInfo == null) continue;

            // If we had an exception, adjust template positions
            if (hasException)
            {
                var firstCommaIndex = callContent.IndexOf(',');
                if (firstCommaIndex > 0)
                {
                    // Adjust positions back to original callContent coordinates
                    templateInfo.RelativeStart += firstCommaIndex + 1;
                    templateInfo.RelativeEnd += firstCommaIndex + 1;
                }
            }

            // Find arguments after the template (use original callContent for correct positions)
            var arguments = ExtractArguments(callContent, templateInfo.RelativeEnd, methodEnd);

            return new SerilogCallInfo
            {
                Template = templateInfo.Template,
                TemplateStart = methodEnd + templateInfo.RelativeStart,
                Arguments = arguments,
                OriginalTemplate = templateInfo.OriginalTemplate,
                IsVerbatimString = templateInfo.IsVerbatimString
            };
        }

        return null;
    }

    private bool HasExceptionFirstParam(string callContent)
    {
        // For LogError, the first parameter might be an exception
        // callContent is already the content between parentheses (doesn't include the parens)
        // We need to check if the first parameter is not a string literal

        var trimmedContent = callContent.TrimStart();

        // Check if it starts with a string literal (template)
        if (trimmedContent.StartsWith("\"") ||
            trimmedContent.StartsWith("@\"") ||
            trimmedContent.StartsWith("$\"") ||
            (trimmedContent.Length > 2 && trimmedContent[0] == '\"' && trimmedContent[1] == '\"' && trimmedContent[2] == '\"'))  // Raw string literals
        {
            // First parameter is a string template, no exception
            return false;
        }

        // If the first parameter is not a string, it's likely an exception
        return true;
    }

    private TemplateStringInfo ExtractTemplate(string callContent)
    {
        // Find the first string literal in the call content
        int searchPos = 0;

        // Skip whitespace at the beginning
        while (searchPos < callContent.Length && char.IsWhiteSpace(callContent[searchPos]))
            searchPos++;

        if (searchPos >= callContent.Length)
            return null;

        // Try to parse a string literal using StringLiteralParser
        if (_stringParser.TryParseStringLiteral(callContent, searchPos, out var result))
        {
            // Determine if it's a verbatim string
            bool isVerbatim = searchPos < callContent.Length - 1 &&
                              callContent[searchPos] == '@' &&
                              callContent[searchPos + 1] == '"';

            // For verbatim strings, we need to store the original content with "" for position mapping
            if (isVerbatim)
            {
                // Extract the original content with escaped quotes intact
                var originalContent = callContent.Substring(result.Start + 2, result.End - result.Start - 2);
                var cleanedContent = originalContent.Replace("\"\"", "\"");

                return new TemplateStringInfo
                {
                    Template = cleanedContent,
                    RelativeStart = result.Start + 2, // Skip @"
                    RelativeEnd = result.End + 1,
                    OriginalTemplate = originalContent,
                    IsVerbatimString = true
                };
            }

            // For raw strings, check quote count
            int quoteCount = 0;
            if (callContent[searchPos] == '"')
            {
                int pos = searchPos;
                while (pos < callContent.Length && callContent[pos] == '"')
                {
                    quoteCount++;
                    pos++;
                }
            }

            // For raw strings (3+ quotes), adjust start position
            if (quoteCount >= 3)
            {
                return new TemplateStringInfo
                {
                    Template = result.Content,
                    RelativeStart = result.Start + quoteCount,
                    RelativeEnd = result.End + 1
                };
            }

            // For regular strings, unescape the content
            string unescapedContent = result.Content;
            if (quoteCount == 1) // Regular string with escape sequences
            {
                try
                {
                    unescapedContent = Regex.Unescape(result.Content);
                }
                catch
                {
                    // If unescaping fails, use content as-is
                }
            }

            return new TemplateStringInfo
            {
                Template = unescapedContent,
                RelativeStart = result.Start + 1, // Skip opening quote
                RelativeEnd = result.End + 1
            };
        }

        return null;
    }

    private List<ArgumentInfo> ExtractArguments(string callContent, int templateEnd, int absoluteOffset)
    {
        var arguments = new List<ArgumentInfo>();
        var pos = templateEnd;

        // Skip to first comma after template
        while (pos < callContent.Length && callContent[pos] != ',')
            pos++;

        while (pos < callContent.Length)
        {
            if (callContent[pos] == ',')
            {
                pos++; // Skip comma

                // Skip whitespace
                while (pos < callContent.Length && char.IsWhiteSpace(callContent[pos]))
                    pos++;

                if (pos >= callContent.Length) break;

                // Find argument boundaries
                var argStart = pos;
                var parenDepth = 0;
                var bracketDepth = 0;
                var braceDepth = 0; // Track braces for anonymous objects
                var inString = false;
                var stringChar = '\0';

                while (pos < callContent.Length)
                {
                    var ch = callContent[pos];

                    // Handle string literals
                    if (!inString && (ch == '"' || ch == '\''))
                    {
                        inString = true;
                        stringChar = ch;
                    }
                    else if (inString && ch == stringChar && (pos == 0 || callContent[pos - 1] != '\\'))
                    {
                        inString = false;
                    }
                    else if (!inString)
                    {
                        if (ch == '(') parenDepth++;
                        else if (ch == ')')
                        {
                            if (parenDepth == 0) break; // End of call
                            parenDepth--;
                        }
                        else if (ch == '[') bracketDepth++;
                        else if (ch == ']') bracketDepth--;
                        else if (ch == '{') braceDepth++; // Track opening braces
                        else if (ch == '}') braceDepth--; // Track closing braces
                        else if (ch == ',' && parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                            break; // Next argument only when all depths are 0
                    }

                    pos++;
                }

                // Get the full argument text and find the trimmed bounds
                var fullArgText = callContent.Substring(argStart, pos - argStart);
                var trimmedArgText = fullArgText.Trim();
                if (!string.IsNullOrEmpty(trimmedArgText))
                {
                    // Find where the trimmed text starts in the full text
                    var trimStart = fullArgText.IndexOf(trimmedArgText);
                    arguments.Add(new ArgumentInfo
                    {
                        Start = absoluteOffset + argStart + trimStart,
                        Length = trimmedArgText.Length
                    });
                }
            }
            else
            {
                pos++;
            }
        }

        return arguments;
    }

    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
        if (_disposed || spans.Count == 0 || _state?.IsDismissed == true)
            return [];

        return _currentTags.Where(tag => spans.Any(span => span.IntersectsWith(tag.Span)));
    }

    private int MapCleanedPositionToOriginal(string originalTemplate, int cleanedPosition)
    {
        // Map a position from the cleaned template (where "" is replaced with ")
        // back to the original template with escaped quotes
        var originalPos = 0;
        var cleanedPos = 0;

        while (originalPos < originalTemplate.Length && cleanedPos < cleanedPosition)
        {
            if (originalPos < originalTemplate.Length - 1 &&
                originalTemplate[originalPos] == '"' &&
                originalTemplate[originalPos + 1] == '"')
            {
                // Found an escaped quote in original
                originalPos += 2; // Skip both quotes in original
                cleanedPos++; // Only one quote in cleaned version
            }
            else
            {
                // Regular character
                originalPos++;
                cleanedPos++;
            }
        }

        return originalPos;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_state != null)
        {
            _state.StateChanged -= OnStateChanged;
        }

        if (_view != null)
        {
            _view.Caret.PositionChanged -= CaretPositionChanged;
            _view.LayoutChanged -= ViewLayoutChanged;
        }

        _currentTags.Clear();
    }

    private class SerilogCallInfo
    {
        public string Template { get; set; }

        public int TemplateStart { get; set; }

        public List<ArgumentInfo> Arguments { get; set; } = [];

        public string OriginalTemplate { get; set; }

        public bool IsVerbatimString { get; set; }
    }

    private class TemplateStringInfo
    {
        public string Template { get; set; }

        public int RelativeStart { get; set; }

        public int RelativeEnd { get; set; }

        public string OriginalTemplate { get; set; }

        public bool IsVerbatimString { get; set; }
    }

    private class ArgumentInfo
    {
        public int Start { get; set; }

        public int Length { get; set; }
    }
}