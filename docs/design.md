# serilog-syntax - Design Document

## Overview

serilog-syntax is a Visual Studio extension that provides syntax highlighting and navigation for Serilog message templates in C#/.NET projects. Similar to tree-sitter-mtlog for Go, it focuses solely on visual enhancements and navigation - no diagnostics, no validation, no code fixes.

## Project Goals

### In Scope
- **Syntax highlighting** of properties within Serilog message template strings
- **Navigation** support (Go to Definition, Find All References) between template properties and arguments
- **Brace matching** for template property delimiters

### Out of Scope
- Error detection or diagnostics
- Code fixes or refactorings
- IntelliSense/completion
- Validation of any kind
- Runtime behavior analysis

## Technical Approach

### Core Technology
- **Roslyn Classification API** - For syntax highlighting via `IClassifier`
- **Roslyn Tagging API** - For brace matching and text markers
- **Visual Studio Editor API** - For navigation features

### Architecture

```
┌─────────────────────────────────────────┐
│          VS Editor Layer                │
├─────────────────────────────────────────┤
│   Serilog Template Classifier           │
│   - Identifies template strings         │
│   - Parses template syntax              │
│   - Returns classification spans       │
├─────────────────────────────────────────┤
│   Navigation Provider                   │
│   - Maps properties to arguments        │
│   - Handles Go to Definition            │
│   - Provides Find References            │
├─────────────────────────────────────────┤
│   Template Parser (Lightweight)         │
│   - Extracts properties                 │
│   - No validation                       │
│   - Position tracking only              │
└─────────────────────────────────────────┘
```

## Implementation Details

### 1. Template String Detection

Identify Serilog logging method calls:
```csharp
// Direct logger calls
Log.Information("User {UserId} logged in", userId);
Log.Warning("Failed to process {@Order}", order);

// ILogger interface calls
_logger.LogInformation("User {UserId} logged in", userId);

// Contextual logger calls
Log.ForContext<UserService>().Information("User {UserId} logged in", userId);
```

### 2. Template Syntax Elements

Elements to highlight:
```csharp
// Property names
"User {UserId} logged in"         // UserId gets highlighted

// Destructuring operator
"Order details: {@Order}"         // @ gets operator color, Order gets property color

// Stringification operator  
"Error: {$Exception}"            // $ gets operator color, Exception gets property color

// Format specifiers
"Price: {Price:C2}"              // Price gets property color, :C2 gets format color

// Positional properties
"Processing {0} of {1} items"     // 0 and 1 get numeric color

// Alignment
"Name: {Name,-20}"               // Name gets property color, ,-20 gets format color
```

### 3. Classification Types

Define custom classification types:
```csharp
internal static class SerilogClassificationTypes
{
    public const string PropertyName = "Serilog.PropertyName";
    public const string DestructureOperator = "Serilog.DestructureOperator";
    public const string StringifyOperator = "Serilog.StringifyOperator";
    public const string FormatSpecifier = "Serilog.FormatSpecifier";
    public const string PropertyBrace = "Serilog.PropertyBrace";
    public const string NumericIndex = "Serilog.NumericIndex";
}
```

### 4. Color Definitions

Default color scheme:
- **Property names**: Light blue (#569CD6)
- **Operators** (@, $): Yellow (#DCDCAA)
- **Format specifiers**: Light green (#4EC9B0)
- **Braces**: Match string color but slightly brighter
- **Numeric indices**: Light purple (#C586C0)

### 5. Navigation Features

#### Go to Definition
- Click on `{UserId}` → Navigate to the corresponding argument position
- Works with positional properties: `{0}` → First argument

#### Find All References
- Find all usages of the same property name within the solution
- Limited to Serilog logging calls only

#### Brace Matching
- Highlight matching `{` and `}` when cursor is on either brace
- Similar to existing C# brace matching behavior

## File Structure

```
serilog-syntax/
├── source.extension.vsixmanifest
├── serilog-syntax.csproj
├── Classification/
│   ├── SerilogClassifier.cs
│   ├── SerilogClassificationTypes.cs
│   └── SerilogClassifierProvider.cs
├── Parsing/
│   ├── TemplateParser.cs
│   └── TemplateProperty.cs
├── Navigation/
│   ├── SerilogNavigationProvider.cs
│   └── PropertyArgumentMapper.cs
├── Tagging/
│   ├── SerilogBraceMatchingTagger.cs
│   └── SerilogBraceMatchingTaggerProvider.cs
└── SerilogPackage.cs
```

## Key Classes

### TemplateParser
```csharp
internal class TemplateParser
{
    public IEnumerable<TemplateProperty> Parse(string template)
    {
        // Simple state machine to extract properties
        // No validation, just position tracking
    }
}

internal class TemplateProperty
{
    public string Name { get; set; }
    public int StartIndex { get; set; }
    public int Length { get; set; }
    public bool IsDestructured { get; set; }
    public bool IsStringified { get; set; }
    public string FormatSpecifier { get; set; }
}
```

### SerilogClassifier
```csharp
internal class SerilogClassifier : IClassifier
{
    public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
    {
        // 1. Find Serilog method calls in span
        // 2. Extract template string literals
        // 3. Parse templates for properties
        // 4. Return classification spans for each element
    }
}
```

## Performance Considerations

- **Incremental parsing**: Only reparse changed spans
- **Caching**: Cache parsed templates by text snapshot
- **Async where possible**: Don't block the UI thread
- **Minimal allocations**: Reuse parser instances and collections

## Testing Strategy

- Unit tests for template parser
- Integration tests with sample Serilog code
- Performance tests with large files
- Manual testing in Visual Studio

## Distribution

- **Visual Studio Marketplace** - Primary distribution as VSIX
- **NuGet** (optional) - For programmatic usage
- **Open source** on GitHub

## Future Considerations (Explicitly Not in V1)

- VS Code support via C# DevKit extension API
- Rider support via ReSharper plugin
- Custom color themes
- Property renaming support
- Template snippets

## Summary

serilog-syntax provides a focused, lightweight enhancement for Serilog users in Visual Studio by adding syntax highlighting and basic navigation to message templates. By keeping the scope minimal and avoiding any validation or diagnostics, we ensure fast performance and avoid duplicating functionality better handled by dedicated analyzers.