using System;
using System.Threading;
using System.Threading.Tasks;
using YSBCaptain.Core.Performance;

namespace YSBCaptain.Core.Interfaces
{
    /// <summary>
    /// Interface for monitoring system performance metrics and events.
    /// </summary>
    public interface IPerformanceMonitor : IDisposable
    {
        /// <summary>
        /// Starts monitoring system performance.
        /// </summary>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops monitoring system performance.
        /// </summary>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        Task StopAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs a performance-related event.
        /// </summary>
        /// <param name="eventName">Name of the event.</param>
        /// <param name="details">Optional event details.</param>
        Task LogEventAsync(string eventName, string details = null);

        /// <summary>
        /// Gets the current performance metrics.
        /// </summary>
        /// <param name="cancellationToken">Optional token to cancel the operation.</param>
        /// <returns>Current performance metrics.</returns>
        Task<PerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default);
    }
}
