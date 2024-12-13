using System.Collections.Generic;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.Interfaces
{
    /// <summary>
    /// Interface for checking and managing the health status of system components.
    /// </summary>
    public interface IHealthCheck
    {
        /// <summary>
        /// Updates the health status of a specific component.
        /// </summary>
        /// <param name="component">The name of the component.</param>
        /// <param name="status">The current health status of the component.</param>
        /// <param name="message">Optional message providing additional status details.</param>
        void UpdateStatus(string component, HealthStatus status, string message);

        /// <summary>
        /// Gets the current health status of a specific component.
        /// </summary>
        /// <param name="component">The name of the component.</param>
        /// <returns>The current health status of the component.</returns>
        HealthStatus GetComponentStatus(string component);

        /// <summary>
        /// Gets the status message for a specific component.
        /// </summary>
        /// <param name="component">The name of the component.</param>
        /// <returns>The status message for the component.</returns>
        string GetStatusMessage(string component);

        /// <summary>
        /// Gets the health status of all components.
        /// </summary>
        /// <returns>A dictionary mapping component names to their health status.</returns>
        Dictionary<string, HealthStatus> GetAllComponentStatus();
    }
}
