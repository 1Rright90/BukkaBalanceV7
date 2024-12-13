using System;
using System.Collections.Generic;

namespace YSBCaptain.Core.Health
{
    public class HealthCheckResult
    {
        public HealthStatus Status { get; set; }
        public string Description { get; set; }
        public Exception Exception { get; set; }
        public IDictionary<string, object> Data { get; set; }

        public HealthCheckResult(
            HealthStatus status,
            string description = null,
            Exception exception = null,
            IDictionary<string, object> data = null)
        {
            Status = status;
            Description = description;
            Exception = exception;
            Data = data ?? new Dictionary<string, object>();
        }

        public static HealthCheckResult Healthy(string description = null, IDictionary<string, object> data = null)
        {
            return new HealthCheckResult(HealthStatus.Healthy, description, null, data);
        }

        public static HealthCheckResult Degraded(string description = null, Exception exception = null, IDictionary<string, object> data = null)
        {
            return new HealthCheckResult(HealthStatus.Degraded, description, exception, data);
        }

        public static HealthCheckResult Unhealthy(string description = null, Exception exception = null, IDictionary<string, object> data = null)
        {
            return new HealthCheckResult(HealthStatus.Unhealthy, description, exception, data);
        }
    }

    public enum HealthStatus
    {
        Healthy,
        Degraded,
        Unhealthy
    }
}
