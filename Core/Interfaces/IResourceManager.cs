using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YSBCaptain.Core.Interfaces
{
    /// <summary>
    /// Interface for managing and tracking system resources.
    /// </summary>
    public interface IResourceManager
    {
        /// <summary>
        /// Represents the current status of a managed resource.
        /// </summary>
        public enum ResourceStatus
        {
            /// <summary>
            /// The status of the resource is unknown.
            /// </summary>
            Unknown,

            /// <summary>
            /// The resource is currently loading.
            /// </summary>
            Loading,

            /// <summary>
            /// The resource is ready for use.
            /// </summary>
            Ready,

            /// <summary>
            /// The resource is in an error state.
            /// </summary>
            Error,

            /// <summary>
            /// The resource has been disposed.
            /// </summary>
            Disposed
        }

        /// <summary>
        /// Asynchronously acquires a resource of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of resource to acquire.</typeparam>
        /// <param name="resourceId">The unique identifier of the resource.</param>
        /// <returns>The acquired resource.</returns>
        Task<T> AcquireResourceAsync<T>(string resourceId) where T : class;

        /// <summary>
        /// Releases a previously acquired resource.
        /// </summary>
        /// <param name="resourceId">The unique identifier of the resource to release.</param>
        void ReleaseResource(string resourceId);

        /// <summary>
        /// Checks if a resource is currently available.
        /// </summary>
        /// <param name="resourceId">The unique identifier of the resource to check.</param>
        /// <returns>True if the resource is available, false otherwise.</returns>
        Task<bool> IsResourceAvailableAsync(string resourceId);

        /// <summary>
        /// Gets the current status of all managed resources.
        /// </summary>
        /// <returns>A dictionary mapping resource IDs to their current status.</returns>
        Dictionary<string, ResourceStatus> GetResourceStatuses();

        /// <summary>
        /// Event that is raised when a resource is acquired.
        /// </summary>
        event EventHandler<string> ResourceAcquired;

        /// <summary>
        /// Event that is raised when a resource is released.
        /// </summary>
        event EventHandler<string> ResourceReleased;
    }
}
