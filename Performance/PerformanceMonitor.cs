using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Base;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Core.Models;
using YSBCaptain.Core.Configuration;
using YSBCaptain.Core.Telemetry;

namespace YSBCaptain.Performance
{
    /// <summary>
    /// Monitors and tracks performance metrics in Mount &amp; Blade II: Bannerlord.
    /// Implements TaleWorlds' patterns for performance monitoring.
    /// </summary>
    /// <remarks>
    /// This class follows the performance monitoring patterns used in TaleWorlds.Core
    /// and provides thread-safe performance tracking capabilities.
    /// </remarks>
    public class PerformanceMonitor : BaseComponent, IPerformanceMonitor
    {
        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly ITelemetryClient _telemetryClient;
        private readonly IConfigurationManager _configManager;
        private readonly Process _currentProcess;
        private readonly ConcurrentDictionary<string, double> _metrics;
        private readonly SemaphoreSlim _monitorLock;
        private volatile bool _isMonitoring;
        private double _cpuWarningThreshold;
        private double _cpuCriticalThreshold;
        private double _memoryWarningThreshold;
        private double _memoryCriticalThreshold;
        private readonly TimeSpan _metricsInterval;

        public PerformanceMonitor(
            ILogger<PerformanceMonitor> logger,
            ITelemetryClient telemetryClient = null,
            IConfigurationManager configManager = null) : base("PerformanceMonitor")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryClient = telemetryClient ?? TelemetryClientFactory.Create();
            _configManager = configManager ?? ConfigurationManager.Instance;
            _currentProcess = Process.GetCurrentProcess();
            _metrics = new ConcurrentDictionary<string, double>();
            _monitorLock = new SemaphoreSlim(1, 1);

            LoadConfiguration();
        }

        private void LoadConfiguration()
        {
            try
            {
                if (_configManager == null)
                    throw new InvalidOperationException("Configuration manager not initialized");

                _cpuWarningThreshold = ValidateThreshold(
                    _configManager.GetValue("CpuWarningThreshold", 80.0),
                    "CpuWarningThreshold");
                _cpuCriticalThreshold = ValidateThreshold(
                    _configManager.GetValue("CpuCriticalThreshold", 90.0),
                    "CpuCriticalThreshold");
                _memoryWarningThreshold = ValidateThreshold(
                    _configManager.GetValue("MemoryWarningThreshold", 80.0),
                    "MemoryWarningThreshold");
                _memoryCriticalThreshold = ValidateThreshold(
                    _configManager.GetValue("MemoryCriticalThreshold", 90.0),
                    "MemoryCriticalThreshold");

                var intervalSeconds = _configManager.GetValue("MetricsIntervalSeconds", 5);
                if (intervalSeconds <= 0)
                    throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Metrics interval must be positive");

                _metricsInterval = TimeSpan.FromSeconds(intervalSeconds);
                _logger.LogInformation("Configuration loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration. Using default values.");
                SetDefaultConfiguration();
            }
        }

        private double ValidateThreshold(double value, string name)
        {
            if (value < 0 || value > 100)
                throw new ArgumentOutOfRangeException(name, $"{name} must be between 0 and 100");
            return value;
        }

        private void SetDefaultConfiguration()
        {
            _cpuWarningThreshold = 80.0;
            _cpuCriticalThreshold = 90.0;
            _memoryWarningThreshold = 80.0;
            _memoryCriticalThreshold = 90.0;
            _metricsInterval = TimeSpan.FromSeconds(5);
        }

