using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.VisualStudio.Text;
using SerilogSyntax.Diagnostics;
using SerilogSyntax.Expressions;
using SerilogSyntax.Utilities;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;

namespace SerilogSyntax.Classification;

/// <summary>
/// Analyzes C# syntax trees to determine if a position is inside a Serilog method call.
/// This provides detection that understands the actual code structure rather than relying
/// on text pattern matching.
/// </summary>
internal static class SyntaxTreeAnalyzer
{
    /// <summary>
    /// Cache for parsed syntax trees per snapshot to avoid reparsing
    /// </summary>
    private static readonly ConcurrentDictionary<ITextSnapshot, (SyntaxTree tree, SyntaxNode root)> _syntaxTreeCache = new();

    /// <summary>
    /// Cache for invocation analysis results to avoid reanalyzing the same method calls
    /// Key is a combination of snapshot hash and span to ensure uniqueness across different files/snapshots
    /// </summary>
    private static readonly ConcurrentDictionary<(int snapshotHash, int spanStart), bool> _invocationCache = new();

    /// <summary>
    /// Conditional diagnostic logging that only executes in DEBUG builds
    /// </summary>
    [Conditional("DEBUG")]
    private static void LogDiagnostic(string message)
    {
        DiagnosticLogger.Log(message);
    }

    /// <summary>
    /// Clears caches when they get too large to prevent memory leaks
    /// </summary>
    public static void ClearCachesIfNeeded()
    {
        // Reasonable limit for VS usage
        const int maxCacheSize = 50;

        if (_syntaxTreeCache.Count > maxCacheSize)
        {
            _syntaxTreeCache.Clear();
            LogDiagnostic($"[SyntaxTreeAnalyzer] Cleared syntax tree cache (size exceeded {maxCacheSize})");
        }

        if (_invocationCache.Count > maxCacheSize * 10) // Invocations are smaller, allow more
        {
            _invocationCache.Clear();
            LogDiagnostic($"[SyntaxTreeAnalyzer] Cleared invocation cache (size exceeded {maxCacheSize * 10})");
        }
    }

    /// <summary>
    /// Gets or creates a cached syntax tree for the given snapshot
    /// </summary>
    private static (SyntaxTree tree, SyntaxNode root) GetCachedSyntaxTree(ITextSnapshot snapshot)
    {
        // Periodically clear caches to prevent memory leaks
        ClearCachesIfNeeded();

        return _syntaxTreeCache.GetOrAdd(snapshot, s =>
        {
            var text = s.GetText();
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();
            LogDiagnostic($"[SyntaxTreeAnalyzer] Parsed new syntax tree for snapshot");
            return (tree, root);
        });
    }

    /// <summary>
    /// Fast path check to avoid expensive syntax tree parsing when unnecessary
    /// </summary>
    private static bool RequiresSyntaxTreeAnalysis(string lineText)
    {
        // Quick rejection for lines that definitely aren't Serilog related
        if (!lineText.Contains("Log") && !lineText.Contains("log") &&
            !lineText.Contains("{") && !lineText.Contains("}") &&
            !lineText.Contains("Filter") && !lineText.Contains("Enrich") &&
            !lineText.Contains("WriteTo") && !lineText.Contains("ExpressionTemplate"))
        {
            return false;
        }

        // Lines that likely need detailed analysis
        return lineText.Contains("\"\"\"") ||  // Raw strings
               lineText.Contains("@\"") ||     // Verbatim strings
               (lineText.Contains("Log") && lineText.Contains("(")) || // Method calls
               (lineText.Contains("{") && lineText.Contains("}")) || // Template syntax
               lineText.Contains("Filter.") || // Expression filters
               lineText.Contains("Enrich.") || // Expression enrichers
               lineText.Contains("WriteTo.") || // Conditional writes
               lineText.Contains("ExpressionTemplate"); // Expression templates
    }

