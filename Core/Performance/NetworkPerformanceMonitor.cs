using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.Performance
{
    /// <summary>
    /// Monitors and tracks network performance metrics for the game.
    /// </summary>
    public class NetworkPerformanceMonitor : BaseComponent
    {
        private readonly ConcurrentDictionary<string, long> _bytesTransferred;
        private readonly ConcurrentDictionary<string, int> _packetCounts;
        private readonly Stopwatch _measurementWindow;
        private readonly TimeSpan _sampleInterval = TimeSpan.FromSeconds(1);
        private volatile bool _isMonitoring;

        /// <summary>
        /// Initializes a new instance of the NetworkPerformanceMonitor class.
        /// </summary>
        /// <param name="logger">The logger instance to use for monitoring events.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public NetworkPerformanceMonitor(ILogger<NetworkPerformanceMonitor> logger)
            : base("NetworkPerformanceMonitor")
        {
            _bytesTransferred = new ConcurrentDictionary<string, long>();
            _packetCounts = new ConcurrentDictionary<string, int>();
            _measurementWindow = new Stopwatch();
        }

        /// <summary>
        /// Starts the network performance monitoring process.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to use for the operation.</param>
        /// <returns>A task representing the monitoring operation.</returns>
        protected override async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await base.StartAsync(cancellationToken);
                _logger.LogInformation("Network performance monitor started");

                if (_isMonitoring)
                    return;

                _isMonitoring = true;
                _measurementWindow.Start();

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    try
                    {
                        while (_isMonitoring && !cts.Token.IsCancellationRequested)
                        {
                            await CollectMetricsAsync().ConfigureAwait(false);
                            await Task.Delay(_sampleInterval, cts.Token).ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                    {
                        // Expected when monitoring is stopped
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting network performance monitor");
                throw;
            }
        }

        /// <summary>
        /// Stops the network performance monitoring process.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to use for the operation.</param>
        /// <returns>A task representing the stop operation.</returns>
        protected override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await base.StopAsync(cancellationToken);
                _logger.LogInformation("Network performance monitor stopped");

                _isMonitoring = false;
                _measurementWindow.Stop();
                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping network performance monitor");
                throw;
            }
        }

        /// <summary>
        /// Records network traffic for a specific connection.
        /// </summary>
        /// <param name="connectionId">The unique identifier of the connection.</param>
        /// <param name="bytesSent">The number of bytes sent.</param>
        /// <param name="bytesReceived">The number of bytes received.</param>
        /// <param name="packetsSent">The number of packets sent.</param>
        /// <param name="packetsReceived">The number of packets received.</param>
        public void RecordTraffic(string connectionId, long bytesSent, long bytesReceived, int packetsSent, int packetsReceived)
        {
            if (string.IsNullOrEmpty(connectionId))
                throw new ArgumentException("Connection ID cannot be null or empty", nameof(connectionId));

            _bytesTransferred.AddOrUpdate(
                $"{connectionId}_sent",
                bytesSent,
                (_, current) => current + bytesSent);

            _bytesTransferred.AddOrUpdate(
                $"{connectionId}_received",
                bytesReceived,
                (_, current) => current + bytesReceived);

            _packetCounts.AddOrUpdate(
                $"{connectionId}_sent",
                packetsSent,
                (_, current) => current + packetsSent);

            _packetCounts.AddOrUpdate(
                $"{connectionId}_received",
                packetsReceived,
                (_, current) => current + packetsReceived);
        }

        /// <summary>
        /// Collects network metrics for all active connections.
        /// </summary>
        /// <returns>A task representing the collection operation.</returns>
        private async Task CollectMetricsAsync()
        {
            try
            {
                var elapsedSeconds = _measurementWindow.Elapsed.TotalSeconds;
                if (elapsedSeconds <= 0)
                    return;

                foreach (var kvp in _bytesTransferred)
                {
                    var connectionId = kvp.Key.Split('_')[0];
                    var direction = kvp.Key.Split('_')[1];
                    var bytesPerSecond = kvp.Value / elapsedSeconds;

                    await UpdateMetricAsync($"{connectionId}_{direction}_bps", bytesPerSecond).ConfigureAwait(false);
                }

                foreach (var kvp in _packetCounts)
                {
                    var connectionId = kvp.Key.Split('_')[0];
                    var direction = kvp.Key.Split('_')[1];
                    var packetsPerSecond = kvp.Value / elapsedSeconds;

                    await UpdateMetricAsync($"{connectionId}_{direction}_pps", packetsPerSecond).ConfigureAwait(false);
                }

                // Reset counters for next window
                _measurementWindow.Restart();
                _bytesTransferred.Clear();
                _packetCounts.Clear();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error collecting network metrics");
            }
        }

        /// <summary>
        /// Updates a specific network metric.
        /// </summary>
        /// <param name="metricName">The name of the metric to update.</param>
        /// <param name="value">The new value of the metric.</param>
        /// <returns>A task representing the update operation.</returns>
        private async Task UpdateMetricAsync(string metricName, double value)
        {
            try
            {
                // Update the metric in your monitoring system
                await Task.CompletedTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metric {MetricName}", metricName);
            }
        }
    }
}
