using SerilogSyntax.Classification;
using SerilogSyntax.Diagnostics;
using System;
using System.Collections.Generic;

namespace SerilogSyntax.Expressions;

/// <summary>
/// Parses Serilog expressions and templates to extract classified regions.
/// </summary>
internal class ExpressionParser
{   
    private readonly ExpressionTokenizer _tokenizer;
    private readonly string _text;
    
    public ExpressionParser(string text)
    {
        _text = text ?? string.Empty;
        _tokenizer = new ExpressionTokenizer(_text);
    }

    /// <summary>
    /// Maps token types to classification types.
    /// </summary>
    private string GetClassificationType(Token token)
    {
        return token.Type switch
        {
            // Literals
            TokenType.StringLiteral => SerilogClassificationTypes.ExpressionLiteral,
            TokenType.NumberLiteral => SerilogClassificationTypes.ExpressionLiteral,
            TokenType.BooleanLiteral => SerilogClassificationTypes.ExpressionKeyword,
            TokenType.NullLiteral => SerilogClassificationTypes.ExpressionKeyword,

            // Identifiers and properties
            TokenType.Identifier => SerilogClassificationTypes.ExpressionProperty,
            TokenType.BuiltinProperty => SerilogClassificationTypes.ExpressionBuiltin,

            // Operators
            TokenType.ComparisonOperator => SerilogClassificationTypes.ExpressionOperator,
            TokenType.BooleanOperator => SerilogClassificationTypes.ExpressionOperator,
            TokenType.ArithmeticOperator => SerilogClassificationTypes.ExpressionOperator,
            TokenType.StringOperator => SerilogClassificationTypes.ExpressionOperator,
            TokenType.MembershipOperator => SerilogClassificationTypes.ExpressionOperator,
            TokenType.NullOperator => SerilogClassificationTypes.ExpressionOperator,
            TokenType.CaseModifier => SerilogClassificationTypes.ExpressionOperator,

            // Functions
            TokenType.Function => SerilogClassificationTypes.ExpressionFunction,

            // Keywords
            TokenType.Keyword => SerilogClassificationTypes.ExpressionKeyword,

            // Template directives
            TokenType.IfDirective => SerilogClassificationTypes.ExpressionDirective,
            TokenType.ElseIfDirective => SerilogClassificationTypes.ExpressionDirective,
            TokenType.ElseDirective => SerilogClassificationTypes.ExpressionDirective,
            TokenType.EndDirective => SerilogClassificationTypes.ExpressionDirective,
            TokenType.EachDirective => SerilogClassificationTypes.ExpressionDirective,
            TokenType.DelimitDirective => SerilogClassificationTypes.ExpressionDirective,

            // Don't classify structural elements for now
            TokenType.Dot => null,
            TokenType.Comma => null,
            TokenType.Colon => null,
            TokenType.OpenParen => null,
            TokenType.CloseParen => null,
            TokenType.OpenBracket => null,
            TokenType.CloseBracket => null,
            TokenType.OpenBrace => null,
            TokenType.CloseBrace => null,

            // Special operators
            TokenType.SpreadOperator => SerilogClassificationTypes.ExpressionOperator,
            TokenType.Wildcard => SerilogClassificationTypes.ExpressionOperator,
            TokenType.At => SerilogClassificationTypes.ExpressionOperator,
            TokenType.Dollar => SerilogClassificationTypes.ExpressionOperator,
            TokenType.Hash => SerilogClassificationTypes.ExpressionDirective,

            _ => null
        };
    }

    /// <summary>
    /// Parses the expression and returns classified regions.
    /// </summary>
    public IEnumerable<ClassifiedRegion> Parse()
    {
        var regions = new List<ClassifiedRegion>();
        
        foreach (var token in _tokenizer.Tokenize())
        {
            var classificationType = GetClassificationType(token);

            if (!string.IsNullOrEmpty(classificationType))
            {
                regions.Add(new ClassifiedRegion(
                    classificationType,
                    token.Start,
                    token.Length,
                    token.Value));
            }
        }
        
        return regions;
    }
    