    /// <summary>
    /// Determines if the given position is inside a string literal that is an argument
    /// to a Serilog method call.
    /// </summary>
    /// <param name="snapshot">The text snapshot containing the code</param>
    /// <param name="position">The position to check</param>
    /// <returns>True if the position is inside a Serilog template string, false otherwise</returns>
    public static bool IsPositionInsideSerilogTemplate(ITextSnapshot snapshot, int position)
    {
        try
        {
            // Fast path: Check current line first to avoid expensive parsing when possible
            var currentLine = snapshot.GetLineFromPosition(position);
            var currentLineText = currentLine.GetText();

            if (!RequiresSyntaxTreeAnalysis(currentLineText))
            {
                LogDiagnostic($"[SyntaxTreeAnalyzer] Fast path rejection for line: '{currentLineText.Trim()}'");
                return false;
            }

            // Use cached syntax tree to avoid reparsing
            var (syntaxTree, root) = GetCachedSyntaxTree(snapshot);

            // Find the token at the given position
            var token = root.FindToken(position);
            var node = token.Parent;

            LogDiagnostic($"[SyntaxTreeAnalyzer] Position {position}, token: {token.Kind()}, node: {node?.GetType().Name}");

            // Check if the token itself is a string literal
            if (token.IsKind(SyntaxKind.StringLiteralToken))
            {
                LogDiagnostic($"[SyntaxTreeAnalyzer] Found string literal token, parent: {node?.GetType().Name}");

                if (node is LiteralExpressionSyntax literalExpression)
                {
                    var result = IsStringLiteralInSerilogCall(literalExpression);
                    LogDiagnostic($"[SyntaxTreeAnalyzer] String literal in Serilog call: {result}");
                    return result;
                }
            }

            // Check if we're inside a multi-line raw string literal
            if (token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken))
            {
                LogDiagnostic($"[SyntaxTreeAnalyzer] Found multi-line raw string token");
                if (node is LiteralExpressionSyntax literalExpression)
                {
                    var result = IsStringLiteralInSerilogCall(literalExpression);
                    LogDiagnostic($"[SyntaxTreeAnalyzer] Multi-line raw string in Serilog call: {result}");
                    return result;
                }
            }

            // Also check for single-line raw string literals
            if (token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken))
            {
                LogDiagnostic($"[SyntaxTreeAnalyzer] Found single-line raw string token");
                if (node is LiteralExpressionSyntax literalExpression)
                {
                    var result = IsStringLiteralInSerilogCall(literalExpression);
                    LogDiagnostic($"[SyntaxTreeAnalyzer] Single-line raw string in Serilog call: {result}");
                    return result;
                }
            }


            // Walk up the syntax tree to find if we're inside a string literal
            int depth = 0;
            while (node != null && depth < 10)  // Prevent infinite loops
            {
                LogDiagnostic($"[SyntaxTreeAnalyzer] Checking node at depth {depth}: {node.GetType().Name}");

                // Check if this is a string literal (handles verbatim strings like @"...")
                if (node is LiteralExpressionSyntax literalExpression &&
                    literalExpression.Token.IsKind(SyntaxKind.StringLiteralToken))
                {
                    LogDiagnostic($"[SyntaxTreeAnalyzer] Found literal expression at depth {depth}");
                    var result = IsStringLiteralInSerilogCall(literalExpression);
                    LogDiagnostic($"[SyntaxTreeAnalyzer] Literal expression in Serilog call: {result}");
                    return result;
                }

                // Check if this is a raw string literal (C# 11: """...""")
                if (node is LiteralExpressionSyntax rawStringLiteral &&
                    (rawStringLiteral.Token.Text.StartsWith("\"\"\"") || rawStringLiteral.Token.Text.StartsWith("@\"\"\"")))
                {
                    LogDiagnostic($"[SyntaxTreeAnalyzer] Found raw string literal at depth {depth}");
                    var result = IsStringLiteralInSerilogCall(rawStringLiteral);
                    LogDiagnostic($"[SyntaxTreeAnalyzer] Raw string in Serilog call: {result}");
                    return result;
                }

                // Check if this is an interpolated string
                if (node is InterpolatedStringExpressionSyntax)
                {
                    LogDiagnostic($"[SyntaxTreeAnalyzer] Found interpolated string - not a Serilog template");
                    // Interpolated strings are not Serilog templates
                    return false;
                }

                node = node.Parent;
                depth++;
            }

            // If the current line itself contains a Serilog call, allow classification
            if (SerilogCallDetector.IsSerilogCall(currentLineText))
            {
                LogDiagnostic($"[SyntaxTreeAnalyzer] Current line contains Serilog call pattern");
                return true;
            }

