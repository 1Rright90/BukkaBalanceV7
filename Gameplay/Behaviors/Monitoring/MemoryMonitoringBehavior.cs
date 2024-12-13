using System;
using System.Diagnostics;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Core.HealthMonitoring;
using YSBCaptain.Core.Logging;
using YSBCaptain.Core.ResourceManagement;
using Microsoft.Extensions.Logging;

namespace YSBCaptain.Gameplay.Behaviors.Monitoring
{
    /// <summary>
    /// Behavior for monitoring memory usage during mission execution.
    /// Tracks memory allocation, usage patterns, and triggers cleanup when thresholds are exceeded.
    /// </summary>
    /// <remarks>
    /// This behavior:
    /// - Monitors process memory usage at configurable intervals
    /// - Reports memory usage metrics to the health monitoring system
    /// - Triggers resource cleanup when memory thresholds are exceeded
    /// - Provides detailed memory metrics for performance analysis
    /// </remarks>
    public class MemoryMonitoringBehavior : MissionBehavior
    {
        private readonly float _interval;
        private float _timer;
        private readonly Process _currentProcess;
        private long _lastMemoryUsage;
        private readonly IHealthMonitor _healthMonitor;
        private readonly IResourceManager _resourceManager;
        private readonly AppConfiguration _config;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the MemoryMonitoringBehavior class.
        /// </summary>
        /// <param name="config">Application configuration containing monitoring settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
        public MemoryMonitoringBehavior(AppConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _interval = (float)config.Performance.MonitoringInterval.TotalSeconds;
            _timer = 0f;
            _currentProcess = Process.GetCurrentProcess();
            _lastMemoryUsage = _currentProcess.WorkingSet64;
            _healthMonitor = HealthMonitor.Instance;
            _resourceManager = ResourceManager.Instance;
            _logger = LoggerFactory.Create(builder => 
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Information))
                .CreateLogger<MemoryMonitoringBehavior>();
        }

        /// <summary>
        /// Gets the behavior type for mission registration.
        /// </summary>
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        /// <summary>
        /// Called every mission tick to update memory monitoring.
        /// </summary>
        /// <param name="dt">Time elapsed since last tick in seconds.</param>
        public override void OnMissionTick(float dt)
        {
            _timer += dt;
            if (_timer >= _interval)
            {
                _timer = 0f;
                CheckMemoryUsage();
            }
        }

        /// <summary>
        /// Checks current memory usage and triggers appropriate actions based on thresholds.
        /// </summary>
        private void CheckMemoryUsage()
        {
            try
            {
                var currentMemory = _currentProcess.WorkingSet64;
                var memoryDelta = currentMemory - _lastMemoryUsage;
                _lastMemoryUsage = currentMemory;

                var currentMemoryMB = currentMemory / (1024 * 1024);
                var memoryDeltaMB = memoryDelta / (1024 * 1024);
                var memoryThresholdBytes = _config.Performance.MemoryThresholdPercent * _currentProcess.MaxWorkingSet.ToInt64() / 100;
                
                if (currentMemory > memoryThresholdBytes)
                {
                    _logger.LogWarning($"High memory usage detected: {currentMemoryMB}MB (Threshold: {memoryThresholdBytes / (1024 * 1024)}MB)");
                    _healthMonitor.ReportIssue(HealthIssueType.HighMemoryUsage, $"Memory usage: {currentMemoryMB}MB");
                    
                    if (_config.Resources.EnableAutoCleanup)
                    {
                        _resourceManager.RequestCleanup();
                    }

                    if (currentMemory > memoryThresholdBytes * 1.2)
                    {
                        InformationManager.DisplayMessage(
                            new InformationMessage(
                                "Warning: High memory usage detected. Performance may be affected.",
                                Colors.Red
                            )
                        );
                    }
                }

                if (_config.Performance.EnableDetailedMetrics)
                {
                    _logger.LogDebug($"Memory Usage - Current: {currentMemoryMB}MB, Delta: {(memoryDeltaMB >= 0 ? "+" : "")}{memoryDeltaMB}MB");
                }

                if (Mission.Current != null)
                {
                    var missionTime = Mission.Current.CurrentTime;
                    _healthMonitor.RecordMetric("MissionMemoryUsage", currentMemoryMB, missionTime);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring memory usage");
                throw new InvalidOperationException("Failed to monitor memory usage", ex);
            }
        }

        /// <summary>
        /// Called when the behavior is being destroyed.
        /// </summary>
        public override void OnRemoveBehavior()
        {
            try
            {
                base.OnRemoveBehavior();
                _logger.LogInformation("Memory monitoring behavior removed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error destroying memory monitoring behavior");
                throw;
            }
        }
    }
}
