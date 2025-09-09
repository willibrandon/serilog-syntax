using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using SerilogSyntax.Diagnostics;
using SerilogSyntax.Parsing;
using SerilogSyntax.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
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
        
        return new SerilogSuggestedActionsSource(textView);
    }
}

/// <summary>
/// Provides suggested actions for navigating from Serilog template properties to their arguments.
/// </summary>
internal class SerilogSuggestedActionsSource(ITextView textView) : ISuggestedActionsSource
{
    public event EventHandler<EventArgs> SuggestedActionsChanged { add { } remove { } }

    private readonly TemplateParser _parser = new();

    /// <summary>
    /// Determines whether suggested actions are available at the given location.
    /// </summary>
    /// <param name="requestedActionCategories">The requested action categories.</param>
    /// <param name="range">The span to check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if navigation is available from a template property at the cursor position.</returns>
    public async Task<bool> HasSuggestedActionsAsync(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            DiagnosticLogger.Log("=== HasSuggestedActionsAsync called ===");
            var triggerPoint = range.Start;
            var line = triggerPoint.GetContainingLine();
            var lineText = line.GetText();
            var lineStart = line.Start.Position;

            DiagnosticLogger.Log($"Line text: '{lineText}'");
            DiagnosticLogger.Log($"Trigger position: {triggerPoint.Position}");

            // Check if we're in a Serilog call
            var serilogMatch = SerilogCallDetector.FindSerilogCall(lineText);
            DiagnosticLogger.Log($"Serilog match on current line: {(serilogMatch != null ? $"Found at {serilogMatch.Index}" : "Not found")}");
            if (serilogMatch == null)
            {
                // Check if we're inside a multi-line template
                var multiLineResult = FindSerilogCallInPreviousLines(range.Snapshot, line);
                DiagnosticLogger.Log($"Multi-line Serilog call: {(multiLineResult != null ? "Found" : "Not found")}");
                if (multiLineResult == null)
                {
                    DiagnosticLogger.Log("No Serilog call found - returning false");
                    return false;
                }
            }

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
    public IEnumerable<SuggestedActionSet> GetSuggestedActions(
        ISuggestedActionCategorySet requestedActionCategories,
        SnapshotSpan range,
        CancellationToken cancellationToken)
    {
        var triggerPoint = range.Start;
        var line = triggerPoint.GetContainingLine();
        var lineText = line.GetText();
        var lineStart = line.Start.Position;

        // Check if we're in a Serilog call
        var serilogMatch = SerilogCallDetector.FindSerilogCall(lineText);
        ITextSnapshotLine serilogCallLine = line;
        
        // If no Serilog call found on current line, check if we're inside a multi-line template
        if (serilogMatch == null)
        {
            var multiLineResult = FindSerilogCallInPreviousLines(range.Snapshot, line);
            if (multiLineResult == null)
                yield break;
                
            serilogMatch = multiLineResult.Value.Match;
            serilogCallLine = multiLineResult.Value.Line;
        }

        // Find the template string - handle both single-line and multi-line scenarios
        string template;
        int templateStartPosition;
        int templateEndPosition;
        
        if (serilogCallLine == line)
        {
            // Same-line scenario: template starts on the same line as the Serilog call
            var templateMatch = FindTemplateString(lineText, serilogMatch.Index + serilogMatch.Length);
            if (!templateMatch.HasValue)
            {
                // No complete template found on this line - check if it's a multi-line template starting here
                var multiLineTemplate = ReconstructMultiLineTemplate(range.Snapshot, serilogCallLine, line);
                if (multiLineTemplate == null)
                    yield break;
                    
                template = multiLineTemplate.Value.Template;
                templateStartPosition = multiLineTemplate.Value.StartPosition;
                templateEndPosition = multiLineTemplate.Value.EndPosition;
                
                // Check if cursor is within the multi-line template bounds
                if (triggerPoint.Position < templateStartPosition || triggerPoint.Position > templateEndPosition)
                    yield break;
            }
            else
            {
                // Complete single-line template found
                var (templateStart, templateEnd) = templateMatch.Value;
                template = lineText.Substring(templateStart, templateEnd - templateStart);
                templateStartPosition = lineStart + templateStart;
                templateEndPosition = lineStart + templateEnd;
                
                // Check if cursor is within template
                var positionInLine = triggerPoint.Position - lineStart;
                if (positionInLine < templateStart || positionInLine > templateEnd)
                    yield break;
            }
        }
        else
        {
            // Multi-line scenario: reconstruct the full template from multiple lines
            var multiLineTemplate = ReconstructMultiLineTemplate(range.Snapshot, serilogCallLine, line);
            if (multiLineTemplate == null)
                yield break;
                
            template = multiLineTemplate.Value.Template;
            templateStartPosition = multiLineTemplate.Value.StartPosition;
            templateEndPosition = multiLineTemplate.Value.EndPosition;
            
            // Check if cursor is within the multi-line template bounds
            if (triggerPoint.Position < templateStartPosition || triggerPoint.Position > templateEndPosition)
                yield break;
        }

        // Parse template to find properties
        var properties = _parser.Parse(template).ToList();
        
        // Find which property the cursor is on
        var cursorPosInTemplate = triggerPoint.Position - templateStartPosition;
        var property = properties.FirstOrDefault(p => 
            cursorPosInTemplate >= p.BraceStartIndex && 
            cursorPosInTemplate <= p.BraceEndIndex);

        if (property == null)
            yield break;

        // Find the corresponding argument by position
        var propertyIndex = GetArgumentIndex(properties, property);
        if (propertyIndex >= 0)
        {
            // For both single-line and multi-line templates starting on the same line as the Serilog call,
            // we need to search for arguments from the template end position
            var multiLineLocation = FindArgumentInMultiLineCall(range.Snapshot, templateEndPosition, propertyIndex);
            if (multiLineLocation.HasValue)
            {
                var actions = new ISuggestedAction[] 
                {
                    new NavigateToArgumentAction(
                        textView,
                        multiLineLocation.Value.Item1,
                        multiLineLocation.Value.Item2,
                        property.Name,
                        property.Type)
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
        // Check if this is LogError with exception parameter
        bool hasExceptionParam = line.Contains("LogError") && HasExceptionParameterBeforeTemplate(line, startIndex);
        
        // Look for string literal after Serilog method call
        // If this has an exception parameter, we need to skip over it to find the template string
        int searchPos = startIndex;
        if (hasExceptionParam)
        {
            // Skip over the exception parameter by finding the first comma at parenthesis depth 1
            int parenDepth = 1;
            while (searchPos < line.Length && parenDepth > 0)
            {
                char c = line[searchPos];
                if (c == '(') parenDepth++;
                else if (c == ')') parenDepth--;
                else if (c == ',' && parenDepth == 1)
                {
                    searchPos++; // Move past the comma
                    break;
                }
                searchPos++;
            }
        }
        
        for (int i = searchPos; i < line.Length; i++)
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
    /// Determines if a LogError call has an exception parameter before the template string.
    /// </summary>
    /// <param name="line">The line of text containing the LogError call.</param>
    /// <param name="searchStart">The position to start searching from.</param>
    /// <returns>True if this is LogError with exception parameter, false otherwise.</returns>
    private bool HasExceptionParameterBeforeTemplate(string line, int searchStart)
    {
        // Look for the parameter structure by finding the first comma at depth 1
        // LogError(exception, "message", args...) has a comma before the first string
        // LogError("message", args...) has the string as the first parameter
        
        int pos = searchStart;
        int parenDepth = 1; // We start after LogError(
        bool foundCommaBeforeString = false;
        
        while (pos < line.Length && parenDepth > 0)
        {
            char c = line[pos];
            
            if (c == '(')
            {
                parenDepth++;
            }
            else if (c == ')')
            {
                parenDepth--;
                if (parenDepth == 0) break;
            }
            else if (c == ',' && parenDepth == 1)
            {
                foundCommaBeforeString = true;
            }
            else if (c == '"' && parenDepth == 1)
            {
                // Found a string - return whether we found a comma before it
                return foundCommaBeforeString;
            }
            
            pos++;
        }
        
        return false;
    }

    /// <summary>
    /// Searches previous lines to find a Serilog method call when the current line is inside a multi-line template.
    /// </summary>
    /// <param name="snapshot">The text snapshot to search in.</param>
    /// <param name="currentLine">The current line where the cursor is positioned.</param>
    /// <returns>A tuple of the Serilog match and the line it was found on, or null if not found.</returns>
    private (Match Match, ITextSnapshotLine Line)? FindSerilogCallInPreviousLines(ITextSnapshot snapshot, ITextSnapshotLine currentLine)
    {
        // Look backward up to 10 lines to find a Serilog call
        for (int i = currentLine.LineNumber - 1; i >= Math.Max(0, currentLine.LineNumber - 10); i--)
        {
            var checkLine = snapshot.GetLineFromLineNumber(i);
            var checkText = checkLine.GetText();
            
            // Check if this line contains a Serilog call
            var match = SerilogCallDetector.FindSerilogCall(checkText);
            if (match != null)
            {
                // Verify we're actually inside a multi-line template by checking if the call is still open
                if (IsInsideMultiLineTemplate(snapshot, checkLine, currentLine))
                {
                    return (match, checkLine);
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Determines if the current line is inside a multi-line template that started on the Serilog call line.
    /// </summary>
    /// <param name="snapshot">The text snapshot.</param>
    /// <param name="serilogCallLine">The line containing the Serilog method call.</param>
    /// <param name="currentLine">The current line where the cursor is positioned.</param>
    /// <returns>True if inside a multi-line template, false otherwise.</returns>
    private bool IsInsideMultiLineTemplate(ITextSnapshot snapshot, ITextSnapshotLine serilogCallLine, ITextSnapshotLine currentLine)
    {
        // Count string delimiters to determine if we're inside a multi-line string
        bool inString = false;
        bool inVerbatimString = false;
        bool inRawString = false;
        int rawStringQuoteCount = 0;
        
        for (int lineNum = serilogCallLine.LineNumber; lineNum <= currentLine.LineNumber; lineNum++)
        {
            var line = snapshot.GetLineFromLineNumber(lineNum);
            var lineText = line.GetText();
            
            for (int i = 0; i < lineText.Length; i++)
            {
                char c = lineText[i];
                
                if (!inString && !inVerbatimString && !inRawString)
                {
                    // Check for start of raw string (""")
                    if (i + 2 < lineText.Length && lineText.Substring(i, 3) == "\"\"\"")
                    {
                        inRawString = true;
                        rawStringQuoteCount = 3;
                        i += 2; // Skip next 2 quotes
                        continue;
                    }
                    // Check for verbatim string (@")
                    else if (i + 1 < lineText.Length && c == '@' && lineText[i + 1] == '"')
                    {
                        inVerbatimString = true;
                        i++; // Skip the quote
                        continue;
                    }
                    // Check for regular string
                    else if (c == '"')
                    {
                        inString = true;
                        continue;
                    }
                }
                else if (inRawString)
                {
                    // Look for end of raw string
                    if (c == '"')
                    {
                        int consecutiveQuotes = 1;
                        while (i + consecutiveQuotes < lineText.Length && lineText[i + consecutiveQuotes] == '"')
                            consecutiveQuotes++;
                        
                        if (consecutiveQuotes >= rawStringQuoteCount)
                        {
                            inRawString = false;
                            i += consecutiveQuotes - 1;
                        }
                    }
                }
                else if (inVerbatimString)
                {
                    // In verbatim string, "" is escaped quote
                    if (c == '"')
                    {
                        if (i + 1 < lineText.Length && lineText[i + 1] == '"')
                        {
                            i++; // Skip escaped quote
                        }
                        else
                        {
                            inVerbatimString = false;
                        }
                    }
                }
                else if (inString)
                {
                    // Regular string with \ escapes
                    if (c == '\\' && i + 1 < lineText.Length)
                    {
                        i++; // Skip escaped character
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }
                }
            }
            
            // If we've reached the current line and we're inside a string, return true
            if (lineNum == currentLine.LineNumber && (inString || inVerbatimString || inRawString))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Reconstructs the full template string from a multi-line Serilog call.
    /// </summary>
    /// <param name="snapshot">The text snapshot.</param>
    /// <param name="serilogCallLine">The line containing the Serilog method call.</param>
    /// <param name="currentLine">The current line where the cursor is positioned.</param>
    /// <returns>The reconstructed template with start and end positions, or null if reconstruction fails.</returns>
    private (string Template, int StartPosition, int EndPosition)? ReconstructMultiLineTemplate(ITextSnapshot snapshot, ITextSnapshotLine serilogCallLine, ITextSnapshotLine currentLine)
    {
        var templateBuilder = new System.Text.StringBuilder();
        int templateStartPosition = -1;
        
        bool foundTemplateStart = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inRawString = false;
        int rawStringQuoteCount = 0;
        
        for (int lineNum = serilogCallLine.LineNumber; lineNum <= currentLine.LineNumber + 5 && lineNum < snapshot.LineCount; lineNum++)
        {
            var line = snapshot.GetLineFromLineNumber(lineNum);
            var lineText = line.GetText();
            
            for (int i = 0; i < lineText.Length; i++)
            {
                char c = lineText[i];
                int absolutePosition = line.Start.Position + i;
                
                if (!foundTemplateStart && !inString && !inVerbatimString && !inRawString)
                {
                    // Look for template start
                    if (i + 2 < lineText.Length && lineText.Substring(i, 3) == "\"\"\"")
                    {
                        // Raw string start
                        foundTemplateStart = true;
                        inRawString = true;
                        rawStringQuoteCount = 3;
                        templateStartPosition = absolutePosition + 3; // Position after opening """
                        i += 2; // Skip next 2 quotes
                        continue;
                    }
                    else if (i + 1 < lineText.Length && c == '@' && lineText[i + 1] == '"')
                    {
                        // Verbatim string start
                        foundTemplateStart = true;
                        inVerbatimString = true;
                        templateStartPosition = absolutePosition + 2; // Position after @"
                        i++; // Skip the quote
                        continue;
                    }
                    else if (c == '"')
                    {
                        // Regular string start
                        foundTemplateStart = true;
                        inString = true;
                        templateStartPosition = absolutePosition + 1; // Position after opening "
                        continue;
                    }
                }
                else if (foundTemplateStart)
                {
                    if (inRawString)
                    {
                        if (c == '"')
                        {
                            int consecutiveQuotes = 1;
                            while (i + consecutiveQuotes < lineText.Length && lineText[i + consecutiveQuotes] == '"')
                                consecutiveQuotes++;
                            
                            if (consecutiveQuotes >= rawStringQuoteCount)
                            {
                                // End of raw string
                                return (templateBuilder.ToString(), templateStartPosition, absolutePosition + consecutiveQuotes);
                            }
                        }
                        templateBuilder.Append(c);
                    }
                    else if (inVerbatimString)
                    {
                        if (c == '"')
                        {
                            if (i + 1 < lineText.Length && lineText[i + 1] == '"')
                            {
                                // Escaped quote in verbatim string
                                templateBuilder.Append("\"\"");
                                i++; // Skip next quote
                            }
                            else
                            {
                                // End of verbatim string
                                return (templateBuilder.ToString(), templateStartPosition, absolutePosition);
                            }
                        }
                        else
                        {
                            templateBuilder.Append(c);
                        }
                    }
                    else if (inString)
                    {
                        if (c == '\\' && i + 1 < lineText.Length)
                        {
                            // Escaped character in regular string
                            templateBuilder.Append(c);
                            i++;
                            if (i < lineText.Length)
                                templateBuilder.Append(lineText[i]);
                        }
                        else if (c == '"')
                        {
                            // End of regular string
                            return (templateBuilder.ToString(), templateStartPosition, absolutePosition);
                        }
                        else
                        {
                            templateBuilder.Append(c);
                        }
                    }
                }
            }
            
            // Add actual line ending for multi-line strings to preserve character positions
            if (foundTemplateStart && (inVerbatimString || inRawString) && lineNum < snapshot.LineCount - 1)
            {
                var nextLine = snapshot.GetLineFromLineNumber(lineNum + 1);
                if (nextLine.LineNumber <= currentLine.LineNumber + 5)
                {
                    // Get the actual line break text from the snapshot
                    var lineBreakStart = line.End.Position;
                    var lineBreakEnd = nextLine.Start.Position;
                    if (lineBreakEnd > lineBreakStart)
                    {
                        var lineBreakText = snapshot.GetText(lineBreakStart, lineBreakEnd - lineBreakStart);
                        templateBuilder.Append(lineBreakText);
                    }
                }
            }
        }
        
        return null; // Template reconstruction failed
    }

    /// <summary>
    /// Parses arguments starting after a comma delimiter.
    /// </summary>
    /// <param name="lineText">The line text to parse.</param>
    /// <param name="commaIndex">The index of the comma delimiter.</param>
    /// <returns>List of argument positions and lengths relative to the line start.</returns>
    private List<(int start, int length)> ParseArgumentsAfterComma(string lineText, int commaIndex)
    {
        return commaIndex >= 0 ? ParseArguments(lineText, commaIndex + 1) : [];
    }

    /// <summary>
    /// Finds arguments in a multi-line Serilog call where the template spans multiple lines.
    /// </summary>
    /// <param name="snapshot">The text snapshot.</param>
    /// <param name="templateEndPosition">The absolute position where the template ends.</param>
    /// <param name="argumentIndex">The zero-based index of the argument to find.</param>
    /// <returns>A tuple of (absolute position, length) of the argument, or null if not found.</returns>
    private (int, int)? FindArgumentInMultiLineCall(ITextSnapshot snapshot, int templateEndPosition, int argumentIndex)
    {
        var allArguments = new List<(int absolutePosition, int length)>();
        
        // Start searching from the line containing the template end position
        var templateEndLine = snapshot.GetLineFromPosition(templateEndPosition);
        
        // Parse any arguments on the template end line (after the template ends)
        var templateEndLineText = templateEndLine.GetText();
        var templateEndInLine = templateEndPosition - templateEndLine.Start.Position;
        
        if (templateEndInLine < templateEndLineText.Length)
        {
            // Find the comma that starts the arguments (after the template)
            var commaIndex = templateEndLineText.IndexOf(',', templateEndInLine);
            var endLineArguments = ParseArgumentsAfterComma(templateEndLineText, commaIndex);
            foreach (var (start, length) in endLineArguments)
            {
                allArguments.Add((templateEndLine.Start.Position + start, length));
            }
        }
        
        // Continue searching subsequent lines until we find closing parenthesis
        for (int lineNum = templateEndLine.LineNumber + 1; lineNum < snapshot.LineCount; lineNum++)
        {
            var nextLine = snapshot.GetLineFromLineNumber(lineNum);
            var originalLineText = nextLine.GetText();
            var nextLineText = originalLineText.TrimStart();
            
            if (string.IsNullOrEmpty(nextLineText))
                continue;
                
            var trimOffset = originalLineText.Length - nextLineText.Length;
                
            var closingParenIndex = nextLineText.IndexOf(");");
            if (closingParenIndex >= 0)
            {
                var finalLineArguments = ParseArguments(nextLineText, 0);
                foreach (var (start, length) in finalLineArguments)
                {
                    if (start < closingParenIndex)
                    {
                        allArguments.Add((nextLine.Start.Position + trimOffset + start, length));
                    }
                }
                break;
            }
            else
            {
                var lineArguments = ParseArguments(nextLineText, 0);
                foreach (var (start, length) in lineArguments)
                {
                    allArguments.Add((nextLine.Start.Position + trimOffset + start, length));
                }
            }
        }
        
        if (argumentIndex < allArguments.Count)
        {
            return allArguments[argumentIndex];
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
}

/// <summary>
/// Represents an action to navigate from a template property to its corresponding argument.
/// </summary>
internal class NavigateToArgumentAction(
    ITextView textView,
    int position,
    int length,
    string propertyName,
    PropertyType propertyType) : ISuggestedAction
{
    internal int ArgumentStart => position;
    internal int ArgumentLength => length;
    
    public string DisplayText => propertyType == PropertyType.Positional 
        ? $"Navigate to argument at position {propertyName}" 
        : $"Navigate to '{propertyName}' argument";

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
        var snapshot = textView.TextBuffer.CurrentSnapshot;
        var span = new SnapshotSpan(snapshot, position, length);
        
        textView.Caret.MoveTo(span.Start);
        textView.ViewScroller.EnsureSpanVisible(span);
        textView.Selection.Select(span, false);
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