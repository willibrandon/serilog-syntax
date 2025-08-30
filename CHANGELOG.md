# Changelog

All notable changes to the Serilog Syntax Highlighting extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2025-08-30

### Added
- Full support for C# 11 raw string literals (`"""..."""`)
  - Single-line and multi-line raw string templates
  - Handles custom delimiter counts (4+ quotes)
  - Proper indentation handling for closing quotes
  - Properties highlighted across all lines in multi-line raw strings
- Smart cache invalidation system
  - Detects when changes affect raw string boundaries
  - Minimal invalidation for normal edits (±2 lines)
  - Wider invalidation only when raw string delimiters are modified
  - Measured 268x-510x performance improvement with caching
- Mock test infrastructure for VS components
  - MockTextBuffer, MockTextSnapshot for testing without VS
  - MockClassificationTypeRegistry for type registration
  - Enables unit testing of VS integration code
- Performance tests with timing validation
  - Tests verify cache effectiveness with actual measurements
  - Smart invalidation test confirms minimal overhead

### Changed
- Improved raw string detection algorithm
  - Now checks current line in addition to previous lines
  - Handles edge cases where raw string starts on the queried line
- Enhanced diagnostic logging (debug builds only)
  - Synchronous implementation to prevent VS hangs
  - Automatic cleanup keeps only 5 most recent log files

## [0.2.0] - 2025-08-30

### Added
- Full support for multi-line verbatim strings (`@"..."`)
  - Properties highlighted across all lines in multi-line templates
  - Correct handling of escaped quotes (`""`) in verbatim strings
  - Detection of unclosed verbatim strings when processing line-by-line
- Error recovery in template parser
  - Parser continues processing after encountering malformed templates
  - Handles missing closing braces gracefully
- Performance benchmarking infrastructure
  - BenchmarkDotNet project for measuring performance
  - Benchmarks for parser, cache, and call detection
- LRU cache for parsed templates
  - 10x performance improvement for repeated templates (490ns → 35ns)
  - Thread-safe implementation with configurable capacity
- Factory methods for TemplateProperty creation
  - Simplified API with CreateStandard(), CreateDestructured(), etc.
  - Reduces constructor complexity

### Changed
- Optimized Serilog call detection with pre-check
  - 8x faster rejection of non-Serilog code (250ns → 30ns)
  - Quick string check before expensive regex matching
- Improved cache invalidation strategy
  - Incremental invalidation only for changed spans
  - Reduced memory churn during editing
- Enhanced template parser performance
  - Better handling of complex templates with multiple properties
  - Optimized state machine for property extraction

### Fixed
- Verbatim string property highlighting now works correctly
  - Fixed index offset calculation for `@"` prefix (requires +2 offset)
  - Properties in multi-line verbatim strings now properly classified
- Template parser now handles edge cases
  - Correctly parses templates with consecutive braces `{{}}`
  - Handles templates with only opening or closing braces

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

[0.3.0]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.3.0
[0.2.0]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.2.0
[0.1.1]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.1.1
[0.1.0]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.1.0