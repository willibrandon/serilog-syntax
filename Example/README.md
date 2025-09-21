# Serilog Syntax Example

This is a standalone console application that demonstrates all the features of the Serilog Syntax Highlighting extension for Visual Studio.

## Running the Example

1. **Prerequisites**: Ensure you have .NET 8.0 SDK installed
2. **Build and Run**:
   ```bash
   cd Example
   dotnet run
   ```

## What This Example Demonstrates

### Basic Property Logging
- Simple property substitution: `{UserId}`, `{UserName}`
- Multiple properties in one message
- Different log levels (Debug, Information, Warning, Error)

### Destructuring & Stringification
- **Destructuring with `@`**: `{@User}` - Captures object structure
- **Stringification with `$`**: `{$Settings}` - Forces string representation
- Complex object logging with nested properties

### Formatting Features
- **Date/Time formatting**: `{Timestamp:yyyy-MM-dd HH:mm:ss}`
- **Numeric formatting**: `{Price:C}`, `{Rate:P2}`
- **Alignment**: `{Name,10}`, `{Price,8:C}`
- **Combined formatting**: `{Price,8:C2}`

### Verbatim String Support
- **Multi-line templates**: Properties highlighted across all lines in `@"..."` strings
- **Escaped quotes**: Proper handling of `""` in verbatim strings
- **Complex formatting**: Format specifiers and alignment work in verbatim strings
- **Mixed content**: Verbatim strings with positional parameters and operators

### Raw String Literal Support (C# 11)
- **Triple-quote strings**: Properties highlighted in `"""..."""` strings
- **Multi-line raw strings**: No escape sequences needed
- **Custom delimiters**: Support for 4+ quotes for complex scenarios

### Serilog.Expressions
- **Filter expressions**: `Filter.ByExcluding("RequestPath like '/health%'")`
- **Conditional expressions**: `Filter.ByIncludingOnly("Level = 'Error' or StatusCode >= 500")`
- **Computed properties**: `Enrich.WithComputed("ResponseTime", "Elapsed * 2")`
- **Expression templates** with control flow:
  - `{#if}`, `{#else}`, `{#end}` for conditional rendering
  - `{#each}` for iteration over collections
  - Built-in properties: `@t`, `@m`, `@l`, `@x`, `@i`, `@p`
- **Operators**: `and`, `or`, `not`, `like`, `in`, `is null`, `=`, `<>`, `>`, `<`, `>=`, `<=`
- **Functions**: `StartsWith()`, `EndsWith()`, `Contains()`, `Length()`, `Has()`
- **Property paths**: `User.Name`, `Order.Customer.Address.City`

### Configuration Templates
- **Output templates** in Serilog configuration
- File sink templates with custom formatting
- Bootstrap logger configuration

### Error Handling
- Exception logging with structured properties
- Legacy positional parameters: `{0}`, `{1}`
- Context-aware error messages

### Performance Logging
- Timing and metrics capture
- Structured performance data with `@`
- Scoped logging with `BeginScope`

## Testing the Extension

Open `Program.cs` in Visual Studio with the Serilog Syntax extension installed to see:

### Message Template Highlighting
- **Purple braces** `{` `}` around properties
- **Blue** property names
- **Dark goldenrod** destructuring `@` and stringification `$` operators  
- **Teal** format specifiers
- **Red** alignment specifiers
- **Dark violet** positional indices

### Expression Syntax Highlighting
- **Orange** for expression operators (`and`, `or`, `not`, `like`, `in`)
- **Blue** for expression functions (`StartsWith()`, `Contains()`, etc.)
- **Green** for string literals in expressions
- **Purple** for numeric literals
- **Dark cyan** for expression directives (`{#if}`, `{#each}`, `{#end}`)
- **Dark blue** for built-in properties (`@t`, `@m`, `@l`)
- **Teal** for property paths in expressions

### Interactive Features
- **Property-argument highlighting** when cursor is on a property or argument
- **Light bulb navigation** from properties to arguments
- **Immediate highlighting** as you type (before closing quotes)
- **Multi-line support** for verbatim and raw strings

## Output

The example creates both console output and log files in the `logs/` directory, demonstrating real Serilog usage while showcasing all the syntax highlighting features.