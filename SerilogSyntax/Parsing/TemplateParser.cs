using System.Collections.Generic;

namespace SerilogSyntax.Parsing;

/// <summary>
/// Parses Serilog message templates to extract property information.
/// Uses a state machine to identify properties and their components.
/// </summary>
internal class TemplateParser
{
    /// <summary>
    /// Parses a Serilog message template and returns all properties found.
    /// </summary>
    /// <param name="template">The message template string to parse.</param>
    /// <returns>An enumerable collection of template properties.</returns>
    public IEnumerable<TemplateProperty> Parse(string template)
    {
        if (string.IsNullOrEmpty(template))
            yield break;

        var state = ParseState.Outside;
        var recovery = new ParserRecovery();
        var results = new List<TemplateProperty>();
        
        for (int i = 0; i < template.Length; i++)
        {
            char ch = template[i];
            char nextCh = i + 1 < template.Length ? template[i + 1] : '\0';
            TemplateProperty propertyToAdd = null;

            try
            {
                switch (state)
                {
                    case ParseState.Outside:
                        if (ch == '{')
                        {
                            if (nextCh == '{') // Escaped brace
                            {
                                i++; // Skip next brace
                            }
                            else
                            {
                                recovery.StartProperty(i);
                                state = ParseState.OpenBrace;
                            }
                        }
                        break;

                    case ParseState.OpenBrace:
                        if (char.IsWhiteSpace(ch))
                        {
                            // Mark that we've seen leading whitespace
                            recovery.HasLeadingWhitespace = true;
                            continue;
                        }
                        else if (ch == '@')
                        {
                            // If we have leading whitespace, invalid property
                            if (recovery.HasLeadingWhitespace)
                            {
                                recovery.Reset();
                                state = ParseState.Outside;
                            }
                            else
                            {
                                recovery.PropertyType = PropertyType.Destructured;
                                recovery.OperatorIndex = i;
                                recovery.PropertyNameStart = i + 1;
                                state = ParseState.Property;
                            }
                        }
                        else if (ch == '$')
                        {
                            // If we have leading whitespace, invalid property
                            if (recovery.HasLeadingWhitespace)
                            {
                                recovery.Reset();
                                state = ParseState.Outside;
                            }
                            else
                            {
                                recovery.PropertyType = PropertyType.Stringified;
                                recovery.OperatorIndex = i;
                                recovery.PropertyNameStart = i + 1;
                                state = ParseState.Property;
                            }
                        }
                        else if (char.IsDigit(ch))
                        {
                            // If we have leading whitespace, invalid property
                            if (recovery.HasLeadingWhitespace)
                            {
                                recovery.Reset();
                                state = ParseState.Outside;
                            }
                            else
                            {
                                recovery.PropertyType = PropertyType.Positional;
                                recovery.PropertyNameStart = i;
                                state = ParseState.Property;
                            }
                        }
                        else if (IsValidPropertyStartChar(ch))
                        {
                            // If we have leading whitespace, invalid property
                            if (recovery.HasLeadingWhitespace)
                            {
                                recovery.Reset();
                                state = ParseState.Outside;
                            }
                            else
                            {
                                recovery.PropertyNameStart = i;
                                state = ParseState.Property;
                            }
                        }
                        else if (ch == '}')
                        {
                            // Empty property - skip
                            recovery.Reset();
                            state = ParseState.Outside;
                        }
                        else if (!char.IsWhiteSpace(ch) && ch != '@' && ch != '$')
                        {
                            // Try to recover by finding the next valid position
                            var recovered = recovery.TryRecover(template, i);
                            if (recovered.HasValue)
                            {
                                i = recovered.Value - 1; // -1 because loop will increment
                                state = ParseState.Outside;
                            }
                            else
                            {
                                recovery.Reset();
                                state = ParseState.Outside;
                            }
                        }
                        break;

                    case ParseState.Property:
                        if (ch == '}')
                        {
                            // End of property
                            if (recovery.PropertyNameStart >= 0)
                            {
                                propertyToAdd = CreateProperty(template, recovery.PropertyStart, i, recovery.PropertyNameStart, 
                                    recovery.FormatStart, recovery.AlignmentStart, recovery.OperatorIndex, recovery.PropertyType);
                            }
                            recovery.Reset();
                            state = ParseState.Outside;
                        }
                        else if (ch == ':')
                        {
                            recovery.FormatStart = i;
                            state = ParseState.Format;
                        }
                        else if (ch == ',')
                        {
                            recovery.AlignmentStart = i;
                            state = ParseState.Alignment;
                        }
                        else if (char.IsWhiteSpace(ch))
                        {
                            // If we've seen any property name characters already,
                            // this space makes the property invalid
                            if (recovery.PropertyNameStart != -1 && i > recovery.PropertyNameStart)
                            {
                                // We've already started collecting a property name
                                // Any space in the middle makes it invalid
                                // Try to recover
                                var recovered = recovery.TryRecover(template, i);
                                if (recovered.HasValue)
                                {
                                    i = recovered.Value - 1;
                                }
                                recovery.Reset();
                                state = ParseState.Outside;
                            }
                            // Otherwise it's leading whitespace, which is OK - just continue
                        }
                        else if (recovery.PropertyType == PropertyType.Positional && !char.IsDigit(ch))
                        {
                            // Invalid positional property - try to recover
                            var recovered = recovery.TryRecover(template, i);
                            if (recovered.HasValue)
                            {
                                i = recovered.Value - 1;
                            }
                            recovery.Reset();
                            state = ParseState.Outside;
                        }
                        else if (!IsValidPropertyChar(ch))
                        {
                            // Invalid property character - try to recover
                            var recovered = recovery.TryRecover(template, i);
                            if (recovered.HasValue)
                            {
                                i = recovered.Value - 1;
                            }
                            recovery.Reset();
                            state = ParseState.Outside;
                        }
                        break;

                    case ParseState.Alignment:
                        if (ch == '}')
                        {
                            // End with alignment
                            if (recovery.PropertyNameStart >= 0)
                            {
                                propertyToAdd = CreateProperty(template, recovery.PropertyStart, i, recovery.PropertyNameStart,
                                    recovery.FormatStart, recovery.AlignmentStart, recovery.OperatorIndex, recovery.PropertyType);
                            }
                            recovery.Reset();
                            state = ParseState.Outside;
                        }
                        else if (ch == ':')
                        {
                            recovery.FormatStart = i;
                            state = ParseState.Format;
                        }
                        else if (ch == '|' || ch == '{')
                        {
                            // Pipe character or opening brace in alignment means we've gone too far - unclosed property
                            // Reset without yielding a property
                            recovery.Reset();
                            state = ParseState.Outside;
                            
                            // If we hit an opening brace, step back so it can be processed as a new property
                            if (ch == '{')
                            {
                                i--;
                            }
                        }
                        break;

                    case ParseState.Format:
                        if (ch == '}')
                        {
                            // Check if it's escaped in format string
                            if (nextCh == '}')
                            {
                                i++; // Skip next brace - it's part of the format
                            }
                            else
                            {
                                // End of property with format
                                if (recovery.PropertyNameStart >= 0)
                                {
                                    propertyToAdd = CreateProperty(template, recovery.PropertyStart, i, recovery.PropertyNameStart,
                                        recovery.FormatStart, recovery.AlignmentStart, recovery.OperatorIndex, recovery.PropertyType);
                                }
                                recovery.Reset();
                                state = ParseState.Outside;
                            }
                        }
                        else if (ch == '|' || ch == '{')
                        {
                            // Pipe character or opening brace in format specifier means we've gone too far - unclosed property
                            // Reset without yielding a property
                            recovery.Reset();
                            state = ParseState.Outside;
                            
                            // If we hit an opening brace, step back so it can be processed as a new property
                            if (ch == '{')
                            {
                                i--;
                            }
                        }
                        break;
                }
            }
            catch
            {
                // Recover from unexpected errors
                state = ParseState.Outside;
                recovery.Reset();
            }

            // Add property outside of try block
            if (propertyToAdd != null)
            {
                results.Add(propertyToAdd);
            }
        }
        
        // Don't add unclosed properties - they're invalid and cause spillover highlighting
        // If we're still inside a property when we reach the end of the string,
        // that means we never found a closing brace, so the property is invalid

        // Return all collected results
        foreach (var result in results)
        {
            yield return result;
        }
    }