            // Before returning false, check if this is a multi-line string continuation
            // that's part of a Serilog call started on a previous line

            // Only check if this line has template syntax
            if (currentLineText.Contains("{") && currentLineText.Contains("}"))
            {
                // Check ONLY the immediately preceding lines for an unclosed Serilog call
                for (int i = currentLine.LineNumber - 1; i >= Math.Max(0, currentLine.LineNumber - 3); i--)
                {
                    var checkLine = snapshot.GetLineFromLineNumber(i);
                    var checkText = checkLine.GetText();

                    // Check if this line starts a Serilog call with an unclosed string
                    if (SerilogCallDetector.IsSerilogCall(checkText) &&
                        (checkText.Contains("\"\"\"") || checkText.Contains("@\"")))
                    {
                        // This could be a continuation of a multi-line Serilog string
                        LogDiagnostic($"[SyntaxTreeAnalyzer] Found potential multi-line Serilog string start at line {i}");
                        return true;
                    }
                }
            }

            LogDiagnostic($"[SyntaxTreeAnalyzer] No string literal found in syntax tree - defaulting to false");
            return false;  // Default to false - only classify when we're certain it's a Serilog call
        }
        catch (System.Exception ex)
        {
            // If parsing fails, check if this looks like a Serilog call
            // using the original text-based detection
            LogDiagnostic($"[SyntaxTreeAnalyzer] Exception parsing syntax tree: {ex.Message}");

            try
            {
                var line = snapshot.GetLineFromPosition(position);
                var lineText = line.GetText();

                LogDiagnostic($"[SyntaxTreeAnalyzer] Fallback check on line: '{lineText.Trim()}'");

                // If the line appears to be inside a Serilog call, assume it is
                // This prevents false negatives when Roslyn can't parse
                if (SerilogCallDetector.IsSerilogCall(lineText))
                {
                    LogDiagnostic($"[SyntaxTreeAnalyzer] Fallback: Line matches Serilog pattern, returning true");
                    return true;
                }

                // Also check nearby lines for Serilog context
                for (int i = Math.Max(0, line.LineNumber - 3); i <= Math.Min(snapshot.LineCount - 1, line.LineNumber + 1); i++)
                {
                    var contextLine = snapshot.GetLineFromLineNumber(i);
                    var contextText = contextLine.GetText();
                    if (SerilogCallDetector.IsSerilogCall(contextText))
                    {
                        LogDiagnostic($"[SyntaxTreeAnalyzer] Fallback: Found Serilog context on line {i}, returning true");
                        return true;
                    }
                }

                LogDiagnostic($"[SyntaxTreeAnalyzer] Fallback: No Serilog pattern found, returning false");
                return false;
            }
            catch
            {
                LogDiagnostic($"[SyntaxTreeAnalyzer] Complete fallback failure, returning false");
                return false;
            }
        }
    }

    /// <summary>
    /// Determines if a string literal is an argument to a Serilog method call.
    /// </summary>
    /// <param name="stringLiteral">The string literal syntax node</param>
    /// <returns>True if this string is a Serilog template, false otherwise</returns>
    private static bool IsStringLiteralInSerilogCall(LiteralExpressionSyntax stringLiteral)
    {
        // Walk up to find the actual invocation this string belongs to
        var node = stringLiteral.Parent;
        int depth = 0;

        LogDiagnostic($"[IsStringLiteralInSerilogCall] Starting from string literal: '{stringLiteral.Token.Text.Substring(0, Math.Min(50, stringLiteral.Token.Text.Length))}'");

        while (node != null && depth < 10)  // Prevent infinite loops
        {
            LogDiagnostic($"[IsStringLiteralInSerilogCall] Depth {depth}: {node.GetType().Name}");

            if (node is InvocationExpressionSyntax invocation)
            {
                LogDiagnostic($"[IsStringLiteralInSerilogCall] Found invocation at depth {depth}");

                // This is the KEY: Check if THIS invocation is a Serilog call
                // Don't just check ANY invocation in the tree

                // Is the string literal a direct argument to THIS invocation?
                var arguments = invocation.ArgumentList?.Arguments;
                bool isDirectArgument = false;

                if (arguments != null)
                {
                    foreach (var arg in arguments)
                    {
                        // Check if this argument contains our string literal
                        if (ContainsNode(arg.Expression, stringLiteral))
                        {
                            isDirectArgument = true;
                            LogDiagnostic($"[IsStringLiteralInSerilogCall] String is direct argument to invocation");
                            break;
                        }
                    }
                }

                if (isDirectArgument)
                {
                    // Only return true if THIS invocation is a Serilog method
                    var result = IsSerilogMethodInvocation(invocation);
                    LogDiagnostic($"[IsStringLiteralInSerilogCall] Direct invocation is Serilog: {result}");
                    return result;
                }
                else
                {
                    LogDiagnostic($"[IsStringLiteralInSerilogCall] String is not direct argument, continuing search");
                }
            }

            // Check for string concatenation (could be part of ExpressionTemplate or Serilog call)
            if (node is BinaryExpressionSyntax binaryExpression &&
                binaryExpression.IsKind(SyntaxKind.AddExpression))
            {
                LogDiagnostic($"[IsStringLiteralInSerilogCall] Found string concatenation at depth {depth}");

                // Check if this concatenation contains our string literal
                if (ContainsNode(binaryExpression, stringLiteral))
                {
                    // Continue walking up to see if this concatenation is used in a Serilog call
                    // Don't return here, just continue the loop
                    LogDiagnostic($"[IsStringLiteralInSerilogCall] String is part of concatenation, continuing search");
                }
            }

            // Check for new ExpressionTemplate()
            if (node is ObjectCreationExpressionSyntax objectCreation)
            {
                var typeName = objectCreation.Type.ToString();
                LogDiagnostic($"[IsStringLiteralInSerilogCall] Found object creation at depth {depth}: '{typeName}'");

                // Check if this is ExpressionTemplate
                if (typeName == "ExpressionTemplate" ||
                    typeName.EndsWith(".ExpressionTemplate") ||
                    typeName.Contains("ExpressionTemplate"))
                {
                    LogDiagnostic($"[IsStringLiteralInSerilogCall] Detected ExpressionTemplate type");

                    // Is the string literal a direct argument OR part of a concatenation argument?
                    var arguments = objectCreation.ArgumentList?.Arguments;
                    if (arguments != null && arguments.Value.Count > 0)
                    {
                        LogDiagnostic($"[IsStringLiteralInSerilogCall] ExpressionTemplate has {arguments.Value.Count} arguments");

                        for (int i = 0; i < arguments.Value.Count; i++)
                        {
                            var arg = arguments.Value[i];
                            LogDiagnostic($"[IsStringLiteralInSerilogCall] Checking argument {i}: {arg.Expression.GetType().Name}");

                            if (ContainsNode(arg.Expression, stringLiteral))
                            {
                                LogDiagnostic($"[IsStringLiteralInSerilogCall] String is argument {i} to ExpressionTemplate constructor");
                                return true;
                            }
                        }

                        LogDiagnostic($"[IsStringLiteralInSerilogCall] String literal not found in any ExpressionTemplate arguments");
                    }
                    else
                    {
                        LogDiagnostic($"[IsStringLiteralInSerilogCall] ExpressionTemplate has no arguments");
                    }
                }
                else
                {
                    LogDiagnostic($"[IsStringLiteralInSerilogCall] Not an ExpressionTemplate: '{typeName}'");
                }
            }

            node = node.Parent;
            depth++;
        }

        LogDiagnostic($"[IsStringLiteralInSerilogCall] No invocation found, checking context");

        // If we can't find an invocation, this string is likely just assigned to a variable
        // or used in some other non-Serilog context
        // Only return true if we have strong evidence this is a Serilog template

        // Check if the string is part of a variable assignment
        var parent = stringLiteral.Parent;
        if (parent is EqualsValueClauseSyntax ||
            parent?.Parent is EqualsValueClauseSyntax ||
            parent?.Parent?.Parent is LocalDeclarationStatementSyntax)
        {
            LogDiagnostic($"[IsStringLiteralInSerilogCall] String is in variable assignment, returning false");
            return false;
        }

        // Default to false when no invocation is found
        LogDiagnostic($"[IsStringLiteralInSerilogCall] No invocation found, returning false");
        return false;
    }

    /// <summary>
    /// Determines if an invocation expression is a Serilog method call.
    /// </summary>
    /// <param name="invocation">The method invocation to check</param>
    /// <returns>True if this is a Serilog method call, false otherwise</returns>
    private static bool IsSerilogMethodInvocation(InvocationExpressionSyntax invocation)
    {
        // Use a combination of the syntax tree hash and span position as cache key
        // This ensures uniqueness across different test cases and files
        var syntaxTree = invocation.SyntaxTree;
        var cacheKey = (syntaxTree.GetText().GetHashCode(), invocation.Span.Start);

        return _invocationCache.GetOrAdd(cacheKey, _ =>
        {
            return AnalyzeInvocation(invocation);
        });
    }

    /// <summary>
    /// Analyzes an invocation to determine if it's a Serilog method call (uncached)
    /// </summary>
    private static bool AnalyzeInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            LogDiagnostic($"[IsSerilogMethodInvocation] No member access found");
            return false;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        LogDiagnostic($"[IsSerilogMethodInvocation] Method name: {methodName}");

        // Check for Serilog method names
        var serilogMethods = new[]
        {
            "LogTrace", "LogDebug", "LogInformation", "LogWarning", "LogError", "LogCritical", "LogFatal",
            "Verbose", "Debug", "Information", "Warning", "Error", "Fatal",
            "BeginScope",
            // Serilog.Expressions methods
            "ByExcluding", "ByIncludingOnly", "WithComputed", "When", "Conditional"
        };

        // For expression methods, check if they're called on Filter/Enrich/WriteTo
        if (methodName == "ByExcluding" || methodName == "ByIncludingOnly")
        {
            if (memberAccess.Expression is IdentifierNameSyntax filterIdentifier &&
                filterIdentifier.Identifier.ValueText == "Filter")
            {
                LogDiagnostic($"[IsSerilogMethodInvocation] Matched Filter.{methodName}");
                return true;
            }
        }

        if (methodName == "WithComputed" || methodName == "When")
        {
            if (memberAccess.Expression is IdentifierNameSyntax enrichIdentifier &&
                enrichIdentifier.Identifier.ValueText == "Enrich")
            {
                LogDiagnostic($"[IsSerilogMethodInvocation] Matched Enrich.{methodName}");
                return true;
            }
        }

        if (methodName == "Conditional")
        {
            if (memberAccess.Expression is IdentifierNameSyntax writeToIdentifier &&
                writeToIdentifier.Identifier.ValueText == "WriteTo")
            {
                LogDiagnostic($"[IsSerilogMethodInvocation] Matched WriteTo.{methodName}");
                return true;
            }
        }

        if (!serilogMethods.Contains(methodName))
        {
            LogDiagnostic($"[IsSerilogMethodInvocation] Method name '{methodName}' not in Serilog methods");
            return false;
        }

        // Check if the target is a logger-like object
        var target = memberAccess.Expression;
        LogDiagnostic($"[IsSerilogMethodInvocation] Target type: {target.GetType().Name}");

        // Direct static calls: Log.Information, Log.Debug, etc.
        if (target is IdentifierNameSyntax identifier)
        {
            var targetName = identifier.Identifier.ValueText;
            LogDiagnostic($"[IsSerilogMethodInvocation] Identifier target: {targetName}");
            if (targetName == "Log")
            {
                LogDiagnostic($"[IsSerilogMethodInvocation] Matched static Log call");
                return true;
            }
        }

        // Instance calls: logger.LogInformation, _logger.LogDebug, etc.
        if (target is IdentifierNameSyntax instanceIdentifier)
        {
            var instanceName = instanceIdentifier.Identifier.ValueText.ToLowerInvariant();
            LogDiagnostic($"[IsSerilogMethodInvocation] Instance identifier: {instanceName}");
            if (instanceName.Contains("log"))
            {
                LogDiagnostic($"[IsSerilogMethodInvocation] Matched instance logger call");
                return true;
            }
        }

        // Member access: this.logger.LogInformation, etc.
        if (target is MemberAccessExpressionSyntax nestedMemberAccess)
        {
            var memberName = nestedMemberAccess.Name.Identifier.ValueText.ToLowerInvariant();
            LogDiagnostic($"[IsSerilogMethodInvocation] Member access: {memberName}");
            if (memberName.Contains("log"))
            {
                LogDiagnostic($"[IsSerilogMethodInvocation] Matched member access logger call");
                return true;
            }
        }

        // Contextual loggers: Log.ForContext<T>().Information, etc.
        if (target is InvocationExpressionSyntax nestedInvocation)
        {
            var contextualMemberAccess = nestedInvocation.Expression as MemberAccessExpressionSyntax;
            if (contextualMemberAccess?.Expression is IdentifierNameSyntax nestedIdentifier)
            {
                var nestedTargetName = nestedIdentifier.Identifier.ValueText;
                LogDiagnostic($"[IsSerilogMethodInvocation] Contextual target: {nestedTargetName}");
                if (nestedTargetName == "Log")
                {
                    LogDiagnostic($"[IsSerilogMethodInvocation] Matched contextual Log call");
                    return true;
                }
            }
        }

        LogDiagnostic($"[IsSerilogMethodInvocation] No match found");
        return false;
    }

    /// <summary>
    /// Determines what type of expression context a string literal is in, if any.
    /// Returns the appropriate ExpressionContext or None if not in an expression.
    /// </summary>
    /// <param name="snapshot">The text snapshot containing the code</param>
    /// <param name="position">The position to check</param>
    /// <returns>
    /// The expression context for the position
    /// </returns>
    public static ExpressionContext GetExpressionContext(ITextSnapshot snapshot, int position)
    {
        try
        {
            // Use cached syntax tree to avoid reparsing
            var (syntaxTree, root) = GetCachedSyntaxTree(snapshot);

            // Find the token at the given position
            var token = root.FindToken(position);
            var node = token.Parent;

            LogDiagnostic($"[GetExpressionContext] Position {position}, token: {token.Kind()}, node: {node?.GetType().Name}");

            // Find the string literal
            LiteralExpressionSyntax stringLiteral = null;

            if (token.IsKind(SyntaxKind.StringLiteralToken)
                || token.IsKind(SyntaxKind.MultiLineRawStringLiteralToken)
                || token.IsKind(SyntaxKind.SingleLineRawStringLiteralToken))
            {
                if (node is LiteralExpressionSyntax literal)
                {
                    stringLiteral = literal;
                }
            }

            if (stringLiteral == null)
            {
                // Walk up to find a string literal
                var current = node;
                while (current != null && current is not LiteralExpressionSyntax)
                {
                    current = current.Parent;
                }

                stringLiteral = current as LiteralExpressionSyntax;
            }

            if (stringLiteral == null)
            {
                LogDiagnostic($"[GetExpressionContext] No string literal found");
                return ExpressionContext.None;
            }

            // Walk up to find the invocation or object creation this string belongs to
            var parent = stringLiteral.Parent;
            int depth = 0;

            while (parent != null && depth < 10)
            {
                LogDiagnostic($"[GetExpressionContext] Checking parent at depth {depth}: {parent.GetType().Name}");

                // Check for ExpressionTemplate constructor
                if (parent is ObjectCreationExpressionSyntax objectCreation)
                {
                    var typeName = objectCreation.Type.ToString();
                    if (typeName.Contains("ExpressionTemplate"))
                    {
                        // Check if our string is an argument to this constructor
                        var arguments = objectCreation.ArgumentList?.Arguments;
                        if (arguments != null)
                        {
                            foreach (var arg in arguments.Value)
                            {
                                if (ContainsNode(arg.Expression, stringLiteral))
                                {
                                    LogDiagnostic($"[GetExpressionContext] String is argument to ExpressionTemplate");
                                    return ExpressionContext.ExpressionTemplate;
                                }
                            }
                        }
                    }
                }

                // Check for method invocations
                if (parent is InvocationExpressionSyntax invocation)
                {
                    // Check if this string is a direct argument to this invocation
                    var arguments = invocation.ArgumentList?.Arguments;
                    bool isDirectArgument = false;
                    int argumentIndex = -1;

                    if (arguments != null)
                    {
                        for (int i = 0; i < arguments.Value.Count; i++)
                        {
                            if (ContainsNode(arguments.Value[i].Expression, stringLiteral))
                            {
                                isDirectArgument = true;
                                argumentIndex = i;
                                break;
                            }
                        }
                    }

                    if (isDirectArgument && invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                    {
                        var methodName = memberAccess.Name.Identifier.ValueText;
                        LogDiagnostic($"[GetExpressionContext] String is argument {argumentIndex} to method {methodName}");

                        // Check for Filter expressions
                        if (methodName == "ByExcluding" || methodName == "ByIncludingOnly")
                        {
                            // Check if this is called on Filter (either Filter.ByExcluding or .Filter.ByExcluding)
                            var expression = memberAccess.Expression;
                            string targetName = null;

                            if (expression is IdentifierNameSyntax identifier)
                            {
                                targetName = identifier.Identifier.ValueText;
                            }
                            else if (expression is MemberAccessExpressionSyntax nestedAccess &&
                                     nestedAccess.Name is IdentifierNameSyntax nestedIdentifier)
                            {
                                targetName = nestedIdentifier.Identifier.ValueText;
                            }

                            if (targetName == "Filter")
                            {
                                LogDiagnostic($"[GetExpressionContext] Detected Filter.{methodName}");
                                return ExpressionContext.FilterExpression;
                            }
                        }

                        // Check for Enrich.When
                        if (methodName == "When")
                        {
                            var expression = memberAccess.Expression;
                            string targetName = null;

                            if (expression is IdentifierNameSyntax identifier)
                            {
                                targetName = identifier.Identifier.ValueText;
                            }
                            else if (expression is MemberAccessExpressionSyntax nestedAccess &&
                                     nestedAccess.Name is IdentifierNameSyntax nestedIdentifier)
                            {
                                targetName = nestedIdentifier.Identifier.ValueText;
                            }

                            if (targetName == "Enrich")
                            {
                                LogDiagnostic($"[GetExpressionContext] Detected Enrich.When");
                                return ExpressionContext.FilterExpression; // When uses filter expressions
                            }
                        }

                        // Check for Enrich.WithComputed  
                        if (methodName == "WithComputed")
                        {
                            var expression = memberAccess.Expression;
                            string targetName = null;

                            if (expression is IdentifierNameSyntax identifier)
                            {
                                targetName = identifier.Identifier.ValueText;
                            }
                            else if (expression is MemberAccessExpressionSyntax nestedAccess &&
                                     nestedAccess.Name is IdentifierNameSyntax nestedIdentifier)
                            {
                                targetName = nestedIdentifier.Identifier.ValueText;
                            }

                            if (targetName == "Enrich")
                            {
                                // WithComputed has two string arguments: property name and expression
                                // The second argument (index 1) is the expression
                                if (argumentIndex == 1)
                                {
                                    LogDiagnostic($"[GetExpressionContext] Detected Enrich.WithComputed expression argument");
                                    return ExpressionContext.ComputedProperty;
                                }
                                // First argument is just the property name, not an expression
                            }
                        }

                        // Check for WriteTo.Conditional
                        if (methodName == "Conditional")
                        {
                            var expression = memberAccess.Expression;
                            string targetName = null;

                            if (expression is IdentifierNameSyntax identifier)
                            {
                                targetName = identifier.Identifier.ValueText;
                            }
                            else if (expression is MemberAccessExpressionSyntax nestedAccess &&
                                     nestedAccess.Name is IdentifierNameSyntax nestedIdentifier)
                            {
                                targetName = nestedIdentifier.Identifier.ValueText;
                            }

                            if (targetName == "WriteTo")
                            {
                                // First argument is the conditional expression
                                if (argumentIndex == 0)
                                {
                                    LogDiagnostic($"[GetExpressionContext] Detected WriteTo.Conditional");
                                    return ExpressionContext.ConditionalExpression;
                                }
                            }
                        }
                    }
                }

                parent = parent.Parent;
                depth++;
            }

            LogDiagnostic($"[GetExpressionContext] No expression context found");
            return ExpressionContext.None;
        }
        catch (Exception ex)
        {
            LogDiagnostic($"[GetExpressionContext] Exception: {ex.Message}");
            return ExpressionContext.None;
        }
    }

    /// <summary>
    /// Checks if a syntax node contains another node in its descendant tree.
    /// </summary>
    private static bool ContainsNode(SyntaxNode parent, SyntaxNode target)
    {
        if (parent == target) return true;

        foreach (var child in parent.ChildNodes())
        {
            if (ContainsNode(child, target))
                return true;
        }

        return false;
    }
}