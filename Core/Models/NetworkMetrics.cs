using System;

namespace YSBCaptain.Core.Models
{
    /// <summary>
    /// Represents network performance metrics
    /// </summary>
    public class NetworkMetrics
    {
        /// <summary>
        /// Gets the bandwidth usage in Kbps
        /// </summary>
        public double BandwidthUsageKbps { get; }

        /// <summary>
        /// Gets the network latency in milliseconds
        /// </summary>
        public double LatencyMs { get; }

        /// <summary>
        /// Gets the packet loss percentage
        /// </summary>
        public int PacketLossPercentage { get; }

        /// <summary>
        /// Gets the timestamp when these metrics were collected
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Gets the number of active connections
        /// </summary>
        public int ActiveConnections { get; }

        /// <summary>
        /// Gets the number of bytes sent
        /// </summary>
        public long BytesSent { get; }

        /// <summary>
        /// Gets the number of bytes received
        /// </summary>
        public long BytesReceived { get; }

        /// <summary>
        /// Gets the number of packets sent per second
        /// </summary>
        public double PacketsSentPerSecond { get; }

        /// <summary>
        /// Gets the number of packets received per second
        /// </summary>
        public double PacketsReceivedPerSecond { get; }

        /// <summary>
        /// Gets the bandwidth usage in Mbps
        /// </summary>
        public double BandwidthUsageMbps => BandwidthUsageKbps / 1024.0;

        /// <summary>
        /// Creates a new instance of NetworkMetrics
        /// </summary>
        public NetworkMetrics(
            double bandwidthUsageKbps,
            double latencyMs,
            int packetLossPercentage,
            int activeConnections,
            long bytesSent,
            long bytesReceived,
            double packetsSentPerSecond,
            double packetsReceivedPerSecond)
        {
            if (bandwidthUsageKbps < 0)
                throw new ArgumentOutOfRangeException(nameof(bandwidthUsageKbps));
            if (latencyMs < 0)
                throw new ArgumentOutOfRangeException(nameof(latencyMs));
            if (packetLossPercentage < 0 || packetLossPercentage > 100)
                throw new ArgumentOutOfRangeException(nameof(packetLossPercentage));
            if (activeConnections < 0)
                throw new ArgumentOutOfRangeException(nameof(activeConnections));
            if (bytesSent < 0)
                throw new ArgumentOutOfRangeException(nameof(bytesSent));
            if (bytesReceived < 0)
                throw new ArgumentOutOfRangeException(nameof(bytesReceived));
            if (packetsSentPerSecond < 0)
                throw new ArgumentOutOfRangeException(nameof(packetsSentPerSecond));
            if (packetsReceivedPerSecond < 0)
                throw new ArgumentOutOfRangeException(nameof(packetsReceivedPerSecond));

            BandwidthUsageKbps = bandwidthUsageKbps;
            LatencyMs = latencyMs;
            PacketLossPercentage = packetLossPercentage;
            ActiveConnections = activeConnections;
            BytesSent = bytesSent;
            BytesReceived = bytesReceived;
            PacketsSentPerSecond = packetsSentPerSecond;
            PacketsReceivedPerSecond = packetsReceivedPerSecond;
            Timestamp = DateTime.UtcNow;
        }
    }
}
