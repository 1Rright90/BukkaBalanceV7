using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Performance
{
    /// <summary>
    /// Base class for performance monitoring functionality.
    /// Provides core metrics tracking, timing operations, and periodic reporting capabilities.
    /// </summary>
    /// <remarks>
    /// This class follows TaleWorlds' Native code patterns and provides thread-safe operations
    /// for performance monitoring in a Mount &amp; Blade II: Bannerlord environment.
    /// </remarks>
    public abstract class BasePerformanceMonitor : BaseInitializable
    {
        protected readonly ConcurrentDictionary<string, long> _metrics;
        protected readonly ConcurrentDictionary<string, Stopwatch> _timers;
        protected readonly object _metricsLock = new object();
        protected Timer _reportingTimer;
        protected readonly TimeSpan _reportingInterval = TimeSpan.FromMinutes(1);

        protected BasePerformanceMonitor() : base()
        {
            _metrics = new ConcurrentDictionary<string, long>();
            _timers = new ConcurrentDictionary<string, Stopwatch>();
        }

        protected override void OnInitialize()
        {
            StartPeriodicReporting();
        }

        /// <summary>
        /// Called when the monitor is being disposed.
        /// </summary>
        protected override void OnDispose()
        {
            try
            {
                base.OnDispose();
                _reportingTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                _reportingTimer?.Dispose();
                ResetMetrics();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during performance monitor disposal");
            }
        }

        /// <summary>
        /// Start periodic reporting of metrics
        /// </summary>
        protected virtual void StartPeriodicReporting()
        {
            _reportingTimer = new Timer(ReportCallback, null, _reportingInterval, _reportingInterval);
        }

        /// <summary>
        /// Reporting callback
        /// </summary>
        protected virtual void ReportCallback(object state)
        {
            if (_isDisposed) return;

            lock (_metricsLock)
            {
                try
                {
                    OnReport();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error during performance reporting", ex);
                }
            }
        }

        /// <summary>
        /// Override to implement custom reporting logic
        /// </summary>
        protected virtual void OnReport()
        {
        }

        /// <summary>
        /// Start timing an operation
        /// </summary>
        protected virtual void StartTiming(string operation)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _timers.AddOrUpdate(operation, stopwatch, (_, __) => stopwatch);
        }

        /// <summary>
        /// Stop timing an operation and record the duration
        /// </summary>
        protected virtual long StopTiming(string operation)
        {
            if (_timers.TryRemove(operation, out var stopwatch))
            {
                stopwatch.Stop();
                var duration = stopwatch.ElapsedMilliseconds;
                RecordMetric($"{operation}_duration", duration);
                return duration;
            }
            return 0;
        }

        /// <summary>
        /// Records a metric with overflow checking.
        /// </summary>
        /// <param name="name">The name of the metric to record.</param>
        /// <param name="value">The value to record.</param>
        /// <exception cref="OverflowException">Thrown when the value would cause an overflow.</exception>
        /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
        protected virtual void RecordMetric(string name, long value)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            try
            {
                checked
                {
                    _metrics.AddOrUpdate(name, value, (_, __) => value);
                }
            }
            catch (OverflowException ex)
            {
                Logger.LogError($"Overflow detected when recording metric {name}", ex);
                throw;
            }
        }

        /// <summary>
        /// Increments a metric with overflow checking.
        /// </summary>
        /// <param name="name">The name of the metric to increment.</param>
        /// <returns>The new value of the metric.</returns>
        /// <exception cref="OverflowException">Thrown when incrementing would cause an overflow.</exception>
        /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
        protected virtual long IncrementMetric(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            try
            {
                return checked(_metrics.AddOrUpdate(name, 1, (_, value) => value + 1));
            }
            catch (OverflowException ex)
            {
                Logger.LogError($"Overflow detected when incrementing metric {name}", ex);
                throw;
            }
        }

        /// <summary>
        /// Gets the value of a metric.
        /// </summary>
        /// <param name="name">The name of the metric to retrieve.</param>
        /// <returns>The current value of the metric, or 0 if not found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
        protected virtual long GetMetric(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            return _metrics.GetOrAdd(name, 0);
        }

        /// <summary>
        /// Reset all metrics
        /// </summary>
        protected virtual void ResetMetrics()
        {
            _metrics.Clear();
            _timers.Clear();
        }
    }
}
