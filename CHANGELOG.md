# Changelog

All notable changes to the Serilog Syntax Highlighting extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.1] - 2025-08-29

### Added
- ESC key dismissal for brace matching highlights
  - Press ESC to temporarily dismiss brace highlights
  - Highlights automatically restore when cursor moves away and returns

### Changed
- Improved brace highlight appearance
  - Changed from filled blue background to subtle gray border
  - Better visibility across different themes
  - Made colors fully customizable in VS settings

### Fixed
- Added proper disposal patterns for view event handlers
- Improved state management for brace matching

## [0.1.0] - 2025-08-28

### Added
- Initial release of Serilog Syntax Highlighting for Visual Studio 2022
- Syntax highlighting for Serilog message template properties
  - Standard properties: `{PropertyName}`
  - Destructured properties: `{@PropertyName}`
  - Stringified properties: `{$PropertyName}`
  - Positional parameters: `{0}`, `{1}`, `{2}`
  - Format specifiers: `{Property:format}`
  - Alignment: `{Property,alignment}`
- Brace matching for template delimiters
- Navigation support (Go to Definition) from template properties to arguments
- Support for various Serilog/ILogger methods:
  - Direct Serilog calls: `Log.Information()`, `Log.Debug()`, etc.
  - ILogger interface: `logger.LogInformation()`, `logger.LogError()`, etc.
  - ForContext chains: `Log.ForContext<T>().Information()`
  - BeginScope: `logger.BeginScope("Operation={Id}", id)`
  - LogError with exception: `logger.LogError(ex, "Message", args)`
- Configuration template support: `outputTemplate: "[{Timestamp:HH:mm:ss}]"`
- WCAG AA compliant colors for both light and dark themes
- Customizable colors via Tools > Options > Environment > Fonts and Colors
- Real-time highlighting as you type

[0.1.0]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.1.0