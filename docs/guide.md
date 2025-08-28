# serilog-syntax Implementation Guide for Claude Code

## Overview

This guide provides step-by-step instructions for Claude Code to implement serilog-syntax, a Visual Studio extension for syntax highlighting and navigation of Serilog message templates. The implementation should be minimal, focused, and avoid over-engineering.

## Prerequisites

Before starting, ensure you have:
- Visual Studio 2022 with the Visual Studio SDK workload
- .NET Framework 4.7.2 or higher
- Basic understanding of VSIX extension development

## Project Setup

### 1. Create the VSIX Project

```
1. Create new project: "VSIX Project" template
2. Name: serilog-syntax
3. Framework: .NET Framework 4.7.2
4. Delete the auto-generated Command1.cs and related files
```

### 2. Update source.extension.vsixmanifest

```xml
<?xml version="1.0" encoding="utf-8"?>
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011">
  <Metadata>
    <Identity Id="serilog-syntax" Version="1.0" Language="en-US" Publisher="Your Name" />
    <DisplayName>serilog-syntax</DisplayName>
    <Description>Syntax highlighting and navigation for Serilog message templates</Description>
    <Tags>serilog, logging, syntax, highlighting</Tags>
  </Metadata>
  <Installation>
    <InstallationTarget Id="Microsoft.VisualStudio.Community" Version="[17.0, 18.0)" />
  </Installation>
  <Dependencies>
    <Dependency Id="Microsoft.Framework.NDP" DisplayName="Microsoft .NET Framework" d:Source="Manual" Version="[4.5,)" />
  </Dependencies>
  <Assets>
    <Asset Type="Microsoft.VisualStudio.MefComponent" d:Source="Project" d:ProjectName="%CurrentProject%" Path="|%CurrentProject%|" />
  </Assets>
</PackageManifest>
```

### 3. Install NuGet Packages

```
Microsoft.VisualStudio.SDK
Microsoft.VisualStudio.Language.StandardClassification
Microsoft.VisualStudio.Text.UI.Wpf
```

## Core Implementation Steps

### Step 1: Create the Template Parser

Create `Parsing/TemplateProperty.cs`:
```csharp
namespace SerilogSyntax.Parsing
{
    internal class TemplateProperty
    {
        public string Name { get; set; }
        public int StartIndex { get; set; }
        public int Length { get; set; }
        public PropertyType Type { get; set; }
        public string FormatSpecifier { get; set; }
        public int FormatStartIndex { get; set; }
    }

    internal enum PropertyType
    {
        Standard,        // {Property}
        Destructured,    // {@Property}
        Stringified,     // {$Property}
        Positional       // {0}, {1}, etc.
    }
}
```

Create `Parsing/TemplateParser.cs`:
```csharp
// Implementation notes:
// 1. Use a simple state machine approach
// 2. States: Outside, OpenBrace, Property, Format, CloseBrace
// 3. Track positions for each element
// 4. Handle nested braces in format specifiers
// 5. NO validation - just extract positions
```

Key parser requirements:
- Parse `{PropertyName}` - standard property
- Parse `{@PropertyName}` - destructured property  
- Parse `{$PropertyName}` - stringified property
- Parse `{PropertyName:format}` - with format specifier
- Parse `{PropertyName,alignment}` - with alignment
- Parse `{PropertyName,alignment:format}` - with both
- Parse `{0}`, `{1}` - positional properties
- Handle escaped braces `{{` and `}}`
- Track exact character positions for highlighting

### Step 2: Create Classification Types

Create `Classification/SerilogClassificationTypes.cs`:
```csharp
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace SerilogSyntax.Classification
{
    internal static class SerilogClassificationTypes
    {
        public const string PropertyName = "serilog.property.name";
        public const string DestructureOperator = "serilog.operator.destructure";
        public const string StringifyOperator = "serilog.operator.stringify";
        public const string FormatSpecifier = "serilog.format";
        public const string PropertyBrace = "serilog.brace";
        public const string PositionalIndex = "serilog.index";

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(PropertyName)]
        internal static ClassificationTypeDefinition PropertyNameType = null;

        // Repeat for each classification type...
    }
}
```

Create `Classification/SerilogClassificationFormats.cs`:
```csharp
// Define the visual formatting for each classification type
// Use EditorFormatDefinition with appropriate colors
// PropertyName: #569CD6 (light blue)
// Operators: #DCDCAA (yellow)
// FormatSpecifier: #4EC9B0 (light green)
// Braces: Slightly brighter than string color
// PositionalIndex: #C586C0 (light purple)
```

### Step 3: Implement the Classifier

Create `Classification/SerilogClassifier.cs`:

Key implementation points:
1. **Detect Serilog calls**: Look for patterns like:
   - `Log.Information("...", ...)` 
   - `_logger.LogInformation("...", ...)`
   - `Log.ForContext<T>().Information("...", ...)`
   - Use Roslyn syntax tree to find invocation expressions

