# Serilog Syntax Highlighting for Visual Studio

A Visual Studio 2022 extension that provides syntax highlighting, brace matching, and navigation features for Serilog message templates in C#/.NET projects.

![Visual Studio Extension](https://img.shields.io/badge/Visual%20Studio-2022-blue)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-green)
![License](https://img.shields.io/badge/license-MIT-blue)

## Features

### 🎨 Syntax Highlighting
- **Property names** highlighted in light blue: `{UserId}`, `{UserName}`
- **Destructuring operator** `@` highlighted in yellow: `{@User}`
- **Stringification operator** `$` highlighted in yellow: `{$Settings}`
- **Format specifiers** highlighted in light green: `{Timestamp:yyyy-MM-dd}`
- **Alignment** highlighted in light green: `{Name,10}`, `{Price,-8}`
- **Positional parameters** highlighted in light purple: `{0}`, `{1}`
- **Property braces** highlighted for easy identification

### 🔗 Smart Detection
- Works with any logger variable name (not just `_logger` or `log`)
- Supports both direct Serilog calls: `Log.Information(...)`
- Supports Microsoft.Extensions.Logging integration: `_logger.LogInformation(...)`
- Recognizes configuration templates: `outputTemplate: "[{Timestamp:HH:mm:ss}...]"`

### ⚡ Real-time Highlighting
- Immediate visual feedback as you type
- Highlighting appears as soon as you close braces `}` (doesn't wait for closing quotes)
- Supports incomplete strings during editing

### 🧭 Navigation Features
- **Light bulb suggestions** when hovering over template properties
- **Navigate to argument** - jump from template properties to their corresponding arguments
- Click the light bulb and select "Navigate to 'PropertyName' argument"

### 🔍 Brace Matching
- Highlight matching braces when cursor is positioned on `{` or `}`
- Visual indication of brace pairs in complex templates
- Helps identify mismatched or nested braces

## Installation

1. Download the latest `.vsix` file from the [releases page](../../releases)
2. Double-click the `.vsix` file to install in Visual Studio 2022
3. Restart Visual Studio
4. Open any C# file with Serilog logging to see the syntax highlighting

## Supported Serilog Syntax

The extension recognizes and highlights all Serilog message template features:

```csharp
// Basic properties
logger.LogInformation("User {UserId} logged in at {LoginTime}", userId, loginTime);

// Destructuring (captures object structure)
logger.LogInformation("Processing user {@User}", user);

// Stringification (forces string representation)  
logger.LogInformation("Configuration loaded {$Settings}", settings);

// Format specifiers
logger.LogInformation("Current time: {Timestamp:yyyy-MM-dd HH:mm:ss}", DateTime.Now);

// Alignment
logger.LogInformation("Item: {Name,10} | Price: {Price,8:C}", name, price);

// Positional parameters (legacy support)
logger.LogWarning("Error {0} occurred in {1}", errorCode, methodName);

// Configuration templates
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}")
    .CreateLogger();
```

## Example Project

The solution includes a complete example project (`Example/`) that demonstrates all syntax highlighting features:

```bash
cd Example
dotnet run
```

Open `Example/Program.cs` in Visual Studio to see the extension in action with comprehensive examples of every supported syntax feature.

## Supported Logger Names

The extension automatically detects Serilog calls regardless of how you name your logger variables:

```csharp
// All of these work automatically
_logger.LogInformation("Message with {Property}", value);
logger.LogDebug("Debug message with {Data}", data);  
myCustomLogger.LogWarning("Warning with {Details}", details);
log.LogError("Error with {Context}", context);
```

## Development

### Prerequisites
- Visual Studio 2022 (17.0 or later)
- Visual Studio SDK
- .NET Framework 4.7.2

### Building
```bash
# Build the extension
.\build.ps1

# Run tests  
.\test.ps1

# The built .vsix file will be in SerilogSyntax\bin\Debug\
```

### Architecture

The extension uses Visual Studio's extensibility APIs:

- **Roslyn Classification API** - For syntax highlighting via `IClassifier`
- **Roslyn Tagging API** - For brace matching via `ITagger<TextMarkerTag>`  
- **Suggested Actions API** - For navigation features via `ISuggestedActionsSourceProvider`
- **MEF (Managed Extensibility Framework)** - For Visual Studio integration

Key components:
- `SerilogClassifier` - Handles syntax highlighting
- `SerilogBraceMatcher` - Provides brace matching
- `SerilogNavigationProvider` - Enables property-to-argument navigation
- `TemplateParser` - Parses Serilog message templates

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new features
5. Run the test suite: `.\test.ps1`
6. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
