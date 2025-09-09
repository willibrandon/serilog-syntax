# Changelog

All notable changes to the Serilog Syntax Highlighting extension will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.6.2] - 2025-09-09

### Fixed
- Multi-line template navigation for early properties in verbatim strings
  - Navigation from early template properties (like `{AppName}`, `{Version}`, `{Environment}`) now works correctly in multi-line verbatim strings
  - Fixed ReconstructMultiLineTemplate search range to properly find string termination beyond initial 5-line limit
  - Extended search range from `currentLine + 5` to `Math.Max(currentLine + 5, serilogCallLine + 20)` for better coverage
  - Resolves issue where navigation worked for later properties but failed for early ones in long multi-line templates

## [0.6.1] - 2025-09-09

### Fixed
- Multi-line Serilog template navigation position calculation (#11, #14)
  - Navigation from template properties to arguments now works correctly when Serilog calls span multiple lines
  - Fixed template detection for multi-line verbatim strings (`@"..."`) and raw string literals (`"""..."""`)
  - Corrected argument position calculation by starting after comma delimiter instead of at comma
  - Added fallback to ReconstructMultiLineTemplate() when cursor is on first line of multi-line template
  - Enhanced FindArgumentInMultiLineCall() to properly handle template end position calculation
- LogError calls with exception parameters (#12, #13)
  - Syntax highlighting now works correctly for `logger.LogError(exception, "template {Property}", arg)` patterns
  - Fixed string literal parser to skip exception constructor parameter and find actual message template
  - Added IsLogErrorWithExceptionParameter() method to detect LogError patterns with exception parameters
  - Enhanced FindStringLiteral() with skipFirstString parameter to locate correct message template
  - Handles both string literal and variable exception constructors

## [0.6.0] - 2025-09-06

### Added
- Automatic theme-aware WCAG-compliant colors (#9)
  - Colors now automatically switch between Light and Dark Visual Studio themes
  - All colors maintain ≥4.5:1 contrast ratio for WCAG AA compliance
  - SerilogThemeColors service with VSColorTheme.ThemeChanged event handling
  - Theme detection using background color heuristic for custom theme compatibility
  - Semantic color grouping: properties (blue family), operators (warm colors), functions (purple family)
  - All 15 classification format definitions converted to use SerilogClassificationFormatBase
  - Automatic color updates when themes change without VS restart

### Fixed
- ExpressionTemplate multi-line highlighting regression
  - Removed overlapping classifications that caused highlighting artifacts
  - Simplified regex pattern detection for better reliability
  - Added deduplication logic to prevent classification conflicts

## [0.5.2] - 2025-09-06

### Fixed
- Multi-line ForContext patterns now correctly highlighted (#3)
  - Properties in templates like `{@Items}` are now highlighted when ForContext is on one line and logging method on the next
  - Added SyntaxTreeAnalyzer fallback for complex multi-line patterns
  - Enhanced regex detection for ForContext chains split across lines
- Multi-line outputTemplate patterns now correctly highlighted (#5)
  - Properties like `{Timestamp}` are now highlighted when `outputTemplate:` parameter is on one line and template string on the next
  - Enhanced concatenated template fragment detection
  - Added dedicated regex for multi-line outputTemplate detection
- ForContext logger variables now correctly detected (#7)
  - Template properties like `{ListenUri}` are now highlighted when using logger variables from ForContext calls (e.g., `var program = log.ForContext<Program>()`)
  - Enhanced SerilogCallDetector with dotted method patterns (`.Information`, `.Debug`, etc.)
  - Added duplicate processing detection to prevent overlapping matches between regex patterns
  - Separated quick check patterns into logical arrays for better maintainability

### Added
- Performance benchmarking infrastructure for overlap detection algorithms
  - Benchmarks comparing O(n²) vs binary search vs SortedSet approaches
  - Empirical validation showing original O(n²) algorithm performs best for typical usage patterns

## [0.5.1] - 2025-09-04

### Fixed
- Template properties in concatenated strings are now correctly highlighted (#1)
  - Properties in all string fragments of a concatenation chain are now detected
  - Handles both line-by-line processing (how VS sends text) and full-span processing
  - Supports various concatenation patterns: `" +`, `",`, and mixed regular/verbatim strings
  - Example: In `"User {Id}" + "Name {Name}"`, both `{Id}` and `{Name}` are now highlighted
- Improved escape sequence handling in string literals
  - Correctly counts consecutive backslashes to determine if a character is escaped
  - Handles complex patterns like `\\\"` (escaped backslash followed by quote)
- Enhanced verbatim string (`@"..."`) detection in concatenated contexts

## [0.5.0] - 2025-09-03

### Added
- Full support for Serilog.Expressions syntax highlighting
  - Filter expressions in `Filter.ByExcluding()` and `Filter.ByIncludingOnly()`
  - Expression templates with `ExpressionTemplate` class
  - Conditional expressions in `WriteTo.Conditional()`
  - Computed properties in `Enrich.WithComputed()`
- Expression language syntax elements
  - Operators: `and`, `or`, `not`, `like`, `in`, `is null`, `=`, `<>`, `>`, `>=`, `<`, `<=`
  - Functions: `StartsWith()`, `EndsWith()`, `Contains()`, `Length()`, `Has()`, etc.
  - String literals with escape sequences
  - Numeric and boolean literals
  - Property paths with dot notation: `User.Name`, `Order.Customer.Address.City`
  - Case-insensitive operators: `ci` suffix for string comparisons
- Expression template control flow directives
  - `{#if}`, `{#else}`, `{#elseif}`, `{#end}` for conditional rendering
  - `{#each}` for iteration
  - Built-in properties: `@t`, `@m`, `@l`, `@x`, `@i`, `@p`, `@tr`, `@sp`
  - Nested directive support with proper scope tracking
- Brace matching for expression templates
  - Consistent with Visual Studio standard behavior
  - Works across multi-line expression templates
  - Handles nested directives correctly

## [0.4.5] - 2025-09-01

### Fixed
- Unclosed properties with format or alignment specifiers no longer cause highlight spillover
  - Properties like `{@PerformanceData,` without closing brace no longer highlight subsequent text
  - Format specifiers like `{Timestamp:HH:mm:ss` properly stop at pipes and opening braces
  - Alignment specifiers like `{Name,10` properly stop at pipes and opening braces
  - Multiple unclosed properties in a template are handled independently
  - Prevents visual confusion when typing incomplete templates

## [0.4.4] - 2025-09-01

### Fixed
- False positive detection in strings containing Serilog-like text
  - Properties in test mock data (e.g., `MockTextBuffer.Create(@"logger.LogInformation...")`) no longer highlighted
  - Documentation strings with logger examples no longer treated as Serilog calls
  - Regular string assignments like `var msg = "User {Name} logged in"` no longer classified

## [0.4.3] - 2025-08-30

### Fixed
- Parser now correctly rejects properties with leading or trailing spaces
  - `{ PropName}` is now treated as literal text (leading space)
  - `{PropName }` is now treated as literal text (trailing space)
  - `{ @User}` and `{ $Value}` are also rejected (leading space with operators)
  - Only `{PropName}` without any spaces is valid
  - Fixes issue where extension was incorrectly highlighting invalid Serilog syntax

## [0.4.2] - 2025-08-30

### Fixed
- Parser now correctly rejects properties with internal spaces to match Serilog behavior
  - `{ and }` is now treated as literal text, not a property named "and"
  - `{PropertyName}` remains valid
  - `{ PropertyName }` is now correctly treated as literal text
  - Fixes compatibility issue where extension was highlighting invalid Serilog syntax

## [0.4.1] - 2025-08-30

### Fixed
- Removed incorrect claim about supporting properties split across lines
- Documentation now correctly states that Serilog properties must be complete on a single line

## [0.4.0] - 2025-08-30

### Added
- Multi-line brace matching support for Serilog templates
  - Brace highlights now work across line boundaries in multi-line strings
  - Full support for verbatim strings (`@"..."`) spanning multiple lines
  - Full support for C# 11 raw string literals (`"""..."""`) spanning multiple lines
  - ESC key dismissal works with multi-line matches
- Configurable performance limits for multi-line detection
  - `MaxLookbackLines` (20) - lines to search backward for string starts
  - `MaxLookforwardLines` (50) - lines to search forward for string ends
  - `MaxPropertyLength` (200) - max character distance for brace matching
- Test infrastructure for brace matching
  - New `MultiLineBraceMatcherTests` with 6 multi-line scenarios
  - Updated `SerilogBraceMatcherTests` to use real implementation
  - Mock implementations for `ITextView`, `ITextCaret`, and related VS interfaces

### Changed
- Enhanced `SerilogBraceMatcher` with multi-line awareness
  - Added `IsInsideMultiLineString()` method for context detection
  - Added `FindMultiLineBraceMatch()` for cross-line brace matching
  - Improved caret position tracking with initialization in constructor
- Refactored test helpers with complete interface implementations
  - `MockTextView` with full event support
  - `MockTextCaret` with position change notifications
  - `MockTrackingPoint` for snapshot tracking

### Fixed
- Brace matching now correctly handles:
  - Properties in multi-line verbatim strings
  - Properties in multi-line raw string literals
  - Escaped braces (`{{` and `}}`) in multi-line contexts
  - Cursor positioning after closing braces

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

[0.6.1]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.6.1
[0.6.0]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.6.0
[0.5.2]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.5.2
[0.5.1]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.5.1
[0.5.0]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.5.0
[0.4.5]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.4.5
[0.4.4]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.4.4
[0.4.3]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.4.3
[0.4.2]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.4.2
[0.4.1]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.4.1
[0.4.0]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.4.0
[0.3.0]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.3.0
[0.2.0]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.2.0
[0.1.1]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.1.1
[0.1.0]: https://github.com/willibrandon/serilog-syntax/releases/tag/v0.1.0