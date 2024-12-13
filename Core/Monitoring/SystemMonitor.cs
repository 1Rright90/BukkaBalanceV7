using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YSBCaptain.Core.Interfaces;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.Monitoring
{
    /// <summary>
    /// Monitors system-wide performance metrics including CPU, memory, and disk usage.
    /// </summary>
    public class SystemMonitor : ISystemMonitor, IDisposable
    {
        private readonly Microsoft.Extensions.Logging.ILogger<SystemMonitor> _logger;
        private readonly Process _currentProcess;
        private readonly CancellationTokenSource _cts;
        private bool _isMonitoring;
        private bool _disposed;

        public SystemMonitor(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SystemMonitor>();
            _currentProcess = Process.GetCurrentProcess();
            _cts = new CancellationTokenSource();
        }

        public double GetCpuUsage()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SystemMonitor));
            return GetProcessCpuUsage();
        }

        public double GetMemoryUsage()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SystemMonitor));
            return _currentProcess.WorkingSet64 / (1024.0 * 1024.0); // Convert to MB
        }

        public double GetDiskUsage()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SystemMonitor));
            var driveInfo = new DriveInfo(AppDomain.CurrentDomain.BaseDirectory[0].ToString());
            var totalSpace = driveInfo.TotalSize;
            var freeSpace = driveInfo.AvailableFreeSpace;
            return ((double)(totalSpace - freeSpace) / totalSpace) * 100;
        }

        public async Task StartMonitoringAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SystemMonitor));
            if (_isMonitoring) return;

            try
            {
                _isMonitoring = true;
                _logger.LogInformation("System monitoring started");
                
                await MonitorSystemMetricsAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start system monitoring");
                throw;
            }
        }

        public async Task StopMonitoringAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SystemMonitor));
            if (!_isMonitoring) return;

            try
            {
                _cts.Cancel();
                _isMonitoring = false;
                _logger.LogInformation("System monitoring stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop system monitoring");
                throw;
            }
        }

        public async Task<SystemMetrics> GetSystemMetricsAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SystemMonitor));

            try
            {
                var cpuUsage = GetCpuUsage();
                var memoryUsageMB = GetMemoryUsage();
                var diskUsagePercentage = GetDiskUsage();
                
                var computerInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
                var totalMemoryMB = computerInfo.TotalPhysicalMemory / (1024 * 1024);
                var availableMemoryMB = GetAvailableMemory() / (1024 * 1024);
                
                var driveInfo = new DriveInfo(AppDomain.CurrentDomain.BaseDirectory[0].ToString());
                var totalDiskSpaceMB = driveInfo.TotalSize / (1024 * 1024);
                var availableDiskSpaceMB = driveInfo.AvailableFreeSpace / (1024 * 1024);

                return new SystemMetrics(
                    cpuUsage,
                    memoryUsageMB,
                    diskUsagePercentage,
                    totalMemoryMB,
                    availableMemoryMB,
                    totalDiskSpaceMB,
                    availableDiskSpaceMB,
                    _currentProcess.TotalProcessorTime
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get system metrics");
                throw;
            }
        }

        private async Task MonitorSystemMetricsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var metrics = await GetSystemMetricsAsync().ConfigureAwait(false);
                    _logger.LogDebug("System Metrics - CPU: {CpuUsage}%, Memory: {MemoryUsage}MB, Disk: {DiskUsage}%",
                        metrics.CpuUsagePercentage,
                        metrics.MemoryUsageMB,
                        metrics.DiskUsagePercentage);

                    await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring system metrics");
                }
            }
        }

        private double GetProcessCpuUsage()
        {
            try
            {
                _currentProcess.Refresh();
                return _currentProcess.TotalProcessorTime.TotalMilliseconds / 
                       Environment.ProcessorCount / 
                       _currentProcess.UserProcessorTime.TotalMilliseconds * 100;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting CPU usage");
                return 0;
            }
        }

        private long GetAvailableMemory()
        {
            try
            {
                return new PerformanceCounter("Memory", "Available Bytes").RawValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available memory");
                return 0;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _cts.Cancel();
                _cts.Dispose();
                _currentProcess.Dispose();
                _disposed = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing SystemMonitor");
            }
        }
    }
}
