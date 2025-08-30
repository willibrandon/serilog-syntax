# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Visual Studio extension (VSIX) project called SerilogSyntax that targets Visual Studio 2022 (version 17.0+). The extension is built using the Visual Studio SDK and appears to be in early development stage.

## Build and Development Commands

### Building the Project
```powershell
# Build using the build script (recommended)
.\build.ps1

# Build for release
.\build.ps1 -Configuration Release

# Build with different verbosity levels
.\build.ps1 -Verbosity detailed
.\build.ps1 -Verbosity diagnostic

# Clean and rebuild
msbuild SerilogSyntax.sln /t:Clean
.\build.ps1
```

### Running Tests
```powershell
# Run all tests
.\test.ps1

# Run tests without rebuilding
.\test.ps1 -NoBuild

# Run specific tests (filter by name)
.\test.ps1 -Filter "TestMethodName"

# Run release tests
.\test.ps1 -Configuration Release
```

### Running Benchmarks
```powershell
# Run all benchmarks
.\benchmark.ps1

# Run specific benchmark category
.\benchmark.ps1 -Filter "Parser*"
.\benchmark.ps1 -Filter "Cache*"

# Run benchmarks in Release mode (default)
.\benchmark.ps1 -Configuration Release
```

### Running and Debugging
The project is configured to launch Visual Studio with the experimental instance when debugging:
- Start Program: `devenv.exe`
- Start Arguments: `/rootsuffix Exp`

To test the extension, press F5 in Visual Studio which will launch a new VS instance with the extension loaded.

## Architecture

### Project Structure
- **SerilogSyntax.sln** - Main solution file
- **SerilogSyntax/** - Main VSIX extension project
  - **SerilogSyntaxPackage.cs** - Main package class that implements the VS extension entry point
  - **source.extension.vsixmanifest** - Extension manifest defining metadata and installation targets
  - **Properties/AssemblyInfo.cs** - Assembly metadata
- **SerilogSyntax.Tests/** - xUnit test project (.NET Framework 4.7.2)
- **SerilogSyntax.Benchmarks/** - BenchmarkDotNet performance tests (.NET Framework 4.7.2)
- **Example/** - Standalone console app demonstrating all syntax features (.NET 8.0)
- **build.ps1** - Build script for the solution
- **test.ps1** - Test runner script
- **benchmark.ps1** - Benchmark runner script

### Key Components

1. **SerilogSyntaxPackage** (SerilogSyntaxPackage.cs:28)
   - Derives from `AsyncPackage` for async initialization
   - Package GUID: `66cc1951-17f2-469d-ac86-0278240f240c`
   - Uses managed resources only and allows background loading

### Development Notes

- The extension targets .NET Framework 4.7.2
- Uses Visual Studio SDK v17.0.32112.339
- Configured for Visual Studio Community 2022 (17.0-18.0)
- Fully functional with syntax highlighting, navigation, and brace matching
- Supports C# 11 raw string literals ("""...""")

## Implementation Overview

This extension provides syntax highlighting and navigation for Serilog message templates in C#/.NET projects. The implementation focuses solely on visual enhancements - no diagnostics, validation, or code fixes.

### Features
- **Syntax highlighting** of properties within Serilog message template strings
- **Navigation** support (Go to Definition) between template properties and arguments
- **Brace matching** for template property delimiters

### Technical Stack
- **Roslyn Classification API** - For syntax highlighting via `IClassifier`
- **Roslyn Tagging API** - For brace matching via `ITagger<TextMarkerTag>`
- **Visual Studio Editor API** - For navigation features
- **MEF (Managed Extensibility Framework)** - For VS integration

## Implementation Details

### Template Syntax Support
The extension highlights the following Serilog template elements:
- `{PropertyName}` - Standard property (teal)
- `{@PropertyName}` - Destructured property (@ in orange, property in teal)
- `{$PropertyName}` - Stringified property ($ in orange, property in teal)
- `{PropertyName:format}` - Format specifier (property in teal, :format in green)
- `{PropertyName,alignment}` - Alignment (property in teal, alignment in red)
- `{0}`, `{1}` - Positional properties (purple)
- Braces `{` `}` - Gray for structure

Colors meet WCAG AA accessibility standards and are customizable via Tools > Options > Environment > Fonts and Colors.

### Supported Serilog Calls
- Direct logger: `Log.Information("User {UserId} logged in", userId)`
- ILogger interface: `_logger.LogInformation("User {UserId} logged in", userId)`
- Contextual logger: `Log.ForContext<T>().Information("User {UserId} logged in", userId)`
- BeginScope: `logger.BeginScope("Operation={Id}", operationId)`
- LogError with exception: `logger.LogError(ex, "Error: {Message}", msg)`

### Key Implementation Files
The extension includes these components:
1. **Parsing/TemplateParser.cs** - State machine to extract properties from message templates
2. **Classification/SerilogClassifier.cs** - Implements `IClassifier` for syntax highlighting
3. **Classification/SerilogClassifierProvider.cs** - MEF export for classifier
4. **Navigation/SerilogNavigationProvider.cs** - Navigation from properties to arguments via light bulb
5. **Tagging/SerilogBraceMatcher.cs** - Implements `ITagger<TextMarkerTag>` for brace matching
6. **Utilities/SerilogCallDetector.cs** - Centralized Serilog call detection logic
7. **Utilities/LruCache.cs** - Thread-safe LRU cache for parsed templates

### Performance Considerations
- LRU cache for parsed templates (10x improvement for repeated templates)
- Pre-check optimization before regex matching (8x faster for non-Serilog code)
- Smart cache invalidation that only clears affected lines (268x-510x speedup)
- Detection of multi-line strings (verbatim and raw) without full document parsing
- Classes over structs for better .NET Framework performance