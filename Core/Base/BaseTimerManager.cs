using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Models;
using YSBCaptain.Core.Interfaces;

namespace YSBCaptain.Core.Base
{
    /// <summary>
    /// Base class for components that need periodic execution with comprehensive state tracking and error handling
    /// </summary>
    public abstract class BaseTimerManager : BaseComponent
    {
        private readonly TimeSpan _interval;
        private readonly TimeSpan _maxExecutionTime;
        private DateTime _lastExecutionTime;
        private long _totalExecutions;
        private long _successfulExecutions;
        private long _failedExecutions;
        private readonly object _statsLock = new object();
        private CancellationTokenSource _cancellationTokenSource;
        private Task _timerTask;
        private volatile bool _isRunning;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseTimerManager"/> class.
        /// </summary>
        /// <param name="componentName">Name of the component.</param>
        /// <param name="interval">The interval between timer ticks.</param>
        /// <param name="maxExecutionTime">Maximum execution time for each tick.</param>
        /// <param name="performanceMonitor">The performance monitor.</param>
        protected BaseTimerManager(
            string componentName,
            TimeSpan interval,
            TimeSpan? maxExecutionTime = null,
            IPerformanceMonitor performanceMonitor = null)
            : base(componentName, LogLevel.Information, performanceMonitor)
        {
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval));

            _interval = interval;
            _maxExecutionTime = maxExecutionTime ?? interval;
            _lastExecutionTime = DateTime.MinValue;
        }

        /// <summary>
        /// Starts the timer manager asynchronously.
        /// </summary>
        public override async Task StartAsync()
        {
            ThrowIfDisposed("StartAsync");
            
            if (_isRunning)
            {
                Logger.LogWarning($"Timer {ComponentName} is already running");
                return;
            }

            try
            {
                _isRunning = true;
                _cancellationTokenSource = new CancellationTokenSource();
                _timerTask = RunTimerAsync(_cancellationTokenSource.Token);

                await base.StartAsync().ConfigureAwait(false);
                await PerformanceMonitor.LogEventAsync($"{ComponentName}_Started").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                Logger.LogError(ex, $"Failed to start timer {ComponentName}");
                throw;
            }
        }

        /// <summary>
        /// Stops the timer manager asynchronously.
        /// </summary>
        public override async Task StopAsync()
        {
            if (!_isRunning)
            {
                Logger.LogWarning($"Timer {ComponentName} is not running");
                return;
            }

            try
            {
                _isRunning = false;
                _cancellationTokenSource?.Cancel();

                if (_timerTask != null)
                {
                    try
                    {
                        using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                        {
                            await Task.WhenAny(_timerTask, Task.Delay(Timeout.Infinite, timeoutCts.Token)).ConfigureAwait(false);
                            
                            if (!_timerTask.IsCompleted)
                            {
                                Logger.LogWarning($"Timer {ComponentName} did not stop within timeout");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when cancelling
                    }
                }

                await base.StopAsync().ConfigureAwait(false);
                await PerformanceMonitor.LogEventAsync($"{ComponentName}_Stopped").ConfigureAwait(false);
            }
            finally
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                _timerTask = null;
            }
        }

        private async Task RunTimerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var startTime = DateTime.UtcNow;
                var success = false;

                try
                {
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        cts.CancelAfter(_maxExecutionTime);
                        await OnTimerTickAsync(cts.Token).ConfigureAwait(false);
                        success = true;

                        var executionTime = DateTime.UtcNow - startTime;
                        await PerformanceMonitor.LogPerformanceMetricAsync(
                            $"{ComponentName}_ExecutionTime",
                            executionTime.TotalMilliseconds,
                            cancellationToken
                        ).ConfigureAwait(false);

                        if (executionTime > _maxExecutionTime)
                        {
                            Logger.LogWarning($"Timer {ComponentName} execution exceeded max time: {executionTime.TotalMilliseconds:F2}ms > {_maxExecutionTime.TotalMilliseconds:F2}ms");
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Timer {ComponentName} tick failed");
                    await PerformanceMonitor.LogEventAsync(
                        $"{ComponentName}_Error",
                        ex.Message,
                        cancellationToken
                    ).ConfigureAwait(false);
                }
                finally
                {
                    UpdateExecutionStats(success);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }

            Logger.LogInformation($"Timer {ComponentName} loop ended");
        }

        /// <summary>
        /// When overridden in a derived class, performs the timer tick operation.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        protected abstract Task OnTimerTickAsync(CancellationToken cancellationToken);

        private void UpdateExecutionStats(bool success)
        {
            var timestamp = DateTime.UtcNow;
            lock (_statsLock)
            {
                _lastExecutionTime = timestamp;
                _totalExecutions++;
                
                if (success)
                {
                    _successfulExecutions++;
                }
                else
                {
                    _failedExecutions++;
                }
            }
        }

        /// <summary>
        /// Gets the current execution statistics.
        /// </summary>
        /// <returns>A tuple containing total, successful, and failed execution counts.</returns>
        public (long Total, long Successful, long Failed) GetExecutionStats()
        {
            lock (_statsLock)
            {
                return (_totalExecutions, _successfulExecutions, _failedExecutions);
            }
        }

        /// <summary>
        /// Releases the managed resources used by the <see cref="BaseTimerManager"/>.
        /// </summary>
        protected override void OnDispose()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                base.OnDispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during timer manager disposal");
                throw;
            }
        }

        /// <summary>
        /// Gets the timer interval.
        /// </summary>
        public TimeSpan Interval => _interval;

        /// <summary>
        /// Gets the maximum execution time allowed for each tick.
        /// </summary>
        public TimeSpan MaxExecutionTime => _maxExecutionTime;

        /// <summary>
        /// Gets the timestamp of the last execution.
        /// </summary>
        public DateTime LastExecutionTime => _lastExecutionTime;

        /// <summary>
        /// Gets a value indicating whether the timer is running.
        /// </summary>
        public bool IsRunning => _isRunning;
    }
}
