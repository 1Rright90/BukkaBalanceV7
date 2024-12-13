using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.HealthMonitoring
{
    /// <summary>
    /// Manages telemetry events and ensures thread-safe operations with enhanced functionality.
    /// </summary>
    public class Telemetry : ITelemetry, IDisposable
    {
        private readonly ILogger<Telemetry> _logger;
        private readonly ConcurrentQueue<TelemetryEvent> _eventQueue;
        private readonly SemaphoreSlim _flushLock;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private const int MaxBatchSize = 100;
        private volatile bool _isDisposed;

        // Circuit breaker state
        private int _failureCount;
        private const int FailureThreshold = 5;
        private const int CircuitResetTimeMs = 30000;
        private volatile bool _circuitOpen;
        private DateTime _circuitOpenedTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="Telemetry"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public Telemetry(ILogger<Telemetry> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventQueue = new ConcurrentQueue<TelemetryEvent>();
            _flushLock = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();
            _isDisposed = false;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(Telemetry));
            }
        }

        /// <summary>
        /// Tracks a telemetry event with optional properties.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="properties">Optional properties associated with the event.</param>
        public void TrackEvent(string eventName, IDictionary<string, string> properties = null)
        {
            ThrowIfDisposed();

            if (_circuitOpen && DateTime.UtcNow - _circuitOpenedTime < TimeSpan.FromMilliseconds(CircuitResetTimeMs))
            {
                _logger.LogWarning("Circuit breaker is open. Dropping telemetry event.");
                return;
            }

            var telemetryEvent = new TelemetryEvent
            {
                Name = eventName,
                Properties = new ConcurrentDictionary<string, string>(properties ?? new Dictionary<string, string>()),
                Timestamp = DateTime.UtcNow
            };

            _eventQueue.Enqueue(telemetryEvent);
            _logger.LogInformation($"Tracked event: {eventName}");
        }

        /// <summary>
        /// Asynchronously tracks a telemetry event with optional properties.
        /// </summary>
        /// <param name="eventName">The name of the event.</param>
        /// <param name="properties">Optional properties associated with the event.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task TrackEventAsync(string eventName, IDictionary<string, string> properties = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await Task.Run(() => TrackEvent(eventName, properties), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Flushes all pending telemetry events.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await _flushLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var batch = new List<TelemetryEvent>();

                while (!_eventQueue.IsEmpty && !cancellationToken.IsCancellationRequested)
                {
                    while (batch.Count < MaxBatchSize && _eventQueue.TryDequeue(out var evt))
                    {
                        batch.Add(evt);
                    }

                    if (batch.Count > 0)
                    {
                        await ProcessBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                        batch.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                HandleFailure(ex);
                throw;
            }
            finally
            {
                _flushLock.Release();
            }
        }

        private async Task ProcessBatchAsync(List<TelemetryEvent> batch, CancellationToken cancellationToken)
        {
            try
            {
                // Simulate processing batch asynchronously (e.g., sending to a telemetry service)
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                _failureCount = 0; // Reset failure count on successful processing
                _logger.LogInformation($"Successfully processed batch of {batch.Count} telemetry events.");
            }
            catch (Exception ex)
            {
                HandleFailure(ex);
                throw;
            }
        }

        private void HandleFailure(Exception ex)
        {
            _failureCount++;
            _logger.LogError(ex, "Telemetry processing failed.");

            if (_failureCount >= FailureThreshold)
            {
                _circuitOpen = true;
                _circuitOpenedTime = DateTime.UtcNow;
                _logger.LogWarning("Circuit breaker activated due to consecutive failures.");
            }
        }

        /// <summary>
        /// Tracks a metric with optional properties.
        /// </summary>
        /// <param name="name">The name of the metric.</param>
        /// <param name="value">The value of the metric.</param>
        /// <param name="properties">Optional properties associated with the metric.</param>
        public void TrackMetric(string name, double value, IDictionary<string, string> properties = null)
        {
            try
            {
                _logger.LogInformation($"Metric: {name} = {value}");
                // Additional metric tracking logic here
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error tracking metric {name}");
                throw;
            }
        }

        /// <summary>
        /// Asynchronously tracks a metric with optional properties.
        /// </summary>
        /// <param name="name">The name of the metric.</param>
        /// <param name="value">The value of the metric.</param>
        /// <param name="properties">Optional properties associated with the metric.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task TrackMetricAsync(string name, double value, IDictionary<string, string> properties = null, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => TrackMetric(name, value, properties), cancellationToken);
        }

        /// <summary>
        /// Tracks an exception with optional properties.
        /// </summary>
        /// <param name="exception">The exception to track.</param>
        /// <param name="properties">Optional properties associated with the exception.</param>
        public void TrackException(Exception exception, IDictionary<string, string> properties = null)
        {
            try
            {
                _logger.LogError(exception, "Exception tracked");
                // Additional exception tracking logic here
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking exception");
                throw;
            }
        }

        /// <summary>
        /// Asynchronously tracks an exception with optional properties.
        /// </summary>
        /// <param name="exception">The exception to track.</param>
        /// <param name="properties">Optional properties associated with the exception.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task TrackExceptionAsync(Exception exception, IDictionary<string, string> properties = null, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => TrackException(exception, properties), cancellationToken);
        }

        /// <summary>
        /// Tracks a dependency with optional properties.
        /// </summary>
        /// <param name="dependencyType">The type of the dependency.</param>
        /// <param name="target">The target of the dependency.</param>
        /// <param name="name">The name of the dependency.</param>
        /// <param name="startTime">The start time of the dependency.</param>
        /// <param name="duration">The duration of the dependency.</param>
        /// <param name="success">Whether the dependency was successful.</param>
        public void TrackDependency(string dependencyType, string target, string name, DateTimeOffset startTime, TimeSpan duration, bool success)
        {
            try
            {
                _logger.LogInformation($"Dependency: {dependencyType} - {target} - {name} ({duration.TotalMilliseconds}ms) - {(success ? "Success" : "Failed")}");
                // Additional dependency tracking logic here
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tracking dependency");
                throw;
            }
        }

        /// <summary>
        /// Asynchronously tracks a dependency with optional properties.
        /// </summary>
        /// <param name="dependencyType">The type of the dependency.</param>
        /// <param name="target">The target of the dependency.</param>
        /// <param name="name">The name of the dependency.</param>
        /// <param name="startTime">The start time of the dependency.</param>
        /// <param name="duration">The duration of the dependency.</param>
        /// <param name="success">Whether the dependency was successful.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        public async Task TrackDependencyAsync(string dependencyType, string target, string name, DateTimeOffset startTime, TimeSpan duration, bool success, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => TrackDependency(dependencyType, target, name, startTime, duration, success), cancellationToken);
        }

        /// <summary>
        /// Flushes any buffered telemetry data.
        /// </summary>
        public void Flush()
        {
            try
            {
                // Implement any buffered telemetry flushing here
                _logger.LogInformation("Telemetry flushed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing telemetry");
                throw;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            try
            {
                _cancellationTokenSource.Cancel();
                Task.Run(() => FlushAsync(_cancellationTokenSource.Token)).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Telemetry flush cancelled during disposal.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing telemetry.");
            }
            finally
            {
                _cancellationTokenSource.Dispose();
                _flushLock.Dispose();
                _isDisposed = true;
            }
        }
    }

    /// <summary>
    /// Represents a telemetry event with associated properties and metadata.
    /// </summary>
    public class TelemetryEvent
    {
        /// <summary>
        /// Gets or sets the name of the event.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the properties associated with the event.
        /// </summary>
        public ConcurrentDictionary<string, string> Properties { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the event occurred.
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}
