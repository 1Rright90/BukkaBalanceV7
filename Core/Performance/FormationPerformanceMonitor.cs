using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.Performance
{
    /// <summary>
    /// Monitors and tracks performance metrics related to unit formations in the game.
    /// </summary>
    public class FormationPerformanceMonitor : BaseComponent
    {
        private readonly ConcurrentDictionary<string, FormationMetrics> _formationMetrics;
        private readonly ConcurrentDictionary<string, Stopwatch> _adaptationTimers;
        private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(1);
        private volatile bool _isMonitoring;

        /// <summary>
        /// Initializes a new instance of the FormationPerformanceMonitor class.
        /// </summary>
        /// <param name="logger">The logger instance to use for monitoring events.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
        public FormationPerformanceMonitor(ILogger<FormationPerformanceMonitor> logger)
            : base("FormationPerformanceMonitor")
        {
            _formationMetrics = new ConcurrentDictionary<string, FormationMetrics>();
            _adaptationTimers = new ConcurrentDictionary<string, Stopwatch>();
        }

        /// <summary>
        /// Starts the formation performance monitoring process.
        /// </summary>
        /// <returns>A task representing the monitoring operation.</returns>
        protected override async Task OnStartAsync()
        {
            if (_isMonitoring)
                return;

            _isMonitoring = true;

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken))
            {
                try
                {
                    while (_isMonitoring && !cts.Token.IsCancellationRequested)
                    {
                        await UpdateMetricsAsync().ConfigureAwait(false);
                        await Task.Delay(_updateInterval, cts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                {
                    // Expected when monitoring is stopped
                }
            }
        }

        /// <summary>
        /// Stops the formation performance monitoring process.
        /// </summary>
        /// <returns>A task representing the stop operation.</returns>
        protected override async Task OnStopAsync()
        {
            _isMonitoring = false;
            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Starts formation adaptation metrics for a specific formation.
        /// </summary>
        /// <param name="formationId">The unique identifier of the formation.</param>
        public void StartFormationAdaptation(string formationId)
        {
            var timer = new Stopwatch();
            timer.Start();
            _adaptationTimers.AddOrUpdate(formationId, timer, (_, __) => timer);
        }

        /// <summary>
        /// Ends formation adaptation metrics for a specific formation.
        /// </summary>
        /// <param name="formationId">The unique identifier of the formation.</param>
        public void EndFormationAdaptation(string formationId)
        {
            if (_adaptationTimers.TryRemove(formationId, out var timer))
            {
                timer.Stop();
                var adaptationTime = timer.ElapsedMilliseconds;
                
                UpdateFormationMetric(formationId, "AdaptationTime", adaptationTime);
                Logger.LogInformation($"Formation {formationId} adaptation completed in {adaptationTime}ms");
            }
        }

        /// <summary>
        /// Updates metrics for a specific formation.
        /// </summary>
        /// <param name="formationId">The unique identifier of the formation.</param>
        /// <param name="unitCount">The number of units in the formation.</param>
        /// <param name="cohesion">The cohesion of the formation.</param>
        /// <param name="spacing">The spacing of the formation.</param>
        /// <param name="recalcCost">The recalculation cost of the formation.</param>
        public void UpdateFormation(string formationId, int unitCount, float cohesion, float spacing, double recalcCost)
        {
            _formationMetrics.AddOrUpdate(
                formationId,
                new FormationMetrics
                {
                    UnitCount = unitCount,
                    Cohesion = cohesion,
                    Spacing = spacing,
                    RecalcCost = recalcCost,
                    LastUpdate = DateTime.UtcNow,
                    StabilityIndex = CalculateStabilityIndex(formationId)
                },
                (_, existing) =>
                {
                    existing.UnitCount = unitCount;
                    existing.Cohesion = cohesion;
                    existing.Spacing = spacing;
                    existing.RecalcCost = recalcCost;
                    existing.LastUpdate = DateTime.UtcNow;
                    existing.StabilityIndex = CalculateStabilityIndex(formationId);
                    return existing;
                });
        }

        /// <summary>
        /// Calculates the stability index for a specific formation.
        /// </summary>
        /// <param name="formationId">The unique identifier of the formation.</param>
        /// <returns>The stability index of the formation.</returns>
        private double CalculateStabilityIndex(string formationId)
        {
            if (_formationMetrics.TryGetValue(formationId, out var metrics))
            {
                // Calculate stability based on recent metrics
                var timeSinceLastUpdate = DateTime.UtcNow - metrics.LastUpdate;
                var recalcFrequency = metrics.RecalcCost > 0 ? 1000.0 / metrics.RecalcCost : 0;
                
                // Higher stability when:
                // 1. Less frequent recalculations
                // 2. Lower recalc cost
                // 3. Higher cohesion
                return (1.0 - Math.Min(1.0, recalcFrequency / 30.0)) * // Recalc frequency factor
                       (1.0 - Math.Min(1.0, metrics.RecalcCost / 100.0)) * // Recalc cost factor
                       metrics.Cohesion; // Formation cohesion factor
            }
            return 1.0; // Default to perfect stability if no metrics
        }

        /// <summary>
        /// Updates a specific metric for a formation.
        /// </summary>
        /// <param name="formationId">The unique identifier of the formation.</param>
        /// <param name="metricName">The name of the metric to update.</param>
        /// <param name="value">The new value of the metric.</param>
        private void UpdateFormationMetric(string formationId, string metricName, double value)
        {
            if (_formationMetrics.TryGetValue(formationId, out var metrics))
            {
                switch (metricName)
                {
                    case "AdaptationTime":
                        metrics.AdaptationTime = value;
                        break;
                    case "RecalcCost":
                        metrics.RecalcCost = value;
                        break;
                }
            }
        }

        /// <summary>
        /// Updates metrics for all tracked formations.
        /// </summary>
        /// <returns>A task representing the update operation.</returns>
        private async Task UpdateMetricsAsync()
        {
            var totalUnits = 0;
            var avgCohesion = 0.0f;
            var avgSpacing = 0.0f;
            var formationCount = 0;
            var totalStability = 0.0;
            var maxRecalcCost = 0.0;

            foreach (var metric in _formationMetrics.Values)
            {
                totalUnits += metric.UnitCount;
                avgCohesion += metric.Cohesion;
                avgSpacing += metric.Spacing;
                totalStability += metric.StabilityIndex;
                maxRecalcCost = Math.Max(maxRecalcCost, metric.RecalcCost);
                formationCount++;
            }

            if (formationCount > 0)
            {
                avgCohesion /= formationCount;
                avgSpacing /= formationCount;
                var avgStability = totalStability / formationCount;

                await PerformanceMonitor.LogPerformanceMetricAsync("formation_total_units", totalUnits).ConfigureAwait(false);
                await PerformanceMonitor.LogPerformanceMetricAsync("formation_avg_cohesion", avgCohesion).ConfigureAwait(false);
                await PerformanceMonitor.LogPerformanceMetricAsync("formation_avg_spacing", avgSpacing).ConfigureAwait(false);
                await PerformanceMonitor.LogPerformanceMetricAsync("formation_count", formationCount).ConfigureAwait(false);
                await PerformanceMonitor.LogPerformanceMetricAsync("formation_avg_stability", avgStability).ConfigureAwait(false);
                await PerformanceMonitor.LogPerformanceMetricAsync("formation_max_recalc_cost", maxRecalcCost).ConfigureAwait(false);
                
                if (maxRecalcCost > 50) // Alert on high recalc cost
                {
                    Logger.LogWarning($"High formation recalculation cost detected: {maxRecalcCost}ms");
                }
            }
        }

        /// <summary>
        /// Represents metrics for a specific formation.
        /// </summary>
        private class FormationMetrics
        {
            /// <summary>
            /// The number of units in the formation.
            /// </summary>
            public int UnitCount { get; set; }
            /// <summary>
            /// The cohesion of the formation.
            /// </summary>
            public float Cohesion { get; set; }
            /// <summary>
            /// The spacing of the formation.
            /// </summary>
            public float Spacing { get; set; }
            /// <summary>
            /// The recalculation cost of the formation.
            /// </summary>
            public double RecalcCost { get; set; }
            /// <summary>
            /// The adaptation time of the formation.
            /// </summary>
            public double AdaptationTime { get; set; }
            /// <summary>
            /// The stability index of the formation.
            /// </summary>
            public double StabilityIndex { get; set; }
            /// <summary>
            /// The last update time of the formation metrics.
            /// </summary>
            public DateTime LastUpdate { get; set; }
        }
    }
}
