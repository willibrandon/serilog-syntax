using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using SerilogSyntax.Parsing;
using SerilogSyntax.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SerilogSyntax.Navigation;

/// <summary>
/// Provides navigation support from Serilog template properties to their corresponding arguments.
/// </summary>
[Export(typeof(ISuggestedActionsSourceProvider))]
[Name("Serilog Navigation")]
[ContentType("CSharp")]
internal class SerilogSuggestedActionsSourceProvider : ISuggestedActionsSourceProvider
{
    /// <summary>
    /// Gets or sets the text structure navigator selector service.
    /// </summary>
    [Import]
    internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

    /// <summary>
    /// Creates a suggested actions source for the given text view and buffer.
    /// </summary>
    /// <param name="textView">The text view.</param>
    /// <param name="textBuffer">The text buffer.</param>
    /// <returns>A new <see cref="SerilogSuggestedActionsSource"/> or null if the parameters are null.</returns>
    public ISuggestedActionsSource CreateSuggestedActionsSource(ITextView textView, ITextBuffer textBuffer)
    {
        if (textBuffer == null || textView == null)
            return null;
        
        return new SerilogSuggestedActionsSource(this, textView, textBuffer);
    }
}

/// <summary>
/// Provides suggested actions for navigating from Serilog template properties to their arguments.
/// </summary>
internal class SerilogSuggestedActionsSource : ISuggestedActionsSource
{
    private readonly SerilogSuggestedActionsSourceProvider _provider;
    private readonly ITextView _textView;
    private readonly ITextBuffer _textBuffer;
    private readonly TemplateParser _parser = new TemplateParser();

    public SerilogSuggestedActionsSource(SerilogSuggestedActionsSourceProvider provider, ITextView textView, ITextBuffer textBuffer)
    {
        _provider = provider;
        _textView = textView;
        _textBuffer = textBuffer;
    }

