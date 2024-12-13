using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Interfaces;

namespace YSBCaptain.Core.Performance
{
    /// <summary>
    /// Monitors and tracks system performance metrics and events.
    /// </summary>
    public class PerformanceMonitor : IPerformanceMonitor
    {
        private readonly ILogger<PerformanceMonitor> _logger;
        private readonly ConcurrentDictionary<string, Stopwatch> _eventTimers;
        private readonly ConcurrentDictionary<string, double> _metrics;
        private bool _isDisposed;
        private CancellationTokenSource _cancellationTokenSource;
        private Process _currentProcess;

        public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventTimers = new ConcurrentDictionary<string, Stopwatch>();
            _metrics = new ConcurrentDictionary<string, double>();
            _cancellationTokenSource = new CancellationTokenSource();
            _currentProcess = Process.GetCurrentProcess();
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
            {
                _logger.LogWarning("Cannot start disposed PerformanceMonitor");
                return;
            }

            try
            {
                await OnStartAsync();
                _logger.LogInformation("Performance monitoring started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start performance monitoring");
                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
            {
                _logger.LogWarning("Cannot stop disposed PerformanceMonitor");
                return;
            }

            try
            {
                await OnStopAsync();
                _logger.LogInformation("Performance monitoring stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop performance monitoring");
                throw;
            }
        }

        public async Task<PerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
            {
                _logger.LogWarning("Cannot get metrics from disposed PerformanceMonitor");
                return null;
            }

            try
            {
                var metrics = new PerformanceMetrics
                {
                    CpuUsage = await GetCpuUsageAsync(),
                    MemoryUsage = await GetMemoryUsageAsync(),
                    NetworkLatency = await GetNetworkLatencyAsync(),
                    NetworkBandwidth = await GetNetworkBandwidthAsync()
                };

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get performance metrics");
                throw;
            }
        }

        public async Task LogEventAsync(string eventName, string details = null)
        {
            if (_isDisposed)
            {
                _logger.LogWarning("Cannot log event from disposed PerformanceMonitor");
                return;
            }

            try
            {
                var stopwatch = _eventTimers.GetOrAdd(eventName, _ => new Stopwatch());
                if (!stopwatch.IsRunning)
                {
                    stopwatch.Start();
                }
                else
                {
                    stopwatch.Stop();
                    var elapsedMs = stopwatch.ElapsedMilliseconds;
                    _metrics.AddOrUpdate(eventName, elapsedMs, (_, __) => elapsedMs);
                    _logger.LogInformation($"Performance event: {eventName}, Duration: {elapsedMs}ms, Details: {details ?? "None"}");
                    stopwatch.Reset();
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to log event {eventName}");
                throw;
            }
        }

        protected virtual async Task OnStartAsync()
        {
            await Task.CompletedTask;
        }

        protected virtual async Task OnStopAsync()
        {
            await Task.CompletedTask;
        }

        protected virtual async Task<double> GetCpuUsageAsync()
        {
            try
            {
                _currentProcess.Refresh();
                var cpuTime = _currentProcess.TotalProcessorTime;
                await Task.Delay(100); // Sample over 100ms
                _currentProcess.Refresh();
                var cpuTimeDelta = _currentProcess.TotalProcessorTime - cpuTime;
                return cpuTimeDelta.TotalMilliseconds / (Environment.ProcessorCount * 100.0) * 100.0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get CPU usage");
                return 0.0;
            }
        }

        protected virtual async Task<double> GetMemoryUsageAsync()
        {
            try
            {
                _currentProcess.Refresh();
                return _currentProcess.WorkingSet64 / (1024.0 * 1024.0); // Convert to MB
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get memory usage");
                return 0.0;
            }
        }

        protected virtual async Task<double> GetNetworkLatencyAsync()
        {
            // Implement network latency measurement
            return await Task.FromResult(0.0);
        }

        protected virtual async Task<double> GetNetworkBandwidthAsync()
        {
            // Implement network bandwidth measurement
            return await Task.FromResult(0.0);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _currentProcess?.Dispose();
            _currentProcess = null;

            GC.SuppressFinalize(this);
        }
    }
}
