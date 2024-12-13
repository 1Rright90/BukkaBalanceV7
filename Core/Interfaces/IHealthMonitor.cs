using System;
using System.Threading.Tasks;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.Interfaces
{
    /// <summary>
    /// Interface for monitoring the health of system components and reporting status changes.
    /// </summary>
    public interface IHealthMonitor
    {
        /// <summary>
        /// Gets the current overall health status of the system.
        /// </summary>
        Models.HealthStatus CurrentStatus { get; }

        /// <summary>
        /// Starts the health monitoring process.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StartMonitoringAsync();

        /// <summary>
        /// Stops the health monitoring process.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task StopMonitoringAsync();

        /// <summary>
        /// Reports the health status of a specific component.
        /// </summary>
        /// <param name="component">The name of the component.</param>
        /// <param name="status">The current health status of the component.</param>
        /// <param name="message">Optional message providing additional status details.</param>
        void ReportHealth(string component, Models.HealthStatus status, string message = null);

        /// <summary>
        /// Event that is raised when the overall health status changes.
        /// </summary>
        event EventHandler<Models.HealthStatus> HealthStatusChanged;

        /// <summary>
        /// Gets a value indicating whether the health monitor is currently active.
        /// </summary>
        bool IsMonitoring { get; }
    }
}