    /// <summary>
    /// Determines whether suggested actions are available at the given location.
    /// </summary>
    /// <param name="requestedActionCategories">The requested action categories.</param>
    /// <param name="range">The span to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if navigation is available from a template property at the cursor position.</returns>
    public async Task<bool> HasSuggestedActionsAsync(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            var triggerPoint = range.Start;
            var line = triggerPoint.GetContainingLine();
            var lineText = line.GetText();
            var lineStart = line.Start.Position;

            // Check if we're in a Serilog call
            var serilogMatch = SerilogCallDetector.FindSerilogCall(lineText);
            if (serilogMatch == null)
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

            return property != null;
        }, cancellationToken);
    }

    /// <summary>
    /// Gets the suggested actions available at the given location.
    /// </summary>
    /// <param name="requestedActionCategories">The requested action categories.</param>
    /// <param name="range">The span to get actions for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of suggested action sets for navigating to arguments.</returns>
    public IEnumerable<SuggestedActionSet> GetSuggestedActions(ISuggestedActionCategorySet requestedActionCategories, SnapshotSpan range, CancellationToken cancellationToken)
    {
        var triggerPoint = range.Start;
        var line = triggerPoint.GetContainingLine();
        var lineText = line.GetText();
        var lineStart = line.Start.Position;

        // Check if we're in a Serilog call
        var serilogMatch = SerilogCallDetector.FindSerilogCall(lineText);
        if (serilogMatch == null)
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

        if (property == null)
            yield break;

        // Find the corresponding argument by position
        var propertyIndex = GetArgumentIndex(properties, property);
        if (propertyIndex >= 0)
        {
            var argumentLocation = FindArgumentByPosition(lineText, templateEnd, propertyIndex);
            if (argumentLocation.HasValue)
            {
                var actions = new ISuggestedAction[] 
                {
                    new NavigateToArgumentAction(_textView, lineStart + argumentLocation.Value.Item1, argumentLocation.Value.Item2, property.Name, property.Type)
                };
                yield return new SuggestedActionSet(null, actions, null, SuggestedActionSetPriority.Medium);
            }
        }
    }

    /// <summary>
    /// Determines the argument index for a given template property.
    /// </summary>
    /// <param name="properties">All properties in the template.</param>
    /// <param name="targetProperty">The property to get the index for.</param>
    /// <returns>The zero-based argument index, or -1 if not found.</returns>
    private int GetArgumentIndex(List<TemplateProperty> properties, TemplateProperty targetProperty)
    {
        if (targetProperty.Type == PropertyType.Positional)
        {
            // For positional properties, parse the index from the property name
            if (int.TryParse(targetProperty.Name, out int index))
                return index;
            return -1;
        }
        else
        {
            // For named properties, find their position among all named properties
            var namedProperties = properties.Where(p => p.Type != PropertyType.Positional).ToList();
            return namedProperties.IndexOf(targetProperty);
        }
    }

    /// <summary>
    /// Finds the boundaries of a string literal containing a message template.
    /// </summary>
    /// <param name="line">The line of code to search.</param>
    /// <param name="startIndex">The index to start searching from.</param>
    /// <returns>A tuple of (start, end) indices of the string content, or null if not found.</returns>
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

    /// <summary>
    /// Finds the location of an argument at the specified position.
    /// </summary>
    /// <param name="line">The line containing the arguments.</param>
    /// <param name="templateEnd">The end position of the template string.</param>
    /// <param name="argumentIndex">The zero-based index of the argument to find.</param>
    /// <returns>A tuple of (start position, length) of the argument, or null if not found.</returns>
    private (int, int)? FindArgumentByPosition(string line, int templateEnd, int argumentIndex)
    {
        // Find the start of arguments (after the template string)
        var argumentsStart = line.IndexOf(',', templateEnd);
        if (argumentsStart < 0)
            return null;

        // Parse comma-separated arguments, accounting for nested parentheses and brackets
        var arguments = ParseArguments(line, argumentsStart + 1);
        
        if (argumentIndex < arguments.Count)
        {
            var (start, length) = arguments[argumentIndex];
            return (start, length);
        }

        return null;
    }

    /// <summary>
    /// Parses comma-separated arguments from a method call, handling nested structures.
    /// </summary>
    /// <param name="line">The line containing the arguments.</param>
    /// <param name="startIndex">The index to start parsing from.</param>
    /// <returns>A list of tuples containing the start position and length of each argument.</returns>
    private List<(int start, int length)> ParseArguments(string line, int startIndex)
    {
        var arguments = new List<(int start, int length)>();
        var current = startIndex;
        var parenDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        var inString = false;
        var stringChar = '\0';

        // Skip leading whitespace
        while (current < line.Length && char.IsWhiteSpace(line[current]))
            current++;
        var argumentStart = current;

        for (; current < line.Length; current++)
        {
            var c = line[current];

            // Handle string literals
            if (!inString && (c == '"' || c == '\''))
            {
                inString = true;
                stringChar = c;
                continue;
            }
            else if (inString && c == stringChar)
            {
                // Check for escaped quote
                if (current > 0 && line[current - 1] != '\\')
                {
                    inString = false;
                }
                continue;
            }
            else if (inString)
            {
                continue; // Skip everything inside strings
            }

            // Handle nested structures
            switch (c)
            {
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth--;
                    if (parenDepth < 0) // End of method call
                    {
                        // Add the current argument if we have content
                        if (current > argumentStart)
                        {
                            var argText = line.Substring(argumentStart, current - argumentStart).Trim();
                            if (!string.IsNullOrEmpty(argText))
                            {
                                arguments.Add((argumentStart, argText.Length));
                            }
                        }
                        return arguments;
                    }
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth--;
                    break;
                case ',':
                    // Only treat as argument separator if we're at the top level
                    if (parenDepth == 0 && bracketDepth == 0 && braceDepth == 0)
                    {
                        var argText = line.Substring(argumentStart, current - argumentStart).Trim();
                        if (!string.IsNullOrEmpty(argText))
                        {
                            arguments.Add((argumentStart, argText.Length));
                        }
                        
                        // Move to next argument
                        current++;
                        while (current < line.Length && char.IsWhiteSpace(line[current]))
                            current++;
                        argumentStart = current;
                        current--; // Compensate for loop increment
                    }
                    break;
            }
        }

        // Add final argument if exists
        if (argumentStart < current)
        {
            var argText = line.Substring(argumentStart, current - argumentStart).Trim();
            if (!string.IsNullOrEmpty(argText))
            {
                arguments.Add((argumentStart, argText.Length));
            }
        }

        return arguments;
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

/// <summary>
/// Represents an action to navigate from a template property to its corresponding argument.
/// </summary>
internal class NavigateToArgumentAction : ISuggestedAction
{
    private readonly ITextView _textView;
    private readonly int _position;
    private readonly int _length;
    private readonly string _propertyName;
    private readonly PropertyType _propertyType;

    public NavigateToArgumentAction(ITextView textView, int position, int length, string propertyName, PropertyType propertyType)
    {
        _textView = textView;
        _position = position;
        _length = length;
        _propertyName = propertyName;
        _propertyType = propertyType;
    }

    public string DisplayText => _propertyType == PropertyType.Positional 
        ? $"Navigate to argument at position {_propertyName}" 
        : $"Navigate to '{_propertyName}' argument";

    public string IconAutomationText => null;

    public bool HasActionSets => false;

    public bool HasPreview => false;

    public string InputGestureText => null;

    public ImageMoniker IconMoniker => default;

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