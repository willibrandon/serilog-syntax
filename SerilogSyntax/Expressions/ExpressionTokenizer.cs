using System;
using System.Collections.Generic;

namespace SerilogSyntax.Expressions;

/// <summary>
/// Tokenizes Serilog expression syntax into discrete tokens for classification.
/// </summary>
internal class ExpressionTokenizer(string text)
{
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "if", "then", "else", "undefined"
    };
    
    private static readonly HashSet<string> BooleanOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "not"
    };
    
    private static readonly HashSet<string> StringOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "like"
    };
    
    private static readonly HashSet<string> MembershipOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "in"
    };
    
    private static readonly HashSet<string> NullOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "is"
    };
    
    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Coalesce", "Concat", "Contains", "ElementAt", "EndsWith", "IndexOf", "IndexOfMatch",
        "Inspect", "IsMatch", "IsDefined", "LastIndexOf", "Length", "Nest", "Now", "Replace",
        "Rest", "Round", "StartsWith", "Substring", "TagOf", "ToString", "TypeOf", "Undefined",
        "UtcDateTime"
    };
    
    private string _text = text ?? string.Empty;
    private int _position = 0;

    /// <summary>
    /// Sets new text for tokenization, resetting the position. Used for pooling.
    /// </summary>
    public void SetText(string text)
    {
        _text = text ?? string.Empty;
        _position = 0;
    }

    /// <summary>
    /// Tokenizes the entire expression.
    /// </summary>
    public IEnumerable<Token> Tokenize()
    {
        while (_position < _text.Length)
        {
            // Skip whitespace
            if (char.IsWhiteSpace(_text[_position]))
            {
                _position++;
                continue;
            }
            
            var token = NextToken();
            if (token != null)
                yield return token;
        }
    }
    
    private Token NextToken()
    {
        if (_position >= _text.Length)
            return null;
        
        var ch = _text[_position];
        var start = _position;
        
        // String literals
        if (ch == '\'')
        {
            return ReadStringLiteral();
        }
        
        // Numbers
        if (char.IsDigit(ch) || (ch == '-' && _position + 1 < _text.Length && char.IsDigit(_text[_position + 1])))
        {
            return ReadNumber();
        }
        
        // Template directives
        if (ch == '{' && _position + 1 < _text.Length && _text[_position + 1] == '#')
        {
            return ReadDirective();
        }
        
        // Operators and structural elements
        switch (ch)
        {
            case '.':
                if (_position + 1 < _text.Length && _text[_position + 1] == '.')
                {
                    _position += 2;
                    return new Token(TokenType.SpreadOperator, "..", start, 2);
                }

                _position++;
                return new Token(TokenType.Dot, ".", start, 1);
                
            case ',':
                _position++;
                return new Token(TokenType.Comma, ",", start, 1);
                
            case ':':
                _position++;
                return new Token(TokenType.Colon, ":", start, 1);
                
            case '(':
                _position++;
                return new Token(TokenType.OpenParen, "(", start, 1);
                
            case ')':
                _position++;
                return new Token(TokenType.CloseParen, ")", start, 1);
                
            case '[':
                _position++;
                return new Token(TokenType.OpenBracket, "[", start, 1);
                
            case ']':
                _position++;
                return new Token(TokenType.CloseBracket, "]", start, 1);
                
            case '{':
                _position++;
                return new Token(TokenType.OpenBrace, "{", start, 1);
                
            case '}':
                _position++;
                return new Token(TokenType.CloseBrace, "}", start, 1);
                
            case '@':
                // Check for built-in properties
                var builtIn = ReadBuiltinProperty();

                if (builtIn != null)
                    return builtIn;
                
                _position++;
                return new Token(TokenType.At, "@", start, 1);
                
            case '$':
                _position++;
                return new Token(TokenType.Dollar, "$", start, 1);
                
            case '#':
                _position++;
                return new Token(TokenType.Hash, "#", start, 1);
                
            case '?':
                _position++;
                return new Token(TokenType.Wildcard, "?", start, 1);
                
            case '*':
                // Check if it's multiplication or wildcard based on context
                // For now, treat as arithmetic operator since wildcards are typically in brackets
                _position++;
                return new Token(TokenType.ArithmeticOperator, "*", start, 1);
                
            case '=':
                _position++;
                return new Token(TokenType.ComparisonOperator, "=", start, 1);
                
            case '<':
                if (_position + 1 < _text.Length)
                {
                    if (_text[_position + 1] == '>')
                    {
                        _position += 2;
                        return new Token(TokenType.ComparisonOperator, "<>", start, 2);
                    }

                    if (_text[_position + 1] == '=')
                    {
                        _position += 2;
                        return new Token(TokenType.ComparisonOperator, "<=", start, 2);
                    }
                }

                _position++;
                return new Token(TokenType.ComparisonOperator, "<", start, 1);
                
            case '>':
                if (_position + 1 < _text.Length && _text[_position + 1] == '=')
                {
                    _position += 2;
                    return new Token(TokenType.ComparisonOperator, ">=", start, 2);
                }

                _position++;
                return new Token(TokenType.ComparisonOperator, ">", start, 1);
                
            case '+':
            case '-':
            case '/':
            case '^':
            case '%':
                _position++;
                return new Token(TokenType.ArithmeticOperator, ch.ToString(), start, 1);
        }
        
        // Identifiers, keywords, functions
        if (char.IsLetter(ch) || ch == '_')
        {
            return ReadIdentifierOrKeyword();
        }
        
        // Unknown
        _position++;
        return new Token(TokenType.Unknown, ch.ToString(), start, 1);
    }

    private bool IsHexDigit(char ch) =>
        char.IsDigit(ch) || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F');

    private string PeekNextWord()
    {
        if (_position >= _text.Length)
            return null;

        var startPos = _position;
        var pos = _position;

        while (pos < _text.Length && (char.IsLetterOrDigit(_text[pos]) || _text[pos] == '_'))
        {
            pos++;
        }

        return pos > startPos ? _text.Substring(startPos, pos - startPos) : null;
    }

    private Token ReadBuiltinProperty()
    {
        var start = _position;

        if (_position >= _text.Length || _text[_position] != '@')
            return null;

        // Check what follows the @
        if (_position + 1 < _text.Length)
        {
            var ch1 = _text[_position + 1];
            var nextIsValid = _position + 2 >= _text.Length
                || !char.IsLetterOrDigit(_text[_position + 2]) && _text[_position + 2] != '_';

            switch (ch1)
            {
                case 't' when _position + 2 < _text.Length && _text[_position + 2] == 'r':
                    if (_position + 3 >= _text.Length || (!char.IsLetterOrDigit(_text[_position + 3]) && _text[_position + 3] != '_'))
                    {
                        _position += 3;
                        return new Token(TokenType.BuiltinProperty, "@tr", start, 3);
                    }

                    break;

                case 't' when nextIsValid:
                    _position += 2;
                    return new Token(TokenType.BuiltinProperty, "@t", start, 2);

                case 'm' when _position + 2 < _text.Length && _text[_position + 2] == 't':
                    if (_position + 3 >= _text.Length || (!char.IsLetterOrDigit(_text[_position + 3]) && _text[_position + 3] != '_'))
                    {
                        _position += 3;
                        return new Token(TokenType.BuiltinProperty, "@mt", start, 3);
                    }

                    break;

                case 'm' when nextIsValid:
                    _position += 2;
                    return new Token(TokenType.BuiltinProperty, "@m", start, 2);

                case 'l' when nextIsValid:
                    _position += 2;
                    return new Token(TokenType.BuiltinProperty, "@l", start, 2);

                case 'x' when nextIsValid:
                    _position += 2;
                    return new Token(TokenType.BuiltinProperty, "@x", start, 2);

                case 'p' when nextIsValid:
                    _position += 2;
                    return new Token(TokenType.BuiltinProperty, "@p", start, 2);

                case 'i' when nextIsValid:
                    _position += 2;
                    return new Token(TokenType.BuiltinProperty, "@i", start, 2);

                case 'r' when nextIsValid:
                    _position += 2;
                    return new Token(TokenType.BuiltinProperty, "@r", start, 2);

                case 's' when _position + 2 < _text.Length && _text[_position + 2] == 'p':
                    if (_position + 3 >= _text.Length || (!char.IsLetterOrDigit(_text[_position + 3]) && _text[_position + 3] != '_'))
                    {
                        _position += 3;
                        return new Token(TokenType.BuiltinProperty, "@sp", start, 3);
                    }

                    break;
            }
        }

        return null;
    }

    private Token ReadDirective()
    {
        var start = _position;
        var directiveStart = _position;
        
        // Read until we hit a space or }
        while (_position < _text.Length && _text[_position] != ' ' && _text[_position] != '}')
        {
            _position++;
        }

        var directive = _text.Substring(directiveStart, _position - directiveStart);
        var type = directive switch
        {
            "{#if" => TokenType.IfDirective,
            "{#else" => TokenType.ElseDirective,
            "{#end" => TokenType.EndDirective,
            "{#each" => TokenType.EachDirective,
            "{#delimit" => TokenType.DelimitDirective,
            _ => TokenType.Unknown
        };

        // Check for "else if"
        if (type == TokenType.ElseDirective && _position < _text.Length && _text[_position] == ' ')
        {
            var nextPos = _position + 1;

            if (nextPos + 2 <= _text.Length && _text.Substring(nextPos, 2) == "if")
            {
                _position = nextPos + 2;
                return new Token(TokenType.ElseIfDirective, "{#else if", start, _position - start);
            }
        }

        return new Token(type, directive, start, _position - start);
    }

    private Token ReadIdentifierOrKeyword()
    {
        var start = _position;
        
        // Count the length first to avoid StringBuilder for small identifiers
        var end = _position;
        while (end < _text.Length && (char.IsLetterOrDigit(_text[end]) || _text[end] == '_'))
        {
            end++;
        }
        
        // Extract the identifier directly as a substring - much more efficient for typical identifiers
        var identifier = _text.Substring(start, end - start);
        _position = end;

            // Check for "ci" modifier after certain contexts
            if (identifier.Equals("ci", StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenType.CaseModifier, identifier, start, _position - start);
            }

            // Check for boolean literals
            if (identifier.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                identifier.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenType.BooleanLiteral, identifier, start, _position - start);
            }

            // Check for null literal
            if (identifier.Equals("null", StringComparison.OrdinalIgnoreCase))
            {
                return new Token(TokenType.NullLiteral, identifier, start, _position - start);
            }

            // Check for keywords
            if (Keywords.Contains(identifier))
            {
                return new Token(TokenType.Keyword, identifier, start, _position - start);
            }

            // Check for operators that are words
            if (BooleanOperators.Contains(identifier))
            {
                // Check for "not like" and "not in"
                if (identifier.Equals("not", StringComparison.OrdinalIgnoreCase))
                {
                    var savedPos = _position;
                    SkipWhitespace();
                    var nextWord = PeekNextWord();

                    if (nextWord != null)
                    {
                        if (nextWord.Equals("like", StringComparison.OrdinalIgnoreCase))
                        {
                            _position += nextWord.Length;
                            return new Token(TokenType.StringOperator, "not like", start, _position - start);
                        }

                        if (nextWord.Equals("in", StringComparison.OrdinalIgnoreCase))
                        {
                            _position += nextWord.Length;
                            return new Token(TokenType.MembershipOperator, "not in", start, _position - start);
                        }

                        if (nextWord.Equals("null", StringComparison.OrdinalIgnoreCase))
                        {
                            // This will be handled by "is not null"
                            _position = savedPos;
                        }
                    }
                    else
                    {
                        _position = savedPos;
                    }
                }

                return new Token(TokenType.BooleanOperator, identifier, start, _position - start);
            }

            if (StringOperators.Contains(identifier))
            {
                return new Token(TokenType.StringOperator, identifier, start, _position - start);
            }

            if (MembershipOperators.Contains(identifier))
            {
                return new Token(TokenType.MembershipOperator, identifier, start, _position - start);
            }

            // Check for "is null" and "is not null"
            if (NullOperators.Contains(identifier))
            {
                var savedPos = _position;
                SkipWhitespace();
                var nextWord = PeekNextWord();

                if (nextWord != null)
                {
                    if (nextWord.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        _position += nextWord.Length;
                        return new Token(TokenType.NullOperator, "is null", start, _position - start);
                    }

                    if (nextWord.Equals("not", StringComparison.OrdinalIgnoreCase))
                    {
                        _position += nextWord.Length;
                        SkipWhitespace();
                        var thirdWord = PeekNextWord();

                        if (thirdWord != null && thirdWord.Equals("null", StringComparison.OrdinalIgnoreCase))
                        {
                            _position += thirdWord.Length;
                            return new Token(TokenType.NullOperator, "is not null", start, _position - start);
                        }
                    }
                }

                _position = savedPos;
            }

            // Check if it's a function (followed by parenthesis)
            if (Functions.Contains(identifier))
            {
                var savedPos = _position;
                SkipWhitespace();

                if (_position < _text.Length && _text[_position] == '(')
                {
                    _position = savedPos; // Don't consume the parenthesis
                    return new Token(TokenType.Function, identifier, start, identifier.Length);
                }

                _position = savedPos;
            }

            // Default to identifier
            return new Token(TokenType.Identifier, identifier, start, _position - start);
        }

    private Token ReadNumber()
    {
        var start = _position;
        var numberStart = _position;
        
        // Handle negative sign
        if (_text[_position] == '-')
        {
            _position++;
        }

        // Handle hex numbers
        if (_position + 1 < _text.Length && _text[_position] == '0' &&
            (_text[_position + 1] == 'x' || _text[_position + 1] == 'X'))
        {
            _position += 2;

            while (_position < _text.Length && IsHexDigit(_text[_position]))
            {
                _position++;
            }
        }
        else
        {
            // Regular number
            while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.'))
            {
                _position++;
            }
        }

        var number = _text.Substring(numberStart, _position - numberStart);
        return new Token(TokenType.NumberLiteral, number, start, _position - start);
    }

    private Token ReadStringLiteral()
    {
        var start = _position;
        _position++; // Skip opening quote
        var literalStart = _position;
        var stringStart = _position;
        
        // First pass: check if we need special handling (for escaped quotes)
        var hasEscapedQuotes = false;
        var scanPos = _position;
        while (scanPos < _text.Length)
        {
            if (_text[scanPos] == '\'')
            {
                if (scanPos + 1 < _text.Length && _text[scanPos + 1] == '\'')
                {
                    hasEscapedQuotes = true;
                    break;
                }
                break;
            }
            scanPos++;
        }
        
        // If no escaped quotes, use simple substring
        if (!hasEscapedQuotes)
        {
            while (_position < _text.Length)
            {
                var ch = _text[_position];
                
                if (ch == '\'')
                {
                    var result = _text.Substring(stringStart, _position - stringStart);
                    _position++; // Skip closing quote
                    return new Token(TokenType.StringLiteral, result, start, _position - start);
                }
                else if ((ch == '+' || ch == '-' || ch == '*' || ch == '/') && 
                         _position - literalStart <= 3)
                {
                    // Special case: '. followed by space and operator likely means unclosed string
                    var currentContent = _text.Substring(stringStart, _position - stringStart).Trim();
                    if (currentContent.EndsWith("."))
                    {
                        return new Token(TokenType.StringLiteral, currentContent.TrimEnd(), start, _position - start);
                    }
                }
                else if (_position - literalStart > 50 && (ch == '}' || ch == ')' || ch == ']'))
                {
                    // If we've been reading for a while (>50 chars) and hit a closing bracket,
                    // the string is likely unclosed. This prevents spillover while allowing
                    // normal strings with operators inside them.
                    var result = _text.Substring(stringStart, _position - stringStart);
                    return new Token(TokenType.StringLiteral, result, start, _position - start);
                }
                _position++;
            }
        }
        else
        {
            // Has escaped quotes - need to build string with replacements
            var result = new System.Text.StringBuilder();
            
            while (_position < _text.Length)
            {
                var ch = _text[_position];
                
                if (ch == '\'')
                {
                    // Check for escaped quote
                    if (_position + 1 < _text.Length && _text[_position + 1] == '\'')
                    {
                        result.Append('\''); // Add single quote to result
                        _position += 2;
                    }
                    else
                    {
                        _position++; // Skip closing quote
                        return new Token(TokenType.StringLiteral, result.ToString(), start, _position - start);
                    }
                }
                else if ((ch == '+' || ch == '-' || ch == '*' || ch == '/') && 
                         _position - literalStart <= 3 &&
                         result.ToString().Trim().EndsWith("."))
                {
                    // Special case: '. followed by space and operator likely means unclosed string
                    return new Token(TokenType.StringLiteral, result.ToString().TrimEnd(), start, _position - start);
                }
                else if (_position - literalStart > 50 && (ch == '}' || ch == ')' || ch == ']'))
                {
                    // If we've been reading for a while (>50 chars) and hit a closing bracket,
                    // the string is likely unclosed. This prevents spillover while allowing
                    // normal strings with operators inside them.
                    return new Token(TokenType.StringLiteral, result.ToString(), start, _position - start);
                }
                else
                {
                    result.Append(ch);
                    _position++;
                }
            }
            
            // Unclosed string at end of text
            return new Token(TokenType.StringLiteral, result.ToString(), start, _position - start);
        }
        
        // Unclosed string at end of text (no escaped quotes path)
        var remaining = _text.Substring(stringStart, _position - stringStart);
        return new Token(TokenType.StringLiteral, remaining, start, _position - start);
    }
    
    private void SkipWhitespace()
    {
        while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
        {
            _position++;
        }
    }
}