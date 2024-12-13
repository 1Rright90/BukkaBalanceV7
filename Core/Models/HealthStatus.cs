namespace YSBCaptain.Core.Models
{
    /// <summary>
    /// Represents the health status of a system component.
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// The component is functioning normally with no issues.
        /// </summary>
        Healthy,

        /// <summary>
        /// The component is experiencing minor issues but is still operational.
        /// </summary>
        Warning,

        /// <summary>
        /// The component is experiencing severe issues that require immediate attention.
        /// </summary>
        Critical,

        /// <summary>
        /// The health status of the component cannot be determined.
        /// </summary>
        Unknown
    }
}
