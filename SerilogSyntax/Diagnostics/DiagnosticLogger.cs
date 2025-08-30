using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace SerilogSyntax.Diagnostics;

/// <summary>
/// Provides diagnostic logging capabilities for the SerilogSyntax extension.
/// Logging is only active in DEBUG builds and automatically manages log file cleanup.
/// </summary>
public static class DiagnosticLogger
{
    private static readonly object _lock = new();
#pragma warning disable CS0169 // Field is never used (in Release builds)
    private static StreamWriter _fileLogger;
#pragma warning restore CS0169
#pragma warning disable CS0414 // Field is assigned but never used (in Release builds)
    private static bool _initialized = false;
#pragma warning restore CS0414

    /// <summary>
    /// Initializes the diagnostic logger. Only active in DEBUG builds.
    /// Creates a new log file and cleans up old log files (keeping the 5 most recent).
    /// </summary>
    public static void Initialize()
    {
#if DEBUG
        if (_initialized) return;
        
        lock (_lock)
        {
            if (_initialized) return;
            
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SerilogSyntax"
                );
                
                Directory.CreateDirectory(logDir);
                
                // Clean up old log files (keep last 5)
                CleanupOldLogs(logDir);
                
                var logPath = Path.Combine(logDir, $"debug_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                
                _fileLogger = new StreamWriter(logPath, false) { AutoFlush = true };
                _initialized = true;
                
                Log($"=== SerilogSyntax Diagnostic Log Started ===");
                Log($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                Log($"Process ID: {Process.GetCurrentProcess().Id}");
                Log($"Log file: {logPath}");
                Log($"=========================================");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize diagnostic logging: {ex}");
            }
        }
#endif
    }

    /// <summary>
    /// Logs a message with timestamp. Only active in DEBUG builds.
    /// Messages are written to the log file, debug output, and VS Output window.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public static void Log(string message)
    {
#if DEBUG
        if (!_initialized) Initialize();
        
        lock (_lock)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var fullMessage = $"[{timestamp}] {message}";
            
            _fileLogger?.WriteLine(fullMessage);
            Debug.WriteLine(fullMessage);
        }
#endif
    }

    /// <summary>
    /// Logs detailed character analysis of a text string. Only active in DEBUG builds.
    /// Shows each character with its position, value, and Unicode code point.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <param name="maxChars">Maximum number of characters to analyze (default: 100).</param>
    public static void LogCharacterCodes(string text, int maxChars = 100)
    {
#if DEBUG
        if (string.IsNullOrEmpty(text)) return;
        
        var sb = new StringBuilder();
        sb.AppendLine("Character analysis:");
        
        for (int i = 0; i < Math.Min(text.Length, maxChars); i++)
        {
            char c = text[i];
            sb.AppendFormat("  [{0}]: '{1}' (U+{2:X4})", i, 
                c == '\r' ? "\\r" : c == '\n' ? "\\n" : c == '"' ? "\\\"" : c.ToString(), 
                (int)c);
            
            // Highlight triple quotes
            if (i < text.Length - 2 && c == '"' && text[i + 1] == '"' && text[i + 2] == '"')
            {
                sb.Append(" <-- TRIPLE QUOTE START");
            }
            sb.AppendLine();
        }
        
        Log(sb.ToString());
#endif
    }

    /// <summary>
    /// Logs exception details including type, message, and stack trace. Only active in DEBUG builds.
    /// </summary>
    /// <param name="ex">The exception to log.</param>
    /// <param name="context">Optional context information about where the exception occurred.</param>
    public static void LogException(Exception ex, string context = "")
    {
#if DEBUG
        Log($"EXCEPTION in {context}: {ex.GetType().Name}");
        Log($"Message: {ex.Message}");
        Log($"StackTrace:\n{ex.StackTrace}");
#endif
    }

    /// <summary>
    /// Cleans up old log files in the specified directory, keeping only the 5 most recent files.
    /// Silently handles any failures during cleanup to ensure logging continues to work.
    /// </summary>
    /// <param name="logDir">The directory containing log files to clean up.</param>
    private static void CleanupOldLogs(string logDir)
    {
        try
        {
            var logFiles = Directory.GetFiles(logDir, "debug_*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Skip(5) // Keep the 5 most recent
                .ToList();

            foreach (var oldLog in logFiles)
            {
                try
                {
                    oldLog.Delete();
                }
                catch (Exception ex)
                {
#if DEBUG
                    Debug.WriteLine($"DiagnosticLogger: Failed to delete old log {oldLog.FullName}: {ex.Message}");
#else
                    _ = ex; // Suppress unused variable warning in Release builds
#endif
                    // Ignore individual file deletion failures
                }
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"DiagnosticLogger: Failed to cleanup old logs in {logDir}: {ex.Message}");
#else
            _ = ex; // Suppress unused variable warning in Release builds
#endif
            // Ignore cleanup failures - logging should still work
        }
    }
}