    /// <summary>
    /// Parses an expression template and returns both template properties and expression regions.
    /// This handles mixed content like: "{@t:HH:mm:ss} {#if Level = 'Error'}ERROR{#end} {@m}"
    /// </summary>
    public IEnumerable<ClassifiedRegion> ParseExpressionTemplate()
    {
        DiagnosticLogger.Log($"[ExpressionParser.ParseExpressionTemplate] Starting parse of text length: {_text?.Length}");
        var regions = new List<ClassifiedRegion>();
        var i = 0;
        var loopVariables = new HashSet<string>(); // Track loop variables from #each directives
        
        while (i < _text.Length)
        {
            if (_text[i] == '{' && i + 1 < _text.Length)
            {
                // Check for escaped brace
                if (_text[i + 1] == '{')
                {
                    i += 2;
                    continue;
                }
                
                var start = i;
                
                // Special handling for directives that might be unclosed
                bool isSimpleDirective = false;
                int directiveLen = 0;
                
                if (i + 4 < _text.Length && _text.Substring(i + 1, 4) == "#end")
                {
                    isSimpleDirective = true;
                    directiveLen = 4;
                }
                else if (i + 8 < _text.Length && _text.Substring(i + 1, 8) == "#else if")
                {
                    // #else if is NOT a simple directive - it has an expression that needs parsing
                    // Don't set isSimpleDirective = true, let it fall through to normal directive handling
                    isSimpleDirective = false;
                }
                else if (i + 5 < _text.Length && _text.Substring(i + 1, 5) == "#else")
                {
                    isSimpleDirective = true;
                    directiveLen = 5;
                }
                
                if (isSimpleDirective && directiveLen > 0)
                {
                    // Look for the closing brace, but be aware it might be missing
                    var spaceAfter = _text.IndexOf(' ', i + 1 + directiveLen);
                    var braceAfter = _text.IndexOf('}', i + 1 + directiveLen);
                    
                    if (spaceAfter > 0 && spaceAfter == i + 1 + directiveLen && 
                        (braceAfter == -1 || spaceAfter < braceAfter))
                    {
                        // Space immediately after directive and before any closing brace - this is unclosed
                        var directive = _text.Substring(i + 1, directiveLen);
                        DiagnosticLogger.Log($"[ParseExpressionTemplate] Detected unclosed {directive} at position {i}");
                        
                        // Just classify the directive part
                        regions.Add(new ClassifiedRegion(
                            SerilogClassificationTypes.ExpressionDirective,
                            i + 1,
                            directiveLen,
                            directive));
                        
                        // Move past the unclosed directive but keep parsing
                        i = spaceAfter;
                        continue;
                    }
                }
                
                var end = _text.IndexOf('}', i + 1);
                if (end == -1)
                {
                    i++;
                    continue;
                }
                
                // Check if there's a mismatch bracket that comes immediately after a format specifier
                // This handles cases like {@l:u3] where ] is used instead of }
                var content = _text.Substring(i + 1, end - i - 1);
                if (content.Contains(":") && content.Contains("]"))
                {
                    var colonPos = content.IndexOf(':');
                    var bracketPos = content.IndexOf(']');
                    
                    // If ] comes right after format specifier and before any space or other character
                    // it's likely a typo (] instead of })
                    if (bracketPos > colonPos && bracketPos < content.Length - 1)
                    {
                        var formatPart = content.Substring(colonPos + 1, bracketPos - colonPos - 1);
                        // Check if the format part looks like a valid format string (no spaces, no special chars)
                        if (!formatPart.Contains(" ") && !formatPart.Contains("{"))
                        {
                            // This looks like {@x:format] - an unclosed property
                            DiagnosticLogger.Log($"[ParseExpressionTemplate] Detected unclosed property with mismatched bracket at position {i}");
                            end = i + 1 + bracketPos;
                        }
                    }
                }
                
                // Check if this is an #each directive to extract loop variables
                var segmentContent = _text.Substring(start + 1, end - start - 1);
                if (segmentContent.StartsWith("#each "))
                {
                    ExtractLoopVariables(segmentContent, loopVariables);
                }
                else if (segmentContent == "#end")
                {
                    // Clear loop variables when exiting a block (simple heuristic)
                    loopVariables.Clear();
                }
                
                ProcessTemplateSegment(regions, start, end, loopVariables);
                i = end + 1;
            }
            else
            {
                i++;
            }
        }

        return regions;
    }
    
