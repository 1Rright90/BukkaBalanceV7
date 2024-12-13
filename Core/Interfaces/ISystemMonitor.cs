using System;
using System.Threading.Tasks;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.Interfaces
{
    /// <summary>
    /// Interface for monitoring system-wide performance metrics.
    /// </summary>
    public interface ISystemMonitor : IDisposable
    {
        /// <summary>
        /// Gets the current CPU usage percentage.
        /// </summary>
        double GetCpuUsage();

        /// <summary>
        /// Gets the current memory usage in MB.
        /// </summary>
        double GetMemoryUsage();

        /// <summary>
        /// Gets the current disk usage percentage.
        /// </summary>
        double GetDiskUsage();

        /// <summary>
        /// Gets the current system metrics.
        /// </summary>
        /// <returns>Current system metrics including CPU, memory, and disk usage.</returns>
        Task<SystemMetrics> GetSystemMetricsAsync();

        /// <summary>
        /// Starts monitoring system metrics.
        /// </summary>
        Task StartMonitoringAsync();

        /// <summary>
        /// Stops monitoring system metrics.
        /// </summary>
        Task StopMonitoringAsync();
    }
}
