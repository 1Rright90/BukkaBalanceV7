using System;
using System.Collections.Generic;

namespace YSBCaptain.Core.Models
{
    /// <summary>
    /// Represents performance metrics for the game
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>
        /// UTC timestamp when metrics were collected
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// CPU usage percentage (0-100)
        /// </summary>
        public double CpuUsage { get; set; }

        /// <summary>
        /// Memory usage percentage (0-100)
        /// </summary>
        public double MemoryUsage { get; set; }

        /// <summary>
        /// Memory usage in megabytes
        /// </summary>
        public double MemoryUsageMB { get; set; }

        /// <summary>
        /// Network latency in milliseconds
        /// </summary>
        public double NetworkLatencyMs { get; set; }

        /// <summary>
        /// Frame time in milliseconds
        /// </summary>
        public double FrameTimeMs { get; set; }

        /// <summary>
        /// Frames per second
        /// </summary>
        public double FPS { get; set; }

        /// <summary>
        /// Number of active players in the game
        /// </summary>
        public int ActivePlayers { get; set; }

        /// <summary>
        /// Total number of entities in the game world
        /// </summary>
        public int TotalEntities { get; set; }

        /// <summary>
        /// Total available system memory in bytes
        /// </summary>
        public long TotalMemory { get; set; }

        /// <summary>
        /// Available system memory in bytes
        /// </summary>
        public long AvailableMemory { get; set; }

        /// <summary>
        /// Number of active threads
        /// </summary>
        public int ThreadCount { get; set; }

        /// <summary>
        /// Number of system handles
        /// </summary>
        public int HandleCount { get; set; }

        /// <summary>
        /// Process uptime
        /// </summary>
        public TimeSpan ProcessUptime { get; set; }

        /// <summary>
        /// Number of active network connections
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// Number of packets sent per second
        /// </summary>
        public double PacketsSentPerSecond { get; set; }

        /// <summary>
        /// Number of packets received per second
        /// </summary>
        public double PacketsReceivedPerSecond { get; set; }

        /// <summary>
        /// Custom metrics dictionary for additional game-specific metrics
        /// </summary>
        public Dictionary<string, double> CustomMetrics { get; set; } = new Dictionary<string, double>();

        /// <summary>
        /// Creates a new instance of PerformanceMetrics
        /// </summary>
        public PerformanceMetrics()
        {
            Timestamp = DateTime.UtcNow;
        }
    }
}
