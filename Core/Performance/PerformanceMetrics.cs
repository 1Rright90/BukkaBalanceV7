using System;
using System.Collections.Generic;

namespace YSBCaptain.Core.Performance
{
    public class PerformanceMetrics
    {
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public double NetworkLatency { get; set; }
        public double NetworkBandwidth { get; set; }
        public Dictionary<string, double> CustomMetrics { get; set; }
        public DateTimeOffset Timestamp { get; set; }

        public PerformanceMetrics()
        {
            CustomMetrics = new Dictionary<string, double>();
            Timestamp = DateTimeOffset.UtcNow;
        }
    }
}