    /// <summary>
    /// Handles error recovery for malformed template properties.
    /// </summary>
    private class ParserRecovery
    {
        /// <summary>
        /// Index of the opening brace '{'.
        /// </summary>
        public int PropertyStart { get; private set; } = -1;
        
        /// <summary>
        /// Starting index of the property name.
        /// </summary>
        public int PropertyNameStart { get; set; } = -1;
        
        /// <summary>
        /// Type of property (Standard, Destructured, Stringified, or Positional).
        /// </summary>
        public PropertyType PropertyType { get; set; } = PropertyType.Standard;
        
        /// <summary>
        /// Starting index of the format specifier ':'.
        /// </summary>
        public int FormatStart { get; set; } = -1;
        
        /// <summary>
        /// Starting index of the alignment specifier ','.
        /// </summary>
        public int AlignmentStart { get; set; } = -1;
        
        /// <summary>
        /// Index of the operator ('@' or '$') if present.
        /// </summary>
        public int OperatorIndex { get; set; } = -1;
        
        /// <summary>
        /// Tracks if we've seen whitespace after the opening brace.
        /// </summary>
        public bool HasLeadingWhitespace { get; set; } = false;
        
        /// <summary>
        /// Starts tracking a new property at the given position.
        /// </summary>
        public void StartProperty(int position)
        {
            PropertyStart = position;
            PropertyNameStart = -1;
            PropertyType = PropertyType.Standard;
            FormatStart = -1;
            AlignmentStart = -1;
            OperatorIndex = -1;
        }
        
