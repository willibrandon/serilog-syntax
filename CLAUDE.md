# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Visual Studio extension (VSIX) project called SerilogSyntax that targets Visual Studio 2022 (version 17.0+). The extension is built using the Visual Studio SDK and appears to be in early development stage.

## Build and Development Commands

### Building the Project
```bash
# Build the solution
msbuild SerilogSyntax.sln /p:Configuration=Debug

# Build for release
msbuild SerilogSyntax.sln /p:Configuration=Release

# Clean the solution
msbuild SerilogSyntax.sln /t:Clean
```

### Running and Debugging
The project is configured to launch Visual Studio with the experimental instance when debugging:
- Start Program: `devenv.exe`
- Start Arguments: `/rootsuffix Exp`

To test the extension, press F5 in Visual Studio which will launch a new VS instance with the extension loaded.

## Architecture

### Project Structure
- **SerilogSyntax.sln** - Main solution file
- **SerilogSyntax/** - Main project directory
  - **SerilogSyntaxPackage.cs** - Main package class that implements the VS extension entry point
  - **source.extension.vsixmanifest** - Extension manifest defining metadata and installation targets
  - **Properties/AssemblyInfo.cs** - Assembly metadata

### Key Components

1. **SerilogSyntaxPackage** (SerilogSyntaxPackage.cs:28)
   - Derives from `AsyncPackage` for async initialization
   - Package GUID: `66cc1951-17f2-469d-ac86-0278240f240c`
   - Uses managed resources only and allows background loading

### Development Notes

- The extension targets .NET Framework 4.7.2
- Uses Visual Studio SDK v17.0.32112.339
- Configured for Visual Studio Community 2022 (17.0-18.0)
- Currently an empty VSIX project template - no Serilog-specific functionality implemented yet

### Next Steps for Implementation

When implementing Serilog syntax highlighting functionality:
1. Add classification types and format definitions for Serilog message templates
2. Implement a classifier provider to identify Serilog log statements
3. Add TextMate grammar support or use the VS editor classification system
4. Consider adding IntelliSense support for Serilog message template properties