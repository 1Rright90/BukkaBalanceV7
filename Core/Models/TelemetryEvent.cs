using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace YSBCaptain.Core.Models
{
    /// <summary>
    /// Represents a telemetry event with associated properties and metrics.
    /// </summary>
    public class TelemetryEvent
    {
        /// <summary>
        /// Gets the name of the telemetry event.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the properties associated with this event.
        /// </summary>
        public IImmutableDictionary<string, string> Properties { get; }

        /// <summary>
        /// Gets the exception if this is an error event.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Gets the metric value if this is a metric event.
        /// </summary>
        public double? MetricValue { get; }

        /// <summary>
        /// Gets the timestamp when this event was created.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the severity level of the event.
        /// </summary>
        public TelemetryEventSeverity Severity { get; }

        /// <summary>
        /// Creates a new instance of TelemetryEvent.
        /// </summary>
        /// <param name="name">The name of the event.</param>
        /// <param name="properties">Optional properties associated with the event.</param>
        /// <param name="exception">Optional exception if this is an error event.</param>
        /// <param name="metricValue">Optional metric value if this is a metric event.</param>
        /// <param name="severity">The severity level of the event.</param>
        /// <exception cref="ArgumentNullException">Thrown when name is null or empty.</exception>
        public TelemetryEvent(
            string name,
            IDictionary<string, string> properties = null,
            Exception exception = null,
            double? metricValue = null,
            TelemetryEventSeverity severity = TelemetryEventSeverity.Information)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            Name = name;
            Properties = properties?.ToImmutableDictionary() ?? ImmutableDictionary<string, string>.Empty;
            Exception = exception;
            MetricValue = metricValue;
            Severity = severity;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates an informational telemetry event.
        /// </summary>
        public static TelemetryEvent Information(string name, IDictionary<string, string> properties = null) =>
            new TelemetryEvent(name, properties, severity: TelemetryEventSeverity.Information);

        /// <summary>
        /// Creates a warning telemetry event.
        /// </summary>
        public static TelemetryEvent Warning(string name, IDictionary<string, string> properties = null) =>
            new TelemetryEvent(name, properties, severity: TelemetryEventSeverity.Warning);

        /// <summary>
        /// Creates an error telemetry event.
        /// </summary>
        public static TelemetryEvent Error(string name, Exception exception, IDictionary<string, string> properties = null) =>
            new TelemetryEvent(name, properties, exception, severity: TelemetryEventSeverity.Error);

        /// <summary>
        /// Creates a metric telemetry event.
        /// </summary>
        public static TelemetryEvent Metric(string name, double value, IDictionary<string, string> properties = null) =>
            new TelemetryEvent(name, properties, metricValue: value);

        /// <summary>
        /// Returns a string representation of the telemetry event.
        /// </summary>
        public override string ToString()
        {
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Severity}] {Name}";
        }
    }

    /// <summary>
    /// Represents the severity level of a telemetry event.
    /// </summary>
    public enum TelemetryEventSeverity
    {
        /// <summary>
        /// Verbose level for detailed troubleshooting.
        /// </summary>
        Verbose,

        /// <summary>
        /// Debug level for internal system events.
        /// </summary>
        Debug,

        /// <summary>
        /// Information level for general operational events.
        /// </summary>
        Information,

        /// <summary>
        /// Warning level for non-critical issues.
        /// </summary>
        Warning,

        /// <summary>
        /// Error level for issues that require attention.
        /// </summary>
        Error,

        /// <summary>
        /// Critical level for severe issues that require immediate attention.
        /// </summary>
        Critical
    }
}
