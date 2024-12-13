using System;

namespace YSBCaptain.Core.Performance
{
    public class MessageStatistics
    {
        public long TotalMessages { get; set; }
        public long ProcessedMessages { get; set; }
        public long FailedMessages { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public DateTime LastProcessedTime { get; set; }
        public double MessagesPerSecond { get; set; }
    }
}
