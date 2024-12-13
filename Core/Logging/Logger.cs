using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Configuration;

namespace YSBCaptain.Core.Logging
{
    /// <summary>
    /// Defines the logging levels available in the YSBCaptain system.
    /// </summary>
    public enum YSBLogLevel
    {
        /// <summary>
        /// Debug-level messages for detailed troubleshooting.
        /// </summary>
        Debug,

        /// <summary>
        /// Information-level messages for general operational events.
        /// </summary>
        Information,

        /// <summary>
        /// Warning-level messages for potentially harmful situations.
        /// </summary>
        Warning,

        /// <summary>
        /// Error-level messages for error events that might still allow the application to continue running.
        /// </summary>
        Error,

        /// <summary>
        /// Critical-level messages for failures that require immediate attention.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Public static logger facade. Provides static methods for logging, but maintains all
    /// mutable state in a private instance to avoid static mutable fields.
    /// </summary>
    public static class Logger
    {
        // Provide a single immutable reference to an instance of LoggerImpl
        private static readonly LoggerImpl InstanceImpl = new LoggerImpl();

        /// <summary>
        /// Initializes the logger with the specified directory and minimum level.
        /// </summary>
        /// <param name="logDirectory">The directory where log files will be stored.</param>
        /// <param name="minimumLevel">The minimum level of messages to log.</param>
        public static void Initialize(string logDirectory, YSBLogLevel minimumLevel)
        {
            InstanceImpl.Initialize(logDirectory, minimumLevel);
        }

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogDebug(string message)
        {
            InstanceImpl.LogDebug(message);
        }

        /// <summary>
        /// Logs an information message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogInformation(string message)
        {
            InstanceImpl.LogInformation(message);
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public static void LogWarning(string message)
        {
            InstanceImpl.LogWarning(message);
        }

        /// <summary>
        /// Logs an error message with an optional exception.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="exception">Optional exception associated with the error.</param>
        public static void LogError(string message, Exception exception = null)
        {
            InstanceImpl.LogError(message, exception);
        }

        /// <summary>
        /// Logs a critical error message with an optional exception.
        /// </summary>
        /// <param name="message">The critical error message to log.</param>
        /// <param name="exception">Optional exception associated with the critical error.</param>
        public static void LogCritical(string message, Exception exception = null)
        {
            InstanceImpl.LogCritical(message, exception);
        }

        /// <summary>
        /// Flushes any buffered log messages to disk.
        /// </summary>
        public static void FlushBuffer()
        {
            InstanceImpl.FlushBuffer();
        }

        /// <summary>
        /// Internal class that holds all mutable state and logic, ensuring no static mutable fields.
        /// </summary>
        private sealed class LoggerImpl
        {
            private readonly object _lock = new object();
            private volatile bool _initialized;
            private YSBLogLevel _minimumLevel = YSBLogLevel.Information;
            private string _logFilePath;
            private readonly StringBuilder _messageBuffer = new StringBuilder();

            public void Initialize(string logDirectory, YSBLogLevel minimumLevel)
            {
                lock (_lock)
                {
                    _minimumLevel = minimumLevel;
                    _logFilePath = string.IsNullOrEmpty(logDirectory)
                        ? Path.Combine(BasePath.Name, "Modules", "YSBCaptain", "YSBCaptain.log")
                        : Path.Combine(logDirectory, "YSBCaptain.log");

                    var directory = Path.GetDirectoryName(_logFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Test write access
                    using (File.AppendText(_logFilePath)) { }

                    _initialized = true;
                    LogInternal("Logger initialized successfully", YSBLogLevel.Information);
                }
            }

            public void LogDebug(string message)
            {
                if (_minimumLevel <= YSBLogLevel.Debug)
                {
                    LogInternal(message, YSBLogLevel.Debug);
                }
            }

            public void LogInformation(string message)
            {
                if (_minimumLevel <= YSBLogLevel.Information)
                {
                    LogInternal(message, YSBLogLevel.Information);
                }
            }

            public void LogWarning(string message)
            {
                if (_minimumLevel <= YSBLogLevel.Warning)
                {
                    LogInternal(message, YSBLogLevel.Warning);
                }
            }

            public void LogError(string message, Exception exception)
            {
                if (_minimumLevel <= YSBLogLevel.Error)
                {
                    var fullMessage = exception != null
                        ? $"{message} Exception: {exception.Message}"
                        : message;
                    LogInternal(fullMessage, YSBLogLevel.Error);
                }
            }

            public void LogCritical(string message, Exception exception)
            {
                if (_minimumLevel <= YSBLogLevel.Critical)
                {
                    var fullMessage = exception != null
                        ? $"{message} Exception: {exception.Message}"
                        : message;
                    LogInternal(fullMessage, YSBLogLevel.Critical);
                }
            }

            public void FlushBuffer()
            {
                lock (_lock)
                {
                    if (_messageBuffer.Length > 0)
                    {
                        try
                        {
                            File.AppendAllText(_logFilePath, _messageBuffer.ToString());
                            _messageBuffer.Clear();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to flush log buffer: {ex}");
                        }
                    }
                }
            }

            private void LogInternal(string message, YSBLogLevel level)
            {
                lock (_lock)
                {
                    if (!_initialized)
                    {
                        // Lazy initialization fallback
                        Initialize("Modules/YSBCaptain", YSBLogLevel.Information);
                    }

                    try
                    {
                        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                        var formattedMessage = $"[{timestamp}] [{level}] {message}";

                        // Log to TaleWorlds system
                        DisplayInGame(formattedMessage, message, level);

                        // Also write to file
                        File.AppendAllText(_logFilePath, formattedMessage + Environment.NewLine);

                        // Also write to debug output for development
                        Debug.Print(formattedMessage);
                    }
                    catch (Exception ex)
                    {
                        // Fallback to console if TaleWorlds logging fails
                        Console.WriteLine($"Logging failed: {ex.Message}");
                        Console.WriteLine($"Original message: {message}");
                    }
                }
            }

            private void DisplayInGame(string formattedMessage, string rawMessage, YSBLogLevel level)
            {
                // Use TaleWorlds' built-in color system instead of System.Drawing
                uint color;
                switch (level)
                {
                    case YSBLogLevel.Debug:
                        color = 0x808080; // Gray
                        break;
                    case YSBLogLevel.Information:
                        color = 0xFFFFFF; // White
                        break;
                    case YSBLogLevel.Warning:
                        color = 0xFFFF00; // Yellow
                        break;
                    case YSBLogLevel.Error:
                    case YSBLogLevel.Critical:
                        color = 0xFF0000; // Red
                        break;
                    default:
                        color = 0xFFFFFF; // White
                        break;
                }

                if (level == YSBLogLevel.Debug)
                {
                    Debug.Print(formattedMessage);
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage(rawMessage, Color.FromUint(color)));
                }
            }
        }
    }
}
