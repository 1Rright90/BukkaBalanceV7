using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.HealthMonitoring
{
    /// <summary>
    /// Provides health monitoring capabilities for system components.
    /// </summary>
    public class HealthCheck : BaseComponent, IHealthCheck
    {
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<string, (HealthStatus Status, string Message)> _componentStatuses;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly AsyncLock _lock;
        private Task _monitoringTask;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="HealthCheck"/> class.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public HealthCheck(ILogger logger) : base("HealthCheck")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _componentStatuses = new ConcurrentDictionary<string, (HealthStatus Status, string Message)>();
            _cancellationTokenSource = new CancellationTokenSource();
            _lock = new AsyncLock();
        }

        /// <summary>
        /// Starts the health monitoring process.
        /// </summary>
        public override async Task StartAsync()
        {
            using (await _lock.LockAsync().ConfigureAwait(false))
            {
                if (_monitoringTask != null)
                    return;

                await base.StartAsync().ConfigureAwait(false);
                _monitoringTask = MonitorHealthAsync(_cancellationTokenSource.Token);
            }
        }

        /// <summary>
        /// Stops the health monitoring process.
        /// </summary>
        public override async Task StopAsync()
        {
            using (await _lock.LockAsync().ConfigureAwait(false))
            {
                if (_monitoringTask == null)
                    return;

                _cancellationTokenSource.Cancel();
                try
                {
                    await _monitoringTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when stopping
                }
                finally
                {
                    _monitoringTask = null;
                }

                await base.StopAsync().ConfigureAwait(false);
            }
        }

        private async Task MonitorHealthAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await CheckComponentHealthAsync(cancellationToken).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when stopping
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health monitoring failed");
            }
        }

        private async Task CheckComponentHealthAsync(CancellationToken cancellationToken)
        {
            foreach (var kvp in _componentStatuses)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    // Add your health check logic here
                    await Task.CompletedTask.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Health check failed for component {kvp.Key}");
                    UpdateStatus(kvp.Key, HealthStatus.Error, ex.Message);
                }
            }
        }

        /// <summary>
        /// Updates the health status of a component.
        /// </summary>
        /// <param name="component">The component name.</param>
        /// <param name="status">The health status.</param>
        /// <param name="message">Optional status message.</param>
        /// <exception cref="ArgumentNullException">Thrown when component is null or empty.</exception>
        public void UpdateStatus(string component, HealthStatus status, string message)
        {
            if (string.IsNullOrEmpty(component))
                throw new ArgumentNullException(nameof(component));

            try
            {
                var componentHealth = (status, message);

                _componentStatuses.AddOrUpdate(component, componentHealth, (_, __) => componentHealth);
                _logger.LogInformation($"Health status updated for {component}: {status} - {message ?? "No message"}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating health status for component {component}");
                throw;
            }
        }

        /// <summary>
        /// Gets the health status of a component.
        /// </summary>
        /// <param name="component">The component name.</param>
        /// <returns>The component's health status.</returns>
        /// <exception cref="ArgumentNullException">Thrown when component is null or empty.</exception>
        public HealthStatus GetComponentStatus(string component)
        {
            if (string.IsNullOrEmpty(component))
                throw new ArgumentNullException(nameof(component));

            return _componentStatuses.TryGetValue(component, out var health) ? health.Status : HealthStatus.Unknown;
        }

        /// <summary>
        /// Gets the status message for a component.
        /// </summary>
        /// <param name="component">The component name.</param>
        /// <returns>The component's status message.</returns>
        /// <exception cref="ArgumentNullException">Thrown when component is null or empty.</exception>
        public string GetStatusMessage(string component)
        {
            if (string.IsNullOrEmpty(component))
                throw new ArgumentNullException(nameof(component));

            return _componentStatuses.TryGetValue(component, out var health) ? health.Message : null;
        }

        /// <summary>
        /// Gets the health status of all components.
        /// </summary>
        /// <returns>A dictionary containing component names and their health status.</returns>
        public Dictionary<string, HealthStatus> GetAllComponentStatus()
        {
            using (_lock.Lock())
            {
                var statuses = new Dictionary<string, HealthStatus>();
                foreach (var component in _componentStatuses)
                {
                    statuses[component.Key] = component.Value.Status;
                }
                return statuses;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        protected override void OnDispose()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _lock?.Dispose();
                base.OnDispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during HealthCheck disposal");
                throw;
            }
        }
    }
}
