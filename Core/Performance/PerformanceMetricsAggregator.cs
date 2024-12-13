using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.Performance
{
    /// <summary>
    /// Aggregates performance metrics from various monitors into a unified view.
    /// </summary>
    public class PerformanceMetricsAggregator : BaseComponent
    {
        private readonly PerformanceMonitor _performanceMonitor;
        private readonly NetworkPerformanceMonitor _networkMonitor;
        private readonly FormationPerformanceMonitor _formationMonitor;
        private readonly TimeSpan _aggregationInterval = TimeSpan.FromSeconds(5);
        private volatile bool _isAggregating;
        private readonly ILogger<PerformanceMetricsAggregator> _logger;

        /// <summary>
        /// Initializes a new instance of the PerformanceMetricsAggregator class.
        /// </summary>
        /// <param name="logger">The logger instance to use for monitoring events.</param>
        /// <param name="performanceMonitor">The performance monitor instance.</param>
        /// <param name="networkMonitor">The network monitor instance.</param>
        /// <param name="formationMonitor">The formation monitor instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
        public PerformanceMetricsAggregator(
            ILogger<PerformanceMetricsAggregator> logger,
            PerformanceMonitor performanceMonitor,
            NetworkPerformanceMonitor networkMonitor,
            FormationPerformanceMonitor formationMonitor)
            : base("PerformanceMetricsAggregator")
        {
            _logger = logger;
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _networkMonitor = networkMonitor ?? throw new ArgumentNullException(nameof(networkMonitor));
            _formationMonitor = formationMonitor ?? throw new ArgumentNullException(nameof(formationMonitor));
        }

        /// <summary>
        /// Starts the metrics aggregation process.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the aggregation operation.</returns>
        protected override async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await base.StartAsync(cancellationToken);
                _logger.LogInformation("Performance metrics aggregator started");

                if (_isAggregating)
                    return;

                _isAggregating = true;

                // Start all monitors
                await Task.WhenAll(
                    _performanceMonitor.StartAsync().ConfigureAwait(false),
                    _networkMonitor.StartAsync().ConfigureAwait(false),
                    _formationMonitor.StartAsync().ConfigureAwait(false)
                ).ConfigureAwait(false);

                await Task.Run(async () =>
                {
                    while (_isAggregating)
                    {
                        await AggregateMetricsAsync().ConfigureAwait(false);
                        await Task.Delay(_aggregationInterval).ConfigureAwait(false);
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting performance metrics aggregator");
                throw;
            }
        }

        /// <summary>
        /// Stops the metrics aggregation process.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task representing the stop operation.</returns>
        protected override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await base.StopAsync(cancellationToken);
                _logger.LogInformation("Performance metrics aggregator stopped");

                _isAggregating = false;

                // Stop all monitors
                await Task.WhenAll(
                    _performanceMonitor.StopAsync().ConfigureAwait(false),
                    _networkMonitor.StopAsync().ConfigureAwait(false),
                    _formationMonitor.StopAsync().ConfigureAwait(false)
                ).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping performance metrics aggregator");
                throw;
            }
        }

        /// <summary>
        /// Aggregates metrics from all monitors into a unified view.
        /// </summary>
        /// <returns>A task representing the aggregation operation.</returns>
        private async Task AggregateMetricsAsync()
        {
            try
            {
                var metrics = await _performanceMonitor.GetPerformanceMetricsAsync().ConfigureAwait(false);

                // Log aggregated metrics
                await _performanceMonitor.LogPerformanceMetricAsync("system_health_score", CalculateHealthScore(metrics)).ConfigureAwait(false);
                await _performanceMonitor.LogEventAsync("metrics_aggregated", "Performance metrics successfully aggregated").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error aggregating performance metrics");
            }
        }

        /// <summary>
        /// Calculates the system health score based on the provided metrics.
        /// </summary>
        /// <param name="metrics">The performance metrics.</param>
        /// <returns>The calculated system health score.</returns>
        private double CalculateHealthScore(PerformanceMetrics metrics)
        {
            var weights = new Dictionary<string, double>
            {
                { "cpu", 0.3 },
                { "memory", 0.3 },
                { "network", 0.2 },
                { "formation", 0.2 }
            };

            var cpuScore = 100 - metrics.CpuUsage;
            var memoryScore = 100 - metrics.MemoryUsage;
            var networkScore = CalculateNetworkScore(metrics);
            var formationScore = 100; // Default to 100 if no formation issues

            return (cpuScore * weights["cpu"]) +
                   (memoryScore * weights["memory"]) +
                   (networkScore * weights["network"]) +
                   (formationScore * weights["formation"]);
        }

        /// <summary>
        /// Calculates the network score based on the provided metrics.
        /// </summary>
        /// <param name="metrics">The performance metrics.</param>
        /// <returns>The calculated network score.</returns>
        private double CalculateNetworkScore(PerformanceMetrics metrics)
        {
            // Network score is inversely proportional to latency
            // 0ms = 100%, 1000ms = 0%
            var latencyScore = Math.Max(0, 100 - (metrics.NetworkLatencyMs / 10));
            return latencyScore;
        }
    }
}
