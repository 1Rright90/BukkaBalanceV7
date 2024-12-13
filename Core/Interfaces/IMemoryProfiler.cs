using System;
using YSBCaptain.Core.Models;

namespace YSBCaptain.Core.Interfaces
{
    public interface IMemoryProfiler : IDisposable
    {
        /// <summary>
        /// Gets the current memory metrics of the application.
        /// </summary>
        /// <returns>A snapshot of the current memory metrics.</returns>
        Core.Models.MemoryMetrics GetCurrentMetrics();

        /// <summary>
        /// Takes a memory snapshot with an optional label.
        /// </summary>
        /// <param name="label">Optional label to identify the snapshot.</param>
        void TakeSnapshot(string label = null);

        /// <summary>
        /// Starts monitoring memory usage.
        /// </summary>
        void StartMonitoring();

        /// <summary>
        /// Stops monitoring memory usage.
        /// </summary>
        void StopMonitoring();

        /// <summary>
        /// Gets the memory usage report.
        /// </summary>
        /// <returns>A formatted string containing memory usage information.</returns>
        string GetReport();
    }
}
