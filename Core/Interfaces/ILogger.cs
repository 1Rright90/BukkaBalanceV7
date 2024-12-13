using System;

namespace YSBCaptain.Core.Interfaces
{
    /// <summary>
    /// Interface for logging messages at different severity levels.
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// Logs a trace message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogTrace(string message);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogDebug(string message);

        /// <summary>
        /// Logs an information message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogInformation(string message);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message with an optional exception.
        /// </summary>
        /// <param name="message">The error message to log.</param>
        /// <param name="exception">Optional exception associated with the error.</param>
        void LogError(string message, Exception exception = null);

        /// <summary>
        /// Logs a critical error message with an optional exception.
        /// </summary>
        /// <param name="message">The critical error message to log.</param>
        /// <param name="exception">Optional exception associated with the critical error.</param>
        void LogCritical(string message, Exception exception = null);
    }
}
