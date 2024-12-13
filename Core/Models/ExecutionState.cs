namespace YSBCaptain.Core.Models
{
    /// <summary>
    /// Represents the current execution state of a system component.
    /// </summary>
    public enum ExecutionState
    {
        /// <summary>
        /// The component is stopped and not executing.
        /// </summary>
        Stopped,

        /// <summary>
        /// The component is in the process of starting up.
        /// </summary>
        Starting,

        /// <summary>
        /// The component is currently running and operational.
        /// </summary>
        Running,

        /// <summary>
        /// The component is in the process of stopping.
        /// </summary>
        Stopping,

        /// <summary>
        /// The component has encountered an error during execution.
        /// </summary>
        Error
    }
}
