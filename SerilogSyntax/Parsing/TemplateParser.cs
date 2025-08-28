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
        var startIndex = -1;
        var propertyStartIndex = -1;
        var formatStartIndex = -1;
        var alignmentStartIndex = -1;
        var operatorIndex = -1;
        var propertyType = PropertyType.Standard;
        
        for (int i = 0; i < template.Length; i++)
        {
            char ch = template[i];
            char nextCh = i + 1 < template.Length ? template[i + 1] : '\0';

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
                            startIndex = i;
                            state = ParseState.OpenBrace;
                            propertyStartIndex = -1;
                            formatStartIndex = -1;
                            alignmentStartIndex = -1;
                            operatorIndex = -1;
                            propertyType = PropertyType.Standard;
                        }
                    }
                    break;

                case ParseState.OpenBrace:
                    if (char.IsWhiteSpace(ch))
                    {
                        // Skip leading whitespace
                        continue;
                    }
                    else if (ch == '@')
                    {
                        propertyType = PropertyType.Destructured;
                        operatorIndex = i;
                        propertyStartIndex = i + 1;
                        state = ParseState.Property;
                    }
                    else if (ch == '$')
                    {
                        propertyType = PropertyType.Stringified;
                        operatorIndex = i;
                        propertyStartIndex = i + 1;
                        state = ParseState.Property;
                    }
                    else if (char.IsDigit(ch))
                    {
                        propertyType = PropertyType.Positional;
                        propertyStartIndex = i;
                        state = ParseState.Property;
                    }
                    else if (IsValidPropertyStartChar(ch))
                    {
                        propertyStartIndex = i;
                        state = ParseState.Property;
                    }
                    else if (ch == '}')
                    {
                        // Empty property - skip
                        state = ParseState.Outside;
                    }
                    else if (!char.IsWhiteSpace(ch))
                    {
                        // Invalid - skip this brace
                        state = ParseState.Outside;
                    }
                    break;

                case ParseState.Property:
                    if (ch == '}')
                    {
                        // End of property
                        if (propertyStartIndex >= 0)
                        {
                            var prop = CreateProperty(template, startIndex, i, propertyStartIndex, 
                                formatStartIndex, alignmentStartIndex, operatorIndex, propertyType);
                            if (prop != null)
                                yield return prop;
                        }
                        state = ParseState.Outside;
                    }
                    else if (ch == ':')
                    {
                        formatStartIndex = i;
                        state = ParseState.Format;
                    }
                    else if (ch == ',')
                    {
                        alignmentStartIndex = i;
                        state = ParseState.Alignment;
                    }
                    else if (char.IsWhiteSpace(ch))
                    {
                        // Check if we have any following non-whitespace chars before }
                        bool hasMoreContent = false;
                        for (int j = i + 1; j < template.Length; j++)
                        {
                            if (template[j] == '}')
                                break;
                            if (!char.IsWhiteSpace(template[j]))
                            {
                                hasMoreContent = true;
                                break;
                            }
                        }
                        if (hasMoreContent)
                        {
                            // Invalid - property names can't contain spaces
                            state = ParseState.Outside;
                        }
                        // Otherwise, it's trailing whitespace - continue parsing
                    }
                    else if (propertyType == PropertyType.Positional && !char.IsDigit(ch))
                    {
                        // Invalid positional property
                        state = ParseState.Outside;
                    }
                    else if (!IsValidPropertyChar(ch))
                    {
                        // Invalid property character
                        state = ParseState.Outside;
                    }
                    break;

                case ParseState.Alignment:
                    if (ch == '}')
                    {
                        // End with alignment
                        if (propertyStartIndex >= 0)
                        {
                            var prop = CreateProperty(template, startIndex, i, propertyStartIndex,
                                formatStartIndex, alignmentStartIndex, operatorIndex, propertyType);
                            if (prop != null)
                                yield return prop;
                        }
                        state = ParseState.Outside;
                    }
                    else if (ch == ':')
                    {
                        formatStartIndex = i;
                        state = ParseState.Format;
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
                            if (propertyStartIndex >= 0)
                            {
                                var prop = CreateProperty(template, startIndex, i, propertyStartIndex,
                                    formatStartIndex, alignmentStartIndex, operatorIndex, propertyType);
                                if (prop != null)
                                    yield return prop;
                            }
                            state = ParseState.Outside;
                        }
                    }
                    break;
            }
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

        var name = template.Substring(propertyStart, propertyLength).Trim();
        if (string.IsNullOrEmpty(name))
            return null;
        
        var property = new TemplateProperty
        {
            Name = name,
            StartIndex = propertyStart,
            Length = propertyLength,
            Type = type,
            BraceStartIndex = braceStart,
            BraceEndIndex = braceEnd
        };

        if (operatorIndex >= 0)
        {
            property.OperatorIndex = operatorIndex;
        }

        if (alignmentStart >= 0)
        {
            int alignmentEnd = formatStart >= 0 ? formatStart : braceEnd;
            property.AlignmentStartIndex = alignmentStart + 1; // Skip the comma
            property.Alignment = template.Substring(alignmentStart + 1, alignmentEnd - alignmentStart - 1).Trim();
        }

        if (formatStart >= 0)
        {
            property.FormatStartIndex = formatStart + 1; // Skip the colon
            property.FormatSpecifier = template.Substring(formatStart + 1, braceEnd - formatStart - 1).Trim();
        }

        return property;
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