2. **Extract template strings**: 
   - Must be string literals (not interpolated strings)
   - Must be the first argument
   - Cache results by text snapshot version

3. **Parse and classify**:
   - Use TemplateParser to extract properties
   - Create ClassificationSpan for each element
   - Handle overlapping spans correctly

Create `Classification/SerilogClassifierProvider.cs`:
```csharp
[Export(typeof(IClassifierProvider))]
[ContentType("CSharp")]
internal class SerilogClassifierProvider : IClassifierProvider
{
    // Import classification registry and aggregator service
    // Create classifier instances per text buffer
    // Cache classifiers to avoid recreation
}
```

### Step 4: Implement Navigation (Go to Definition)

Create `Navigation/SerilogGoToDefinitionProvider.cs`:

Implementation approach:
1. Hook into the existing Go to Definition command via VS command infrastructure
2. Check if cursor is on a property name in a template
3. Find the corresponding argument in the method call
4. For positional properties {0}, {1}, map to argument index
5. For named properties, map by position (1st property → 1st arg after template)
6. Navigate to the argument's span using `ITextView.Caret.MoveTo`

Key challenges:
- Handle method calls that span multiple lines
- Account for named arguments
- Handle params array arguments
- Deal with missing arguments gracefully

### Step 5: Implement Brace Matching

Create `Tagging/SerilogBraceMatchingTagger.cs`:

Requirements:
- Highlight matching `{` and `}` pairs
- Only within Serilog template strings
- Handle nested braces in format specifiers
- Use `ITextMarkerTag` for highlighting
- Respond to caret position changes

### Step 6: Performance Optimization

Critical optimizations:
1. **Caching**:
   - Cache parsed templates by string content
   - Cache classification results by text version
   - Clear cache on document changes

2. **Async processing**:
   - Use background thread for parsing
   - Don't block UI thread
   - Implement cancellation tokens

3. **Incremental updates**:
   - Only reparse changed regions
   - Track dirty spans
   - Batch multiple changes

## Testing Strategy

### Unit Tests
Create test project with:
- Template parser tests (all syntax variations)
- Classification tests (verify correct spans)
- Navigation tests (property to argument mapping)

### Integration Tests
- Test with real Serilog code
- Test performance with large files
- Test with various C# language versions

### Manual Testing Checklist
- [ ] Properties highlight correctly
- [ ] Operators (@, $) have distinct colors  
- [ ] Format specifiers highlight
- [ ] Go to Definition works
- [ ] Brace matching works
- [ ] No performance degradation
- [ ] Works with different themes

## Common Pitfalls to Avoid

1. **Don't parse interpolated strings** - Serilog doesn't use them
2. **Don't validate templates** - Not our job
3. **Don't block the UI thread** - Use async where possible
4. **Don't over-cache** - Memory leaks are real
5. **Don't assume argument order** - Handle named arguments
6. **Don't parse complex expressions** - Only string literals

## Debugging Tips

1. Use `IVsOutputWindow` for debug logging
2. Attach debugger to experimental VS instance
3. Use ETW tracing for performance analysis
4. Enable MEF composition logging for export issues

## File Organization

```
serilog-syntax/
├── Properties/
│   └── AssemblyInfo.cs
├── Parsing/
│   ├── TemplateParser.cs
│   └── TemplateProperty.cs
├── Classification/
│   ├── SerilogClassifier.cs
│   ├── SerilogClassifierProvider.cs
│   ├── SerilogClassificationTypes.cs
│   └── SerilogClassificationFormats.cs
├── Navigation/
│   └── SerilogGoToDefinitionProvider.cs
├── Tagging/
│   ├── SerilogBraceMatchingTagger.cs
│   └── SerilogBraceMatchingTaggerProvider.cs
├── Utilities/
│   └── SerilogCallDetector.cs
├── source.extension.vsixmanifest
└── serilog-syntax.csproj
```

## Key APIs to Use

- `IClassifier` - For syntax highlighting
- `IClassificationTypeRegistryService` - For registering types
- `ITextBuffer` - Text storage
- `ITextSnapshot` - Immutable text version
- `SnapshotSpan` - Text region
- `ITagAggregator` - For combining tags
- VS Command infrastructure - For navigation hooks
- `Microsoft.CodeAnalysis` - For parsing C# syntax

## Success Criteria

The implementation is complete when:
1. Properties in Serilog templates are colored
2. Destructure (@) and stringify ($) operators are highlighted
3. Format specifiers have distinct colors
4. Clicking on a property navigates to its argument
5. Braces match when cursor is on them
6. No noticeable performance impact
7. Works with VS dark, light, and blue themes

## Remember

- Keep it simple - this is just syntax highlighting
- No validation, no diagnostics, no code fixes
- Performance matters - this runs on every keystroke
- When in doubt, do less rather than more