using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using YSBCaptain.Core.Logging;

namespace YSBCaptain.Core.Monitoring
{
    /// <summary>
    /// Base implementation of a metric collector that provides common functionality for metric collection and management.
    /// </summary>
    public abstract class BaseMetricCollector : IMetricCollector
    {
        protected readonly ILogger _logger;
        protected readonly ConcurrentDictionary<string, double> _metrics;
        protected bool _isDisposed;

        /// <summary>
        /// Gets the unique name of the metric collector.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the collection of metric names currently being tracked.
        /// </summary>
        public IEnumerable<string> MetricNames => _metrics.Keys;

        /// <summary>
        /// Initializes a new instance of the BaseMetricCollector class.
        /// </summary>
        /// <param name="name">The name of the metric collector.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null.</exception>
        protected BaseMetricCollector(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _logger = LoggerFactory.Create(GetType());
            _metrics = new ConcurrentDictionary<string, double>();
            _isDisposed = false;
        }

        /// <summary>
        /// Collects the current set of metrics. Must be implemented by derived classes.
        /// </summary>
        public abstract void CollectMetrics();

        /// <summary>
        /// Gets the value of a specific metric.
        /// </summary>
        /// <param name="name">The name of the metric to retrieve.</param>
        /// <param name="defaultValue">The default value to return if the metric doesn't exist.</param>
        /// <returns>The value of the metric, or defaultValue if not found.</returns>
        public virtual double GetMetric(string name, double defaultValue = 0.0)
        {
            ThrowIfDisposed();
            return _metrics.GetOrAdd(name, defaultValue);
        }

        /// <summary>
        /// Sets the value for a specific metric.
        /// </summary>
        /// <param name="name">The name of the metric to set.</param>
        /// <param name="value">The value to set for the metric.</param>
        public virtual void SetMetric(string name, double value)
        {
            ThrowIfDisposed();
            _metrics.AddOrUpdate(name, value, (_, __) => value);
        }

        /// <summary>
        /// Resets all metrics to their default state.
        /// </summary>
        public virtual void Reset()
        {
            ThrowIfDisposed();
            _metrics.Clear();
        }

        /// <summary>
        /// Throws an ObjectDisposedException if the collector has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the collector has been disposed.</exception>
        protected virtual void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// Disposes the metric collector.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Called when the collector is being disposed. Override to implement custom disposal logic.
        /// </summary>
        /// <param name="disposing">True if the collector is being disposed, false if it's being finalized.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                _metrics.Clear();
                OnDispose();
            }

            _isDisposed = true;
        }

        /// <summary>
        /// Called when the collector is being disposed. Override to implement custom disposal logic.
        /// </summary>
        protected virtual void OnDispose()
        {
            // Base implementation does nothing
        }

        ~BaseMetricCollector()
        {
            Dispose(false);
        }
    }
}
