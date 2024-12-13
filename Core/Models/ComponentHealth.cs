using System;

namespace YSBCaptain.Core.Models
{
    /// <summary>
    /// Represents the health status of a system component
    /// </summary>
    public class ComponentHealth
    {
        /// <summary>
        /// Gets the name of the component
        /// </summary>
        public string ComponentName { get; }

        /// <summary>
        /// Gets the current health status of the component
        /// </summary>
        public HealthStatus Status { get; }

        /// <summary>
        /// Gets the timestamp of the last health update
        /// </summary>
        public DateTime LastUpdated { get; }

        /// <summary>
        /// Gets any additional status message or details
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the duration since the last update
        /// </summary>
        public TimeSpan TimeSinceLastUpdate => DateTime.UtcNow - LastUpdated;

        /// <summary>
        /// Creates a new instance of ComponentHealth
        /// </summary>
        public ComponentHealth(string componentName, HealthStatus status, string message = null)
        {
            if (string.IsNullOrWhiteSpace(componentName))
                throw new ArgumentNullException(nameof(componentName));

            ComponentName = componentName;
            Status = status;
            Message = message;
            LastUpdated = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a healthy component status
        /// </summary>
        public static ComponentHealth Healthy(string componentName, string message = null) =>
            new ComponentHealth(componentName, HealthStatus.Healthy, message);

        /// <summary>
        /// Creates a warning component status
        /// </summary>
        public static ComponentHealth Warning(string componentName, string message) =>
            new ComponentHealth(componentName, HealthStatus.Warning, message);

        /// <summary>
        /// Creates a critical component status
        /// </summary>
        public static ComponentHealth Critical(string componentName, string message) =>
            new ComponentHealth(componentName, HealthStatus.Critical, message);

        /// <summary>
        /// Creates an unknown component status
        /// </summary>
        public static ComponentHealth Unknown(string componentName, string message = null) =>
            new ComponentHealth(componentName, HealthStatus.Unknown, message);
    }
}
