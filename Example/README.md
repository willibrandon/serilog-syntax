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

- **Pink braces** `{` `}` around properties
- **Light blue** property names
- **Yellow** destructuring `@` and stringification `$` operators  
- **Light green** format specifiers and alignment
- **Light purple** positional indices
- **Immediate highlighting** as you type (before closing quotes)
- **Light bulb navigation** from properties to arguments

## Output

The example creates both console output and log files in the `logs/` directory, demonstrating real Serilog usage while showcasing all the syntax highlighting features.