using System;
using System.Diagnostics;

namespace YSBCaptain.Core.Monitoring.Collectors
{
    /// <summary>
    /// Collects metrics related to network performance and data transfer.
    /// </summary>
    public class NetworkMetricCollector : BaseMetricCollector
    {
        private readonly Stopwatch _sampleTimer;
        private long _lastBytesSent;
        private long _lastBytesReceived;

        /// <summary>
        /// Initializes a new instance of the NetworkMetricCollector class.
        /// </summary>
        public NetworkMetricCollector() : base("Network")
        {
            _sampleTimer = new Stopwatch();
            _lastBytesSent = 0;
            _lastBytesReceived = 0;
        }

        /// <summary>
        /// Collects network-related metrics including bytes sent/received per second and total transfer.
        /// </summary>
        public override void CollectMetrics()
        {
            _sampleTimer.Restart();

            try
            {
                var (bytesSent, bytesReceived) = GetNetworkStats();
                var elapsedSeconds = _sampleTimer.Elapsed.TotalSeconds;

                if (elapsedSeconds > 0)
                {
                    var sendRate = (bytesSent - _lastBytesSent) / elapsedSeconds;
                    var receiveRate = (bytesReceived - _lastBytesReceived) / elapsedSeconds;

                    SetMetric("BytesSentPerSecond", sendRate);
                    SetMetric("BytesReceivedPerSecond", receiveRate);
                    SetMetric("TotalBytesSent", bytesSent);
                    SetMetric("TotalBytesReceived", bytesReceived);

                    _lastBytesSent = bytesSent;
                    _lastBytesReceived = bytesReceived;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error collecting network metrics: {ex.Message}");
            }
            finally
            {
                _sampleTimer.Stop();
            }
        }

        /// <summary>
        /// Gets the current network statistics.
        /// </summary>
        /// <returns>A tuple containing the total bytes sent and received.</returns>
        private (long bytesSent, long bytesReceived) GetNetworkStats()
        {
            // Implementation to get network statistics
            return (0, 0); // Placeholder
        }

        /// <summary>
        /// Performs cleanup when the collector is disposed.
        /// </summary>
        protected override void OnDispose()
        {
            _sampleTimer.Stop();
        }
    }
}