        public async Task StartMonitoringAsync(CancellationToken cancellationToken = default)
        {
            if (await _monitorLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    if (_isMonitoring)
                        return;

                    _isMonitoring = true;
                    _logger.LogInformation("Performance monitoring started");
                    await _telemetryClient.TrackEventAsync("MonitoringStarted", cancellationToken).ConfigureAwait(false);

                    // Start continuous monitoring
                    _ = MonitorMetricsAsync(cancellationToken);
                }
                finally
                {
                    _monitorLock.Release();
                }
            }
        }

        private async Task MonitorMetricsAsync(CancellationToken cancellationToken)
        {
            while (_isMonitoring)
            {
                try
                {
                    var metrics = await GetPerformanceMetricsAsync(cancellationToken).ConfigureAwait(false);
                    await _telemetryClient.TrackMetricsAsync(metrics, cancellationToken).ConfigureAwait(false);

                    // Check thresholds and log warnings
                    if (metrics.CpuUsage > _cpuCriticalThreshold)
                    {
                        _logger.LogCritical($"CPU usage critical: {metrics.CpuUsage}%");
                        await _telemetryClient.TrackEventAsync("CpuUsageCritical", metrics.CpuUsage, cancellationToken).ConfigureAwait(false);
                    }
                    else if (metrics.CpuUsage > _cpuWarningThreshold)
                    {
                        _logger.LogWarning($"CPU usage high: {metrics.CpuUsage}%");
                        await _telemetryClient.TrackEventAsync("CpuUsageWarning", metrics.CpuUsage, cancellationToken).ConfigureAwait(false);
                    }

                    var memoryUsagePercent = (metrics.AvailableMemory / (double)metrics.TotalMemory) * 100;
                    if (memoryUsagePercent > _memoryCriticalThreshold)
                    {
                        _logger.LogCritical($"Memory usage critical: {memoryUsagePercent}%");
                        await _telemetryClient.TrackEventAsync("MemoryUsageCritical", memoryUsagePercent, cancellationToken).ConfigureAwait(false);
                    }
                    else if (memoryUsagePercent > _memoryWarningThreshold)
                    {
                        _logger.LogWarning($"Memory usage high: {memoryUsagePercent}%");
                        await _telemetryClient.TrackEventAsync("MemoryUsageWarning", memoryUsagePercent, cancellationToken).ConfigureAwait(false);
                    }

                    await Task.Delay(_metricsInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in metrics monitoring loop");
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task StopMonitoringAsync(CancellationToken cancellationToken = default)
        {
            await _monitorLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_isMonitoring)
                    return;

                _isMonitoring = false;
                _logger.LogInformation("Performance monitoring stopped");
                await _telemetryClient.TrackEventAsync("MonitoringStopped", cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _monitorLock.Release();
            }
        }

        public async Task LogPerformanceMetricAsync(string key, double value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            _metrics.AddOrUpdate(key, value, (_, _) => value);
            _logger.LogInformation($"Performance metric - {key}: {value}");
            await _telemetryClient.TrackMetricAsync(key, value, cancellationToken).ConfigureAwait(false);
        }

        public async Task LogEventAsync(string eventName, string details = null)
        {
            try
            {
                await Task.Run(() =>
                {
                    _logger.LogInformation($"Performance event: {eventName} - {details}");
                    // Additional event logging logic here
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error logging event {eventName}");
                throw;
            }
        }

        private double GetMetricValue(string key)
        {
            if (_metrics.TryGetValue(key, out double value))
                return value;
            return -1;
        }

        public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await Task.Run(() => new PerformanceMetrics
                {
                    CpuUsage = GetMetricValue("CpuUsage"),
                    MemoryUsage = GetMetricValue("MemoryUsage"),
                    NetworkLatency = GetMetricValue("NetworkLatency"),
                    FrameTime = GetMetricValue("FrameTime"),
                    ThreadCount = GetMetricValue("ThreadCount")
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance metrics");
                throw;
            }
        }

        public async Task SetThresholdsAsync(double cpuWarning, double cpuCritical, double memoryWarning, double memoryCritical, CancellationToken cancellationToken = default)
        {
            _cpuWarningThreshold = cpuWarning;
            _cpuCriticalThreshold = cpuCritical;
            _memoryWarningThreshold = memoryWarning;
            _memoryCriticalThreshold = memoryCritical;

            _logger.LogInformation("Performance thresholds updated");
            await _telemetryClient.TrackEventAsync("ThresholdsUpdated", cancellationToken).ConfigureAwait(false);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await base.StartAsync(cancellationToken);
                _logger.LogInformation("Performance monitor started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting performance monitor");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                await base.StopAsync(cancellationToken);
                _logger.LogInformation("Performance monitor stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping performance monitor");
                throw;
            }
        }

        protected override async Task OnDisposeAsync()
        {
            try
            {
                await base.OnDisposeAsync();
                _logger.LogInformation("Performance monitor disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing performance monitor");
                throw;
            }
        }
    }
}