        /// <summary>
        /// Resets all tracking state.
        /// </summary>
        public void Reset()
        {
            PropertyStart = -1;
            PropertyNameStart = -1;
            PropertyType = PropertyType.Standard;
            FormatStart = -1;
            AlignmentStart = -1;
            OperatorIndex = -1;
            HasLeadingWhitespace = false;
        }
        
        /// <summary>
        /// Checks if we have enough info to create a partial property.
        /// </summary>
        public bool HasPartialProperty() => PropertyStart >= 0 && PropertyNameStart >= 0;
        
        /// <summary>
        /// Attempts to find a recovery point in the template.
        /// </summary>
        public int? TryRecover(string template, int currentPosition)
        {
            // Look for the next closing brace or opening brace
            for (int i = currentPosition; i < template.Length; i++)
            {
                if (template[i] == '}' || template[i] == '{')
                    return i;
            }
            return null;
        }
    }

    /// <summary>
    /// Creates a TemplateProperty from parsed indices and type information.
    /// </summary>
    /// <param name="template">The template string being parsed.</param>
    /// <param name="braceStart">Index of the opening brace.</param>
    /// <param name="braceEnd">Index of the closing brace.</param>
    /// <param name="propertyStart">Start index of the property name.</param>
    /// <param name="formatStart">Start index of the format specifier, or -1 if none.</param>
    /// <param name="alignmentStart">Start index of the alignment, or -1 if none.</param>
    /// <param name="operatorIndex">Index of the operator (@ or $), or -1 if none.</param>
    /// <param name="type">The type of the property.</param>
    /// <returns>A TemplateProperty instance, or null if the property is invalid.</returns>
    private TemplateProperty CreateProperty(string template, int braceStart, int braceEnd,
        int propertyStart, int formatStart, int alignmentStart, int operatorIndex, PropertyType type)
    {
        if (propertyStart < 0 || propertyStart >= template.Length)
            return null;

        // Calculate property end
        int propertyEnd = braceEnd;
        if (alignmentStart >= 0 && alignmentStart < propertyEnd)
            propertyEnd = alignmentStart;
        if (formatStart >= 0 && formatStart < propertyEnd)
            propertyEnd = formatStart;

        int propertyLength = propertyEnd - propertyStart;
        if (propertyLength <= 0)
            return null;

        var rawName = template.Substring(propertyStart, propertyLength);
        var name = rawName.Trim();
        if (string.IsNullOrEmpty(name))
            return null;
        
        // The state machine should have already rejected properties with internal spaces
        // But as a safety check, verify the trimmed name doesn't contain spaces
        if (name.Contains(" "))
            return null;
        
        string formatSpec = null;
        int formatStartIdx = -1;
        string alignmentValue = null;
        int alignmentStartIdx = -1;

        if (alignmentStart >= 0)
        {
            int alignmentEnd = formatStart >= 0 ? formatStart : braceEnd;
            alignmentStartIdx = alignmentStart + 1; // Skip the comma
            alignmentValue = template.Substring(alignmentStart + 1, alignmentEnd - alignmentStart - 1).Trim();
        }

        if (formatStart >= 0)
        {
            formatStartIdx = formatStart + 1; // Skip the colon
            formatSpec = template.Substring(formatStart + 1, braceEnd - formatStart - 1).Trim();
        }

        return new TemplateProperty(
            name,
            propertyStart,
            propertyLength,
            type,
            braceStart,
            braceEnd,
            formatSpec,
            formatStartIdx,
            operatorIndex,
            alignmentValue,
            alignmentStartIdx);
    }

    /// <summary>
    /// Determines if a character is valid as the first character of a property name.
    /// </summary>
    /// <param name="ch">The character to check.</param>
    /// <returns>True if the character can start a property name; otherwise, false.</returns>
    private bool IsValidPropertyStartChar(char ch)
    {
        return char.IsLetter(ch) || ch == '_';
    }

    /// <summary>
    /// Determines if a character is valid within a property name.
    /// </summary>
    /// <param name="ch">The character to check.</param>
    /// <returns>True if the character can be part of a property name; otherwise, false.</returns>
    private bool IsValidPropertyChar(char ch)
    {
        return char.IsLetterOrDigit(ch) || ch == '_' || ch == '.';
    }

    /// <summary>
    /// Represents the current state of the template parser state machine.
    /// </summary>
    private enum ParseState
    {
        /// <summary>
        /// Outside of any template property.
        /// </summary>
        Outside,

        /// <summary>
        /// Just after an opening brace, expecting property or operator.
        /// </summary>
        OpenBrace,

        /// <summary>
        /// Parsing the property name.
        /// </summary>
        Property,

        /// <summary>
        /// Parsing the alignment value after a comma.
        /// </summary>
        Alignment,

        /// <summary>
        /// Parsing the format specifier after a colon.
        /// </summary>
        Format
    }
}