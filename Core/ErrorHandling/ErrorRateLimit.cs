using System;
using System.Collections.Concurrent;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Core.ErrorHandling
{
    /// <summary>
    /// Provides rate-limited error handling and logging functionality
    /// </summary>
    public class ErrorRateLimit
    {
        private readonly object _logLock = new object();
        private DateTime _lastLogTime;
        private static readonly TimeSpan LogThreshold = TimeSpan.FromSeconds(1);
        private readonly ConcurrentDictionary<string, int> _errorCounts = new ConcurrentDictionary<string, int>();
        private const int DefaultErrorThreshold = 100;
        private const int CleanupIntervalMinutes = 5;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorRateLimit"/> class.
        /// </summary>
        public ErrorRateLimit()
        {
            _lastLogTime = DateTime.MinValue;
        }

        /// <summary>
        /// Handles an error with rate limiting and logging
        /// </summary>
        /// <param name="key">Unique identifier for the error type</param>
        /// <param name="message">Error message to log</param>
        /// <param name="exception">Optional exception to include in logging</param>
        /// <param name="threshold">Optional custom threshold for this error type</param>
        public void HandleError(string key, string message, Exception exception = null, int threshold = DefaultErrorThreshold)
        {
            if (ShouldLog(key, threshold))
            {
                if (exception != null)
                {
                    Logger.Log(YSBLogLevel.Error, message, exception);
                }
                else
                {
                    Logger.Log(YSBLogLevel.Warning, message);
                }
            }
            IncrementErrorCount(key);
        }

        /// <summary>
        /// Checks if an error should be logged based on rate limiting
        /// </summary>
        /// <param name="key">Unique identifier for the error type</param>
        /// <param name="threshold">Optional custom threshold for this error type</param>
        /// <returns>True if the error should be logged, false otherwise</returns>
        public bool ShouldLog(string key, int threshold = DefaultErrorThreshold)
        {
            lock (_logLock)
            {
                var now = DateTime.Now;
                if (now - _lastLogTime > LogThreshold)
                {
                    _lastLogTime = now;

                    // Reset error counts periodically
                    if (_lastLogTime.Minute % CleanupIntervalMinutes == 0)
                    {
                        _errorCounts.Clear();
                    }

                    return _errorCounts.GetOrAdd(key, 0) < threshold;
                }
                return false;
            }
        }

        /// <summary>
        /// Increments the error count for a given key
        /// </summary>
        /// <param name="key">Unique identifier for the error type</param>
        public void IncrementErrorCount(string key)
        {
            _errorCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
        }

        /// <summary>
        /// Clears all error counts
        /// </summary>
        public void Reset()
        {
            _errorCounts.Clear();
        }
    }
}