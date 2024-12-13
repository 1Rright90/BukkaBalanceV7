using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YSBCaptain.Core.Interfaces
{
    /// <summary>
    /// Interface for tracking telemetry events, metrics, exceptions, and dependencies.
    /// </summary>
    public interface ITelemetry : IDisposable
    {
        /// <summary>
        /// Tracks a custom event with optional properties.
        /// </summary>
        /// <param name="eventName">Name of the event to track.</param>
        /// <param name="properties">Optional properties associated with the event.</param>
        void TrackEvent(string eventName, IDictionary<string, string> properties = null);

        /// <summary>
        /// Asynchronously tracks a custom event with optional properties.
        /// </summary>
        /// <param name="eventName">Name of the event to track.</param>
        /// <param name="properties">Optional properties associated with the event.</param>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        Task TrackEventAsync(string eventName, IDictionary<string, string> properties = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Tracks a metric with the specified name and value.
        /// </summary>
        /// <param name="metricName">Name of the metric to track.</param>
        /// <param name="value">Value of the metric.</param>
        /// <param name="properties">Optional properties associated with the metric.</param>
        void TrackMetric(string metricName, double value, IDictionary<string, string> properties = null);

        /// <summary>
        /// Asynchronously tracks a metric with the specified name and value.
        /// </summary>
        /// <param name="metricName">Name of the metric to track.</param>
        /// <param name="value">Value of the metric.</param>
        /// <param name="properties">Optional properties associated with the metric.</param>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        Task TrackMetricAsync(string metricName, double value, IDictionary<string, string> properties = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Tracks an exception with optional properties.
        /// </summary>
        /// <param name="exception">The exception to track.</param>
        /// <param name="properties">Optional properties associated with the exception.</param>
        void TrackException(Exception exception, IDictionary<string, string> properties = null);

        /// <summary>
        /// Asynchronously tracks an exception with optional properties.
        /// </summary>
        /// <param name="exception">The exception to track.</param>
        /// <param name="properties">Optional properties associated with the exception.</param>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        Task TrackExceptionAsync(Exception exception, IDictionary<string, string> properties = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Tracks a dependency call with timing information.
        /// </summary>
        /// <param name="dependencyType">Type of dependency (e.g., SQL, HTTP).</param>
        /// <param name="target">Target of the dependency call.</param>
        /// <param name="name">Name of the dependency call.</param>
        /// <param name="startTime">Start time of the dependency call.</param>
        /// <param name="duration">Duration of the dependency call.</param>
        /// <param name="success">Whether the dependency call was successful.</param>
        void TrackDependency(string dependencyType, string target, string name, DateTimeOffset startTime, TimeSpan duration, bool success);

        /// <summary>
        /// Asynchronously tracks a dependency call with timing information.
        /// </summary>
        /// <param name="dependencyType">Type of dependency (e.g., SQL, HTTP).</param>
        /// <param name="target">Target of the dependency call.</param>
        /// <param name="name">Name of the dependency call.</param>
        /// <param name="startTime">Start time of the dependency call.</param>
        /// <param name="duration">Duration of the dependency call.</param>
        /// <param name="success">Whether the dependency call was successful.</param>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        Task TrackDependencyAsync(string dependencyType, string target, string name, DateTimeOffset startTime, TimeSpan duration, bool success, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Flushes all pending telemetry events immediately.
        /// </summary>
        void Flush();

        /// <summary>
        /// Asynchronously flushes all pending telemetry events.
        /// </summary>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        Task FlushAsync(CancellationToken cancellationToken = default);
    }
}
