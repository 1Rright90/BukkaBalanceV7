using System;

namespace YSBCaptain.Core.Models
{
    /// <summary>
    /// Represents system-wide performance metrics
    /// </summary>
    public class SystemMetrics
    {
        /// <summary>
        /// Gets the CPU usage percentage (0-100)
        /// </summary>
        public double CpuUsagePercentage { get; }

        /// <summary>
        /// Gets the memory usage in megabytes
        /// </summary>
        public double MemoryUsageMB { get; }

        /// <summary>
        /// Gets the disk usage percentage (0-100)
        /// </summary>
        public double DiskUsagePercentage { get; }

        /// <summary>
        /// Gets the timestamp when these metrics were collected
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the total system memory in megabytes
        /// </summary>
        public double TotalMemoryMB { get; }

        /// <summary>
        /// Gets the available system memory in megabytes
        /// </summary>
        public double AvailableMemoryMB { get; }

        /// <summary>
        /// Gets the total disk space in megabytes
        /// </summary>
        public double TotalDiskSpaceMB { get; }

        /// <summary>
        /// Gets the available disk space in megabytes
        /// </summary>
        public double AvailableDiskSpaceMB { get; }

        /// <summary>
        /// Gets the system uptime
        /// </summary>
        public TimeSpan SystemUptime { get; }

        /// <summary>
        /// Gets the memory usage percentage
        /// </summary>
        public double MemoryUsagePercentage => (TotalMemoryMB - AvailableMemoryMB) / TotalMemoryMB * 100;

        /// <summary>
        /// Gets the disk space usage percentage
        /// </summary>
        public double DiskSpaceUsagePercentage => (TotalDiskSpaceMB - AvailableDiskSpaceMB) / TotalDiskSpaceMB * 100;

        /// <summary>
        /// Creates a new instance of SystemMetrics
        /// </summary>
        public SystemMetrics(
            double cpuUsagePercentage,
            double memoryUsageMB,
            double diskUsagePercentage,
            double totalMemoryMB,
            double availableMemoryMB,
            double totalDiskSpaceMB,
            double availableDiskSpaceMB,
            TimeSpan systemUptime)
        {
            if (cpuUsagePercentage < 0 || cpuUsagePercentage > 100)
                throw new ArgumentOutOfRangeException(nameof(cpuUsagePercentage));
            if (memoryUsageMB < 0)
                throw new ArgumentOutOfRangeException(nameof(memoryUsageMB));
            if (diskUsagePercentage < 0 || diskUsagePercentage > 100)
                throw new ArgumentOutOfRangeException(nameof(diskUsagePercentage));
            if (totalMemoryMB < 0)
                throw new ArgumentOutOfRangeException(nameof(totalMemoryMB));
            if (availableMemoryMB < 0 || availableMemoryMB > totalMemoryMB)
                throw new ArgumentOutOfRangeException(nameof(availableMemoryMB));
            if (totalDiskSpaceMB < 0)
                throw new ArgumentOutOfRangeException(nameof(totalDiskSpaceMB));
            if (availableDiskSpaceMB < 0 || availableDiskSpaceMB > totalDiskSpaceMB)
                throw new ArgumentOutOfRangeException(nameof(availableDiskSpaceMB));

            CpuUsagePercentage = cpuUsagePercentage;
            MemoryUsageMB = memoryUsageMB;
            DiskUsagePercentage = diskUsagePercentage;
            TotalMemoryMB = totalMemoryMB;
            AvailableMemoryMB = availableMemoryMB;
            TotalDiskSpaceMB = totalDiskSpaceMB;
            AvailableDiskSpaceMB = availableDiskSpaceMB;
            SystemUptime = systemUptime;
            Timestamp = DateTime.UtcNow;
        }
    }
}
