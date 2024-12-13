using System;
using System.Collections.Generic;

namespace YSBCaptain.Core.Monitoring
{
    /// <summary>
    /// Defines a collector for gathering and managing performance metrics.
    /// </summary>
    public interface IMetricCollector : IDisposable
    {
        /// <summary>
        /// Gets the unique name of the metric collector.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the collection of metric names currently being tracked.
        /// </summary>
        IEnumerable<string> MetricNames { get; }

        /// <summary>
        /// Collects the current set of metrics.
        /// </summary>
        void CollectMetrics();

        /// <summary>
        /// Gets the value of a specific metric.
        /// </summary>
        /// <param name="name">The name of the metric to retrieve.</param>
        /// <param name="defaultValue">The default value to return if the metric doesn't exist.</param>
        /// <returns>The value of the metric, or defaultValue if not found.</returns>
        double GetMetric(string name, double defaultValue = 0.0);

        /// <summary>
        /// Sets the value for a specific metric.
        /// </summary>
        /// <param name="name">The name of the metric to set.</param>
        /// <param name="value">The value to set for the metric.</param>
        void SetMetric(string name, double value);

        /// <summary>
        /// Resets all metrics to their default state.
        /// </summary>
        void Reset();
    }
}
