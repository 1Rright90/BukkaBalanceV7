using System;
using System.Collections.Generic;

namespace YSBCaptain.Core.Interfaces
{
    /// <summary>
    /// Interface for handling and managing errors in the application.
    /// </summary>
    public interface IErrorHandler
    {
        /// <summary>
        /// Handles an error with the specified key and exception.
        /// </summary>
        /// <param name="errorKey">The key identifying the error type.</param>
        /// <param name="ex">The exception that occurred.</param>
        /// <param name="context">Optional context information about where the error occurred.</param>
        void HandleError(string errorKey, Exception ex, string context = null);

        /// <summary>
        /// Handles a warning with the specified key and message.
        /// </summary>
        /// <param name="warningKey">The key identifying the warning type.</param>
        /// <param name="message">The warning message.</param>
        void HandleWarning(string warningKey, string message);

        /// <summary>
        /// Gets statistics about handled errors and rate-limited errors.
        /// </summary>
        /// <returns>A tuple containing the total number of handled errors and rate-limited errors.</returns>
        (long TotalHandled, long RateLimited) GetErrorStats();
    }
}