    private void ExtractLoopVariables(string eachDirective, HashSet<string> loopVariables)
    {
        // Parse "#each name, value in @p" to extract "name" and "value"
        // Expected format: "#each <var1>[, <var2>] in <expr>"
        var inIndex = eachDirective.IndexOf(" in ");
        if (inIndex == -1) return;
        
        var variablePart = eachDirective.Substring(6, inIndex - 6).Trim(); // Skip "#each "
        var variables = variablePart.Split(',');
        
        foreach (var variable in variables)
        {
            var trimmed = variable.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                loopVariables.Add(trimmed);
                DiagnosticLogger.Log($"[ExtractLoopVariables] Added loop variable: '{trimmed}'");
            }
        }
    }

    private void HandleBuiltinProperty(List<ClassifiedRegion> regions, int propertyStart, string content)
    {
        DiagnosticLogger.Log($"[HandleBuiltinProperty] content='{content}', propertyStart={propertyStart}");
        
        // Determine built-in property length
        var builtinEnd = 1; // @
        if (content.Length > 1)
        {
            var ch = content[1];
            if (ch == 't' || ch == 'm' || ch == 'l' || ch == 'x' || ch == 'p' || ch == 'i' || ch == 'r')
            {
                builtinEnd = 2;
            }

            if (content.Length > 2 && (content.StartsWith("@mt") || content.StartsWith("@tr") || content.StartsWith("@sp")))
            {
                builtinEnd = 3;
            }
        }

        regions.Add(new ClassifiedRegion(
            SerilogClassificationTypes.ExpressionBuiltin,
            propertyStart,
            builtinEnd,
            content.Substring(0, Math.Min(builtinEnd, content.Length))));

        // Check for indexer syntax like @p['RequestId'] or @p["RequestId"]
        if (builtinEnd < content.Length && content[builtinEnd] == '[')
        {
            var openBracketIndex = builtinEnd;
            var closeBracketIndex = content.LastIndexOf(']');
            
            if (closeBracketIndex > openBracketIndex)
            {
                // Add the opening bracket as operator
                regions.Add(new ClassifiedRegion(
                    SerilogClassificationTypes.ExpressionOperator,
                    propertyStart + openBracketIndex,
                    1,
                    "["));
                
                // Extract and classify the string literal inside the brackets
                var literalStart = openBracketIndex + 1;
                var literalEnd = closeBracketIndex;
                var literalContent = content.Substring(literalStart, literalEnd - literalStart);
                
                // Check if it's a quoted string literal
                if ((literalContent.StartsWith("'") && literalContent.EndsWith("'")) ||
                    (literalContent.StartsWith("\"") && literalContent.EndsWith("\"")))
                {
                    regions.Add(new ClassifiedRegion(
                        SerilogClassificationTypes.ExpressionLiteral,
                        propertyStart + literalStart,
                        literalContent.Length,
                        literalContent));
                }
                
                // Add the closing bracket as operator
                regions.Add(new ClassifiedRegion(
                    SerilogClassificationTypes.ExpressionOperator,
                    propertyStart + closeBracketIndex,
                    1,
                    "]"));
                
                DiagnosticLogger.Log($"[HandleBuiltinProperty] Added indexer syntax: [{literalContent}]");
                return; // Skip format specifier check when we have indexer syntax
            }
        }

        // Check for format specifier (only if no indexer syntax)
        var colonIndex = content.IndexOf(':');
        DiagnosticLogger.Log($"[HandleBuiltinProperty] colonIndex={colonIndex}, content.Length={content.Length}");

        if (colonIndex > 0 && colonIndex < content.Length - 1)
        {
            DiagnosticLogger.Log($"[HandleBuiltinProperty] Adding format specifier: ':' at {propertyStart + colonIndex}");
            DiagnosticLogger.Log($"[HandleBuiltinProperty] Adding format string: '{content.Substring(colonIndex + 1)}' at {propertyStart + colonIndex + 1}");

            // Add the colon as format specifier
            regions.Add(new ClassifiedRegion(
                SerilogClassificationTypes.FormatSpecifier,
                propertyStart + colonIndex,
                1,
                ":"));
            
            // Add the format string after the colon
            regions.Add(new ClassifiedRegion(
                SerilogClassificationTypes.FormatSpecifier,
                propertyStart + colonIndex + 1,
                content.Length - colonIndex - 1,
                content.Substring(colonIndex + 1)));
        }
    }

    private void HandleDirective(List<ClassifiedRegion> regions, int start, int end, string content)
    {
        // Add the directive itself
        regions.Add(new ClassifiedRegion(
            SerilogClassificationTypes.ExpressionDirective,
            start,
            end - start + 1,
            _text.Substring(start, end - start + 1)));

        // Parse expression within directive if present
        if ((content.StartsWith("#if ") || content.StartsWith("#each ") || content.StartsWith("#else if ")) && content.Length > 4)
        {
            var exprStart = content.IndexOf(' ') + 1;
            
            // For #else if, we need to find the second space (after "if")
            if (content.StartsWith("#else if "))
            {
                var secondSpace = content.IndexOf(' ', exprStart);
                if (secondSpace > exprStart)
                {
                    exprStart = secondSpace + 1;
                }
            }
            
            if (exprStart > 0 && exprStart < content.Length)
            {
                var expr = content.Substring(exprStart);

                // Use direct tokenization instead of recursive parser creation
                TokenizeAndClassifyInline(expr, start + 1 + exprStart, regions);
            }
        }
    }
    
    private void ProcessTemplateSegment(List<ClassifiedRegion> regions, int start, int end, HashSet<string> loopVariables)
    {
        var content = _text.Substring(start + 1, end - start - 1);
        DiagnosticLogger.Log($"[ProcessTemplateSegment] Processing segment: '{content}' from {start} to {end}");
        
        // Add brace classifications
        regions.Add(new ClassifiedRegion(SerilogClassificationTypes.PropertyBrace, start, 1, "{"));
        regions.Add(new ClassifiedRegion(SerilogClassificationTypes.PropertyBrace, end, 1, "}"));
        
        if (content.StartsWith("#"))
        {
            // Handle directive
            HandleDirective(regions, start, end, content);
        }
        else if (content.StartsWith("@"))
        {
            // Handle built-in property
            DiagnosticLogger.Log($"[ProcessTemplateSegment] Found builtin property: '{content}' at position {start}");
            HandleBuiltinProperty(regions, start + 1, content);
        }
        else if (!content.Contains(" ") && !content.Contains("#"))
        {
            // Determine if this is a loop variable reference or a regular property
            var classificationType = (loopVariables != null && loopVariables.Contains(content))
                ? SerilogClassificationTypes.ExpressionProperty  // Loop variable reference
                : SerilogClassificationTypes.PropertyName;       // Regular property
                
            regions.Add(new ClassifiedRegion(
                classificationType,
                start + 1,
                content.Length,
                content));
        }
        else if (content.Length > 0)
        {
            // This could be an expression with functions, operators, etc.
            // Parse it as an expression
            DiagnosticLogger.Log($"[ProcessTemplateSegment] Parsing as expression: '{content}'");

            // Use direct tokenization instead of recursive parser creation
            TokenizeAndClassifyInline(content, start + 1, regions);
        }
    }
    
    /// <summary>
    /// Tokenizes and classifies a substring inline without creating a new parser instance.
    /// This avoids recursive parser creation and reduces memory allocations.
    /// </summary>
    private void TokenizeAndClassifyInline(string text, int baseOffset, List<ClassifiedRegion> regions)
    {
        var tokenizer = new ExpressionTokenizer(text);
        foreach (var token in tokenizer.Tokenize())
        {
            var classificationType = GetClassificationType(token);

            if (!string.IsNullOrEmpty(classificationType))
            {
                regions.Add(new ClassifiedRegion(
                    classificationType,
                    baseOffset + token.Start,
                    token.Length,
                    token.Value));
            }
        }
    }
}