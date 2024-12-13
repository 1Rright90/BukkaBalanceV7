using System;

namespace YSBCaptain.Core.Error
{
    /// <summary>
    /// Defines severity levels for error events.
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// Low severity error that doesn't affect core functionality.
        /// </summary>
        Low,

        /// <summary>
        /// Medium severity error that may affect some functionality.
        /// </summary>
        Medium,

        /// <summary>
        /// High severity error that affects major functionality.
        /// </summary>
        High,

        /// <summary>
        /// Critical error that requires immediate attention.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Provides detailed information about an error event.
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the unique error code.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the associated exception, if any.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets the severity level of the error.
        /// </summary>
        public ErrorSeverity Severity { get; }

        /// <summary>
        /// Gets the UTC timestamp when the error occurred.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the source of the error.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets additional context information about the error.
        /// </summary>
        public string Context { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorEventArgs"/> class.
        /// </summary>
        /// <param name="errorCode">The unique error code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="exception">Optional exception associated with the error.</param>
        /// <param name="severity">The severity level of the error.</param>
        /// <param name="source">The source of the error.</param>
        /// <param name="context">Additional context information.</param>
        /// <exception cref="ArgumentNullException">Thrown when errorCode or message is null.</exception>
        public ErrorEventArgs(
            string errorCode,
            string message,
            Exception exception = null,
            ErrorSeverity severity = ErrorSeverity.Medium,
            string source = null,
            string context = null)
        {
            ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Exception = exception;
            Severity = severity;
            Timestamp = DateTime.UtcNow;
            Source = source ?? "Unknown";
            Context = context;
        }
    }
}
