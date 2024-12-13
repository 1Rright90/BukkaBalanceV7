using System;

namespace YSBCaptain.Core.Models
{
    /// <summary>
    /// Represents memory-related performance metrics
    /// </summary>
    public class MemoryMetrics
    {
        /// <summary>
        /// Gets the timestamp when these metrics were collected
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the working set size in bytes
        /// </summary>
        public long WorkingSet { get; }

        /// <summary>
        /// Gets the private memory size in bytes
        /// </summary>
        public long PrivateMemory { get; }

        /// <summary>
        /// Gets the managed memory size in bytes
        /// </summary>
        public long ManagedMemory { get; }

        /// <summary>
        /// Gets the number of active threads
        /// </summary>
        public int ThreadCount { get; }

        /// <summary>
        /// Gets the number of open handles
        /// </summary>
        public int HandleCount { get; }

        /// <summary>
        /// Gets the process uptime
        /// </summary>
        public TimeSpan ProcessUptime { get; }

        /// <summary>
        /// Gets the working set size in megabytes
        /// </summary>
        public double WorkingSetMB => WorkingSet / 1024.0 / 1024.0;

        /// <summary>
        /// Gets the private memory size in megabytes
        /// </summary>
        public double PrivateMemoryMB => PrivateMemory / 1024.0 / 1024.0;

        /// <summary>
        /// Gets the managed memory size in megabytes
        /// </summary>
        public double ManagedMemoryMB => ManagedMemory / 1024.0 / 1024.0;

        /// <summary>
        /// Creates a new instance of MemoryMetrics
        /// </summary>
        public MemoryMetrics(
            long workingSet,
            long privateMemory,
            long managedMemory,
            int threadCount,
            int handleCount,
            TimeSpan processUptime)
        {
            if (workingSet < 0)
                throw new ArgumentOutOfRangeException(nameof(workingSet));
            if (privateMemory < 0)
                throw new ArgumentOutOfRangeException(nameof(privateMemory));
            if (managedMemory < 0)
                throw new ArgumentOutOfRangeException(nameof(managedMemory));
            if (threadCount < 0)
                throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (handleCount < 0)
                throw new ArgumentOutOfRangeException(nameof(handleCount));

            WorkingSet = workingSet;
            PrivateMemory = privateMemory;
            ManagedMemory = managedMemory;
            ThreadCount = threadCount;
            HandleCount = handleCount;
            ProcessUptime = processUptime;
            Timestamp = DateTime.UtcNow;
        }
    }
}
