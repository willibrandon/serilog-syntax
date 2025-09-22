# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Visual Studio extension (VSIX) project called SerilogSyntax that targets Visual Studio 2022 (version 17.0+). The extension is built using the Visual Studio SDK and appears to be in early development stage.

## Build and Development Commands

All build scripts are located in the `scripts/` folder and are available in both PowerShell and Bash versions.

### Using PowerShell (Windows)
```powershell
# Build using the build script (recommended)
.\scripts\build.ps1

# Build for release
.\scripts\build.ps1 -Configuration Release

# Build with different verbosity levels
.\scripts\build.ps1 -Verbosity detailed
.\scripts\build.ps1 -Verbosity diagnostic

# Run all tests
.\scripts\test.ps1

# Run tests without rebuilding
.\scripts\test.ps1 -NoBuild

# Run specific tests (filter by name)
.\scripts\test.ps1 -Filter "TestMethodName"

# Run tests multiple times to detect flaky tests
.\scripts\test.ps1 -Iterations 10

# Run benchmarks
.\scripts\benchmark.ps1 -Filter "Parser*"

# Deploy to installed VS instances
.\scripts\deploy.ps1 -RestartVS

# Build and deploy in one step
.\scripts\quick-deploy.ps1
```

### Using Bash (WSL, Linux, macOS)
```bash
# Build using the build script
./scripts/build.sh

# Build for release
./scripts/build.sh --configuration Release

# Build with different verbosity levels
./scripts/build.sh --verbosity detailed
./scripts/build.sh --verbosity diagnostic

# Run all tests
./scripts/test.sh

# Run tests without rebuilding
./scripts/test.sh --no-build

# Run specific tests (filter by name)
./scripts/test.sh --filter "TestMethodName"

# Run tests multiple times to detect flaky tests
./scripts/test.sh --iterations 10

# Run benchmarks
./scripts/benchmark.sh --filter "Parser*"

# Deploy to installed VS instances (Windows only)
./scripts/deploy.sh --restart-vs

# Build and deploy in one step (Windows only)
./scripts/quick-deploy.sh
```

### Script Options

#### Build Scripts (build.ps1 / build.sh)
- `-Configuration` / `--configuration`: Debug (default) or Release
- `-Verbosity` / `--verbosity`: quiet, minimal, normal (default), detailed, diagnostic

#### Test Scripts (test.ps1 / test.sh)
- `-Configuration` / `--configuration`: Debug (default) or Release
- `-Filter` / `--filter`: Filter tests by name
- `-NoBuild` / `--no-build`: Skip building before tests
- `-Iterations` / `--iterations`: Run tests N times to detect flaky tests (default: 1)

#### Benchmark Scripts (benchmark.ps1 / benchmark.sh)
- `-Configuration` / `--configuration`: Debug or Release (default)
- `-Filter` / `--filter`: Filter benchmarks by name

#### Deploy Scripts (deploy.ps1 / deploy.sh) - Windows only
- `-Configuration` / `--configuration`: Debug (default) or Release
- `-RestartVS` / `--restart-vs`: Automatically restart Visual Studio
- `-NoBuild` / `--no-build`: Skip building before deployment

### Running and Debugging
The project is configured to launch Visual Studio with the experimental instance when debugging:
- Start Program: `devenv.exe`
- Start Arguments: `/rootsuffix Exp`

To test the extension, press F5 in Visual Studio which will launch a new VS instance with the extension loaded.

### Fast Deployment (Without Reinstall)

The deploy script updates your extension in-place without uninstalling, which is much faster than the install/uninstall cycle. It:
- Finds all VS instances with the extension installed
- Extracts the new VSIX contents
- Copies updated DLLs directly to the installation folder
- Clears the MEF cache to ensure changes are picked up
- Optionally restarts VS for you

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
- **scripts/** - Build and development scripts
  - **build.ps1 / build.sh** - Build script for the solution
  - **test.ps1 / test.sh** - Test runner script
  - **benchmark.ps1 / benchmark.sh** - Benchmark runner script
  - **deploy.ps1 / deploy.sh** - Fast deployment script
  - **quick-deploy.ps1 / quick-deploy.sh** - Build and deploy in one step

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

This extension provides syntax highlighting and navigation for Serilog message templates and Serilog.Expressions in C#/.NET projects. The implementation focuses solely on visual enhancements - no diagnostics, validation, or code fixes.

### Features
- **Syntax highlighting** of properties within Serilog message template strings
- **Syntax highlighting** for Serilog.Expressions filter syntax and expression templates
- **Navigation** support (Go to Definition) between template properties and arguments
- **Brace matching** for template property delimiters and expression templates
- **Property-argument highlighting** shows connections between properties and arguments on cursor position

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

#### Core Components
1. **Parsing/TemplateParser.cs** - State machine to extract properties from message templates
2. **Classification/SerilogClassifier.cs** - Implements `IClassifier` for syntax highlighting
3. **Classification/SerilogClassifierProvider.cs** - MEF export for classifier
4. **Navigation/SerilogNavigationProvider.cs** - Navigation from properties to arguments via light bulb
5. **Tagging/SerilogBraceMatcher.cs** - Implements `ITagger<TextMarkerTag>` for brace matching
6. **Tagging/PropertyArgumentHighlighter.cs** - Implements `ITagger<TextMarkerTag>` for property-argument highlighting
7. **Utilities/SerilogCallDetector.cs** - Centralized Serilog call detection logic
8. **Utilities/LruCache.cs** - Thread-safe LRU cache for parsed templates

#### Serilog.Expressions Support
9. **Expressions/ExpressionTokenizer.cs** - Tokenizes Serilog.Expressions syntax
10. **Expressions/ExpressionParser.cs** - Parses expressions and templates into classified regions
11. **Expressions/ExpressionDetector.cs** - Detects expression contexts (filter, template, etc.)
12. **Classification/SyntaxTreeAnalyzer.cs** - Roslyn-based analysis for expression contexts

### Performance Considerations
- LRU cache for parsed templates (10x improvement for repeated templates)
- Pre-check optimization before regex matching (8x faster for non-Serilog code)
- Smart cache invalidation that only clears affected lines (268x-510x speedup)
- Detection of multi-line strings (verbatim and raw) without full document parsing
- Classes over structs for better .NET Framework performance