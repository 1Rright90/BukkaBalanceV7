using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Logging;
using YSBCaptain.Core.Telemetry;

namespace YSBCaptain.Core.Error
{
    /// <summary>
    /// Manages error handling, logging, and telemetry for the application.
    /// </summary>
    public class ErrorManager : InitializableBase
    {
        private static readonly Lazy<ErrorManager> _instance = new Lazy<ErrorManager>(() => new ErrorManager());
        
        /// <summary>
        /// Gets the singleton instance of the error manager.
        /// </summary>
        public static ErrorManager Instance => _instance.Value;

        private readonly ILogger _logger;
        private readonly ITelemetry _telemetry;
        private readonly ErrorRateLimiter _rateLimiter;
        private readonly ConcurrentDictionary<string, int> _errorCounts;
        private readonly ConcurrentQueue<ErrorEventArgs> _errorQueue;
        private readonly int _maxQueueSize;
        private readonly CancellationTokenSource _processingCts;
        private Task _processingTask;

        /// <summary>
        /// Event raised when any error occurs.
        /// </summary>
        public event EventHandler<ErrorEventArgs> ErrorOccurred;

        /// <summary>
        /// Event raised when a critical error occurs.
        /// </summary>
        public event EventHandler<ErrorEventArgs> CriticalErrorOccurred;

        private ErrorManager()
        {
            _logger = LoggerFactory.Create<ErrorManager>();
            _telemetry = TelemetryFactory.Create();
            _rateLimiter = new ErrorRateLimiter(TimeSpan.FromMinutes(5), 10);
            _errorCounts = new ConcurrentDictionary<string, int>();
            _errorQueue = new ConcurrentQueue<ErrorEventArgs>();
            _maxQueueSize = 1000;
            _processingCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Initializes the error manager and starts error processing.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel initialization.</param>
        protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            _processingTask = ProcessErrorQueueAsync(_processingCts.Token);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Handles an error event with rate limiting and queuing.
        /// </summary>
        /// <param name="errorArgs">The error event arguments.</param>
        /// <exception cref="ArgumentNullException">Thrown when errorArgs is null.</exception>
        public void HandleError(ErrorEventArgs errorArgs)
        {
            if (errorArgs == null)
                throw new ArgumentNullException(nameof(errorArgs));

            ThrowIfDisposed();

            var errorKey = $"{errorArgs.Source}:{errorArgs.ErrorCode}";

            if (!_rateLimiter.ShouldProcessError(errorKey))
            {
                _logger.LogDebug($"Error rate limited: {errorKey}");
                return;
            }

            _errorCounts.AddOrUpdate(errorKey, 1, (_, count) => count + 1);

            if (_errorQueue.Count < _maxQueueSize)
            {
                _errorQueue.Enqueue(errorArgs);
            }
            else
            {
                _logger.LogWarning("Error queue is full, dropping error");
            }

            if (errorArgs.Severity == ErrorSeverity.Critical)
            {
                OnCriticalError(errorArgs);
            }
        }

        /// <summary>
        /// Handles an exception with optional context information.
        /// </summary>
        /// <param name="ex">The exception to handle.</param>
        /// <param name="source">Optional source of the exception.</param>
        /// <param name="context">Optional context information.</param>
        /// <param name="severity">Severity level of the error.</param>
        /// <exception cref="ArgumentNullException">Thrown when ex is null.</exception>
        public void HandleException(Exception ex, string source = null, string context = null, ErrorSeverity severity = ErrorSeverity.Medium)
        {
            if (ex == null)
                throw new ArgumentNullException(nameof(ex));

            var errorArgs = new ErrorEventArgs(
                "EXCEPTION",
                ex.Message,
                ex,
                severity,
                source,
                context
            );

            HandleError(errorArgs);
        }

        private async Task ProcessErrorQueueAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    ErrorEventArgs error;
                    while (_errorQueue.TryDequeue(out error))
                    {
                        try
                        {
                            await ProcessErrorAsync(error).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing error event: {ex.Message}");
                        }
                    }

                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in error processing loop: {ex.Message}");
                    await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task ProcessErrorAsync(ErrorEventArgs error)
        {
            // Log the error
            LogError(error);

            // Send to telemetry
            await _telemetry.TrackErrorAsync(error).ConfigureAwait(false);

            // Raise error event
            OnErrorOccurred(error);
        }

        private void LogError(ErrorEventArgs error)
        {
            var message = $"[{error.Severity}] {error.Source}: {error.Message}";
            if (!string.IsNullOrEmpty(error.Context))
            {
                message += $" | Context: {error.Context}";
            }

            switch (error.Severity)
            {
                case ErrorSeverity.Low:
                    _logger.LogDebug(message);
                    break;
                case ErrorSeverity.Medium:
                    _logger.LogWarning(message);
                    break;
                case ErrorSeverity.High:
                case ErrorSeverity.Critical:
                    _logger.LogError(message);
                    if (error.Exception != null)
                    {
                        _logger.LogError(error.Exception.ToString());
                    }
                    break;
            }
        }

        /// <summary>
        /// Raises the ErrorOccurred event.
        /// </summary>
        /// <param name="e">The error event arguments.</param>
        protected virtual void OnErrorOccurred(ErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the CriticalErrorOccurred event.
        /// </summary>
        /// <param name="e">The error event arguments.</param>
        protected virtual void OnCriticalError(ErrorEventArgs e)
        {
            CriticalErrorOccurred?.Invoke(this, e);
        }

        /// <summary>
        /// Gets the error count for a specific error code and optional source.
        /// </summary>
        /// <param name="errorCode">The error code to check.</param>
        /// <param name="source">Optional source of the error.</param>
        /// <returns>The number of times this error has occurred.</returns>
        public int GetErrorCount(string errorCode, string source = null)
        {
            var key = source != null ? $"{source}:{errorCode}" : errorCode;
            return _errorCounts.GetOrAdd(key, 0);
        }

        /// <summary>
        /// Resets all error counts to zero.
        /// </summary>
        public void ResetErrorCounts()
        {
            _errorCounts.Clear();
        }

        /// <summary>
        /// Disposes of managed resources.
        /// </summary>
        protected override async Task OnDispose()
        {
            _processingCts.Cancel();
            try
            {
                if (_processingTask != null)
                {
                    using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        await Task.WhenAny(_processingTask, Task.Delay(TimeSpan.FromSeconds(5), timeoutCts.Token)).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error waiting for error processing task to complete: {ex.Message}");
            }

            _processingCts.Dispose();
            _errorQueue.Clear();
            _errorCounts.Clear();
        }
    }
}
